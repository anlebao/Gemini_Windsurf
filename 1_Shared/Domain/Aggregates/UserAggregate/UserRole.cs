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
        Masterchef = 5,   // Bếp trưởng - Kitchen operations
        // 2026-10-09 (user directive): 2 role kế toán mới — append-only.
        ChiefAccountant = 6,      // Kế toán trưởng — Kiểm kê + Kế toán + Tài chính (đầy đủ)
        DebtAccountant = 7        // Kế toán công nợ — chỉ Kế toán, ẩn history/import/đóng kỳ/Sổ HKD/BCTC
    }
}
