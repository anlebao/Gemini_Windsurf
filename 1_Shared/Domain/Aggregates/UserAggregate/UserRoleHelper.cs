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

        /// <summary>Tên hiển thị tiếng Việt cho role (UI dropdown + cột vai trò).</summary>
        public static string GetRoleDisplayName(UserRole role) => role switch
        {
            UserRole.Owner => "Chủ quán (Owner)",
            UserRole.StoreKeeper => "Thủ kho (StoreKeeper)",
            UserRole.Guard => "Bảo vệ (Guard)",
            UserRole.Staff => "Nhân viên (Staff)",
            UserRole.Masterchef => "Bếp trưởng (Masterchef)",
            UserRole.ChiefAccountant => "Kế toán trưởng",
            UserRole.DebtAccountant => "Kế toán công nợ",
            _ => role.ToString()
        };
    }
}
