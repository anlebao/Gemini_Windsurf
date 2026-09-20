using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using VanAn.CoreHub.Infrastructure;
using VanAn.CoreHub.Infrastructure.Repositories;
using VanAn.CoreHub.Services;
using VanAn.CoreHub.Tests.TestInfrastructure;
using VanAn.Shared.Domain;
using VanAn.Shared.Domain.Common;
using VanAn.Shared.Services;
using Xunit;

namespace VanAn.Core.Tests.Services;

/// <summary>
/// Loyalty Points Integrity (Batch 2, Phase 1) — PG ledger = single source of truth.
/// SQLite in-memory (VanAnDbContextTestFactory) exercising the REAL LoyaltyRewardsService +
/// repository (Silo) and a mocked AllianceWalletService (Alliance) / mocked budget caps.
///
/// Covers the approved plan Phase 1 test list:
///   - award Silo creates a row at (customer, tenant) — tenant attribution (RC2.4)
///   - award Alliance → wallet EARN with TransactionTenantId
///   - spend Silo at a tenant without points → reject ("luật Silo")
///   - spend Silo at the right tenant → deducts the right row
///   - award twice for the same orderId → centralized guard blocks (RC3)
///   - RevertOrder → points reversed + issuance record marked reversed
/// </summary>
public class LoyaltyPointLedgerServiceTests : IDisposable
{
    private readonly TestContextScope _scope;
    private readonly VanAnDbContext _db;
    private readonly LoyaltyPointLedgerService _sut;
    private readonly Mock<ILoyaltyModeResolver> _modeResolverMock;
    private readonly Mock<IAllianceWalletService> _walletMock;
    private readonly Mock<ILoyaltyBudgetService> _budgetMock;

    private static readonly Guid TenantA = Guid.Parse("00000000-0000-0000-0000-00000000000a");
    private static readonly Guid TenantB = Guid.Parse("00000000-0000-0000-0000-00000000000b");

    public LoyaltyPointLedgerServiceTests()
    {
        _scope = VanAnDbContextTestFactory.Create();
        _db = _scope.Context;

        _modeResolverMock = new Mock<ILoyaltyModeResolver>();
        _modeResolverMock.Setup(m => m.GetEffectiveModeAsync(It.IsAny<Guid>())).ReturnsAsync(LoyaltyMode.Silo);
        _modeResolverMock.Setup(m => m.IsAllianceMemberAsync(It.IsAny<Guid>())).ReturnsAsync(false);

        _walletMock = new Mock<IAllianceWalletService>();
        _walletMock.Setup(w => w.AddPointsAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<Guid?>(), It.IsAny<string?>()))
            .ReturnsAsync((true, 500, (string?)null));
        _walletMock.Setup(w => w.DeductPointsAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string?>()))
            .ReturnsAsync((true, 400, (string?)null));

        _budgetMock = new Mock<ILoyaltyBudgetService>();
        _budgetMock.Setup(b => b.CheckAndAdjustPointsAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<decimal?>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid _, Guid _, decimal? _, int requested, CancellationToken _) => requested);

        _sut = new LoyaltyPointLedgerService(
            _db,
            new LoyaltyRewardsService(
                new LoyaltyRewardsRepository(_db),
                NullLogger<LoyaltyRewardsService>.Instance),
            NullLogger<LoyaltyPointLedgerService>.Instance,
            _modeResolverMock.Object,
            _walletMock.Object,
            _budgetMock.Object,
            customerRepository: null,
            featureFlagService: null);
    }

    public void Dispose() => _scope.Dispose();

    private Customer CreateCustomer(Guid customerId, Guid tenantId, Guid? deviceId = null)
    {
        var customer = new Customer(new TenantId(tenantId), "Test Customer", "0900000001");
        typeof(BaseEntity).GetProperty(nameof(BaseEntity.Id))!.SetValue(customer, customerId);
        typeof(Customer).GetProperty(nameof(Customer.CustomerId))!.SetValue(customer, new CustomerId(customerId));
        customer.UpdateCustomerDetails("Test Customer", "0900000001", null, "Bronze", deviceId, true);
        // Verified so the redeem gate (IdentityLevel >= Verified) passes for spend/revert tests.
        customer.UpgradeIdentityLevel(IdentityLevel.Verified);
        _ = _db.Customers.Add(customer);
        _ = _db.SaveChanges();
        return customer;
    }

    // ──────────────────────────────────────────────────────────
    // Tenant attribution (RC2.4)
    // ──────────────────────────────────────────────────────────

    [Fact(DisplayName = "LED-1: award Silo creates a row per (customer, tenant) — no cross-tenant merging")]
    public async Task Award_Silo_TenantAttribution_OneRowPerTenant()
    {
        var customerId = Guid.NewGuid();
        _ = CreateCustomer(customerId, TenantA);

        // Earn 100 at tenant A
        LedgerResult r1 = await _sut.AwardAsync(new AwardRequest
        {
            CustomerId = customerId,
            TenantId = TenantA,
            Points = 100,
            Reason = "Order #o1"
        });

        // Earn 50 at tenant B — must land in B's row, NOT merge into A's row
        LedgerResult r2 = await _sut.AwardAsync(new AwardRequest
        {
            CustomerId = customerId,
            TenantId = TenantB,
            Points = 50,
            Reason = "Order #o2"
        });

        r1.Status.Should().Be(LedgerOperationStatus.Success);
        r2.Status.Should().Be(LedgerOperationStatus.Success);

        int balanceA = await _sut.GetBalanceAsync(customerId, TenantA);
        int balanceB = await _sut.GetBalanceAsync(customerId, TenantB);
        balanceA.Should().Be(100, "tenant A row must hold only A-earned points");
        balanceB.Should().Be(50, "tenant B row must hold only B-earned points");
    }

    // ──────────────────────────────────────────────────────────
    // Luật Silo (spend only at the awarding tenant)
    // ──────────────────────────────────────────────────────────

    [Fact(DisplayName = "LED-2: spend Silo at a tenant without points → rejected (luật Silo)")]
    public async Task Spend_Silo_AtTenantWithoutPoints_Rejected()
    {
        var customerId = Guid.NewGuid();
        _ = CreateCustomer(customerId, TenantA);

        _ = await _sut.AwardAsync(new AwardRequest
        {
            CustomerId = customerId,
            TenantId = TenantA,
            Points = 100,
            Reason = "Order #o1"
        });

        // Try to spend at tenant B where the customer has no row → Silo law blocks it
        LedgerResult spend = await _sut.SpendAsync(new SpendRequest
        {
            CustomerId = customerId,
            TenantId = TenantB,
            Points = 30,
            Reason = "Redeem at B"
        });

        spend.Status.Should().Be(LedgerOperationStatus.Rejected);
        spend.Error.Should().Contain("tenant");

        // Tenant A balance untouched
        (await _sut.GetBalanceAsync(customerId, TenantA)).Should().Be(100);
    }

    [Fact(DisplayName = "LED-3: spend Silo at the awarding tenant → deducts the right row")]
    public async Task Spend_Silo_AtAwardingTenant_Deducts()
    {
        var customerId = Guid.NewGuid();
        _ = CreateCustomer(customerId, TenantA);

        _ = await _sut.AwardAsync(new AwardRequest
        {
            CustomerId = customerId,
            TenantId = TenantA,
            Points = 100,
            Reason = "Order #o1"
        });

        LedgerResult spend = await _sut.SpendAsync(new SpendRequest
        {
            CustomerId = customerId,
            TenantId = TenantA,
            Points = 30,
            Reason = "Redeem at A"
        });

        spend.Status.Should().Be(LedgerOperationStatus.Success);
        spend.NewBalance.Should().Be(70);
        (await _sut.GetBalanceAsync(customerId, TenantA)).Should().Be(70);
    }

    [Fact(DisplayName = "LED-4: spend more than available at the awarding tenant → rejected")]
    public async Task Spend_Silo_MoreThanBalance_Rejected()
    {
        var customerId = Guid.NewGuid();
        _ = CreateCustomer(customerId, TenantA);

        _ = await _sut.AwardAsync(new AwardRequest
        {
            CustomerId = customerId,
            TenantId = TenantA,
            Points = 10,
            Reason = "Order #o1"
        });

        LedgerResult spend = await _sut.SpendAsync(new SpendRequest
        {
            CustomerId = customerId,
            TenantId = TenantA,
            Points = 999,
            Reason = "Redeem"
        });

        spend.Status.Should().Be(LedgerOperationStatus.Rejected);
    }

    // ──────────────────────────────────────────────────────────
    // Alliance routing
    // ──────────────────────────────────────────────────────────

    [Fact(DisplayName = "LED-5: award Alliance member → routes to AllianceWalletService (EARN)")]
    public async Task Award_AllianceMember_RoutesToWallet()
    {
        _modeResolverMock.Setup(m => m.GetEffectiveModeAsync(It.IsAny<Guid>())).ReturnsAsync(LoyaltyMode.Alliance);
        _modeResolverMock.Setup(m => m.IsAllianceMemberAsync(It.IsAny<Guid>())).ReturnsAsync(true);

        var customerId = Guid.NewGuid();
        var deviceId = Guid.NewGuid();
        _ = CreateCustomer(customerId, TenantA, deviceId);

        LedgerResult result = await _sut.AwardAsync(new AwardRequest
        {
            CustomerId = customerId,
            TenantId = TenantA,
            Points = 100,
            Reason = "Order #o1",
            SourceOrderId = Guid.NewGuid()
        });

        result.Status.Should().Be(LedgerOperationStatus.Success);
        result.NewBalance.Should().Be(500, "mock wallet balance");
        _walletMock.Verify(w => w.AddPointsAsync(deviceId, TenantA, 100, It.IsAny<string>(), It.IsAny<Guid?>(), It.IsAny<string?>()), Times.Once);
    }

    [Fact(DisplayName = "LED-6: spend Alliance member → routes to AllianceWalletService (REDEEM)")]
    public async Task Spend_AllianceMember_RoutesToWallet()
    {
        _modeResolverMock.Setup(m => m.GetEffectiveModeAsync(It.IsAny<Guid>())).ReturnsAsync(LoyaltyMode.Alliance);
        _modeResolverMock.Setup(m => m.IsAllianceMemberAsync(It.IsAny<Guid>())).ReturnsAsync(true);

        var customerId = Guid.NewGuid();
        var deviceId = Guid.NewGuid();
        _ = CreateCustomer(customerId, TenantA, deviceId);

        LedgerResult result = await _sut.SpendAsync(new SpendRequest
        {
            CustomerId = customerId,
            TenantId = TenantA,
            Points = 80,
            Reason = "Redeem",
            VoucherCode = "V1"
        });

        result.Status.Should().Be(LedgerOperationStatus.Success);
        result.NewBalance.Should().Be(400, "mock wallet balance");
        _walletMock.Verify(w => w.DeductPointsAsync(deviceId, TenantA, 80, It.IsAny<string>(), "V1", It.IsAny<string?>()), Times.Once);
    }

    // ──────────────────────────────────────────────────────────
    // Centralized double-award guard (RC3)
    // ──────────────────────────────────────────────────────────

    [Fact(DisplayName = "LED-7: award twice for the same orderId → second is skipped (centralized guard)")]
    public async Task Award_SameOrderTwice_SecondSkipped()
    {
        var customerId = Guid.NewGuid();
        _ = CreateCustomer(customerId, TenantA);
        var orderId = Guid.NewGuid();

        LedgerResult first = await _sut.AwardAsync(new AwardRequest
        {
            CustomerId = customerId,
            TenantId = TenantA,
            Points = 100,
            Reason = $"Hoàn tiền từ chiến dịch X - Đơn hàng #{orderId}",
            SourceOrderId = orderId,
            IdempotencyKey = $"earn:{orderId}"
        });

        LedgerResult second = await _sut.AwardAsync(new AwardRequest
        {
            CustomerId = customerId,
            TenantId = TenantA,
            Points = 100,
            Reason = $"Hoàn tiền từ chiến dịch X - Đơn hàng #{orderId}",
            SourceOrderId = orderId,
            IdempotencyKey = $"earn:{orderId}"
        });

        first.Status.Should().Be(LedgerOperationStatus.Success);
        second.Status.Should().Be(LedgerOperationStatus.Skipped);
        second.Error.Should().Contain(orderId.ToString());

        // Balance awarded exactly once
        (await _sut.GetBalanceAsync(customerId, TenantA)).Should().Be(100);
    }

    [Fact(DisplayName = "LED-8: GetAwardedPointsAsync returns the actual awarded points per order")]
    public async Task GetAwardedPoints_ReturnsActualAward()
    {
        var customerId = Guid.NewGuid();
        _ = CreateCustomer(customerId, TenantA);
        var orderId = Guid.NewGuid();

        _ = await _sut.AwardAsync(new AwardRequest
        {
            CustomerId = customerId,
            TenantId = TenantA,
            Points = 250,
            Reason = $"Order #{orderId}",
            SourceOrderId = orderId
        });

        int? awarded = await _sut.GetAwardedPointsAsync(orderId, TenantA);
        awarded.Should().Be(250);
    }

    // ──────────────────────────────────────────────────────────
    // Reversal (RevertOrderAsync)
    // ──────────────────────────────────────────────────────────

    [Fact(DisplayName = "LED-9: RevertOrder reverses the Silo row and marks the issuance record reversed")]
    public async Task RevertOrder_ReversesSiloPoints()
    {
        var customerId = Guid.NewGuid();
        _ = CreateCustomer(customerId, TenantA);
        var orderId = Guid.NewGuid();

        _ = await _sut.AwardAsync(new AwardRequest
        {
            CustomerId = customerId,
            TenantId = TenantA,
            Points = 100,
            Reason = $"Order #{orderId}",
            SourceOrderId = orderId
        });

        int reversed = await _sut.RevertOrderAsync(orderId, new TenantId(TenantA), "Order cancelled");

        reversed.Should().Be(100);
        (await _sut.GetBalanceAsync(customerId, TenantA)).Should().Be(0);

        var record = await _db.LoyaltyIssuanceRecords
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(r => r.OrderId == orderId);
        record.Should().NotBeNull();
        record!.IsReversed.Should().BeTrue();
    }

    // ──────────────────────────────────────────────────────────
    // Budget enforcement
    // ──────────────────────────────────────────────────────────

    [Fact(DisplayName = "LED-10: budget cap applies — adjusted points are awarded")]
    public async Task Award_BudgetCap_AppliesAdjustedPoints()
    {
        _budgetMock.Setup(b => b.CheckAndAdjustPointsAsync(TenantA, It.IsAny<Guid>(), It.IsAny<decimal?>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(40); // cap 100 → 40

        var customerId = Guid.NewGuid();
        _ = CreateCustomer(customerId, TenantA);

        LedgerResult result = await _sut.AwardAsync(new AwardRequest
        {
            CustomerId = customerId,
            TenantId = TenantA,
            Points = 100,
            Reason = "Order #o1"
        });

        result.Status.Should().Be(LedgerOperationStatus.Success);
        (await _sut.GetBalanceAsync(customerId, TenantA)).Should().Be(40);
        _budgetMock.Verify(b => b.RecordIssuanceAsync(TenantA, 40, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact(DisplayName = "LED-11: budget exhausted → award skipped (order still completes)")]
    public async Task Award_BudgetExhausted_Skipped()
    {
        _budgetMock.Setup(b => b.CheckAndAdjustPointsAsync(TenantA, It.IsAny<Guid>(), It.IsAny<decimal?>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);

        var customerId = Guid.NewGuid();
        _ = CreateCustomer(customerId, TenantA);

        LedgerResult result = await _sut.AwardAsync(new AwardRequest
        {
            CustomerId = customerId,
            TenantId = TenantA,
            Points = 100,
            Reason = "Order #o1"
        });

        result.Status.Should().Be(LedgerOperationStatus.Skipped);
        (await _sut.GetBalanceAsync(customerId, TenantA)).Should().Be(0);
    }
}
