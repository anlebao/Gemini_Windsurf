namespace VanAn.TenantAi.Domain;

/// <summary>
/// SRS v1.1 §7 — per-tenant policy bound to a config version (FR-3 intent→source matrix,
/// refused topics, external allow-list deferred to P3). One policy per (TenantId, ConfigVersion).
/// </summary>
public sealed class AiPolicy
{
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public int ConfigVersion { get; private set; }
    public string IntentScopesJson { get; private set; } = "{}";
    public string ExternalAllowJson { get; private set; } = "[]";
    public string RefusedTopicsJson { get; private set; } = "[]";

    private AiPolicy() { } // EF Core

    public static AiPolicy Create(
        Guid tenantId,
        int configVersion,
        string intentScopesJson = "{}",
        string externalAllowJson = "[]",
        string refusedTopicsJson = "[]")
        => new()
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ConfigVersion = configVersion,
            IntentScopesJson = intentScopesJson,
            ExternalAllowJson = externalAllowJson,
            RefusedTopicsJson = refusedTopicsJson
        };

    /// <summary>task_ai_p6b — upsert in place (unique per TenantId+ConfigVersion).</summary>
    public void Update(string intentScopesJson, string externalAllowJson, string refusedTopicsJson)
    {
        IntentScopesJson = intentScopesJson;
        ExternalAllowJson = externalAllowJson;
        RefusedTopicsJson = refusedTopicsJson;
    }
}
