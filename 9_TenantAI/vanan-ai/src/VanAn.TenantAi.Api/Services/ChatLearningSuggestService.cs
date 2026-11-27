using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using VanAn.TenantAi.Ai;
using VanAn.TenantAi.Contracts;
using VanAn.TenantAi.Domain;
using VanAn.TenantAi.Infra;

namespace VanAn.TenantAi.Api.Services;

/// <summary>Kết quả một lượt chạy learning suggest (cho log + test).</summary>
public sealed record LearningRunResult(
    int TenantsScanned,
    int ClustersFound,
    int SuggestedCreated,
    int SkippedExisting,
    int SkippedNoLlm,
    int SkippedLimit,
    int SkippedTenantOptOut);

/// <summary>
/// W3f — Giải pháp 1 "chat learning suggest" (tự học có kiểm soát):
/// job hằng ngày gom câu hỏi khách mà AI trả NoData → QuestionClusterer gom cụm
/// trùng/đồng nghĩa (≥ MinClusterSize) → FaqDraftGenerator (LLM) soạn FAQ draft →
/// KnowledgeDocument PendingReview với SourceRef "chat-learn:{tenant}:{hash}"
/// (dedupe AI-P11 — idempotent rerun).
///
/// Kiểm soát (reserve impact):
///  · Human-in-the-loop: KHÔNG bao giờ auto-approve — chủ shop duyệt ở màn hình
///    pending hiện hữu (classification "internal" placeholder như AI-P11; owner
///    chọn Public lúc approve nếu muốn public chat dùng).
///  · Caps: max suggestions/tenant/lượt, max pending/tenant, min cluster size.
///  · Chỉ học intent đáng học (Faq/Menu/Price/Availability/Recipe/Nutrition/BrandStory);
///    loại ChitChat/OutOfScope/OrderHelp/PolicyViolation + Refused + channel "preview".
///  · LLM lỗi/off → bỏ qua, không crash. Disabled qua config Learning:Enabled.
/// </summary>
public sealed class ChatLearningSuggestService(
    IServiceProvider services,
    IConfiguration config,
    ILogger<ChatLearningSuggestService> logger) : BackgroundService
{
    public const string SourceRefPrefix = "chat-learn:";
    private const string ReplySuffix = ":reply";
    private const string PreviewChannel = "preview";

    /// <summary>W3f-b — global runtime switch trong system_settings (ops, SQL-managed).
    /// Absent = theo env Learning:Enabled; "false" = tắt toàn cục không cần restart.</summary>
    public const string GlobalSettingKey = "LearningSuggest:Enabled";

    /// <summary>W3f-b — key opt-in per tenant (convention ShopFeature:{tenant}:{toggle}).
    /// Absent/"false" = tenant chưa tham gia.</summary>
    public static string TenantSettingKey(Guid tenantId) => $"LearningSuggest:{tenantId}:Enabled";

    public const string EnabledKey = "Learning:Enabled";
    public const string MinClusterSizeKey = "Learning:MinClusterSize";
    public const string SimilarityThresholdKey = "Learning:SimilarityThreshold";
    public const string MaxSuggestionsKey = "Learning:MaxSuggestionsPerTenant";
    public const string MaxPendingKey = "Learning:MaxPendingPerTenant";
    public const string LookbackDaysKey = "Learning:LookbackDays";
    public const string IntervalHoursKey = "Learning:CleanupIntervalHours";
    public const string StartupDelayMinutesKey = "Learning:StartupDelayMinutes";

    private bool Enabled => config.GetValue(EnabledKey, true);

    /// <summary>Intent đáng học — câu hỏi cần tri thức doc/product mà NoData nghĩa là
    /// kho tri thức thiếu. ChitChat/OutOfScope/OrderHelp/PolicyViolation/ExternalKnowledge
    /// bị loại ngay từ query.</summary>
    private static readonly HashSet<string> AllowedIntents = new(StringComparer.Ordinal)
    {
        ChatIntents.Faq, ChatIntents.MenuQuery, ChatIntents.PriceQuery,
        ChatIntents.AvailabilityQuery, ChatIntents.RecipeQuery,
        ChatIntents.NutritionQuery, ChatIntents.BrandStory
    };

    /// <summary>Chạy 1 lượt — public cho tests + manual trigger.</summary>
    public async Task<LearningRunResult> RunAsync(Guid? tenantId = null, CancellationToken ct = default)
    {
        if (!Enabled)
        {
            logger.LogInformation("learning suggest disabled — skip");
            return new LearningRunResult(0, 0, 0, 0, 0, 0, 0);
        }

        var now = DateTime.UtcNow;
        var since = now.AddDays(-config.GetValue(LookbackDaysKey, 7));

        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<TenantAiDbContext>();
        var llm = scope.ServiceProvider.GetRequiredService<ILlmChatClient>();
        var ingest = scope.ServiceProvider.GetRequiredService<KnowledgeIngestService>();

        // W3f-b lớp 1 — global runtime switch (ops-managed row; absent = env default).
        var globalVal = await db.SystemSettings.AsNoTracking()
            .Where(s => s.Key == GlobalSettingKey).Select(s => s.Value).FirstOrDefaultAsync(ct);
        if (string.Equals(globalVal, "false", StringComparison.OrdinalIgnoreCase))
        {
            logger.LogInformation("learning suggest globally disabled (system_settings) — skip");
            return new LearningRunResult(0, 0, 0, 0, 0, 0, 0);
        }

        // Candidate assistant turns: NoData answer, trong window, chưa hết hạn retention,
        // không refused, intent đáng học.
        var query = db.ConversationMessages.AsNoTracking()
            .Where(m => m.Role == MessageRole.Assistant
                && m.CreatedAtUtc >= since
                && m.ExpiresAtUtc > now
                && !m.Refused
                && m.Intent != null && AllowedIntents.Contains(m.Intent)
                && m.Content.StartsWith(ChatTemplates.NoData));
        if (tenantId.HasValue)
            query = query.Where(m => m.TenantId == tenantId.Value);

        var rows = (await query.ToListAsync(ct))
            .Where(m => m.MessageId.EndsWith(ReplySuffix, StringComparison.Ordinal))
            .ToList();
        if (rows.Count == 0)
            return new LearningRunResult(0, 0, 0, 0, 0, 0, 0);

        // Pair user question với assistant turn (assistant.MessageId = user.MessageId + ":reply").
        var userKeys = rows.Select(m => (m.TenantId, UserId: m.MessageId[..^ReplySuffix.Length])).ToHashSet();
        var userById = (await db.ConversationMessages.AsNoTracking()
                .Where(m => m.Role == MessageRole.User)
                .ToListAsync(ct))
            .Where(m => userKeys.Contains((m.TenantId, m.MessageId)))
            .ToDictionary(m => (m.TenantId, m.MessageId), m => m.Content);

        // Loại session preview (admin test-chat) — không học từ chính admin.
        var previewSet = (await db.ConversationSessions.AsNoTracking()
                .Where(s => s.Channel == PreviewChannel)
                .ToListAsync(ct))
            .Select(s => (s.TenantId, s.SessionId))
            .ToHashSet();

        var byTenant = rows
            .Where(m => !previewSet.Contains((m.TenantId, m.SessionId)))
            .GroupBy(m => m.TenantId);

        var tenantsScanned = 0;
        var clustersFound = 0;
        var created = 0;
        var skippedExisting = 0;
        var skippedNoLlm = 0;
        var skippedLimit = 0;
        var skippedOptOut = 0;

        var minClusterSize = config.GetValue(MinClusterSizeKey, 3);
        var similarityThreshold = config.GetValue(SimilarityThresholdKey, 0.85);
        var maxSuggestions = config.GetValue(MaxSuggestionsKey, 5);
        var maxPending = config.GetValue(MaxPendingKey, 10);

        foreach (var group in byTenant)
        {
            var t = group.Key;

            // W3f-b lớp 2 — per-tenant opt-in: row absent/"false" = chưa tham gia → bỏ qua.
            var optedIn = await db.SystemSettings.AsNoTracking()
                .AnyAsync(s => s.Key == TenantSettingKey(t) && s.Value == "true", ct);
            if (!optedIn)
            {
                skippedOptOut++;
                continue;
            }
            tenantsScanned++;

            var questions = group
                .Select(m => userById.TryGetValue((t, m.MessageId[..^ReplySuffix.Length]), out var q) ? q : null)
                .Where(q => !string.IsNullOrWhiteSpace(q))
                .Select(q => q!)
                .ToList();
            if (questions.Count == 0) continue;

            var clusters = QuestionClusterer.Cluster(questions, similarityThreshold, minClusterSize);
            if (clusters.Count == 0) continue;
            clustersFound += clusters.Count;

            // Chặn ngập hàng chờ duyệt: tenant có đủ pending chat-learn rồi thì bỏ qua.
            var pendingCount = await db.KnowledgeDocuments.AsNoTracking()
                .CountAsync(d => d.TenantId == t && d.Status == DocumentStatus.Active
                    && d.ApprovalStatus == ApprovalStatus.PendingReview
                    && d.SourceRef != null && d.SourceRef.StartsWith(SourceRefPrefix), ct);
            if (pendingCount >= maxPending)
            {
                skippedLimit += clusters.Count;
                continue;
            }

            foreach (var cluster in clusters)
            {
                if (created >= maxSuggestions)
                {
                    skippedLimit++;
                    continue;
                }

                var sourceRef = SourceRefFor(t, cluster.Representative);
                var exists = await db.KnowledgeDocuments.AsNoTracking()
                    .AnyAsync(d => d.TenantId == t && d.SourceRef == sourceRef
                        && d.Status == DocumentStatus.Active, ct);
                if (exists)
                {
                    skippedExisting++;
                    continue;
                }

                if (!llm.Enabled)
                {
                    skippedNoLlm++;
                    continue;
                }

                string? draftText = null;
                try
                {
                    var res = await llm.GenerateAsync(
                        FaqDraftGenerator.BuildPrompt(cluster.Representative), schemaHint: "faq_draft", ct);
                    draftText = res?.Text;
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    logger.LogWarning(ex, "learning suggest LLM failed tenant={Tenant}", t);
                }
                var draft = draftText == null ? null : FaqDraftGenerator.Parse(draftText);
                if (draft == null)
                {
                    logger.LogInformation("learning suggest draft invalid tenant={Tenant} q={Question}",
                        t, cluster.Representative);
                    continue;
                }

                // AddDocumentAsync: sanitize (T3) + SourceRef dedupe (AI-P11) + pending [G14].
                var ingestResult = await ingest.AddDocumentAsync(t,
                    new DocumentIngestRequest("faq", draft.Title, draft.Content,
                        "internal", SourceRef: sourceRef), ct);
                if (ingestResult.Value != null)
                {
                    created++;
                    logger.LogInformation(
                        "learning suggest created doc {DocId} tenant={Tenant} q={Question} count={Count}",
                        ingestResult.Value.Id, t, cluster.Representative, cluster.Count);
                }
            }
        }

        return new LearningRunResult(tenantsScanned, clustersFound, created,
            skippedExisting, skippedNoLlm, skippedLimit, skippedOptOut);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Delay(
            TimeSpan.FromMinutes(config.GetValue(StartupDelayMinutesKey, 60)), stoppingToken);
        var interval = TimeSpan.FromHours(config.GetValue(IntervalHoursKey, 24));
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var r = await RunAsync(ct: stoppingToken);
                logger.LogInformation(
                    "learning suggest pass done tenants={Tenants} clusters={Clusters} created={Created} optOut={OptOut}",
                    r.TenantsScanned, r.ClustersFound, r.SuggestedCreated, r.SkippedTenantOptOut);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogWarning(ex, "learning suggest pass failed");
            }
            await Task.Delay(interval, stoppingToken);
        }
    }

    /// <summary>SourceRef ổn định cho (tenant, câu hỏi normalized) — rerun không trùng.</summary>
    private static string SourceRefFor(Guid tenantId, string question)
    {
        var norm = IntentRouter.Normalize(question);
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(norm)))
            .ToLowerInvariant()[..16];
        return $"{SourceRefPrefix}{tenantId:N}:{hash}";
    }
}