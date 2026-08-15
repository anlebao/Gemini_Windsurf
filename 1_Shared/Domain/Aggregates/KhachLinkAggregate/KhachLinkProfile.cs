namespace VanAn.Shared.Domain.Aggregates.KhachLinkAggregate
{
    /// <summary>
    /// KhachLink instance profile — defines feature set + default nav flags.
    /// SystemAdmin selects profile when creating KhachLinkInstance, can override individual nav flags.
    /// </summary>
    public enum KhachLinkProfile
    {
        /// <summary>Type 4 — full e-commerce, default. All features on.</summary>
        FullCommerce = 0,

        /// <summary>Type 1 — directory only. Hide cart/rewards/redeem/missions.</summary>
        Directory = 1,

        /// <summary>Type 2 — logistics marketplace. Shipper + shop owner community (R3).</summary>
        Logistics = 2,

        /// <summary>Type 3 — job/service marketplace. Job postings as Product (R3).</summary>
        JobMarket = 3,

        /// <summary>Type 5 — reseller MSP. Tenant trung gian, all features + reseller extensions (R2).</summary>
        Reseller = 4
    }
}
