using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using VanAn.CoreHub.Infrastructure;
using VanAn.CoreHub.Services;
using VanAn.Shared.Domain;
using VanAn.Shared.Domain.Aggregates.TenantAggregate;
using VanAn.Shared.Domain.Common;
using Xunit;
using Tenant = VanAn.Shared.Domain.Aggregates.TenantAggregate.Tenant;

namespace VanAn.Core.Tests.Community;

/// <summary>
/// Sprint 7 — WalletService dual-mode tests (T8-T15) + SalesmanService OnMargin (T19-T21).
/// Reseller COD 5-split, Reseller advance (Vạn An → tenant), external payment, community fund spend.
/// </summary>
public class WalletServiceDualModeTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly VanAnDbContext _context;
    private readonly WalletService _service;
    private readonly StubTenantProvider _tenantProvider;
    private static readonly Guid TenantGuid = Guid.Parse("00000000-0000-0000-0000-000000000001");
    private static readonly Guid ShipperId = Guid.NewGuid();
    private static readonly Guid SalesmanId = Guid.NewGuid();
    private readonly TenantId _tenantId = new(TenantGuid);

    public WalletServiceDualModeTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<VanAnDbContext>()
            .UseSqlite(_connection)
            .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        _context = new VanAnDbContext(options);
        _context.Database.EnsureCreated();

        _tenantProvider = new StubTenantProvider(TenantGuid);
        _service = new WalletService(_context, _tenantProvider, NullLogger<WalletService>.Instance);
    }

    public void Dispose()
    {
        _context.Dispose();
        _connection.Dispose();
    }

    private static void SetProp<T>(T obj, string propName, object value)
        => typeof(T).GetProperty(propName)?.SetValue(obj, value);

    private async Task SeedTenantAsync()
    {
        var tenant = Tenant.CreateCompany(_tenantId, "Reseller Test Tenant", TenantSettings.Empty());
        _context.Tenants.Add(tenant);
        await _context.SaveChangesAsync();
    }

    private async Task<Guid> SeedResellerOrderAsync(
        decimal costPrice = 80000m,
        decimal sellPrice = 100000m,
        decimal deliveryFee = 15000m,
        decimal platformFeeRate = 0.30m,
        decimal communityFundRate = 0.05m,
        bool withSalesman = false,
        bool withReferralConfig = false)
    {
        var orderId = Guid.NewGuid();
        var order = new Order(_tenantId, null, sellPrice + deliveryFee);
        SetProp(order, "Id", orderId);
        SetProp(order, "OrderId", new OrderId(orderId));
        SetProp(order, "OrderType", "DELIVERY");
        SetProp(order, "Status", new OrderStatusId("delivering"));
        SetProp(order, "TotalAmount", sellPrice + deliveryFee);
        SetProp(order, "PaymentMethod", "COD");
        SetProp(order, "ShipperId", ShipperId);
        SetProp(order, "CodAmount", sellPrice + deliveryFee);

        var margin = sellPrice - costPrice;
        order.SetResellerPricing(costPrice, sellPrice, margin, deliveryFee, platformFeeRate, communityFundRate);

        if (withSalesman)
        {
            SetProp(order, "SalesmanId", SalesmanId);
            SetProp(order, "ReferralProductId", Guid.NewGuid());
        }

        _context.Orders.Add(order);
        await _context.SaveChangesAsync();

        if (withSalesman && withReferralConfig)
        {
            var productId = (Guid)typeof(Order).GetProperty("ReferralProductId")!.GetValue(order)!;
            var config = new ProductReferralConfig(_tenantId, productId, 0.03m, 10000m, "PROD001", CommissionBase.OnMargin);
            _context.ProductReferralConfigs.Add(config);
            await _context.SaveChangesAsync();
        }

        return orderId;
    }

    private async Task SeedDeliveryTaskAsync(Guid orderId)
    {
        var task = new DeliveryTask(_tenantId, orderId, ShipperId, 10.8, 106.7, 10.9, 106.8);
        _context.DeliveryTasks.Add(task);
        await _context.SaveChangesAsync();
    }

    // T8: Reseller COD — creates 5-split (CODCollection + Settlement + DeliveryFee + PlatformFee + CommunityFund)
    [Fact(DisplayName = "T8: ResellerCOD_Creates5Split")]
    public async Task ResellerCOD_Creates5Split()
    {
        await SeedTenantAsync();
        var orderId = await SeedResellerOrderAsync();
        await SeedDeliveryTaskAsync(orderId);

        var codAmount = 115000m; // sellPrice(100K) + deliveryFee(15K)
        var tx = await _service.ConfirmCodAsync(ShipperId, orderId, codAmount);

        Assert.Equal(WalletTransactionType.CODCollection, tx.Type);
        Assert.Equal(codAmount, tx.Amount);

        // Verify all 5 txs created
        var allTxs = await _context.WalletTransactions
            .IgnoreQueryFilters()
            .Where(t => t.RelatedOrderId == orderId)
            .ToListAsync();

        Assert.Contains(allTxs, t => t.Type == WalletTransactionType.CODCollection);
        Assert.Contains(allTxs, t => t.Type == WalletTransactionType.Settlement);
        Assert.Contains(allTxs, t => t.Type == WalletTransactionType.DeliveryFee);
        Assert.Contains(allTxs, t => t.Type == WalletTransactionType.PlatformFee);
        Assert.Contains(allTxs, t => t.Type == WalletTransactionType.CommunityFund);
        Assert.Equal(5, allTxs.Count);
    }

    // T9: Reseller COD — Settlement amount = CostPrice (Vạn An trả tenant giá vốn)
    [Fact(DisplayName = "T9: ResellerCOD_SettlementEqualsCostPrice")]
    public async Task ResellerCOD_SettlementEqualsCostPrice()
    {
        await SeedTenantAsync();
        var orderId = await SeedResellerOrderAsync(costPrice: 80000m);
        await SeedDeliveryTaskAsync(orderId);

        await _service.ConfirmCodAsync(ShipperId, orderId, 115000m);

        var settlementTx = await _context.WalletTransactions
            .IgnoreQueryFilters()
            .FirstAsync(t => t.RelatedOrderId == orderId && t.Type == WalletTransactionType.Settlement);

        Assert.Equal(80000m, settlementTx.Amount); // +CostPrice to tenant
    }

    // T10: Reseller COD — DeliveryFee amount = order.DeliveryFee
    [Fact(DisplayName = "T10: ResellerCOD_DeliveryFeeCorrect")]
    public async Task ResellerCOD_DeliveryFeeCorrect()
    {
        await SeedTenantAsync();
        var orderId = await SeedResellerOrderAsync(deliveryFee: 20000m);
        await SeedDeliveryTaskAsync(orderId);

        await _service.ConfirmCodAsync(ShipperId, orderId, 120000m);

        var deliveryTx = await _context.WalletTransactions
            .IgnoreQueryFilters()
            .FirstAsync(t => t.RelatedOrderId == orderId && t.Type == WalletTransactionType.DeliveryFee);

        Assert.Equal(20000m, deliveryTx.Amount);
    }

    // T11: Reseller COD — PlatformFee = margin × rate
    [Fact(DisplayName = "T11: ResellerCOD_PlatformFeeCorrect")]
    public async Task ResellerCOD_PlatformFeeCorrect()
    {
        await SeedTenantAsync();
        // margin = 100K - 80K = 20K, platformFeeRate = 0.30 → platformFee = 6000
        var orderId = await SeedResellerOrderAsync(costPrice: 80000m, sellPrice: 100000m, platformFeeRate: 0.30m);
        await SeedDeliveryTaskAsync(orderId);

        await _service.ConfirmCodAsync(ShipperId, orderId, 115000m);

        var platformFeeTx = await _context.WalletTransactions
            .IgnoreQueryFilters()
            .FirstAsync(t => t.RelatedOrderId == orderId && t.Type == WalletTransactionType.PlatformFee);

        Assert.Equal(6000m, platformFeeTx.Amount); // 20000 × 0.30
    }

    // T12: Reseller COD — CommunityFund = margin × rate
    [Fact(DisplayName = "T12: ResellerCOD_CommunityFundCorrect")]
    public async Task ResellerCOD_CommunityFundCorrect()
    {
        await SeedTenantAsync();
        // margin = 20K, communityFundRate = 0.05 → communityFund = 1000
        var orderId = await SeedResellerOrderAsync(communityFundRate: 0.05m);
        await SeedDeliveryTaskAsync(orderId);

        await _service.ConfirmCodAsync(ShipperId, orderId, 115000m);

        var communityFundTx = await _context.WalletTransactions
            .IgnoreQueryFilters()
            .FirstAsync(t => t.RelatedOrderId == orderId && t.Type == WalletTransactionType.CommunityFund);

        Assert.Equal(1000m, communityFundTx.Amount); // 20000 × 0.05
    }

    // T13: Reseller COD — with salesman creates Commission tx (OnMargin)
    [Fact(DisplayName = "T13: ResellerCOD_WithSalesman_CreatesCommission")]
    public async Task ResellerCOD_WithSalesman_CreatesCommission()
    {
        await SeedTenantAsync();
        var orderId = await SeedResellerOrderAsync(withSalesman: true, withReferralConfig: true);
        await SeedDeliveryTaskAsync(orderId);

        await _service.ConfirmCodAsync(ShipperId, orderId, 115000m);

        var commissionTx = await _context.WalletTransactions
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(t => t.RelatedOrderId == orderId && t.Type == WalletTransactionType.Commission);

        Assert.NotNull(commissionTx);
        // margin = 20K, commissionRate = 0.03 → commission = 600
        Assert.Equal(600m, commissionTx!.Amount);
    }

    // T14: Reseller advance — Vạn An ứng (PlatformWallet → tenant), not shipper
    [Fact(DisplayName = "T14: ResellerAdvance_VanAnApps_NotShipper")]
    public async Task ResellerAdvance_VanAnApps_NotShipper()
    {
        await SeedTenantAsync();
        var orderId = await SeedResellerOrderAsync();
        await SeedDeliveryTaskAsync(orderId);

        var advanceTx = await _service.ConfirmAdvanceAsync(ShipperId, orderId, 50000m);

        Assert.Equal(WalletTransactionType.AdvancePayment, advanceTx.Type);
        Assert.Equal(SystemWalletIds.PlatformWallet, advanceTx.OwnerId); // Vạn An, not shipper
        Assert.Equal(-50000m, advanceTx.Amount); // negative = money out

        // Verify Settlement tx for tenant (+amount)
        var settlementTx = await _context.WalletTransactions
            .IgnoreQueryFilters()
            .FirstAsync(t => t.RelatedOrderId == orderId && t.Type == WalletTransactionType.Settlement);

        Assert.Equal(50000m, settlementTx.Amount); // +amount to tenant
    }

    // T15: External payment (Q5) — Reseller only, rejects Marketplace
    [Fact(DisplayName = "T15: ExternalPayment_ResellerOnly_RejectsMarketplace")]
    public async Task ExternalPayment_ResellerOnly_RejectsMarketplace()
    {
        await SeedTenantAsync();
        // Create Marketplace order (default — no SetResellerPricing)
        var orderId = Guid.NewGuid();
        var order = new Order(_tenantId, null, 100000m);
        SetProp(order, "Id", orderId);
        SetProp(order, "OrderId", new OrderId(orderId));
        SetProp(order, "OrderType", "DELIVERY");
        SetProp(order, "Status", new OrderStatusId("delivering"));
        SetProp(order, "TotalAmount", 100000m);
        _context.Orders.Add(order);
        await _context.SaveChangesAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service.ConfirmExternalPaymentAsync(orderId, 100000m, "VQR-123"));
    }

    // T19: SalesmanService OnMargin — commission = margin × rate
    [Fact(DisplayName = "T19: SalesmanCommission_OnMargin_Correct")]
    public async Task SalesmanCommission_OnMargin_Correct()
    {
        await SeedTenantAsync();
        var orderId = await SeedResellerOrderAsync(withSalesman: true, withReferralConfig: true);
        await SeedDeliveryTaskAsync(orderId);

        // Use SalesmanService to create commission
        var riskScoringService = new RiskScoringService();
        var fraudFlagService = new FraudFlagService(_context, NullLogger<FraudFlagService>.Instance);
        var salesmanService = new SalesmanService(_context, riskScoringService, fraudFlagService, NullLogger<SalesmanService>.Instance);

        var referral = await salesmanService.CreateCommissionAsync(orderId);

        Assert.NotNull(referral);
        // margin = 20K, commissionRate = 0.03 → commission = 600
        Assert.Equal(600m, referral!.CommissionAmount);
    }

    // T20: SalesmanService OnOrderTotal — Marketplace commission = orderTotal × rate (existing behavior)
    [Fact(DisplayName = "T20: SalesmanCommission_OnOrderTotal_Correct")]
    public async Task SalesmanCommission_OnOrderTotal_Correct()
    {
        await SeedTenantAsync();

        // Create Marketplace order with salesman
        var productId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var order = new Order(_tenantId, null, 100000m);
        SetProp(order, "Id", orderId);
        SetProp(order, "OrderId", new OrderId(orderId));
        SetProp(order, "OrderType", "DELIVERY");
        SetProp(order, "Status", new OrderStatusId("delivering"));
        SetProp(order, "TotalAmount", 100000m);
        SetProp(order, "SalesmanId", SalesmanId);
        SetProp(order, "ReferralProductId", productId);
        _context.Orders.Add(order);

        // ProductReferralConfig with OnOrderTotal (default)
        var config = new ProductReferralConfig(_tenantId, productId, 0.03m, 10000m, "PROD002", CommissionBase.OnOrderTotal);
        _context.ProductReferralConfigs.Add(config);

        // Seed salesman role
        var role = new CommunityRole(_tenantId, SalesmanId, CommunityRoleType.Salesman, Guid.NewGuid());
        SetProp(role, "SalesmanCode", "SM001");
        _context.CommunityRoles.Add(role);

        await _context.SaveChangesAsync();

        var riskScoringService = new RiskScoringService();
        var fraudFlagService = new FraudFlagService(_context, NullLogger<FraudFlagService>.Instance);
        var salesmanService = new SalesmanService(_context, riskScoringService, fraudFlagService, NullLogger<SalesmanService>.Instance);

        var referral = await salesmanService.CreateCommissionAsync(orderId);

        Assert.NotNull(referral);
        // orderTotal = 100K, commissionRate = 0.03 → commission = 3000
        Assert.Equal(3000m, referral!.CommissionAmount);
    }

    // T21: Community fund spend — insufficient balance rejected
    [Fact(DisplayName = "T21: CommunityFundSpend_Insufficient_Rejected")]
    public async Task CommunityFundSpend_Insufficient_Rejected()
    {
        // No fund balance seeded
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service.SpendCommunityFundAsync(50000m, "Test spend", Guid.NewGuid()));
    }

    // T22: Community fund spend — valid creates tx
    [Fact(DisplayName = "T22: CommunityFundSpend_Valid_CreatesTx")]
    public async Task CommunityFundSpend_Valid_CreatesTx()
    {
        // Seed fund balance
        await _service.CreateTransactionAsync(
            SystemWalletIds.CommunityFund,
            WalletTransactionType.CommunityFund,
            200000m,
            "Seed fund");

        var tx = await _service.SpendCommunityFundAsync(50000m, "Tài trợ sự kiện", Guid.NewGuid());

        Assert.Equal(WalletTransactionType.CommunityFundSpend, tx.Type);
        Assert.Equal(-50000m, tx.Amount);
        Assert.Equal(150000m, tx.BalanceAfter); // 200K - 50K
    }

    private class StubTenantProvider : VanAn.Shared.Domain.Common.ITenantProvider
    {
        private readonly Guid _tenantId;
        public StubTenantProvider(Guid tenantId) => _tenantId = tenantId;
        public Guid TenantId => _tenantId;
        public string? CurrentUser => "test";
        public bool HasTenant => true;
        public void SetTenant(Guid tenantId) { /* no-op for tests */ }
    }
}
