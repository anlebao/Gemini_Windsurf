namespace VanAn.TenantAi.Domain;

/// <summary>Bitmask over knowledge sources an intent may draw from (FR-3 matrix as data).</summary>
[Flags]
public enum KnowledgeSourceFlags : long
{
    None = 0,
    ProductKnowledge = 1,
    PublicDocuments = 2,
    ExternalKnowledge = 4,
    Persona = 8,
    StaticTemplate = 16
}

/// <summary>
/// SRS v1.1 §7 — policy-as-data intent routing. TenantId null = system default row;
/// tenant rows override per intent. PolicyEngine reads this table, never hardcodes.
/// </summary>
public sealed class IntentRoute
{
    public Guid Id { get; private set; }

    /// <summary>null = system default applied when tenant has no override for the intent.</summary>
    public Guid? TenantId { get; private set; }

    /// <summary>Intent label (MenuQuery, Faq, OutOfScope, …) — FR-2 set.</summary>
    public string Intent { get; private set; } = string.Empty;

    public KnowledgeSourceFlags AllowedSourceFlags { get; private set; }

    /// <summary>true = intent requires retrieval facts; no facts → "chưa có dữ liệu" (FR-3).</summary>
    public bool RequiresFacts { get; private set; }

    public string? RefuseTemplate { get; private set; }

    private IntentRoute() { } // EF Core

    public static IntentRoute Create(
        Guid? tenantId,
        string intent,
        KnowledgeSourceFlags allowedSourceFlags,
        bool requiresFacts,
        string? refuseTemplate = null)
        => new()
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Intent = intent,
            AllowedSourceFlags = allowedSourceFlags,
            RequiresFacts = requiresFacts,
            RefuseTemplate = refuseTemplate
        };
}
