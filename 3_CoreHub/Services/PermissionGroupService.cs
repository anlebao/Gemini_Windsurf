using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using VanAn.CoreHub.Infrastructure;
using VanAn.Shared.Domain;
using PermissionGroup = VanAn.Shared.Domain.Aggregates.UserAggregate.PermissionGroup;
using UserRole = VanAn.Shared.Domain.Aggregates.UserAggregate.UserRole;

namespace VanAn.CoreHub.Services
{
    /// <summary>
    /// Permission group management — Wave 6.
    /// </summary>
    public class PermissionGroupService(
        IVanAnDbContext dbContext,
        ILogger<PermissionGroupService> logger) : IPermissionGroupService
    {
        public async Task<PermissionGroup> CreateGroupAsync(TenantId tenantId, string name, string? description, CancellationToken ct = default)
        {
            if (tenantId is null || tenantId.Value == Guid.Empty)
                throw new ArgumentException("TenantId cannot be empty.", nameof(tenantId));
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Name cannot be empty.", nameof(name));

            var group = new PermissionGroup(tenantId, name, description);
            dbContext.PermissionGroups.Add(group);
            await dbContext.SaveChangesAsync(ct);

            logger.LogInformation("Permission group created: {GroupId} ({Name}) in tenant {TenantId}", group.Id, name, tenantId.Value);
            return group;
        }

        public async Task<PermissionGroup?> GetGroupAsync(Guid groupId, TenantId tenantId, CancellationToken ct = default)
        {
            var group = await dbContext.PermissionGroups
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(g => g.Id == groupId, ct);

            if (group != null && group.TenantId != tenantId)
                throw new UnauthorizedAccessException("Cross-tenant operation is not allowed.");

            return group;
        }

        public async Task<IReadOnlyList<PermissionGroup>> ListGroupsAsync(TenantId tenantId, CancellationToken ct = default)
            => await dbContext.PermissionGroups
                .Where(g => g.TenantId == tenantId)
                .OrderBy(g => g.Name)
                .ToListAsync(ct);

        public async Task UpdateGroupAsync(Guid groupId, TenantId tenantId, string name, string? description, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Name cannot be empty.", nameof(name));

            var group = await GetGroupAsync(groupId, tenantId, ct)
                ?? throw new KeyNotFoundException($"Permission group {groupId} not found.");

            group.UpdateProfile(name, description);
            await dbContext.SaveChangesAsync(ct);
            logger.LogInformation("Permission group updated: {GroupId}", groupId);
        }

        public async Task AddRoleToGroupAsync(Guid groupId, TenantId tenantId, UserRole role, CancellationToken ct = default)
        {
            var group = await GetGroupAsync(groupId, tenantId, ct)
                ?? throw new KeyNotFoundException($"Permission group {groupId} not found.");

            group.AddRole(role);
            await dbContext.SaveChangesAsync(ct);
            logger.LogInformation("Role {Role} added to group {GroupId}", role, groupId);
        }

        public async Task RemoveRoleFromGroupAsync(Guid groupId, TenantId tenantId, UserRole role, CancellationToken ct = default)
        {
            var group = await GetGroupAsync(groupId, tenantId, ct)
                ?? throw new KeyNotFoundException($"Permission group {groupId} not found.");

            group.RemoveRole(role);
            await dbContext.SaveChangesAsync(ct);
            logger.LogInformation("Role {Role} removed from group {GroupId}", role, groupId);
        }
    }
}
