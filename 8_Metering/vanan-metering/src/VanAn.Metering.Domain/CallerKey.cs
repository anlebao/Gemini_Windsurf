namespace VanAn.Metering.Domain;

/// <summary>
/// SRS §7 — service-to-service credential. Each caller system (shoperp-a, gateway, …)
/// holds one API key; only the SHA-256 hash is stored (SRS NFR-8).
/// </summary>
public sealed class CallerKey
{
    public Guid Id { get; private set; }
    public string CallerId { get; private set; } = string.Empty;
    public string KeyHash { get; private set; } = string.Empty;
    public bool IsActive { get; private set; }
    public bool IsAdmin { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }

    private CallerKey() { } // EF Core

    public static CallerKey Create(string callerId, string keyHash, bool isAdmin = false)
        => new()
        {
            Id = Guid.NewGuid(),
            CallerId = callerId,
            KeyHash = keyHash,
            IsActive = true,
            IsAdmin = isAdmin,
            CreatedAtUtc = DateTime.UtcNow
        };
}
