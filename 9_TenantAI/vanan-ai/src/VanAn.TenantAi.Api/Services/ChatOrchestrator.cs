using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using VanAn.TenantAi.Ai;
using VanAn.TenantAi.Contracts;
using VanAn.TenantAi.Domain;
using VanAn.TenantAi.Infra;

namespace VanAn.TenantAi.Api.Services;

/// <summary>Orchestrator result — mapped to ChatResponse at the endpoint.</summary>
public sealed record ChatResult(
    string Answer,
    IReadOnlyList<ChatProductDto> Products,
    IReadOnlyList<string> SourceIds,
    double Confidence,
    bool Refused,
    string? RefuseReason,
    bool StaleData,
    bool Degraded,
    string? Intent,
    int DroppedByPermission);

/// <summary>
/// task_ai_p3 / SRS §5 — the 9-step grounded chat pipeline:
/// ctx (done by middleware) → [metering P5] → config → IntentRouter →
/// PolicyEngine (refuse before any LLM call [G6]) → Retriever →
/// UsagePermission filter [G15] → LLM → AnswerValidator → persist → response.
/// Every failure path degrades to a template — this pipeline never throws on
/// provider/knowledge errors.
/// </summary>
public sealed class ChatOrchestrator(
    TenantAiDbContext db,
    IntentRouter intentRouter,
    KnowledgeRetriever retriever,
    PromptBuilder promptBuilder,
    AnswerValidator validator,
    ILlmChatClient llm,
    IConfiguration config,
    ILogger<ChatOrchestrator> logger,
    QuotaGate quota,
    IMeteringIngestQueue ingest)
{
    private int WindowSize => config.GetValue("TenantAiChat:HistoryWindow", 10);
    private int RetentionDays => config.GetValue("TenantAiChat:RetentionDays", 90);
    private int StaleHours => config.GetValue("Knowledge:StaleHours", 24);

    private bool IsStale(ProductKnowledge p)
        => p.SyncedAtUtc < DateTime.UtcNow.AddHours(-StaleHours);

    public async Task<ChatResult> ChatAsync(
        Guid tenantId, string sessionId, string messageId, string text,
        string channel, bool useDraftConfig, CancellationToken ct)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var flags = KnowledgeSourceFlags.None;
        string? intent = null;
        var refused = false;
        string? refuseReason = null;
        var degraded = false;
        var staleData = false;
        var dropped = 0;
        int? promptTokens = null, completionTokens = null;
        string answer;
        double confidence = 0;
        List<Guid> productRefs = [];
        List<string> sourceIds = [];

        // FR-9 kill switch — AI off = refused nhẹ nhàng, never a hard error (P5).
        if (!config.GetValue("TenantAi:Enabled", true))
            return await FinishAsync(ChatTemplates.DegradedUnavailable, intent, [], [], 0,
                refused: true, refuseReason: "ai_disabled", staleData: false, degraded: false, dropped: 0);

        // [G8] messageId dedupe — replay returns the stored assistant turn.
        // (Replay skips the quota gate + ingest: the original turn already metered,
        // and re-serving a stored answer must not be blocked by a current outage.)
        var prior = await db.ConversationMessages.AsNoTracking()
            .FirstOrDefaultAsync(m => m.TenantId == tenantId && m.SessionId == sessionId
                && m.MessageId == messageId + ":reply", ct);
        if (prior != null)
            return await ReplayedResultAsync(tenantId, prior, ct);

        // [G7] PO-1 metering quota gate — deviation có chủ đích khỏi fail-open
        // generic: check fail + cache hết hạn → Degrade structured-only (0 LLM).
        var quotaOutcome = await quota.EvaluateAsync(tenantId, ct);
        var quotaDegraded = quotaOutcome == QuotaOutcome.Degrade;
        if (quotaOutcome == QuotaOutcome.Refuse)
            return await FinishAsync(ChatTemplates.QuotaRefused, intent, [], [], 0,
                refused: true, refuseReason: "quota_denied", staleData: false, degraded: false, dropped: 0);

        var session = await GetOrCreateSessionAsync(tenantId, sessionId, channel, ct);
        var aiConfig = await ResolveConfigAsync(tenantId, useDraftConfig, ct);
        var aiName = aiConfig?.AiName ?? "trợ lý quán";
        var policy = aiConfig != null
            ? await db.AiPolicies.AsNoTracking()
                .FirstOrDefaultAsync(p => p.TenantId == tenantId && p.ConfigVersion == aiConfig.Version, ct)
            : null;

        // Tenant refused-topics → PolicyViolation before the router even runs.
        if (ContainsRefusedTopic(text, policy))
        {
            intent = ChatIntents.PolicyViolation;
            refused = true;
            refuseReason = "policy_violation";
            answer = ChatTemplates.RefusePolite;
            return await FinishAsync(answer, intent, productRefs, sourceIds, confidence,
                refused, refuseReason, staleData, degraded, dropped, promptTokens, completionTokens);
        }

        // Quota-degraded → rules only, never the LLM classify fallback (T8: 0 LLM calls).
        intent = quotaDegraded
            ? intentRouter.MatchRules(text) ?? ChatIntents.OutOfScope
            : await intentRouter.RouteAsync(text, ct);

        // PolicyEngine — tenant routes override system defaults [FR-3].
        var tenantRoutes = await db.IntentRoutes.AsNoTracking()
            .Where(r => r.TenantId == tenantId).ToListAsync(ct);
        var systemRoutes = await db.IntentRoutes.AsNoTracking()
            .Where(r => r.TenantId == null).ToListAsync(ct);
        var route = PolicyEngine.ResolveRoute(intent, tenantRoutes, systemRoutes);
        var decision = PolicyEngine.Decide(intent, route);
        if (IntentScopeDisabled(intent, policy))
            decision = new PolicyDecision(route, true, route?.RefuseTemplate);

        if (decision.Refuse)
        {
            refused = true;
            refuseReason = intent == ChatIntents.PolicyViolation ? "policy_violation" : "out_of_scope";
            answer = decision.RefuseReason ?? ChatTemplates.RefusePolite;
            return await FinishAsync(answer, intent, productRefs, sourceIds, confidence,
                refused, refuseReason, staleData, degraded, dropped, promptTokens, completionTokens);
        }

        // Static-template intents (OrderHelp) — template answer, zero LLM.
        if (route != null && PolicyEngine.IsStaticTemplateOnly(route))
        {
            answer = route.RefuseTemplate ?? ChatTemplates.OrderHelp;
            confidence = 1.0;
            return await FinishAsync(answer, intent, productRefs, sourceIds, confidence,
                refused, refuseReason, staleData, degraded, dropped, promptTokens, completionTokens);
        }

        // Retrieval → [G15] permission filter.
        flags = route?.AllowedSourceFlags ?? KnowledgeSourceFlags.None;
        var raw = await retriever.RetrieveAsync(tenantId, intent, text, flags, ct);
        var tenantPerms = await db.KnowledgeUsagePermissions.AsNoTracking()
            .Where(p => p.TenantId == tenantId).ToListAsync(ct);
        var systemPerms = await db.KnowledgeUsagePermissions.AsNoTracking()
            .Where(p => p.TenantId == null).ToListAsync(ct);
        var filtered = UsagePermissionFilter.Apply(intent, channel, raw, tenantPerms, systemPerms,
            droppedId => logger.LogWarning("G15 drop {SourceId} tenant={Tenant} intent={Intent} channel={Channel}",
                droppedId, tenantId, intent, channel));
        var facts = filtered.Kept;
        dropped = filtered.Dropped;

        if (route is { RequiresFacts: true } && facts.Count == 0)
        {
            answer = ChatTemplates.NoData;
            return await FinishAsync(answer, intent, productRefs, sourceIds, confidence,
                refused, refuseReason, staleData, degraded, dropped, promptTokens, completionTokens);
        }

        // LLM path — prompt = persona + guardrails + facts + window + question.
        var history = await db.ConversationMessages.AsNoTracking()
            .Where(m => m.TenantId == tenantId && m.SessionId == sessionId)
            .OrderByDescending(m => m.CreatedAtUtc)
            .Take(WindowSize)
            .OrderBy(m => m.CreatedAtUtc)
            .Select(m => new ChatTurn(m.Role == MessageRole.User ? "user" : "assistant", m.Content))
            .ToListAsync(ct);

        LlmAnswer? parsed = null;
        if (llm.Enabled && !quotaDegraded)
        {
            var prompt = promptBuilder.Build(aiName, aiConfig?.PersonaJson, aiConfig?.BusinessProfileJson,
                facts, history, text);
            var generated = await SafeGenerateAsync(prompt, ct);
            parsed = validator.TryParse(generated?.Text);
            if (parsed == null && generated != null)
            {
                // FR-4: schema fail → exactly one retry → fallback.
                generated = await SafeGenerateAsync(prompt + "\nNhắc lại: chỉ trả JSON đúng schema.", ct);
                parsed = validator.TryParse(generated?.Text);
            }
            promptTokens = generated?.PromptTokens;
            completionTokens = generated?.CompletionTokens;
        }

        if (parsed == null)
        {
            // Degrade — LLM down/timeout/schema-fail: structured template or polite fallback.
            degraded = true;
            answer = BuildDegradedAnswer(intent, facts);
        }
        else
        {
            var outcome = validator.Validate(parsed, facts, IsStale);
            productRefs = outcome.ProductRefs;
            sourceIds = outcome.SourceIds;
            confidence = outcome.Confidence;
            staleData = outcome.StaleData;
            answer = outcome.UseFallback
                ? staleData ? BuildStaleAnswer(facts) : BuildNoDataAnswer(facts)
                : outcome.Answer!;
        }

        return await FinishAsync(answer, intent, productRefs, sourceIds, confidence,
            refused, refuseReason, staleData, degraded, dropped, promptTokens, completionTokens);

        // ---- local helpers ----

        async Task<ChatResult> FinishAsync(
            string finalAnswer, string? finalIntent,
            List<Guid> refs, List<string> sources, double conf,
            bool refused, string? refuseReason, bool staleData, bool degraded, int dropped,
            int? promptTokens = null, int? completionTokens = null)
        {
            var products = await ToProductDtosAsync(refs, ct);
            await PersistTurnAsync(tenantId, sessionId, messageId, text, finalAnswer,
                finalIntent, refs, sources, conf, refused, refuseReason, degraded,
                promptTokens, completionTokens, ct);
            EnqueueUsage(tenantId, messageId, meterOnly: refused || degraded);
            logger.LogInformation(
                "chat tenant={Tenant} intent={Intent} sources={Src} refused={Ref} degraded={Deg} dropped={Drop} ms={Ms}",
                tenantId, finalIntent ?? "-", flags, refused, degraded, dropped, sw.ElapsedMilliseconds);
            return new ChatResult(finalAnswer, products, sources, conf, refused, refuseReason,
                staleData, degraded, finalIntent, dropped);
        }
    }

    /// <summary>
    /// task_ai_p5 — usage ingest sau response, fire-and-forget qua retry queue.
    /// PO-2: chỉ câu trả lời LLM thật tính `ai.chat.message` (Deny-enforced);
    /// refused/degraded/zero-LLM → `ai.chat.degraded` MeterOnly (đo được, không charge).
    /// </summary>
    private void EnqueueUsage(Guid tenantId, string messageId, bool meterOnly)
    {
        try
        {
            ingest.Enqueue(new MeteringUsageRecord(tenantId,
                meterOnly ? MeteringServiceCodes.ChatDegraded : MeteringServiceCodes.ChatMessage,
                Quantity: 1m, Unit: "message", OccurredAtUtc: DateTime.UtcNow,
                IdempotencyKey: $"chat:{tenantId}:{messageId}", TaskRef: messageId));
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "metering ingest enqueue failed tenant={Tenant}", tenantId);
        }
    }

    private async Task<LlmChatResult?> SafeGenerateAsync(string prompt, CancellationToken ct)
    {
        try { return await llm.GenerateAsync(prompt, "chat_answer", ct); }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "LLM generate failed — degrading");
            return null;
        }
    }

    private static string BuildDegradedAnswer(string intent, IReadOnlyList<ContextItem> facts)
    {
        var products = facts.Where(f => f.Product != null).Select(f => f.Product!).Take(3).ToList();
        if (products.Count > 0)
            return ChatTemplates.ProductFactsFallback(products.Select(PromptBuilder.ProductLine).ToList());
        var chunk = facts.FirstOrDefault(f => f.Chunk != null)?.Chunk?.Content;
        if (!string.IsNullOrWhiteSpace(chunk))
            return $"Theo thông tin của quán: {Trim(chunk, 300)}";
        return intent == ChatIntents.ChitChat ? ChatTemplates.ChitChatFallback : ChatTemplates.DegradedUnavailable;
    }

    /// <summary>Low-confidence / unverifiable-claim fallback: facts if we have them, else no-data.</summary>
    private static string BuildNoDataAnswer(IReadOnlyList<ContextItem> facts)
    {
        var products = facts.Where(f => f.Product != null).Select(f => f.Product!).Take(3).ToList();
        return products.Count > 0
            ? ChatTemplates.ProductFactsFallback(products.Select(PromptBuilder.ProductLine).ToList())
            : ChatTemplates.NoData;
    }

    private string BuildStaleAnswer(IReadOnlyList<ContextItem> facts)
    {
        var names = facts.Where(f => f.Product != null).Select(f => f.Product!.Name).Take(3).ToList();
        return names.Count > 0
            ? $"Mình có thông tin về {string.Join(", ", names)} " + ChatTemplates.StaleSuffix
            : ChatTemplates.NoData + " " + ChatTemplates.StaleSuffix;
    }

    private static string Trim(string s, int max) => s.Length <= max ? s : s[..max] + "…";

    // ---------- persistence + helpers ----------

    private async Task<ConversationSession> GetOrCreateSessionAsync(
        Guid tenantId, string sessionId, string channel, CancellationToken ct)
    {
        var session = await db.ConversationSessions
            .FirstOrDefaultAsync(s => s.TenantId == tenantId && s.SessionId == sessionId, ct);
        if (session == null)
        {
            session = ConversationSession.Create(tenantId, sessionId, channel);
            db.ConversationSessions.Add(session);
            try { await db.SaveChangesAsync(ct); }
            catch (DbUpdateException)
            {
                // Concurrent create hit the unique (tenant, session) key — reload the winner.
                db.Entry(session).State = EntityState.Detached;
                session = await db.ConversationSessions
                    .FirstAsync(s => s.TenantId == tenantId && s.SessionId == sessionId, ct);
            }
        }
        session.Touch();
        return session;
    }

    private async Task<TenantAiConfig?> ResolveConfigAsync(Guid tenantId, bool useDraft, CancellationToken ct)
        => await db.TenantAiConfigs.AsNoTracking()
            .Where(c => c.TenantId == tenantId && (useDraft || c.Status == TenantAiConfigStatus.Published))
            .OrderByDescending(c => c.Version)
            .FirstOrDefaultAsync(ct);

    private static bool ContainsRefusedTopic(string text, AiPolicy? policy)
    {
        var topics = ParseStringArray(policy?.RefusedTopicsJson);
        if (topics.Count == 0) return false;
        var normalized = IntentRouter.Normalize(text);
        return topics.Any(t => normalized.Contains(IntentRouter.Normalize(t), StringComparison.Ordinal));
    }

    /// <summary>ai_policies.intent_scopes_json: {"IntentName": false} disables an intent per tenant.</summary>
    private static bool IntentScopeDisabled(string intent, AiPolicy? policy)
    {
        if (policy == null || string.IsNullOrWhiteSpace(policy.IntentScopesJson)) return false;
        try
        {
            var map = JsonSerializer.Deserialize<Dictionary<string, bool>>(policy.IntentScopesJson);
            return map != null && map.TryGetValue(intent, out var enabled) && !enabled;
        }
        catch (JsonException) { return false; }
    }

    private async Task PersistTurnAsync(
        Guid tenantId, string sessionId, string messageId, string userText, string answer,
        string? intent, List<Guid> refs, List<string> sources, double confidence,
        bool refused, string? refuseReason, bool degraded,
        int? promptTokens, int? completionTokens, CancellationToken ct)
    {
        var expires = DateTime.UtcNow.AddDays(RetentionDays); // [G10] 90d retention
        db.ConversationMessages.Add(ConversationMessage.Create(
            tenantId, sessionId, messageId, MessageRole.User, Trim(userText, 4000), expires));
        var assistant = ConversationMessage.Create(
            tenantId, sessionId, messageId + ":reply", MessageRole.Assistant, answer, expires);
        assistant.SetAnswerMeta(intent,
            refs.Count > 0 ? JsonSerializer.Serialize(refs) : null,
            sources.Count > 0 ? JsonSerializer.Serialize(sources) : null,
            confidence, refused, refuseReason, degraded, promptTokens, completionTokens);
        db.ConversationMessages.Add(assistant);
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateException)
        {
            // [G8] concurrent replay — the first writer's assistant turn wins.
            logger.LogInformation("chat persist dedupe tenant={Tenant} session={Session} msg={Msg}",
                tenantId, sessionId, messageId);
        }
    }

    private async Task<IReadOnlyList<ChatProductDto>> ToProductDtosAsync(List<Guid> refs, CancellationToken ct)
    {
        if (refs.Count == 0) return [];
        var products = await db.ProductKnowledge.AsNoTracking()
            .Where(p => refs.Contains(p.ProductId)).ToListAsync(ct);
        // FR-5: only available, non-stale products may be suggested to cart.
        // Order follows the validated refs (retrieval ranking — e.g. calories asc).
        return products.Where(p => p.Available && !IsStale(p))
            .OrderBy(p => refs.IndexOf(p.ProductId))
            .Select(p => new ChatProductDto(p.ProductId, p.Name, p.Price, null))
            .ToList();
    }

    private async Task<ChatResult> ReplayedResultAsync(Guid tenantId, ConversationMessage prior, CancellationToken ct)
    {
        List<Guid> refs = [];
        List<string> sources = [];
        try
        {
            if (prior.ProductRefsJson != null)
                refs = JsonSerializer.Deserialize<List<Guid>>(prior.ProductRefsJson) ?? [];
            if (prior.SourceIdsJson != null)
                sources = JsonSerializer.Deserialize<List<string>>(prior.SourceIdsJson) ?? [];
        }
        catch (JsonException) { }
        var products = await ToProductDtosAsync(refs, ct);
        return new ChatResult(prior.Content, products, sources, prior.Confidence ?? 0,
            prior.Refused, prior.RefuseReason, StaleData: false, prior.Degraded, prior.Intent, 0);
    }

    private static List<string> ParseStringArray(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return [];
        try { return JsonSerializer.Deserialize<List<string>>(json) ?? []; }
        catch (JsonException) { return []; }
    }
}
