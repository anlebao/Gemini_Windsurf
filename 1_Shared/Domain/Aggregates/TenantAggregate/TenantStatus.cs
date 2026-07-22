namespace VanAn.Shared.Domain.Aggregates.TenantAggregate
{
    /// <summary>
    /// Lifecycle states for a Tenant â€” Wave 5 Rich Domain Model
    /// </summary>
    public enum TenantStatus
    {
        /// <summary>Tenant is fully operational</summary>
        Active = 1,

        /// <summary>Temporarily suspended â€” no new orders, read-only data access</summary>
        Suspended = 2,

        /// <summary>Permanently deactivated â€” archived, no access</summary>
        Inactive = 3,

        /// <summary>HKD Ä‘Ã£ chuyá»ƒn Ä‘á»•i thÃ nh DN â€” read-only, historical reports váº«n truy cáº­p (D9 Option B).</summary>
        Converted = 4
    }
}
