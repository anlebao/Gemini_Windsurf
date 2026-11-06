namespace VanAn.TenantAi.Domain;

/// <summary>
/// SRS v1.1 [D7]/§7 — service-to-service credential. Each caller system
/// (gateway, shoperp-a, …) holds one API key; only the SHA-256 hash is stored.
/// Scopes limit what the caller may invoke; TenantScope limits which tenants
/// a non-gateway caller may address ("*" = all — gateway only).
/// </summary>
public sealed class CallerKey
{
    public Guid Id { get; private set; }
    public string CallerId { get; private set; } = string.Empty;
    public string KeyHash { get; private set; } = string.Empty;
    public bool IsActive { get; private set; }
    public bool IsAdmin { get; private set; }

    /// <summary>JSON array of scopes: public-chat | ingest | admin.</summary>
    public string ScopesJson { get; private set; } = "[]";

    /// <summary>JSON array of tenant Guids the caller may address; null/["*"] = all (gateway only).</summary>
    public string? TenantScopeJson { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }

    private CallerKey() { } // EF Core

    public static CallerKey Create(
        string callerId,
        string keyHash,
        bool isAdmin = false,
        string scopesJson = "[\"public-chat\"]",
        string? tenantScopeJson = null)
        => new()
        {
            Id = Guid.NewGuid(),
            CallerId = callerId,
            KeyHash = keyHash,
            IsActive = true,
            IsAdmin = isAdmin,
            ScopesJson = scopesJson,
            TenantScopeJson = tenantScopeJson,
            CreatedAtUtc = DateTime.UtcNow
        };
}
