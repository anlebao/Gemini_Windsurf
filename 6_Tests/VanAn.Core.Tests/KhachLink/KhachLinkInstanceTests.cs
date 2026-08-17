using VanAn.Shared.Domain;
using VanAn.Shared.Domain.Aggregates.KhachLinkAggregate;
using Xunit;

namespace VanAn.Core.Tests.KhachLink
{
    /// <summary>
    /// KhachLink Multi-Profile R1 Sprint 6: Domain unit tests for KhachLinkInstance + KhachLinkNavFlags.
    /// Verifies factory validation, preset defaults, lifecycle methods, and platform sentinel TenantId.
    /// </summary>
    public class KhachLinkInstanceTests
    {
        private const string TestLabel = "Directory Vạn An";
        private const string TestDomain = "directory.khachvip.online";

        [Fact]
        public void Create_SetsProperties_Correctly()
        {
            var ownerTenant = Guid.NewGuid();
            var instance = new KhachLinkInstance(TestLabel, KhachLinkProfile.Directory, TestDomain, ownerTenant);

            Assert.Equal(TestLabel, instance.Label);
            Assert.Equal(KhachLinkProfile.Directory, instance.Profile);
            Assert.Equal(TestDomain, instance.CustomDomain);
            Assert.Equal(ownerTenant, instance.OwnerTenantId);
            Assert.True(instance.IsActive);
            Assert.NotEqual(Guid.Empty, instance.Id);
        }

        [Fact]
        public void Create_NormalizesCustomDomain_ToLowercase()
        {
            var instance = new KhachLinkInstance("Test", KhachLinkProfile.FullCommerce, "DIRECTORY.KhachVip.Online");

            Assert.Equal("directory.khachvip.online", instance.CustomDomain);
        }

        // ── CanonicalizeDomain tests (Dynamic CORS Sprint 1) ────────────────

        [Theory]
        [InlineData("sanjob.com", "sanjob.com")]
        [InlineData("https://sanjob.com", "sanjob.com")]
        [InlineData("http://sanjob.com", "sanjob.com")]
        [InlineData("sanjob.com/", "sanjob.com")]
        [InlineData("SANJOB.COM", "sanjob.com")]
        [InlineData("sanjob.com/api", "sanjob.com")]
        [InlineData("sanjob.com:8080", "sanjob.com")]
        [InlineData("  sanjob.com  ", "sanjob.com")]
        [InlineData("https://sanjob.com/", "sanjob.com")]
        [InlineData("https://SANJOB.COM/Path", "sanjob.com")]
        public void Create_CanonicalizeDomain_StripsSchemePathPortSlash(string input, string expected)
        {
            var instance = new KhachLinkInstance("Test", KhachLinkProfile.FullCommerce, input);

            Assert.Equal(expected, instance.CustomDomain);
        }

        [Fact]
        public void Create_WithEmptyLabel_ThrowsArgumentException()
        {
            Assert.Throws<ArgumentException>(() =>
                new KhachLinkInstance("", KhachLinkProfile.FullCommerce, TestDomain));
        }

        [Fact]
        public void Create_WithWhitespaceLabel_ThrowsArgumentException()
        {
            Assert.Throws<ArgumentException>(() =>
                new KhachLinkInstance("   ", KhachLinkProfile.FullCommerce, TestDomain));
        }

        [Fact]
        public void Create_WithEmptyCustomDomain_ThrowsArgumentException()
        {
            Assert.Throws<ArgumentException>(() =>
                new KhachLinkInstance(TestLabel, KhachLinkProfile.FullCommerce, ""));
        }

        [Fact]
        public void Create_WithNullCustomDomain_ThrowsArgumentException()
        {
            Assert.Throws<ArgumentException>(() =>
                new KhachLinkInstance(TestLabel, KhachLinkProfile.FullCommerce, null!));
        }

        [Fact]
        public void Create_WithNullNavFlagsOverride_UsesProfilePreset()
        {
            var instance = new KhachLinkInstance(TestLabel, KhachLinkProfile.Directory, TestDomain, null, null);

            // Directory preset: ShowHome/Stores/Profile = true, rest = false
            Assert.True(instance.NavFlags.ShowHome);
            Assert.True(instance.NavFlags.ShowStores);
            Assert.True(instance.NavFlags.ShowProfile);
            Assert.False(instance.NavFlags.ShowCart);
            Assert.False(instance.NavFlags.ShowOrders);
            Assert.False(instance.NavFlags.ShowRewards);
            Assert.False(instance.NavFlags.ShowMissions);
            Assert.False(instance.NavFlags.ShowLoyaltyHistory);
            Assert.False(instance.NavFlags.ShowAllianceWallet);
            Assert.False(instance.NavFlags.ShowCampaigns);
            Assert.False(instance.NavFlags.ShowScan);
            Assert.False(instance.NavFlags.ShowQrClaim);
            Assert.False(instance.NavFlags.ShowCommunity);
            Assert.False(instance.NavFlags.ShowStaffDashboard);
        }

        [Fact]
        public void Create_WithNavFlagsOverride_UsesOverride()
        {
            var customFlags = new KhachLinkNavFlags
            {
                ShowHome = true,
                ShowCart = true,
                ShowOrders = false,
                ShowLoyaltyHistory = false,
                ShowMissions = false,
                ShowRewards = true,
                ShowAllianceWallet = false,
                ShowStores = true,
                ShowCampaigns = false,
                ShowScan = false,
                ShowQrClaim = false,
                ShowCommunity = false,
                ShowJobs = false,
                ShowProfile = true,
                ShowStaffDashboard = false
            };

            var instance = new KhachLinkInstance(TestLabel, KhachLinkProfile.Directory, TestDomain, null, customFlags);

            Assert.True(instance.NavFlags.ShowCart);  // override = true (preset = false)
            Assert.True(instance.NavFlags.ShowRewards); // override = true (preset = false)
            Assert.False(instance.NavFlags.ShowOrders); // override = false
        }

        [Fact]
        public void Create_TenantId_AlwaysGuidEmpty_PlatformSentinel()
        {
            var instance = new KhachLinkInstance(TestLabel, KhachLinkProfile.FullCommerce, TestDomain);

            Assert.Equal(Guid.Empty, instance.TenantId.Value);
        }

        [Fact]
        public void Create_OwnerTenantId_Null_ForPlatformInstance()
        {
            var instance = new KhachLinkInstance(TestLabel, KhachLinkProfile.Directory, TestDomain);

            Assert.Null(instance.OwnerTenantId);
        }

        [Fact]
        public void Create_OwnerTenantId_Set_ForTenantOwnedInstance()
        {
            var ownerTenant = Guid.NewGuid();
            var instance = new KhachLinkInstance(TestLabel, KhachLinkProfile.FullCommerce, TestDomain, ownerTenant);

            Assert.Equal(ownerTenant, instance.OwnerTenantId);
        }

        // ── KhachLinkNavFlags.ForProfile tests ──────────────────────────────

        [Fact]
        public void ForProfile_FullCommerce_AllTrue_ExceptShowJobs()
        {
            var flags = KhachLinkNavFlags.ForProfile(KhachLinkProfile.FullCommerce);

            Assert.True(flags.ShowHome);
            Assert.True(flags.ShowCart);
            Assert.True(flags.ShowOrders);
            Assert.True(flags.ShowLoyaltyHistory);
            Assert.True(flags.ShowMissions);
            Assert.True(flags.ShowRewards);
            Assert.True(flags.ShowAllianceWallet);
            Assert.True(flags.ShowStores);
            Assert.True(flags.ShowCampaigns);
            Assert.True(flags.ShowScan);
            Assert.True(flags.ShowQrClaim);
            Assert.True(flags.ShowCommunity);
            Assert.False(flags.ShowJobs); // R3 — hidden in R1
            Assert.True(flags.ShowProfile);
            Assert.True(flags.ShowStaffDashboard);
        }

        [Fact]
        public void ForProfile_Directory_OnlyHomeStoresProfile_True()
        {
            var flags = KhachLinkNavFlags.ForProfile(KhachLinkProfile.Directory);

            // Directory core: Home, Stores, Profile
            Assert.True(flags.ShowHome);
            Assert.True(flags.ShowStores);
            Assert.True(flags.ShowProfile);

            // Commerce features hidden
            Assert.False(flags.ShowCart);
            Assert.False(flags.ShowOrders);
            Assert.False(flags.ShowLoyaltyHistory);
            Assert.False(flags.ShowMissions);
            Assert.False(flags.ShowRewards);
            Assert.False(flags.ShowAllianceWallet);
            Assert.False(flags.ShowCampaigns);
            Assert.False(flags.ShowScan);
            Assert.False(flags.ShowQrClaim);
            Assert.False(flags.ShowCommunity);
            Assert.False(flags.ShowStaffDashboard);
        }

        [Fact]
        public void ForProfile_Logistics_FallsBackToFullCommerce_Default()
        {
            // R3 preset not implemented yet — falls back to default (all true)
            var flags = KhachLinkNavFlags.ForProfile(KhachLinkProfile.Logistics);

            Assert.True(flags.ShowHome);
            Assert.True(flags.ShowCart); // default true (R3 will override)
        }

        [Fact]
        public void ForProfile_JobMarket_FallsBackToFullCommerce_Default()
        {
            // R3 preset not implemented yet — falls back to default
            var flags = KhachLinkNavFlags.ForProfile(KhachLinkProfile.JobMarket);

            Assert.True(flags.ShowHome);
            Assert.True(flags.ShowCart); // default true (R3 will override)
        }

        [Fact]
        public void ForProfile_Reseller_FallsBackToFullCommerce_Default()
        {
            // R2 preset not implemented yet — falls back to default
            var flags = KhachLinkNavFlags.ForProfile(KhachLinkProfile.Reseller);

            Assert.True(flags.ShowHome);
            Assert.True(flags.ShowCart); // default true (R2 will override)
        }

        // ── UpdateProfile / UpdateNavFlags tests ────────────────────────────

        [Fact]
        public void UpdateProfile_ResetsNavFlags_ToPreset()
        {
            var instance = new KhachLinkInstance(TestLabel, KhachLinkProfile.FullCommerce, TestDomain);

            // Switch to Directory → nav flags should reset to Directory preset
            instance.UpdateProfile(KhachLinkProfile.Directory);

            Assert.Equal(KhachLinkProfile.Directory, instance.Profile);
            Assert.False(instance.NavFlags.ShowCart); // Directory preset
            Assert.True(instance.NavFlags.ShowHome);  // Directory preset
        }

        [Fact]
        public void UpdateProfile_WithOverride_UsesOverride()
        {
            var instance = new KhachLinkInstance(TestLabel, KhachLinkProfile.FullCommerce, TestDomain);

            var customFlags = new KhachLinkNavFlags { ShowCart = false, ShowHome = true };
            instance.UpdateProfile(KhachLinkProfile.Directory, customFlags);

            Assert.False(instance.NavFlags.ShowCart);
            Assert.True(instance.NavFlags.ShowHome);
        }

        [Fact]
        public void UpdateNavFlags_OverridesIndividualFlags()
        {
            var instance = new KhachLinkInstance(TestLabel, KhachLinkProfile.Directory, TestDomain);

            // Override: enable ShowCart (was false in Directory preset)
            var overridden = new KhachLinkNavFlags
            {
                ShowHome = true,
                ShowCart = true, // override
                ShowStores = true,
                ShowProfile = true
            };
            instance.UpdateNavFlags(overridden);

            Assert.True(instance.NavFlags.ShowCart); // overridden
        }

        [Fact]
        public void UpdateNavFlags_WithNull_ThrowsArgumentNullException()
        {
            var instance = new KhachLinkInstance(TestLabel, KhachLinkProfile.FullCommerce, TestDomain);

            Assert.Throws<ArgumentNullException>(() => instance.UpdateNavFlags(null!));
        }

        // ── Activate / Deactivate tests ─────────────────────────────────────

        [Fact]
        public void Deactivate_SetsIsActive_False()
        {
            var instance = new KhachLinkInstance(TestLabel, KhachLinkProfile.FullCommerce, TestDomain);

            Assert.True(instance.IsActive); // default
            instance.Deactivate();
            Assert.False(instance.IsActive);
        }

        [Fact]
        public void Activate_SetsIsActive_True()
        {
            var instance = new KhachLinkInstance(TestLabel, KhachLinkProfile.FullCommerce, TestDomain);
            instance.Deactivate();

            Assert.False(instance.IsActive);
            instance.Activate();
            Assert.True(instance.IsActive);
        }

        // ── UpdateLabel tests ───────────────────────────────────────────────

        [Fact]
        public void UpdateLabel_SetsNewLabel()
        {
            var instance = new KhachLinkInstance(TestLabel, KhachLinkProfile.FullCommerce, TestDomain);

            instance.UpdateLabel("New Label");
            Assert.Equal("New Label", instance.Label);
        }

        [Fact]
        public void UpdateLabel_WithEmpty_ThrowsArgumentException()
        {
            var instance = new KhachLinkInstance(TestLabel, KhachLinkProfile.FullCommerce, TestDomain);

            Assert.Throws<ArgumentException>(() => instance.UpdateLabel(""));
        }
    }
}
