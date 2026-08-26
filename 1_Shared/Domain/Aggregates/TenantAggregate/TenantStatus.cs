namespace VanAn.Shared.Domain.Aggregates.TenantAggregate
{
    /// <summary>
    /// Lifecycle states for a Tenant — Wave 5 Rich Domain Model
    /// </summary>
    public enum TenantStatus
    {
        /// <summary>Tenant is fully operational</summary>
        Active = 1,

        /// <summary>Temporarily suspended — no new orders, read-only data access</summary>
        Suspended = 2,

        /// <summary>Permanently deactivated — archived, no access</summary>
        Inactive = 3,

        /// <summary>HKD đã chuyển đổi thành DN — read-only, historical reports vẫn truy cập (D9 Option B).</summary>
        Converted = 4,

        /// <summary>
        /// Crawl-to-Onboard Pipeline (2026-08-25): Tenant được tạo từ crawl business listing,
        /// chưa có owner claim. Profile read-only public (SĐT HIDDEN per M3), no login, no orders, no accounting.
        /// Owner Claim + SysAdmin Approve → transitions to Active.
        /// Note: Pending=5 (not 0) to avoid EF Core default-value sentinel issue (correction H1).
        /// Existing rows have Status=1 (Active) explicitly set by CreateCompany/CreateHouseholdBusiness factories.
        /// </summary>
        Pending = 5
    }
}
