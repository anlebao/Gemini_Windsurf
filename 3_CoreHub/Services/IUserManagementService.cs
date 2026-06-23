using VanAn.Shared.Domain;
using DemoUser = VanAn.Shared.Domain.Aggregates.UserAggregate.DemoUser;
using UserRole = VanAn.Shared.Domain.Aggregates.UserAggregate.UserRole;

namespace VanAn.CoreHub.Services
{
    /// <summary>
    /// User lifecycle management service — Wave 6.
    /// Owner-only operations: create, list, update profile, change password, deactivate, reactivate.
    /// </summary>
    public interface IUserManagementService
    {
        /// <summary>Creates a new user with a BCrypt-hashed password and raises UserCreatedEvent.</summary>
        Task<DemoUser> CreateUserAsync(
            TenantId tenantId,
            string username,
            string plainPassword,
            string displayName,
            UserRole role,
            CancellationToken ct = default);

        /// <summary>Returns user by ID within the specified tenant. Returns null if not found.</summary>
        Task<DemoUser?> GetUserByIdAsync(Guid userId, TenantId tenantId, CancellationToken ct = default);

        /// <summary>Lists all active users in the tenant.</summary>
        Task<IReadOnlyList<DemoUser>> ListUsersAsync(TenantId tenantId, CancellationToken ct = default);

        /// <summary>Updates user display name.</summary>
        Task UpdateProfileAsync(Guid userId, TenantId tenantId, string displayName, CancellationToken ct = default);

        /// <summary>Changes user password (BCrypt-hashed).</summary>
        Task ChangePasswordAsync(Guid userId, TenantId tenantId, string plainPassword, CancellationToken ct = default);

        /// <summary>Deactivates a user. Guards against deactivating the last Owner.</summary>
        Task DeactivateUserAsync(Guid userId, TenantId tenantId, CancellationToken ct = default);

        /// <summary>Reactivates a deactivated user.</summary>
        Task ReactivateUserAsync(Guid userId, TenantId tenantId, CancellationToken ct = default);
    }
}
