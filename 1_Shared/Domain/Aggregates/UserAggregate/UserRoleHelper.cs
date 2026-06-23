namespace VanAn.Shared.Domain.Aggregates.UserAggregate
{
    public static class UserRoleHelper
    {
        public static IReadOnlyList<UserRole> AssignableRoles { get; } =
            Enum.GetValues<UserRole>()
                .Where(r => r != UserRole.None)
                .ToList()
                .AsReadOnly();
    }
}
