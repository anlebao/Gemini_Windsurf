namespace VanAn.TenantAi.Domain;

/// <summary>Lifecycle of a config version (SRS [G11] — Draft → Publish; rollback = activate older version).</summary>
public enum TenantAiConfigStatus
{
    Draft,
    Published,
    Archived
}

/// <summary>
/// SRS v1.1 §7 — versioned per-tenant AI config: persona + business profile.
/// Append-only per version: one row per (TenantId, Version); public chat resolves
/// the newest Published row. Never updated in place — publish creates a new version.
/// </summary>
public sealed class TenantAiConfig
{
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public int Version { get; private set; }
    public TenantAiConfigStatus Status { get; private set; }
    public string AiName { get; private set; } = string.Empty;
    public string PersonaJson { get; private set; } = "{}";
    public string BusinessProfileJson { get; private set; } = "{}";
    public DateTime? PublishedAtUtc { get; private set; }
    public string? PublishedBy { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }

    private TenantAiConfig() { } // EF Core

    public static TenantAiConfig CreateDraft(
        Guid tenantId,
        int version,
        string aiName,
        string personaJson = "{}",
        string businessProfileJson = "{}")
        => new()
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Version = version,
            Status = TenantAiConfigStatus.Draft,
            AiName = aiName,
            PersonaJson = personaJson,
            BusinessProfileJson = businessProfileJson,
            CreatedAtUtc = DateTime.UtcNow
        };

    public void MarkPublished(string publishedBy)
    {
        Status = TenantAiConfigStatus.Published;
        PublishedAtUtc = DateTime.UtcNow;
        PublishedBy = publishedBy;
    }

    public void MarkArchived() => Status = TenantAiConfigStatus.Archived;
}
