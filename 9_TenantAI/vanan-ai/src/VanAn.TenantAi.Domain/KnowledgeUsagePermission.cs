namespace VanAn.TenantAi.Domain;

/// <summary>Decision of a usage-permission matrix row (SRS [G15]). v1.1: only Public passes to public channel.</summary>
public enum UsagePermission
{
    Allow,
    Deny,
    ConsultFactsOnly
}

/// <summary>
/// SRS v1.1 §7 + [G15] — policy-as-data for the post-retrieval context filter:
/// intent × classification × channel → permission. TenantId null = system default row;
/// tenant rows override defaults. Defense-in-depth independent of PolicyEngine.
/// </summary>
public sealed class KnowledgeUsagePermission
{
    public Guid Id { get; private set; }

    /// <summary>null = system default applied to every tenant.</summary>
    public Guid? TenantId { get; private set; }

    /// <summary>Intent label (MenuQuery, NutritionQuery, …) — FR-2 set.</summary>
    public string Intent { get; private set; } = string.Empty;

    public DocumentClassification Classification { get; private set; }

    /// <summary>Channel label: public | admin | preview.</summary>
    public string Channel { get; private set; } = string.Empty;

    public UsagePermission Permission { get; private set; }

    private KnowledgeUsagePermission() { } // EF Core

    public static KnowledgeUsagePermission Create(
        Guid? tenantId,
        string intent,
        DocumentClassification classification,
        string channel,
        UsagePermission permission)
        => new()
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Intent = intent,
            Classification = classification,
            Channel = channel,
            Permission = permission
        };
}
