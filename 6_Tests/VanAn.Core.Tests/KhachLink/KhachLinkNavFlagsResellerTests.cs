using VanAn.Shared.Domain.Aggregates.KhachLinkAggregate;
using Xunit;

namespace VanAn.Core.Tests.KhachLink;

/// <summary>
/// R2 (2026-09-04): KhachLinkNavFlags Reseller preset tests.
/// Verifies ForProfile(Reseller) returns all 15 nav flags true (full commerce + reseller extensions).
/// </summary>
public class KhachLinkNavFlagsResellerTests
{
    [Fact(DisplayName = "R2-1: ForProfile(Reseller) returns commerce flags true (ShowJobs=false — JobMarket-only)")]
    public void ForProfile_Reseller_AllFlagsTrue()
    {
        var flags = KhachLinkNavFlags.ForProfile(KhachLinkProfile.Reseller);

        Assert.True(flags.ShowHome, "ShowHome should be true for Reseller");
        Assert.True(flags.ShowCart, "ShowCart should be true for Reseller");
        Assert.True(flags.ShowOrders, "ShowOrders should be true for Reseller");
        Assert.True(flags.ShowLoyaltyHistory, "ShowLoyaltyHistory should be true for Reseller");
        Assert.True(flags.ShowMissions, "ShowMissions should be true for Reseller");
        Assert.True(flags.ShowRewards, "ShowRewards should be true for Reseller");
        Assert.True(flags.ShowAllianceWallet, "ShowAllianceWallet should be true for Reseller");
        Assert.True(flags.ShowStores, "ShowStores should be true for Reseller");
        Assert.True(flags.ShowCampaigns, "ShowCampaigns should be true for Reseller");
        Assert.True(flags.ShowScan, "ShowScan should be true for Reseller");
        Assert.True(flags.ShowQrClaim, "ShowQrClaim should be true for Reseller");
        Assert.True(flags.ShowCommunity, "ShowCommunity should be true for Reseller");
        Assert.True(flags.ShowProfile, "ShowProfile should be true for Reseller");
        Assert.True(flags.ShowStaffDashboard, "ShowStaffDashboard should be true for Reseller");
        // ShowJobs is false by default — it's a JobMarket (Type 3 / R3) feature, NOT for Reseller
        Assert.False(flags.ShowJobs, "ShowJobs should be false for Reseller (JobMarket-only feature, R3)");
    }

    [Fact(DisplayName = "R2-2: ForProfile(Reseller) equals default KhachLinkNavFlags (FullCommerce preset)")]
    public void ForProfile_Reseller_EqualsDefault()
    {
        var resellerFlags = KhachLinkNavFlags.ForProfile(KhachLinkProfile.Reseller);
        var defaultFlags = new KhachLinkNavFlags(); // FullCommerce preset — all true

        // All flags match defaults (both all-true)
        Assert.Equal(defaultFlags.ShowHome, resellerFlags.ShowHome);
        Assert.Equal(defaultFlags.ShowCart, resellerFlags.ShowCart);
        Assert.Equal(defaultFlags.ShowOrders, resellerFlags.ShowOrders);
        Assert.Equal(defaultFlags.ShowLoyaltyHistory, resellerFlags.ShowLoyaltyHistory);
        Assert.Equal(defaultFlags.ShowMissions, resellerFlags.ShowMissions);
        Assert.Equal(defaultFlags.ShowRewards, resellerFlags.ShowRewards);
        Assert.Equal(defaultFlags.ShowAllianceWallet, resellerFlags.ShowAllianceWallet);
        Assert.Equal(defaultFlags.ShowStores, resellerFlags.ShowStores);
        Assert.Equal(defaultFlags.ShowCampaigns, resellerFlags.ShowCampaigns);
        Assert.Equal(defaultFlags.ShowScan, resellerFlags.ShowScan);
        Assert.Equal(defaultFlags.ShowQrClaim, resellerFlags.ShowQrClaim);
        Assert.Equal(defaultFlags.ShowCommunity, resellerFlags.ShowCommunity);
        Assert.Equal(defaultFlags.ShowJobs, resellerFlags.ShowJobs);
        Assert.Equal(defaultFlags.ShowProfile, resellerFlags.ShowProfile);
        Assert.Equal(defaultFlags.ShowStaffDashboard, resellerFlags.ShowStaffDashboard);
    }

    [Fact(DisplayName = "R2-3: ForProfile(FullCommerce) still returns all true (R1 unchanged)")]
    public void ForProfile_FullCommerce_StillAllTrue()
    {
        var flags = KhachLinkNavFlags.ForProfile(KhachLinkProfile.FullCommerce);

        Assert.True(flags.ShowHome);
        Assert.True(flags.ShowCart);
        Assert.True(flags.ShowOrders);
        Assert.True(flags.ShowStores);
        Assert.True(flags.ShowProfile);
    }

    [Fact(DisplayName = "R2-4: ForProfile(Directory) still hides commerce (R1 unchanged)")]
    public void ForProfile_Directory_StillHidesCommerce()
    {
        var flags = KhachLinkNavFlags.ForProfile(KhachLinkProfile.Directory);

        Assert.True(flags.ShowHome, "ShowHome should be true for Directory");
        Assert.True(flags.ShowStores, "ShowStores should be true for Directory");
        Assert.True(flags.ShowProfile, "ShowProfile should be true for Directory");
        Assert.False(flags.ShowCart, "ShowCart should be false for Directory");
        Assert.False(flags.ShowOrders, "ShowOrders should be false for Directory");
        Assert.False(flags.ShowCommunity, "ShowCommunity should be false for Directory");
    }

    [Fact(DisplayName = "R2-5: ForProfile(Logistics) + ForProfile(JobMarket) still use safe default (R3 not yet implemented)")]
    public void ForProfile_Logistics_And_JobMarket_StillDefault()
    {
        // R3 not yet implemented — these should fall through to default (all true) per _ => new KhachLinkNavFlags()
        var logisticsFlags = KhachLinkNavFlags.ForProfile(KhachLinkProfile.Logistics);
        var jobMarketFlags = KhachLinkNavFlags.ForProfile(KhachLinkProfile.JobMarket);

        Assert.True(logisticsFlags.ShowHome, "Logistics should fall through to all-true default until R3");
        Assert.True(jobMarketFlags.ShowHome, "JobMarket should fall through to all-true default until R3");
    }
}
