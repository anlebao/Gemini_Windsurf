using VanAn.Shared.Domain;
using UserRole = VanAn.Shared.Domain.Aggregates.UserAggregate.UserRole;

namespace VanAn.CoreHub.Services
{
    /// <summary>
    /// Role assignment and permission group membership service — Wave 6.
    /// </summary>
    public interface IRoleAssignmentService
    {
        /// <summary>Assigns a direct role to a user within a tenant (upsert).</summary>
        Task AssignRoleToUserAsync(Guid userId, TenantId tenantId, UserRole role, CancellationToken ct = default);

        /// <summary>Revokes a direct role from a user (soft delete).</summary>
        Task RevokeRoleAsync(Guid userId, TenantId tenantId, UserRole role, CancellationToken ct = default);

        /// <summary>Returns active direct roles for a user in a tenant.</summary>
        Task<IReadOnlyList<UserRole>> GetUserRolesAsync(Guid userId, TenantId tenantId, CancellationToken ct = default);

        /// <summary>Assigns a user to a permission group.</summary>
        Task AssignUserToGroupAsync(Guid userId, Guid groupId, TenantId tenantId, CancellationToken ct = default);

        /// <summary>Removes a user from a permission group (soft delete).</summary>
        Task RemoveUserFromGroupAsync(Guid userId, Guid groupId, TenantId tenantId, CancellationToken ct = default);

        /// <summary>Returns union of direct roles and group roles (distinct).</summary>
        Task<IReadOnlyList<UserRole>> GetEffectiveRolesAsync(Guid userId, TenantId tenantId, CancellationToken ct = default);
    }
}
