namespace VanAn.Shared.Domain.Aggregates.UserAggregate
{
    public static class UserRoleHelper
    {
        public static IReadOnlyList<UserRole> AssignableRoles { get; } =
            Enum.GetValues<UserRole>()
                .Where(r => r != UserRole.None)
                .ToList()
                .AsReadOnly();

        /// <summary>
        /// Bug 1: Get assignable roles based on current user's role.
        /// SystemAdmin can assign all roles (including Owner).
        /// Owner can only assign non-Owner roles (Staff, Masterchef, Guard, StoreKeeper).
        /// </summary>
        public static IReadOnlyList<UserRole> GetAssignableRoles(bool isSystemAdmin)
        {
            return AssignableRoles
                .Where(r => isSystemAdmin || r != UserRole.Owner)
                .ToList()
                .AsReadOnly();
        }
    }
}
