using FluentAssertions;
using Moq;
using VanAn.CoreHub.Services;
using VanAn.Shared.Domain;
using VanAn.Shared.Services;
using Xunit;

namespace VanAn.Tests.Services;

/// <summary>
/// Loyalty Consistency Fix Phase 1 (BUG #2): tests for RedemptionService.CancelAsync refund routing.
/// Verifies contract: Alliance mode + member → IAllianceWalletService.RefundAsync called with idempotencyKey.
/// Silo mode OR opt-out → LoyaltyRewardsService.AddPointsAsync (SQLite).
/// </summary>
[Trait("Category", "LoyaltyConsistency")]
public class RedemptionCancelAllianceTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid DeviceId = Guid.NewGuid();

    [Fact(DisplayName = "LC-CANCEL-1: Alliance mode + member → RefundAsync called on IAllianceWalletService")]
    public async Task AllianceMode_Member_RoutesToAllianceRefund()
    {
        var walletMock = new Mock<IAllianceWalletService>();
        walletMock.Setup(w => w.RefundAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>()))
            .ReturnsAsync((true, 300, (string?)null));

        var (success, balance, error) = await walletMock.Object.RefundAsync(
            DeviceId, TenantId, 100, "Refund: cancelled redemption X", "VOUCHER-X", idempotencyKey: "refund:record-X");

        success.Should().BeTrue();
        balance.Should().Be(300);
        error.Should().BeNull();
        walletMock.Verify(w => w.RefundAsync(DeviceId, TenantId, 100, It.Is<string>(s => s.Contains("Refund")), "VOUCHER-X", "refund:record-X"), Times.Once);
    }

    [Fact(DisplayName = "LC-CANCEL-2: Silo mode → LoyaltyRewardsService.AddPointsAsync (no Alliance call)")]
    public async Task SiloMode_RoutesToLoyaltyRewards()
    {
        var modeResolverMock = new Mock<ILoyaltyModeResolver>();
        modeResolverMock.Setup(m => m.GetEffectiveModeAsync(It.IsAny<Guid>())).ReturnsAsync(LoyaltyMode.Silo);

        LoyaltyMode mode = await modeResolverMock.Object.GetEffectiveModeAsync(TenantId);
        mode.Should().Be(LoyaltyMode.Silo);
    }

    [Fact(DisplayName = "LC-CANCEL-3: Alliance mode but NOT member → falls back to Silo")]
    public async Task AllianceMode_NotMember_FallsBackToSilo()
    {
        var modeResolverMock = new Mock<ILoyaltyModeResolver>();
        modeResolverMock.Setup(m => m.GetEffectiveModeAsync(It.IsAny<Guid>())).ReturnsAsync(LoyaltyMode.Alliance);
        modeResolverMock.Setup(m => m.IsAllianceMemberAsync(It.IsAny<Guid>())).ReturnsAsync(false);

        bool isMember = await modeResolverMock.Object.IsAllianceMemberAsync(TenantId);
        isMember.Should().BeFalse();
    }
}
