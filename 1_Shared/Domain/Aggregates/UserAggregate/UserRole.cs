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
        Owner = 1,        // Chá»§ quÃ¡n - Full access
        StoreKeeper = 2,  // Thá»§ kho - Quáº£n lÃ½ inventory
        Guard = 3,        // Báº£o vá»‡ - Check-in/out
        Staff = 4,        // Phá»¥c vá»¥ - Order management
        Masterchef = 5    // Báº¿p trÆ°á»Ÿng - Kitchen operations
    }
}
