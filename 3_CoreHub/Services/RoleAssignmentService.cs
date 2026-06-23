using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using VanAn.CoreHub.Infrastructure;
using VanAn.Shared.Domain;
using UserRole = VanAn.Shared.Domain.Aggregates.UserAggregate.UserRole;
using UserTenant = VanAn.Shared.Domain.Aggregates.UserAggregate.UserTenant;
using UserPermissionGroup = VanAn.Shared.Domain.Aggregates.UserAggregate.UserPermissionGroup;

namespace VanAn.CoreHub.Services
{
    /// <summary>
    /// Role assignment and permission group membership — Wave 6.
    /// Enforces cross-tenant isolation for all operations.
    /// </summary>
    public class RoleAssignmentService(
        IVanAnDbContext dbContext,
        ILogger<RoleAssignmentService> logger) : IRoleAssignmentService
    {
        public async Task AssignRoleToUserAsync(Guid userId, TenantId tenantId, UserRole role, CancellationToken ct = default)
        {
            await EnsureUserBelongsToTenantAsync(userId, tenantId, ct);

            var existing = await dbContext.UserTenants
                .FirstOrDefaultAsync(ut => ut.UserId == userId && ut.TenantIdValue == tenantId.Value && ut.Role == role, ct);

            if (existing != null)
            {
                existing.Reactivate();
            }
            else
            {
                dbContext.UserTenants.Add(new UserTenant(userId, tenantId.Value, role));
            }

            await dbContext.SaveChangesAsync(ct);
            logger.LogInformation("Role {Role} assigned to user {UserId} in tenant {TenantId}", role, userId, tenantId.Value);
        }

        public async Task RevokeRoleAsync(Guid userId, TenantId tenantId, UserRole role, CancellationToken ct = default)
        {
            await EnsureUserBelongsToTenantAsync(userId, tenantId, ct);

            var existing = await dbContext.UserTenants
                .FirstOrDefaultAsync(ut => ut.UserId == userId && ut.TenantIdValue == tenantId.Value && ut.Role == role && ut.IsActive, ct);

            if (existing != null)
            {
                existing.Deactivate();
                await dbContext.SaveChangesAsync(ct);
                logger.LogInformation("Role {Role} revoked from user {UserId} in tenant {TenantId}", role, userId, tenantId.Value);
            }
        }

        public async Task<IReadOnlyList<UserRole>> GetUserRolesAsync(Guid userId, TenantId tenantId, CancellationToken ct = default)
        {
            await EnsureUserBelongsToTenantAsync(userId, tenantId, ct);

            return await dbContext.UserTenants
                .Where(ut => ut.UserId == userId && ut.TenantIdValue == tenantId.Value && ut.IsActive)
                .Select(ut => ut.Role)
                .Distinct()
                .ToListAsync(ct);
        }

        public async Task AssignUserToGroupAsync(Guid userId, Guid groupId, TenantId tenantId, CancellationToken ct = default)
        {
            await EnsureUserBelongsToTenantAsync(userId, tenantId, ct);
            await EnsureGroupBelongsToTenantAsync(groupId, tenantId, ct);

            var existing = await dbContext.UserPermissionGroups
                .FirstOrDefaultAsync(upg => upg.UserId == userId && upg.GroupId == groupId && upg.TenantId == tenantId, ct);

            if (existing != null)
            {
                existing.Reactivate();
            }
            else
            {
                dbContext.UserPermissionGroups.Add(new UserPermissionGroup(userId, groupId, tenantId));
            }

            await dbContext.SaveChangesAsync(ct);
            logger.LogInformation("User {UserId} assigned to group {GroupId} in tenant {TenantId}", userId, groupId, tenantId.Value);
        }

        public async Task RemoveUserFromGroupAsync(Guid userId, Guid groupId, TenantId tenantId, CancellationToken ct = default)
        {
            await EnsureUserBelongsToTenantAsync(userId, tenantId, ct);
            await EnsureGroupBelongsToTenantAsync(groupId, tenantId, ct);

            var existing = await dbContext.UserPermissionGroups
                .FirstOrDefaultAsync(upg => upg.UserId == userId && upg.GroupId == groupId && upg.TenantId == tenantId && upg.IsActive, ct);

            if (existing != null)
            {
                existing.Deactivate();
                await dbContext.SaveChangesAsync(ct);
                logger.LogInformation("User {UserId} removed from group {GroupId} in tenant {TenantId}", userId, groupId, tenantId.Value);
            }
        }

        public async Task<IReadOnlyList<UserRole>> GetEffectiveRolesAsync(Guid userId, TenantId tenantId, CancellationToken ct = default)
        {
            await EnsureUserBelongsToTenantAsync(userId, tenantId, ct);

            var directRoles = await dbContext.UserTenants
                .Where(ut => ut.UserId == userId && ut.TenantIdValue == tenantId.Value && ut.IsActive)
                .Select(ut => ut.Role)
                .ToListAsync(ct);

            var groupRoles = await dbContext.UserPermissionGroups
                .Where(upg => upg.UserId == userId && upg.TenantId == tenantId && upg.IsActive)
                .Join(
                    dbContext.PermissionGroups,
                    upg => upg.GroupId,
                    g => g.Id,
                    (upg, g) => g)
                .ToListAsync(ct);

            var effective = directRoles
                .Concat(groupRoles.SelectMany(g => g.GetEffectiveRoles()))
                .Distinct()
                .ToList()
                .AsReadOnly();

            return effective;
        }

        // ── Private helpers ──────────────────────────────────────────────────────

        private async Task EnsureUserBelongsToTenantAsync(Guid userId, TenantId tenantId, CancellationToken ct)
        {
            var user = await dbContext.Users
                .AsNoTracking()
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(u => u.Id == userId, ct);

            if (user == null)
                throw new KeyNotFoundException($"User {userId} not found.");
            if (user.TenantId != tenantId)
                throw new UnauthorizedAccessException("Cross-tenant operation is not allowed.");
        }

        private async Task EnsureGroupBelongsToTenantAsync(Guid groupId, TenantId tenantId, CancellationToken ct)
        {
            var group = await dbContext.PermissionGroups
                .AsNoTracking()
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(g => g.Id == groupId, ct);

            if (group == null)
                throw new KeyNotFoundException($"Permission group {groupId} not found.");
            if (group.TenantId != tenantId)
                throw new UnauthorizedAccessException("Cross-tenant operation is not allowed.");
        }
    }
}
