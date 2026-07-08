using VanAn.Shared.Domain.Common;

namespace VanAn.CoreHub.Infrastructure.Entities;

/// <summary>
/// Platform-level user for SystemAdmin cross-tenant access.
/// Standalone entity (does NOT inherit BaseEntity, does NOT implement IMustHaveTenant):
/// PlatformUsers is global platform data shared across all tenants.
/// This avoids the multi-tenancy query filter (precedent: AccountChartEntity).
/// </summary>
public class PlatformUser
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public string Username { get; private set; } = string.Empty;
    public string PasswordHash { get; private set; } = string.Empty;
    public string DisplayName { get; private set; } = string.Empty;
    public string? Email { get; private set; }
    public PlatformRole Role { get; private set; } = PlatformRole.SystemAdmin;
    public bool IsActive { get; private set; } = true;
    public DateTime CreatedAt { get; private set; } = DateTime.UtcNow;

    private PlatformUser() { }

    public PlatformUser(string username, string passwordHash, string displayName, string? email = null)
    {
        if (string.IsNullOrWhiteSpace(username))
            throw new ArgumentException("Username is required.", nameof(username));
        if (string.IsNullOrWhiteSpace(passwordHash))
            throw new ArgumentException("PasswordHash is required.", nameof(passwordHash));
        if (string.IsNullOrWhiteSpace(displayName))
            throw new ArgumentException("DisplayName is required.", nameof(displayName));

        Username = username;
        PasswordHash = passwordHash;
        DisplayName = displayName;
        Email = email;
        Role = PlatformRole.SystemAdmin;
        IsActive = true;
        CreatedAt = DateTime.UtcNow;
    }
}
