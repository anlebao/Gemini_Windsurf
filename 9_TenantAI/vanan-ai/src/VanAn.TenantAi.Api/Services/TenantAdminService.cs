using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using VanAn.TenantAi.Contracts;
using VanAn.TenantAi.Domain;
using VanAn.TenantAi.Infra;

namespace VanAn.TenantAi.Api.Services;

/// <summary>
/// task_ai_p6b — admin operations (SRS §6.3): profile/config lifecycle [G11],
/// per-tenant policy, conversation review, usage aggregate, tenant purge [NFR-7].
/// TenantId always comes from the signed ctx claim — route tenant must match.
/// </summary>
public sealed class TenantAdminService(TenantAiDbContext db)
{
    private const int MaxPageSize = 100;

    // ---------- Profile / config lifecycle [G11] ----------

    public async Task<ServiceResult<PutProfileResponse>> PutProfileAsync(
        Guid tenantId, PutProfileRequest req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.AiName) || req.AiName.Trim().Length > 128)
            return new(null, "invalid_ai_name");
        var persona = req.PersonaJson ?? "{}";
        var business = req.BusinessProfileJson ?? "{}";
        if (!IsJson(persona) || !IsJson(business))
            return new(null, "invalid_json");

        var nextVersion = (await db.TenantAiConfigs
            .Where(c => c.TenantId == tenantId)
            .MaxAsync(c => (int?)c.Version, ct) ?? 0) + 1;
        // [G11] append-only — PUT always creates a new Draft; publish flips status.
        db.TenantAiConfigs.Add(TenantAiConfig.CreateDraft(
            tenantId, nextVersion, req.AiName.Trim(), persona, business));
        await db.SaveChangesAsync(ct);
        return new(new PutProfileResponse(nextVersion, "draft"), null);
    }

    public async Task<ProfileResponse> GetProfileAsync(Guid tenantId, CancellationToken ct)
    {
        // task_ai_p10 — include recent version history (all statuses) for the rollback picker.
        var configs = await db.TenantAiConfigs.AsNoTracking()
            .Where(c => c.TenantId == tenantId)
            .OrderByDescending(c => c.Version)
            .Take(20)
            .ToListAsync(ct);
        var draft = configs.FirstOrDefault(c => c.Status == TenantAiConfigStatus.Draft);
        var published = configs.FirstOrDefault(c => c.Status == TenantAiConfigStatus.Published);
        return new ProfileResponse(ToDto(draft), ToDto(published),
            configs.Select(c => ToDto(c)!).ToList());
    }

    public async Task<ServiceResult<PublishResponse>> PublishAsync(
        Guid tenantId, string callerId, CancellationToken ct)
    {
        var draft = await db.TenantAiConfigs
            .Where(c => c.TenantId == tenantId && c.Status == TenantAiConfigStatus.Draft)
            .OrderByDescending(c => c.Version)
            .FirstOrDefaultAsync(ct);
        if (draft == null)
            return new(null, "no_draft");

        var published = await db.TenantAiConfigs
            .Where(c => c.TenantId == tenantId && c.Status == TenantAiConfigStatus.Published)
            .ToListAsync(ct);
        foreach (var p in published) p.MarkArchived();

        draft.MarkPublished(callerId);
        await db.SaveChangesAsync(ct);
        return new(new PublishResponse(draft.Version, "published",
            draft.PublishedAtUtc!.Value, callerId), null);
    }

    public async Task<ServiceResult<PublishResponse>> RollbackAsync(
        Guid tenantId, int version, string callerId, CancellationToken ct)
    {
        var source = await db.TenantAiConfigs.AsNoTracking()
            .FirstOrDefaultAsync(c => c.TenantId == tenantId && c.Version == version, ct);
        if (source == null)
            return new(null, "version_not_found");

        // Rollback = new version copying the target content — history stays append-only.
        var put = await PutProfileAsync(tenantId,
            new PutProfileRequest(source.AiName, source.PersonaJson, source.BusinessProfileJson), ct);
        if (put.Value == null) return new(null, put.ErrorCode);
        return await PublishAsync(tenantId, callerId, ct);
    }

    // ---------- Policy ----------

    /// <summary>Current version = newest config row (Draft preferred — what preview edits).</summary>
    private async Task<TenantAiConfig?> CurrentConfigAsync(Guid tenantId, CancellationToken ct)
        => await db.TenantAiConfigs.AsNoTracking()
            .Where(c => c.TenantId == tenantId
                && c.Status != TenantAiConfigStatus.Archived)
            .OrderByDescending(c => c.Status == TenantAiConfigStatus.Draft ? 0 : 1)
            .ThenByDescending(c => c.Version)
            .FirstOrDefaultAsync(ct);

    public async Task<PolicyResponse> GetPolicyAsync(Guid tenantId, CancellationToken ct)
    {
        var cfg = await CurrentConfigAsync(tenantId, ct);
        if (cfg == null)
            return new PolicyResponse(null, "{}", "[]", "[]");
        var policy = await db.AiPolicies.AsNoTracking()
            .FirstOrDefaultAsync(p => p.TenantId == tenantId && p.ConfigVersion == cfg.Version, ct);
        return new PolicyResponse(cfg.Version,
            policy?.IntentScopesJson ?? "{}",
            policy?.ExternalAllowJson ?? "[]",
            policy?.RefusedTopicsJson ?? "[]");
    }

    public async Task<ServiceResult<PolicyResponse>> PutPolicyAsync(
        Guid tenantId, PutPolicyRequest req, CancellationToken ct)
    {
        var cfg = await CurrentConfigAsync(tenantId, ct);
        if (cfg == null)
            return new(null, "no_config");

        var scopes = req.IntentScopesJson ?? "{}";
        var external = req.ExternalAllowJson ?? "[]";
        var topics = req.RefusedTopicsJson ?? "[]";
        if (!IsJson(scopes) || !IsJson(external) || !IsJson(topics))
            return new(null, "invalid_json");
        // External Knowledge Gateway is P3 — v1 accepts the field but rejects non-empty.
        if (!IsEmptyArray(external))
            return new(null, "external_p3");

        var policy = await db.AiPolicies
            .FirstOrDefaultAsync(p => p.TenantId == tenantId && p.ConfigVersion == cfg.Version, ct);
        if (policy == null)
            db.AiPolicies.Add(AiPolicy.Create(tenantId, cfg.Version, scopes, external, topics));
        else
            policy.Update(scopes, external, topics);
        await db.SaveChangesAsync(ct);
        return new(new PolicyResponse(cfg.Version, scopes, external, topics), null);
    }

    // ---------- Conversations (review, read-only) ----------

    public async Task<ConversationListResponse> GetConversationsAsync(
        Guid tenantId, int page, int size, CancellationToken ct)
    {
        page = Math.Max(1, page);
        size = Math.Clamp(size, 1, MaxPageSize);

        var query = db.ConversationSessions.AsNoTracking()
            .Where(s => s.TenantId == tenantId)
            .OrderByDescending(s => s.LastActiveAtUtc);
        var total = await query.CountAsync(ct);
        var sessions = await query.Skip((page - 1) * size).Take(size).ToListAsync(ct);

        var sessionIds = sessions.Select(s => s.SessionId).ToList();
        var stats = await db.ConversationMessages.AsNoTracking()
            .Where(m => m.TenantId == tenantId && sessionIds.Contains(m.SessionId))
            .GroupBy(m => m.SessionId)
            .Select(g => new
            {
                SessionId = g.Key,
                Count = g.Count(),
                Refused = g.Count(m => m.Refused),
                Degraded = g.Count(m => m.Degraded)
            })
            .ToListAsync(ct);
        var bySession = stats.ToDictionary(s => s.SessionId);

        var items = sessions.Select(s =>
        {
            bySession.TryGetValue(s.SessionId, out var st);
            return new ConversationSummaryDto(s.SessionId, s.Channel, s.LastActiveAtUtc,
                st?.Count ?? 0, st?.Refused ?? 0, st?.Degraded ?? 0);
        }).ToList();
        return new ConversationListResponse(page, size, total, items);
    }

    public async Task<ConversationDetailResponse?> GetConversationMessagesAsync(
        Guid tenantId, string sessionId, CancellationToken ct)
    {
        var exists = await db.ConversationSessions.AsNoTracking()
            .AnyAsync(s => s.TenantId == tenantId && s.SessionId == sessionId, ct);
        if (!exists) return null;

        var msgs = await db.ConversationMessages.AsNoTracking()
            .Where(m => m.TenantId == tenantId && m.SessionId == sessionId)
            .OrderBy(m => m.CreatedAtUtc)
            .Take(500)
            .ToListAsync(ct);
        var items = msgs.Select(m => new ConversationMessageDto(
            m.Role == MessageRole.User ? "user" : "assistant",
            m.Content, m.Intent, m.Confidence, m.Refused, m.Degraded, m.CreatedAtUtc)).ToList();
        return new ConversationDetailResponse(sessionId, items);
    }

    // ---------- Usage aggregate (NFR-6/NFR-8) ----------

    public async Task<UsageResponse> GetUsageAsync(Guid tenantId, int days, CancellationToken ct)
    {
        days = Math.Clamp(days, 1, 365);
        var since = DateTime.UtcNow.Date.AddDays(-days);
        var rows = await db.ConversationMessages.AsNoTracking()
            .Where(m => m.TenantId == tenantId
                && m.Role == MessageRole.Assistant
                && m.CreatedAtUtc >= since)
            .GroupBy(m => m.CreatedAtUtc.Date)
            .Select(g => new
            {
                Date = g.Key,
                Messages = g.Count(),
                Refused = g.Count(m => m.Refused),
                Degraded = g.Count(m => m.Degraded),
                Prompt = g.Sum(m => (long?)(m.PromptTokens ?? 0)),
                Completion = g.Sum(m => (long?)(m.CompletionTokens ?? 0))
            })
            .OrderBy(x => x.Date)
            .ToListAsync(ct);

        var perDay = rows.Select(r => new UsageDayDto(
            r.Date.ToString("yyyy-MM-dd"), r.Messages, r.Refused, r.Degraded,
            r.Prompt ?? 0, r.Completion ?? 0)).ToList();
        var total = new UsageDayDto("total",
            perDay.Sum(d => d.Messages), perDay.Sum(d => d.Refused), perDay.Sum(d => d.Degraded),
            perDay.Sum(d => d.PromptTokens), perDay.Sum(d => d.CompletionTokens));
        return new UsageResponse(days, perDay, total);
    }

    // ---------- Purge [NFR-7] ----------

    public async Task<PurgeResponse> PurgeTenantAsync(Guid tenantId, CancellationToken ct)
    {
        var chunks = await db.KnowledgeChunks.Where(c => c.TenantId == tenantId).ToListAsync(ct);
        db.KnowledgeChunks.RemoveRange(chunks);
        var docs = await db.KnowledgeDocuments.Where(d => d.TenantId == tenantId).ToListAsync(ct);
        db.KnowledgeDocuments.RemoveRange(docs);
        var products = await db.ProductKnowledge.Where(p => p.TenantId == tenantId).ToListAsync(ct);
        db.ProductKnowledge.RemoveRange(products);
        var configs = await db.TenantAiConfigs.Where(c => c.TenantId == tenantId).ToListAsync(ct);
        db.TenantAiConfigs.RemoveRange(configs);
        var policies = await db.AiPolicies.Where(p => p.TenantId == tenantId).ToListAsync(ct);
        db.AiPolicies.RemoveRange(policies);
        var perms = await db.KnowledgeUsagePermissions.Where(p => p.TenantId == tenantId).ToListAsync(ct);
        db.KnowledgeUsagePermissions.RemoveRange(perms);
        var messages = await db.ConversationMessages.Where(m => m.TenantId == tenantId).ToListAsync(ct);
        db.ConversationMessages.RemoveRange(messages);
        var sessions = await db.ConversationSessions.Where(s => s.TenantId == tenantId).ToListAsync(ct);
        db.ConversationSessions.RemoveRange(sessions);
        var logs = await db.IngestLogs.Where(l => l.TenantId == tenantId).ToListAsync(ct);
        db.IngestLogs.RemoveRange(logs);
        var routes = await db.IntentRoutes.Where(r => r.TenantId == tenantId).ToListAsync(ct);
        db.IntentRoutes.RemoveRange(routes);

        await db.SaveChangesAsync(ct);
        return new PurgeResponse(
            configs.Count, policies.Count, perms.Count, products.Count, docs.Count,
            chunks.Count, sessions.Count, messages.Count, logs.Count, routes.Count);
    }

    private static ProfileVersionDto? ToDto(TenantAiConfig? c)
        => c == null ? null : new ProfileVersionDto(
            c.Version, c.Status.ToString().ToLowerInvariant(),
            c.AiName, c.PersonaJson, c.BusinessProfileJson, c.PublishedAtUtc, c.PublishedBy);

    private static bool IsJson(string json)
    {
        try { JsonDocument.Parse(json); return true; }
        catch (JsonException) { return false; }
    }

    private static bool IsEmptyArray(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            return doc.RootElement.ValueKind == JsonValueKind.Array
                && !doc.RootElement.EnumerateArray().Any();
        }
        catch (JsonException) { return false; }
    }
}
