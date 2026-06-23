using VanAn.Shared.Domain.Common;

namespace VanAn.Shared.Domain.Aggregates.UserAggregate
{
    /// <summary>
    /// Many-to-many mapping between a user and a <see cref="PermissionGroup"/>.
    /// Wave 6.
    /// </summary>
    public class UserPermissionGroup : BaseEntity
    {
        public Guid UserId { get; private set; }
        public Guid GroupId { get; private set; }
        public DateTime AssignedAt { get; private set; } = DateTime.UtcNow;
        public bool IsActive { get; private set; } = true;

        protected UserPermissionGroup() { }

        public UserPermissionGroup(Guid userId, Guid groupId, TenantId tenantId)
            : base(tenantId)
        {
            if (userId == Guid.Empty)
                throw new ArgumentException("UserId cannot be empty.", nameof(userId));
            if (groupId == Guid.Empty)
                throw new ArgumentException("GroupId cannot be empty.", nameof(groupId));

            UserId = userId;
            GroupId = groupId;
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
    }
}
