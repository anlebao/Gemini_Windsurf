using FluentAssertions;
using VanAn.Shared.Domain;
using Xunit;

namespace VanAn.Core.Tests.Domain
{
    /// <summary>
    /// ID-P1 (2026-11-10): IdentityLevelRank — DeviceVerified=4 > Full=3 phá ordering so sánh enum
    /// trực tiếp → rank map: Guest=0, Social=1, DeviceVerified=1, Verified=2, Full=3.
    /// </summary>
    public class IdentityLevelRankTests
    {
        [Theory]
        [InlineData(IdentityLevel.Guest, 0)]
        [InlineData(IdentityLevel.Social, 1)]
        [InlineData(IdentityLevel.DeviceVerified, 1)]
        [InlineData(IdentityLevel.Verified, 2)]
        [InlineData(IdentityLevel.Full, 3)]
        public void Rank_MapsCorrectly(IdentityLevel level, int expected)
        {
            level.Rank().Should().Be(expected);
        }

        [Fact]
        public void Upgrade_SameRankDeviceVerified_NoOp()
        {
            var customer = new Customer(new TenantId(Guid.NewGuid()), "A", "0901234567"); // default = Social

            customer.UpgradeIdentityLevel(IdentityLevel.DeviceVerified);

            customer.IdentityLevel.Should().Be(IdentityLevel.Social); // same rank → no-op
        }

        [Fact]
        public void Upgrade_DeviceVerifiedToVerified_Allowed()
        {
            var customer = new Customer(new TenantId(Guid.NewGuid()), "A", "0901234567");
            // Social(1) → DeviceVerified(1) no-op, nhưng Verified(2) > 1 → upgrade được
            customer.UpgradeIdentityLevel(IdentityLevel.Verified);
            customer.IdentityLevel.Should().Be(IdentityLevel.Verified);
        }

        [Fact]
        public void Upgrade_FullToDeviceVerified_NoDowngrade()
        {
            var customer = new Customer(new TenantId(Guid.NewGuid()), "A", "0901234567");
            customer.UpgradeIdentityLevel(IdentityLevel.Full);

            customer.UpgradeIdentityLevel(IdentityLevel.DeviceVerified);

            customer.IdentityLevel.Should().Be(IdentityLevel.Full);
        }

        [Fact]
        public void Upgrade_GuestToSocial_Allowed()
        {
            var customer = new Customer(new TenantId(Guid.NewGuid()), "A", "0901234567");
            customer.MarkAsGuestStub(); // IdentityLevel → Guest

            customer.UpgradeIdentityLevel(IdentityLevel.Social);

            customer.IdentityLevel.Should().Be(IdentityLevel.Social);
        }
    }
}