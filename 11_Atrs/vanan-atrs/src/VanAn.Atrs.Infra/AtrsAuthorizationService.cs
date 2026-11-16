using VanAn.Atrs.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace VanAn.Atrs.Infra;

/// <summary>
/// ATRS-10 subset (D5, FR-014): backend-authoritative permission check cho staff endpoints.
/// Thứ tự: ① SystemAdmin role → break-glass allow (controller check role, audit riêng)
/// ② StaffPermissionGrant active (permission + scope tenant + chưa expire/revoke)
/// ③ Legacy role map (config Atrs:LegacyRolePermissions, default Owner → recovery.review+approve
///    — giữ behavior InternalRecoveryController cũ trong transition).
/// </summary>
public interface IAtrsAuthorizationService
{
    Task<bool> HasAsync(Guid staffUserId, string permission, Guid? scopeTenantId = null, CancellationToken ct = default);

    /// <summary>Roles của staff (từ JWT claims) — truyền vào để check legacy map.</summary>
    Task<bool> HasAsync(Guid staffUserId, IReadOnlyList<string> roles, string permission, Guid? scopeTenantId = null, CancellationToken ct = default);

    Task<IReadOnlyList<string>> ListGrantsAsync(Guid staffUserId, CancellationToken ct = default);

    // ── Grant management (SystemAdmin only — enforce ở controller) ──
    Task<IReadOnlyList<StaffPermissionGrant>> ListAllGrantsAsync(Guid? demoUserId = null, CancellationToken ct = default);
    Task<StaffPermissionGrant> GrantAsync(Guid demoUserId, string permission, string grantedBy, Guid? scopeTenantId = null, DateTime? expiresAt = null, CancellationToken ct = default);
    Task<bool> RevokeAsync(Guid grantId, CancellationToken ct = default);
}

public sealed class AtrsAuthorizationService(AtrsDbContext db, IConfiguration configuration) : IAtrsAuthorizationService
{
    public Task<bool> HasAsync(Guid staffUserId, string permission, Guid? scopeTenantId = null, CancellationToken ct = default)
        => HasAsync(staffUserId, [], permission, scopeTenantId, ct);

    public async Task<bool> HasAsync(Guid staffUserId, IReadOnlyList<string> roles, string permission, Guid? scopeTenantId = null, CancellationToken ct = default)
    {
        if (staffUserId == Guid.Empty || string.IsNullOrWhiteSpace(permission))
            return false;

        // SystemAdmin = break-glass (FR-014) — mọi permission, audit ở controller.
        if (roles.Contains("SystemAdmin", StringComparer.OrdinalIgnoreCase))
            return true;

        var now = DateTime.UtcNow;
        bool granted = await db.StaffPermissionGrants
            .AsNoTracking()
            .AnyAsync(g => g.DemoUserId == staffUserId
                && g.Permission == permission
                && !g.IsRevoked
                && (g.ExpiresAt == null || g.ExpiresAt > now)
                // Fail-closed scope: grant scoped-tenant chỉ match request CÙNG tenant;
                // request không scope (account-level) cần grant ScopeTenantId == null.
                && (g.ScopeTenantId == null || g.ScopeTenantId == scopeTenantId), ct);
        if (granted)
            return true;

        // Legacy role fallback — transition: Owner giữ recovery review/approve (behavior cũ).
        foreach (var role in roles)
        {
            var perms = configuration[$"Atrs:LegacyRolePermissions:{role}"];
            if (perms is null)
                continue;
            if (perms.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Contains(permission, StringComparer.OrdinalIgnoreCase))
                return true;
        }

        // Default legacy: Owner → recovery perms (khi config chưa set).
        if (roles.Contains("Owner", StringComparer.OrdinalIgnoreCase)
            && permission is AtrsPermissions.RecoveryReview or AtrsPermissions.RecoveryApprove or AtrsPermissions.RecoveryView)
            return true;

        return false;
    }

    public async Task<IReadOnlyList<string>> ListGrantsAsync(Guid staffUserId, CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        return await db.StaffPermissionGrants
            .AsNoTracking()
            .Where(g => g.DemoUserId == staffUserId && !g.IsRevoked && (g.ExpiresAt == null || g.ExpiresAt > now))
            .Select(g => g.Permission)
            .Distinct()
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<StaffPermissionGrant>> ListAllGrantsAsync(Guid? demoUserId = null, CancellationToken ct = default)
    {
        var q = db.StaffPermissionGrants.AsNoTracking().AsQueryable();
        if (demoUserId.HasValue)
            q = q.Where(g => g.DemoUserId == demoUserId.Value);
        return await q.OrderByDescending(g => g.GrantedAt).Take(500).ToListAsync(ct);
    }

    public async Task<StaffPermissionGrant> GrantAsync(Guid demoUserId, string permission, string grantedBy, Guid? scopeTenantId = null, DateTime? expiresAt = null, CancellationToken ct = default)
    {
        // Idempotent-ish: đã có grant active giống hệt → trả cái cũ.
        var now = DateTime.UtcNow;
        var existing = await db.StaffPermissionGrants.FirstOrDefaultAsync(g =>
            g.DemoUserId == demoUserId && g.Permission == permission
            && g.ScopeTenantId == scopeTenantId && !g.IsRevoked
            && (g.ExpiresAt == null || g.ExpiresAt > now), ct);
        if (existing is not null)
            return existing;

        var grant = new StaffPermissionGrant(Guid.NewGuid(), demoUserId, permission, grantedBy, scopeTenantId, expiresAt);
        db.StaffPermissionGrants.Add(grant);
        await db.SaveChangesAsync(ct);
        return grant;
    }

    public async Task<bool> RevokeAsync(Guid grantId, CancellationToken ct = default)
    {
        var grant = await db.StaffPermissionGrants.FirstOrDefaultAsync(g => g.Id == grantId, ct);
        if (grant is null || grant.IsRevoked)
            return false;
        grant.Revoke();
        await db.SaveChangesAsync(ct);
        return true;
    }
}
