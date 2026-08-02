using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using VanAn.CoreHub.Services;
using VanAn.Shared.Domain;
using VanAn.Shared.Services;
using Xunit;

namespace VanAn.Tests.Services;

/// <summary>
/// Loyalty Consistency Fix Phase 2 (BUG #4, #7, #8): tests for LoyaltyReadRouter.
/// Verifies mode-aware balance routing: Alliance mode + DeviceId → PG wallet balance;
/// Silo mode OR null deps OR no DeviceId → SQLite balance.
/// </summary>
public class LoyaltyReadRoutingTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid DeviceId = Guid.NewGuid();

    [Fact(DisplayName = "LC-READ-1: Alliance mode + member + wallet exists → returns PG balance")]
    public async Task AllianceMode_Member_WalletExists_ReturnsPgBalance()
    {
        var modeResolver = new Mock<ILoyaltyModeResolver>();
        modeResolver.Setup(m => m.GetEffectiveModeAsync(It.IsAny<Guid>())).ReturnsAsync(LoyaltyMode.Alliance);
        modeResolver.Setup(m => m.IsAllianceMemberAsync(It.IsAny<Guid>())).ReturnsAsync(true);

        var walletService = new Mock<IAllianceWalletService>();
        var wallet = new AllianceWallet(DeviceId, null);
        typeof(AllianceWallet).GetProperty(nameof(AllianceWallet.TotalPointBalance))!.SetValue(wallet, 750);
        walletService.Setup(w => w.GetWalletByDeviceIdAsync(DeviceId)).ReturnsAsync(wallet);

        var router = new LoyaltyReadRouter(modeResolver.Object, walletService.Object, NullLogger<LoyaltyReadRouter>.Instance);

        int balance = await router.GetEffectiveBalanceAsync(TenantId, DeviceId, sqliteBalance: 100);

        balance.Should().Be(750, "PG wallet balance returned in Alliance mode");
    }

    [Fact(DisplayName = "LC-READ-2: Silo mode → returns SQLite balance (no PG query)")]
    public async Task SiloMode_ReturnsSqliteBalance()
    {
        var modeResolver = new Mock<ILoyaltyModeResolver>();
        modeResolver.Setup(m => m.GetEffectiveModeAsync(It.IsAny<Guid>())).ReturnsAsync(LoyaltyMode.Silo);

        var walletService = new Mock<IAllianceWalletService>();
        walletService.Setup(w => w.GetWalletByDeviceIdAsync(It.IsAny<Guid>()))
            .ThrowsAsync(new InvalidOperationException("should not be called in Silo mode"));

        var router = new LoyaltyReadRouter(modeResolver.Object, walletService.Object, NullLogger<LoyaltyReadRouter>.Instance);

        int balance = await router.GetEffectiveBalanceAsync(TenantId, DeviceId, sqliteBalance: 100);

        balance.Should().Be(100, "SQLite balance returned in Silo mode");
    }

    [Fact(DisplayName = "LC-READ-3: Alliance mode but NOT member → returns SQLite balance (Q2 opt-out)")]
    public async Task AllianceMode_NotMember_ReturnsSqliteBalance()
    {
        var modeResolver = new Mock<ILoyaltyModeResolver>();
        modeResolver.Setup(m => m.GetEffectiveModeAsync(It.IsAny<Guid>())).ReturnsAsync(LoyaltyMode.Alliance);
        modeResolver.Setup(m => m.IsAllianceMemberAsync(It.IsAny<Guid>())).ReturnsAsync(false);

        var walletService = new Mock<IAllianceWalletService>();
        walletService.Setup(w => w.GetWalletByDeviceIdAsync(It.IsAny<Guid>()))
            .ThrowsAsync(new InvalidOperationException("should not be called for non-member"));

        var router = new LoyaltyReadRouter(modeResolver.Object, walletService.Object, NullLogger<LoyaltyReadRouter>.Instance);

        int balance = await router.GetEffectiveBalanceAsync(TenantId, DeviceId, sqliteBalance: 50);

        balance.Should().Be(50, "SQLite balance returned for tenant opted out of alliance");
    }

    [Fact(DisplayName = "LC-READ-4: Null DeviceId → returns SQLite balance (customer has no device yet)")]
    public async Task NullDeviceId_ReturnsSqliteBalance()
    {
        var modeResolver = new Mock<ILoyaltyModeResolver>();
        var walletService = new Mock<IAllianceWalletService>();

        var router = new LoyaltyReadRouter(modeResolver.Object, walletService.Object, NullLogger<LoyaltyReadRouter>.Instance);

        int balance = await router.GetEffectiveBalanceAsync(TenantId, deviceGuid: null, sqliteBalance: 30);

        balance.Should().Be(30, "SQLite balance returned when customer has no DeviceId");
    }

    [Fact(DisplayName = "LC-READ-5: Null deps → returns SQLite balance (ShopERP Silo-only deployment)")]
    public async Task NullDeps_ReturnsSqliteBalance()
    {
        var router = new LoyaltyReadRouter(modeResolver: null, walletService: null, NullLogger<LoyaltyReadRouter>.Instance);

        int balance = await router.GetEffectiveBalanceAsync(TenantId, DeviceId, sqliteBalance: 80);

        balance.Should().Be(80, "SQLite balance returned when Alliance services unavailable");
    }

    [Fact(DisplayName = "LC-READ-6: Gateway exception → graceful fallback to SQLite balance")]
    public async Task GatewayException_ReturnsSqliteBalance()
    {
        var modeResolver = new Mock<ILoyaltyModeResolver>();
        modeResolver.Setup(m => m.GetEffectiveModeAsync(It.IsAny<Guid>())).ReturnsAsync(LoyaltyMode.Alliance);
        modeResolver.Setup(m => m.IsAllianceMemberAsync(It.IsAny<Guid>())).ReturnsAsync(true);

        var walletService = new Mock<IAllianceWalletService>();
        walletService.Setup(w => w.GetWalletByDeviceIdAsync(It.IsAny<Guid>()))
            .ThrowsAsync(new HttpRequestException("Gateway unreachable"));

        var router = new LoyaltyReadRouter(modeResolver.Object, walletService.Object, NullLogger<LoyaltyReadRouter>.Instance);

        int balance = await router.GetEffectiveBalanceAsync(TenantId, DeviceId, sqliteBalance: 200);

        balance.Should().Be(200, "SQLite balance returned as graceful fallback when Gateway is down");
    }
}
