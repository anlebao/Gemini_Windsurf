using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using VanAn.CoreHub.Domain.Repositories;
using VanAn.CoreHub.Infrastructure;
using VanAn.CoreHub.Services;
using VanAn.Shared.Domain;
using VanAn.Shared.Domain.Common;
using VanAn.Shared.Services;
using Xunit;

namespace VanAn.Tests.Services;

/// <summary>
/// Loyalty Alliance Phase 2C — tests for RedemptionService REDEEM mode routing.
/// Verifies that RedeemAsync routes to AllianceWalletService.DeductPointsAsync when
/// mode=Alliance + tenant is a member, and falls through to existing Silo flow
/// (LoyaltyRewardsService.SubtractPointsAsync) when mode=Silo or tenant opted out.
/// Spec: docs/specs/loyalty-alliance-spec.md v1.0 (Q2: full opt-out).
/// </summary>
public class RedemptionAllianceTests
{
    private static readonly Guid TestTenantGuid = Guid.Parse("00000000-0000-0000-0000-000000000001");
    private static readonly TenantId TestTenantId = new(TestTenantGuid);
    private static readonly Guid TestCustomerId = Guid.NewGuid();
    private static readonly Guid TestCatalogItemId = Guid.NewGuid();
    private static readonly Guid TestDeviceId = Guid.NewGuid();

    /// <summary>
    /// Build a RedemptionService with mocked deps + real SQLite in-memory IVanAnDbContext
    /// (needed for FirstOrDefaultAsync on Customers in Alliance branch).
    /// </summary>
    private static (RedemptionService sut, Mock<IAllianceWalletService> walletMock,
        Mock<ILoyaltyRewardsService> loyaltyMock, Mock<IRedemptionRepository> repoMock, ServiceProvider sp)
        BuildService(LoyaltyMode mode, bool isAllianceMember,
            int walletDeductNewBalance = 500, bool walletDeductSuccess = true, string? walletError = null)
    {
        var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();

        var services = new ServiceCollection();
        services.AddDbContext<VanAnDbContext>(options => options.UseSqlite(connection));
        services.AddScoped<IVanAnDbContext>(sp => sp.GetRequiredService<VanAnDbContext>());
        services.AddLogging(b => b.AddConsole().SetMinimumLevel(LogLevel.Warning));
        ServiceProvider sp = services.BuildServiceProvider();
        VanAnDbContext db = sp.GetRequiredService<VanAnDbContext>();
        _ = db.Database.EnsureCreated();

        // Seed customer with device ID for Alliance wallet lookup
        var customer = new Customer(TestTenantId, "Test Customer", "0901234567");
        customer.UpdateCustomerDetails("Test Customer", "0901234567", null, "Bronze", TestDeviceId, true);
        typeof(BaseEntity).GetProperty(nameof(BaseEntity.Id))!.SetValue(customer, TestCustomerId);
        db.Customers.Add(customer);
        db.SaveChanges();

        var repoMock = new Mock<IRedemptionRepository>();
        var catalogItem = new RedemptionCatalogItem(TestTenantId, "Test Product", 100);
        catalogItem.UpdateDetails("Test Product", "Test", null, 100, null, null, 7);
        repoMock.Setup(r => r.GetCatalogItemByIdAsync(TestCatalogItemId)).ReturnsAsync(catalogItem);
        repoMock.Setup(r => r.AddRecordAsync(It.IsAny<RedemptionRecord>()))
            .ReturnsAsync((RedemptionRecord r) => r);
        repoMock.Setup(r => r.AddVoucherAsync(It.IsAny<Voucher>()))
            .ReturnsAsync((Voucher v) => v);
        repoMock.Setup(r => r.UpdateRecordAsync(It.IsAny<RedemptionRecord>()))
            .ReturnsAsync((RedemptionRecord r) => r);
        repoMock.Setup(r => r.UpdateCatalogItemAsync(It.IsAny<RedemptionCatalogItem>()))
            .ReturnsAsync((RedemptionCatalogItem c) => c);

        var loyaltyMock = new Mock<ILoyaltyRewardsService>();
        loyaltyMock.Setup(l => l.SubtractPointsAsync(It.IsAny<Guid>(), It.IsAny<int>(), It.IsAny<string>()))
            .ReturnsAsync(true);
        loyaltyMock.Setup(l => l.GetCustomerRewardsAsync(It.IsAny<Guid>()))
            .ReturnsAsync(new LoyaltyRewards(TestTenantId, TestCustomerId));

        var tenantProviderMock = new Mock<ITenantProvider>();
        tenantProviderMock.SetupGet(t => t.TenantId).Returns(TestTenantGuid);

        var modeResolverMock = new Mock<ILoyaltyModeResolver>();
        modeResolverMock.Setup(m => m.GetEffectiveModeAsync(It.IsAny<Guid>())).ReturnsAsync(mode);
        modeResolverMock.Setup(m => m.IsAllianceMemberAsync(It.IsAny<Guid>())).ReturnsAsync(isAllianceMember);

        var walletMock = new Mock<IAllianceWalletService>();
        walletMock.Setup(w => w.DeductPointsAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string?>()))
            .ReturnsAsync((walletDeductSuccess, walletDeductNewBalance, walletError));

        var sut = new RedemptionService(
            repoMock.Object,
            loyaltyMock.Object,
            tenantProviderMock.Object,
            db,
            null,
            null,
            NullLogger<RedemptionService>.Instance,
            modeResolverMock.Object,
            walletMock.Object);

        return (sut, walletMock, loyaltyMock, repoMock, sp);
    }

    // ──────────────────────────────────────────────────────────
    // Test 1: Alliance mode + member → deducts from AllianceWalletService
    // ──────────────────────────────────────────────────────────

    [Fact(DisplayName = "LA-RD-1: RedeemAsync — Alliance mode + member deducts from AllianceWalletService")]
    public async Task RedeemAsync_AllianceMode_Member_DeductsFromAllianceWallet()
    {
        var (sut, walletMock, loyaltyMock, repoMock, sp) = BuildService(LoyaltyMode.Alliance, isAllianceMember: true);

        try
        {
            var result = await sut.RedeemAsync(TestCustomerId, TestCatalogItemId);

            Assert.True(result.Success);
            Assert.Equal(500, result.NewPointBalance);

            // AllianceWalletService.DeductPointsAsync MUST be called
            walletMock.Verify(
                w => w.DeductPointsAsync(TestDeviceId, TestTenantGuid, 100, It.Is<string>(s => s.Contains("Redeem")), It.IsAny<string?>()),
                Times.Once,
                "Alliance mode + member must deduct from AllianceWalletService");

            // LoyaltyRewardsService.SubtractPointsAsync must NOT be called (Alliance flow)
            loyaltyMock.Verify(
                l => l.SubtractPointsAsync(It.IsAny<Guid>(), It.IsAny<int>(), It.IsAny<string>()),
                Times.Never,
                "Alliance mode must NOT deduct from local LoyaltyRewardsService");

            // Voucher + RedemptionRecord MUST be created in local SQLite
            repoMock.Verify(r => r.AddRecordAsync(It.IsAny<RedemptionRecord>()), Times.Once);
            repoMock.Verify(r => r.AddVoucherAsync(It.IsAny<Voucher>()), Times.Once);
        }
        finally
        {
            await sp.DisposeAsync();
        }
    }

    // ──────────────────────────────────────────────────────────
    // Test 2: Silo mode → deducts from LoyaltyRewardsService (existing flow)
    // ──────────────────────────────────────────────────────────

    [Fact(DisplayName = "LA-RD-2: RedeemAsync — Silo mode deducts from LoyaltyRewardsService")]
    public async Task RedeemAsync_SiloMode_DeductsFromLoyaltyRewards()
    {
        var (sut, walletMock, loyaltyMock, _, sp) = BuildService(LoyaltyMode.Silo, isAllianceMember: false);

        try
        {
            var result = await sut.RedeemAsync(TestCustomerId, TestCatalogItemId);

            Assert.True(result.Success);

            // AllianceWalletService.DeductPointsAsync must NOT be called
            walletMock.Verify(
                w => w.DeductPointsAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string?>()),
                Times.Never,
                "Silo mode must NOT deduct from AllianceWalletService");

            // LoyaltyRewardsService.SubtractPointsAsync MUST be called
            loyaltyMock.Verify(
                l => l.SubtractPointsAsync(TestCustomerId, 100, It.Is<string>(s => s.Contains("Redeem"))),
                Times.Once,
                "Silo mode must deduct from local LoyaltyRewardsService");
        }
        finally
        {
            await sp.DisposeAsync();
        }
    }

    // ──────────────────────────────────────────────────────────
    // Test 3: Alliance mode + tenant opt-out → returns fail (Q2: full opt-out)
    // ──────────────────────────────────────────────────────────

    [Fact(DisplayName = "LA-RD-3: RedeemAsync — Alliance mode + tenant opt-out returns fail")]
    public async Task RedeemAsync_AllianceMode_TenantOptOut_ReturnsFail()
    {
        var (sut, walletMock, loyaltyMock, _, sp) = BuildService(LoyaltyMode.Alliance, isAllianceMember: false);

        try
        {
            var result = await sut.RedeemAsync(TestCustomerId, TestCatalogItemId);

            // Per spec Q2: tenant opt-out = full Silo. Plan says return Fail for Alliance+opt-out in REDEEM.
            // (Unlike EARN which falls through to Silo, REDEEM in Alliance mode + opt-out is rejected —
            //  the tenant is not in the alliance, so cross-tenant redeem is not possible.)
            Assert.False(result.Success);
            Assert.Contains("liên minh", result.Error ?? "", StringComparison.OrdinalIgnoreCase);

            // Neither wallet nor loyalty should be deducted
            walletMock.Verify(
                w => w.DeductPointsAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string?>()),
                Times.Never);
            loyaltyMock.Verify(
                l => l.SubtractPointsAsync(It.IsAny<Guid>(), It.IsAny<int>(), It.IsAny<string>()),
                Times.Never);
        }
        finally
        {
            await sp.DisposeAsync();
        }
    }

    // ──────────────────────────────────────────────────────────
    // Test 4: Alliance mode + member + insufficient balance → returns fail
    // ──────────────────────────────────────────────────────────

    [Fact(DisplayName = "LA-RD-4: RedeemAsync — Alliance mode + insufficient balance returns fail")]
    public async Task RedeemAsync_AllianceMode_InsufficientBalance_ReturnsFail()
    {
        var (sut, walletMock, loyaltyMock, _, sp) = BuildService(
            LoyaltyMode.Alliance, isAllianceMember: true,
            walletDeductSuccess: false, walletError: "Insufficient points");

        try
        {
            var result = await sut.RedeemAsync(TestCustomerId, TestCatalogItemId);

            Assert.False(result.Success);
            Assert.Contains("Insufficient", result.Error ?? "", StringComparison.OrdinalIgnoreCase);

            // Wallet deduct was attempted (and failed)
            walletMock.Verify(
                w => w.DeductPointsAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string?>()),
                Times.Once);

            // Local LoyaltyRewardsService must NOT be deducted (Alliance flow, wallet failed)
            loyaltyMock.Verify(
                l => l.SubtractPointsAsync(It.IsAny<Guid>(), It.IsAny<int>(), It.IsAny<string>()),
                Times.Never);
        }
        finally
        {
            await sp.DisposeAsync();
        }
    }
}
