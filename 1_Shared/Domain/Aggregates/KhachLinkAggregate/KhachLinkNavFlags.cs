namespace VanAn.Shared.Domain.Aggregates.KhachLinkAggregate
{
    /// <summary>
    /// Nav item visibility flags — SystemAdmin toggle per KhachLinkInstance.
    /// Stored as owned entity (flattened bool columns in KhachLinkInstances table).
    /// Default values set by KhachLinkProfile preset via <see cref="ForProfile"/>, override per-instance.
    /// </summary>
    public class KhachLinkNavFlags
    {
        public bool ShowHome { get; init; } = true;
        public bool ShowCart { get; init; } = true;
        public bool ShowOrders { get; init; } = true;
        public bool ShowLoyaltyHistory { get; init; } = true;
        public bool ShowMissions { get; init; } = true;
        public bool ShowRewards { get; init; } = true;
        public bool ShowAllianceWallet { get; init; } = true;
        public bool ShowStores { get; init; } = true;
        public bool ShowCampaigns { get; init; } = true;
        public bool ShowScan { get; init; } = true;
        public bool ShowQrClaim { get; init; } = true;
        public bool ShowCommunity { get; init; } = true;
        public bool ShowJobs { get; init; } = false;      // Type 3 only — trỏ đến /jobs (R3)
        public bool ShowProfile { get; init; } = true;
        public bool ShowStaffDashboard { get; init; } = true;

        // Public parameterless ctor — init-only properties default to FullCommerce preset.
        // EF Core uses this for materialization; code uses `new KhachLinkNavFlags()` for default.
        public KhachLinkNavFlags() { }

        /// <summary>
        /// Build nav flags from profile preset. SystemAdmin can override individual flags after.
        /// R1: FullCommerce + Directory implemented. R2: Reseller. R3: Logistics + JobMarket.
        /// </summary>
        public static KhachLinkNavFlags ForProfile(KhachLinkProfile profile) => profile switch
        {
            KhachLinkProfile.Directory => new KhachLinkNavFlags
            {
                ShowCart = false,
                ShowOrders = false,
                ShowLoyaltyHistory = false,
                ShowMissions = false,
                ShowRewards = false,
                ShowAllianceWallet = false,
                ShowCampaigns = false,
                ShowScan = false,
                ShowQrClaim = false,
                ShowCommunity = false,
                ShowStaffDashboard = false
                // ShowHome, ShowStores, ShowProfile = true (directory core)
            },
            // TODO R3: Logistics preset (hide commerce, show community)
            // TODO R3: JobMarket preset (hide commerce, show /jobs)
            // TODO R2: Reseller preset (all true + reseller extensions)
            _ => new KhachLinkNavFlags()  // FullCommerce + unimplemented profiles = all true (safe default)
        };
    }
}
