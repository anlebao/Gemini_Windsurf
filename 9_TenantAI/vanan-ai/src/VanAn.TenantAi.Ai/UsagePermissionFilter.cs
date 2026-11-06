using VanAn.TenantAi.Domain;

namespace VanAn.TenantAi.Ai;

/// <summary>
/// task_ai_p3 / SRS [G15] + FR-11 — post-retrieval, pre-prompt context filter.
/// Every item is checked against `intent × classification × channel`; Deny
/// drops it. Tenant rows override system defaults; Intent "*" matches all
/// intents. Missing matrix row = fail closed (non-Public drops, Public on the
/// public/preview channel passes). Independent defense layer — a PolicyEngine
/// bug alone cannot leak [G15].
/// </summary>
public static class UsagePermissionFilter
{
    public sealed record Result(IReadOnlyList<ContextItem> Kept, int Dropped);

    public static Result Apply(
        string intent,
        string channel,
        IReadOnlyList<ContextItem> items,
        IReadOnlyList<KnowledgeUsagePermission> tenantRules,
        IReadOnlyList<KnowledgeUsagePermission> systemRules,
        Action<string>? onDrop = null)
    {
        var kept = new List<ContextItem>(items.Count);
        var dropped = 0;
        foreach (var item in items)
        {
            var permission = Resolve(intent, item.Classification, channel, tenantRules, systemRules);
            if (permission == UsagePermission.Deny)
            {
                dropped++;
                onDrop?.Invoke(item.SourceId);
                continue;
            }
            kept.Add(item);
        }
        return new Result(kept, dropped);
    }

    private static UsagePermission Resolve(
        string intent,
        DocumentClassification classification,
        string channel,
        IReadOnlyList<KnowledgeUsagePermission> tenantRules,
        IReadOnlyList<KnowledgeUsagePermission> systemRules)
    {
        static bool Match(KnowledgeUsagePermission r, string intent, string channel)
            => (r.Intent == intent || r.Intent == "*") && r.Channel == channel;

        var tenant = tenantRules
            .Where(r => Match(r, intent, channel) && r.Classification == classification)
            .OrderByDescending(r => r.Intent == intent) // specific intent beats "*"
            .FirstOrDefault();
        if (tenant != null) return tenant.Permission;

        var system = systemRules
            .Where(r => Match(r, intent, channel) && r.Classification == classification)
            .OrderByDescending(r => r.Intent == intent)
            .FirstOrDefault();
        if (system != null) return system.Permission;

        // No row — fail closed: only Public material may reach public-ish channels.
        return classification == DocumentClassification.Public
            ? UsagePermission.Allow
            : UsagePermission.Deny;
    }
}
