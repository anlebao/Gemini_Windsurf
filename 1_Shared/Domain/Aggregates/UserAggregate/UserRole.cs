namespace VanAn.Shared.Domain.Aggregates.UserAggregate
{
    /// <summary>
    /// Roles within a tenant for the ShopERP user aggregate.
    /// Replaces the anemic <see cref="VanAn.Shared.Domain.UserRole"/> enum (marked [Obsolete] in Domain.cs).
    /// Wave 6: God File split + typed RBAC.
    /// </summary>
    public enum UserRole
    {
        None = 0,
        Owner = 1,        // Chủ quán - Full access
        StoreKeeper = 2,  // Thủ kho - Quản lý inventory
        Guard = 3,        // Bảo vệ - Check-in/out
        Staff = 4,        // Phục vụ - Order management
        Masterchef = 5    // Bếp trưởng - Kitchen operations
    }
}
