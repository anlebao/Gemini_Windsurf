using VanAn.Shared.Domain;
using PermissionGroup = VanAn.Shared.Domain.Aggregates.UserAggregate.PermissionGroup;
using UserRole = VanAn.Shared.Domain.Aggregates.UserAggregate.UserRole;

namespace VanAn.CoreHub.Services
{
    /// <summary>
    /// Permission group management service — Wave 6.
    /// Owner-only operations: create, update, add/remove roles, list groups.
    /// </summary>
    public interface IPermissionGroupService
    {
        /// <summary>Creates a new permission group.</summary>
        Task<PermissionGroup> CreateGroupAsync(TenantId tenantId, string name, string? description, CancellationToken ct = default);

        /// <summary>Returns group by ID within the specified tenant.</summary>
        Task<PermissionGroup?> GetGroupAsync(Guid groupId, TenantId tenantId, CancellationToken ct = default);

        /// <summary>Lists all groups in the tenant.</summary>
        Task<IReadOnlyList<PermissionGroup>> ListGroupsAsync(TenantId tenantId, CancellationToken ct = default);

        /// <summary>Updates group name and description.</summary>
        Task UpdateGroupAsync(Guid groupId, TenantId tenantId, string name, string? description, CancellationToken ct = default);

        /// <summary>Adds a role to a group.</summary>
        Task AddRoleToGroupAsync(Guid groupId, TenantId tenantId, UserRole role, CancellationToken ct = default);

        /// <summary>Removes a role from a group.</summary>
        Task RemoveRoleFromGroupAsync(Guid groupId, TenantId tenantId, UserRole role, CancellationToken ct = default);
    }
}
