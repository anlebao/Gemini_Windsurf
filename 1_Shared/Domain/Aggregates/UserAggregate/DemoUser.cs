using VanAn.Shared.Domain.Common;

namespace VanAn.Shared.Domain.Aggregates.UserAggregate
{
    /// <summary>
    /// DemoUser Aggregate Root — Rich Domain Model.
    /// Replaces the anemic <see cref="VanAn.Shared.Domain.DemoUser"/> class (marked [Obsolete] in Domain.cs).
    /// Wave 6: God File split + lifecycle management.
    /// </summary>
    public class DemoUser : AggregateRoot
    {
        // ── Identity & Profile ─────────────────────────────────────────────────
        public string Username { get; private set; } = string.Empty;
        public string PasswordHash { get; private set; } = string.Empty;
        public string DisplayName { get; private set; } = string.Empty;
        public string? Email { get; private set; }
        public UserRole Role { get; private set; } = UserRole.Staff;
        public bool IsActive { get; private set; } = true;

        // EF Core requires parameterless constructor
        private DemoUser() { }

        public DemoUser(TenantId tenantId, string username, string passwordHash, string displayName, UserRole role)
            : base(tenantId)
        {
            if (tenantId is null || tenantId.Value == Guid.Empty)
                throw new ArgumentException("TenantId cannot be empty.", nameof(tenantId));
            if (string.IsNullOrWhiteSpace(username))
                throw new ArgumentException("Username cannot be empty.", nameof(username));
            if (string.IsNullOrWhiteSpace(passwordHash))
                throw new ArgumentException("PasswordHash cannot be empty.", nameof(passwordHash));
            if (string.IsNullOrWhiteSpace(displayName))
                throw new ArgumentException("DisplayName cannot be empty.", nameof(displayName));

            Username = username;
            PasswordHash = passwordHash;
            DisplayName = displayName;
            Role = role;
            IsActive = true;
        }

        // ── Factory ────────────────────────────────────────────────────────────
        /// <summary>Creates a new user and raises <see cref="UserCreatedEvent"/>.</summary>
        public static DemoUser Create(TenantId tenantId, string username, string passwordHash, string displayName, UserRole role)
        {
            var user = new DemoUser(tenantId, username, passwordHash, displayName, role);
            user.AddDomainEvent(new UserCreatedEvent(
                user.Id,
                tenantId.Value,
                username,
                displayName,
                role,
                DateTime.UtcNow));
            return user;
        }

        // ── Domain Methods ─────────────────────────────────────────────────────
        /// <summary>Deactivate this user. Domain-level guard only checks current state.</summary>
        public void Deactivate()
        {
            if (!IsActive)
                throw new InvalidOperationException("User is already deactivated.");

            IsActive = false;
            AddDomainEvent(new UserDeactivatedEvent(Id, TenantId.Value, DateTime.UtcNow));
        }

        /// <summary>Reactivate a deactivated user.</summary>
        public void Reactivate()
        {
            if (IsActive)
                throw new InvalidOperationException("User is already active.");

            IsActive = true;
        }

        /// <summary>Change password hash. The hash must already be computed by the service layer.</summary>
        public void ChangePassword(string newBcryptHash)
        {
            if (string.IsNullOrWhiteSpace(newBcryptHash))
                throw new ArgumentException("Password hash cannot be empty.", nameof(newBcryptHash));

            PasswordHash = newBcryptHash;
            AddDomainEvent(new UserPasswordChangedEvent(Id, TenantId.Value, DateTime.UtcNow));
        }

        /// <summary>Assign a new role to the user.</summary>
        public void AssignRole(UserRole newRole)
        {
            var previousRole = Role;
            Role = newRole;
            AddDomainEvent(new UserRoleChangedEvent(Id, TenantId.Value, previousRole, newRole, DateTime.UtcNow));
        }

        /// <summary>Update display profile only.</summary>
        public void UpdateProfile(string displayName)
        {
            if (string.IsNullOrWhiteSpace(displayName))
                throw new ArgumentException("DisplayName cannot be empty.", nameof(displayName));

            DisplayName = displayName;
        }
    }
}
