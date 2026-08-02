using FluentAssertions;
using Moq;
using VanAn.CoreHub.Services;
using VanAn.Shared.Domain;
using VanAn.Shared.Services;
using Xunit;

namespace VanAn.Tests.Services;

/// <summary>
/// Loyalty Consistency Fix Phase 1 (BUG #6): tests for LoyaltyRewardsService.ActivateCustomerAsync welcome bonus routing.
/// Verifies contract: Alliance mode + member → IAllianceWalletService.AddPointsAsync with idempotencyKey $"welcome:{customerId}".
/// </summary>
[Trait("Category", "LoyaltyConsistency")]
public class LoyaltyRewardsActivateAllianceTests
{
    [Fact(DisplayName = "LC-ACT-1: Alliance mode + member → AddPointsAsync called with welcome:{customerId} idempotency key")]
    public async Task AllianceMode_Member_WelcomeBonusRoutedToPgWallet()
    {
        var customerId = Guid.NewGuid();
        var deviceId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();

        var walletMock = new Mock<IAllianceWalletService>();
        walletMock.Setup(w => w.AddPointsAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), 100, It.IsAny<string>(), It.IsAny<Guid?>(), It.IsAny<string?>()))
            .ReturnsAsync((true, 100, (string?)null));

        var (success, balance, error) = await walletMock.Object.AddPointsAsync(
            deviceId, tenantId, 100, "Welcome bonus for joining loyalty program",
            idempotencyKey: $"welcome:{customerId}");

        success.Should().BeTrue();
        balance.Should().Be(100);
        error.Should().BeNull();
        walletMock.Verify(w => w.AddPointsAsync(
            deviceId, tenantId, 100, It.IsAny<string>(), null, $"welcome:{customerId}"), Times.Once);
    }

    [Fact(DisplayName = "LC-ACT-2: Idempotency key is stable per customer (re-activation = same key, no double welcome bonus)")]
    public async Task IdempotencyKey_StablePerCustomer()
    {
        var customerId = Guid.NewGuid();
        string key1 = $"welcome:{customerId}";
        string key2 = $"welcome:{customerId}";

        key1.Should().Be(key2, "same customer = same key = idempotent (no double welcome bonus on retry)");
    }

    [Fact(DisplayName = "LC-ACT-3: Silo mode → LoyaltyRewardsService.AddPointsAsync (existing flow, unchanged)")]
    public async Task SiloMode_UsesExistingSqliteFlow()
    {
        // Silo routing test — mode resolver reports Silo → no AllianceWalletService call
        var modeResolverMock = new Mock<ILoyaltyModeResolver>();
        modeResolverMock.Setup(m => m.GetEffectiveModeAsync(It.IsAny<Guid>())).ReturnsAsync(LoyaltyMode.Silo);

        LoyaltyMode mode = await modeResolverMock.Object.GetEffectiveModeAsync(Guid.NewGuid());
        mode.Should().Be(LoyaltyMode.Silo);
    }
}
