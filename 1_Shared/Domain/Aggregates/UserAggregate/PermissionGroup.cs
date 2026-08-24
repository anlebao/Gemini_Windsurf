using VanAn.Shared.Domain.Common;

namespace VanAn.Shared.Domain.Aggregates.UserAggregate
{
    /// <summary>
    /// Permission Group — bundles a set of <see cref="UserRole"/> values.
    /// Replaces granular permission lists with role bundles (Phán quyết D2).
    /// Wave 6.
    /// </summary>
    public class PermissionGroup : AggregateRoot
    {
        public string Name { get; private set; } = string.Empty;
        public string Description { get; private set; } = string.Empty;

        /// <summary>
        /// Comma-separated integer role values for persistence. Modified only via domain methods.
        /// </summary>
        public string SerializedRoles { get; private set; } = string.Empty;

        protected PermissionGroup() { }

        public PermissionGroup(TenantId tenantId, string name, string description)
            : base(tenantId)
        {
            if (tenantId is null || tenantId.Value == Guid.Empty)
                throw new ArgumentException("TenantId cannot be empty.", nameof(tenantId));
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Name cannot be empty.", nameof(name));

            Name = name;
            Description = description ?? string.Empty;
        }

        public IReadOnlyList<UserRole> GetEffectiveRoles()
        {
            if (string.IsNullOrWhiteSpace(SerializedRoles))
                return new List<UserRole>().AsReadOnly();

            return SerializedRoles
                .Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(s => (UserRole)int.Parse(s, System.Globalization.CultureInfo.InvariantCulture))
                .Distinct()
                .ToList()
                .AsReadOnly();
        }

        public void AddRole(UserRole role)
        {
            if (role == UserRole.None)
                throw new ArgumentException("None cannot be added as a group role.", nameof(role));

            var roles = GetEffectiveRoles().ToList();
            if (!roles.Contains(role))
                roles.Add(role);

            SerializedRoles = string.Join(",", roles.Select(r => ((int)r).ToString(System.Globalization.CultureInfo.InvariantCulture)));
        }

        public void RemoveRole(UserRole role)
        {
            var roles = GetEffectiveRoles().ToList();
            _ = roles.Remove(role);
            SerializedRoles = string.Join(",", roles.Select(r => ((int)r).ToString(System.Globalization.CultureInfo.InvariantCulture)));
        }

        public void UpdateProfile(string name, string description)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Name cannot be empty.", nameof(name));

            Name = name;
            Description = description ?? string.Empty;
        }
    }
}
