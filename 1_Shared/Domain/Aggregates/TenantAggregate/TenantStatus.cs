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
        Inactive = 3
    }
}
