using VanAn.Shared.Domain.Common;

namespace VanAn.Shared.Domain.Aggregates.UserAggregate
{
    /// <summary>
    /// User-Tenant membership relationship.
    /// Replaces the anemic <see cref="VanAn.Shared.Domain.UserTenant"/> class (marked [Obsolete] in Domain.cs).
    /// Wave 6: Role is typed as <see cref="UserRole"/> instead of string.
    /// </summary>
    public class UserTenant : BaseEntity
    {
        public Guid UserId { get; private set; }
        public Guid TenantIdValue { get; private set; }
        public UserRole Role { get; private set; } = UserRole.Staff;
        public DateTime AssignedAt { get; private set; } = DateTime.UtcNow;
        public bool IsActive { get; private set; } = true;

        protected UserTenant() { }

        public UserTenant(Guid userId, Guid tenantId, UserRole role)
        {
            if (userId == Guid.Empty)
                throw new ArgumentException("UserId cannot be empty.", nameof(userId));
            if (tenantId == Guid.Empty)
                throw new ArgumentException("TenantId cannot be empty.", nameof(tenantId));

            UserId = userId;
            TenantId = new TenantId(tenantId);
            TenantIdValue = tenantId;
            Role = role;
            AssignedAt = DateTime.UtcNow;
            IsActive = true;
        }

        public void Deactivate()
        {
            IsActive = false;
        }

        public void Reactivate()
        {
            IsActive = true;
        }

        public void ChangeRole(UserRole newRole)
        {
            Role = newRole;
        }
    }
}
