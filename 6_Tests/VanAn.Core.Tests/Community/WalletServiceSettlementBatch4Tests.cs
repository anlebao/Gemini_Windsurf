using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using VanAn.CoreHub.Infrastructure;
using VanAn.CoreHub.Services;
using VanAn.Shared.Domain;
using VanAn.Shared.Domain.Aggregates.TenantAggregate;
using VanAn.Shared.Domain.Audit;
using VanAn.Shared.Domain.Common;
using VanAn.Shared.Services;
using Xunit;
using Tenant = VanAn.Shared.Domain.Aggregates.TenantAggregate.Tenant;

using Microsoft.Extensions.DependencyInjection;
namespace VanAn.Core.Tests.Community;

/// <summary>
/// Settlement Batch-4 (TC-10): hardening tests.
/// S3 margin invariant + rounding · S5 commission base display · S6 pending advances
/// server-side tenant filter · S7 wallet audit trail · S9 external payment hardening.
/// </summary>
public class WalletServiceSettlementBatch4Tests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly VanAnDbContext _context;
    private readonly WalletService _service;
    private readonly StubTenantProvider _tenantProvider;
    private static readonly Guid TenantGuid = Guid.Parse("00000000-0000-0000-0000-000000000001");
    private static readonly Guid ShipperId = Guid.NewGuid();
    private static readonly Guid SalesmanId = Guid.NewGuid();
    private readonly TenantId _tenantId = new(TenantGuid);

    public WalletServiceSettlementBatch4Tests()
    {
        _connection = new SqliteConnection($"DataSource=test_{Guid.NewGuid()};Mode=Memory;Cache=Shared");
        _connection.Open();

        var efServiceProvider = new ServiceCollection().AddEntityFrameworkSqlite().BuildServiceProvider();

        var options = new DbContextOptionsBuilder<VanAnDbContext>()
            .UseInternalServiceProvider(efServiceProvider).UseSqlite(_connection)
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
        var tenant = Tenant.CreateCompany(_tenantId, "Batch4 Test Tenant", TenantSettings.Empty());
        _context.Tenants.Add(tenant);
        await _context.SaveChangesAsync();
    }

    private async Task SeedProductAsync(Guid productId)
    {
        var product = new Product(_tenantId, "P-" + productId.ToString()[..8], 100m, "Test");
        SetProp(product, "Id", productId);
        _context.Products.Add(product);
        await _context.SaveChangesAsync();
    }

    private async Task<Guid> SeedResellerOrderAsync(
        decimal costPrice = 80000m,
        decimal sellPrice = 100000m,
        decimal deliveryFee = 15000m,
        decimal platformFeeRate = 0.30m,
        decimal communityFundRate = 0.05m)
    {
        var orderId = Guid.NewGuid();
        var referredProductId = Guid.NewGuid();
        await SeedProductAsync(referredProductId);
        var referredItem = OrderItem.Create(Guid.NewGuid(), _tenantId, orderId, referredProductId,
            quantity: 1, unitPrice: sellPrice, productName: "Referred", vatRate: 0m);

        var order = Order.Create(orderId, _tenantId, null, [referredItem]);
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

        _context.Orders.Add(order);
        await _context.SaveChangesAsync();
        return orderId;
    }

    private async Task<Guid> SeedMarketplaceOrderAsync(TenantId tenantId)
    {
        var orderId = Guid.NewGuid();
        var order = new Order(tenantId, null, 0);
        SetProp(order, "Id", orderId);
        SetProp(order, "OrderId", new OrderId(orderId));
        SetProp(order, "OrderType", "DELIVERY");
        SetProp(order, "Status", new OrderStatusId("delivering"));
        SetProp(order, "TotalAmount", 100000m);
        SetProp(order, "PaymentMethod", "COD");
        SetProp(order, "ShipperId", ShipperId);
        _context.Orders.Add(order);
        await _context.SaveChangesAsync();
        return orderId;
    }

    private async Task SeedDeliveryTaskAsync(Guid orderId, TenantId tenantId,
        DeliveryTaskStatus status = DeliveryTaskStatus.OutForDelivery)
    {
        var task = new DeliveryTask(tenantId, orderId, ShipperId, 10.8, 106.7, 10.9, 106.8);
        SetProp(task, "Status", status);
        _context.DeliveryTasks.Add(task);
        await _context.SaveChangesAsync();
    }

    // ===== TC-10 S3: margin invariant + rounding =====

    // B4-1: platformFeeRate + communityFundRate > 1 → reject before any wallet leg is written.
    [Fact(DisplayName = "B4-1: ResellerCod_RatesExceed100_ThrowsNoLeaks")]
    public async Task ResellerCod_RatesExceed100_ThrowsNoLeaks()
    {
        await SeedTenantAsync();
        var orderId = await SeedResellerOrderAsync(platformFeeRate: 0.70m, communityFundRate: 0.50m);
        await SeedDeliveryTaskAsync(orderId, _tenantId);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service.ConfirmCodAsync(ShipperId, orderId, 115000m));

        // Invariant check runs before the first leg — nothing leaks even before rollback.
        Assert.Empty(await _context.WalletTransactions.IgnoreQueryFilters()
            .Where(t => t.RelatedOrderId == orderId).ToListAsync());
    }

    // B4-2: negative rate → reject. SetResellerPricing guards 0-1 at write time, so
    // simulate a corrupted/legacy row by bypassing the domain setter.
    [Fact(DisplayName = "B4-2: ResellerCod_NegativeRate_Throws")]
    public async Task ResellerCod_NegativeRate_Throws()
    {
        await SeedTenantAsync();
        var orderId = await SeedResellerOrderAsync();
        var order = await _context.Orders.IgnoreQueryFilters().FirstAsync(o => o.Id == orderId);
        SetProp(order, "PlatformFeeRate", -0.10m);
        await _context.SaveChangesAsync();
        await SeedDeliveryTaskAsync(orderId, _tenantId);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service.ConfirmCodAsync(ShipperId, orderId, 115000m));
    }

    // B4-3: margin split rounds to whole VND (AwayFromZero — InvoicePolicyService convention).
    [Fact(DisplayName = "B4-3: ResellerCod_MarginSplit_RoundsVnd")]
    public async Task ResellerCod_MarginSplit_RoundsVnd()
    {
        await SeedTenantAsync();
        // margin = 100001 - 80000 = 20001 → platformFee = 20001 × 0.3333 = 6666.3333 → 6666
        // communityFund = 20001 × 0.05 = 1000.05 → 1000
        var orderId = await SeedResellerOrderAsync(
            costPrice: 80000m, sellPrice: 100001m,
            platformFeeRate: 0.3333m, communityFundRate: 0.05m);
        await SeedDeliveryTaskAsync(orderId, _tenantId);

        await _service.ConfirmCodAsync(ShipperId, orderId, 115001m);

        var platformFeeTx = await _context.WalletTransactions.IgnoreQueryFilters()
            .FirstAsync(t => t.RelatedOrderId == orderId && t.Type == WalletTransactionType.PlatformFee);
        var fundTx = await _context.WalletTransactions.IgnoreQueryFilters()
            .FirstAsync(t => t.RelatedOrderId == orderId && t.Type == WalletTransactionType.CommunityFund);

        Assert.Equal(6666m, platformFeeTx.Amount);
        Assert.Equal(1000m, fundTx.Amount);
    }

    // ===== TC-10 S6: pending advances — tenant-scoped server-side =====

    // B4-4: advances for OTHER tenants' orders are excluded (regression — the old
    // in-memory join could leak when order lookup was separate from the wallet query).
    [Fact(DisplayName = "B4-4: GetPendingAdvances_OnlyOwnTenant")]
    public async Task GetPendingAdvances_OnlyOwnTenant()
    {
        var ownOrderId = await SeedMarketplaceOrderAsync(_tenantId);
        await SeedDeliveryTaskAsync(ownOrderId, _tenantId);
        await _service.ConfirmAdvanceAsync(ShipperId, ownOrderId, 30000m);

        var otherTenantId = new TenantId(Guid.NewGuid());
        var otherOrderId = await SeedMarketplaceOrderAsync(otherTenantId);
        await SeedDeliveryTaskAsync(otherOrderId, otherTenantId);
        await _service.ConfirmAdvanceAsync(ShipperId, otherOrderId, 40000m);

        var pending = await _service.GetPendingAdvancesAsync(_tenantId.Value);

        var dto = Assert.Single(pending);
        Assert.Equal(ownOrderId, dto.OrderId);
        Assert.Equal(30000m, dto.Amount);
    }

    // ===== TC-10 S9: external payment hardening =====

    // B4-5: a payment reference already used on another order → deterministic reject.
    [Fact(DisplayName = "B4-5: ExternalPayment_DuplicatePaymentRef_Throws")]
    public async Task ExternalPayment_DuplicatePaymentRef_Throws()
    {
        await SeedTenantAsync();
        var orderId1 = await SeedResellerOrderAsync();
        var orderId2 = await SeedResellerOrderAsync();
        await SeedDeliveryTaskAsync(orderId1, _tenantId);
        await SeedDeliveryTaskAsync(orderId2, _tenantId);

        await _service.ConfirmExternalPaymentAsync(orderId1, 115000m, "VQR-DUP-1");

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service.ConfirmExternalPaymentAsync(orderId2, 115000m, "VQR-DUP-1"));

        // Second order must not leak any wallet entries.
        Assert.Empty(await _context.WalletTransactions.IgnoreQueryFilters()
            .Where(t => t.RelatedOrderId == orderId2).ToListAsync());
    }

    // B4-6: cancelled/failed delivery tasks must not receive the delivery-fee leg —
    // the previous FirstOrDefault could credit a cancelled attempt's shipper.
    [Fact(DisplayName = "B4-6: ExternalPayment_CancelledTask_NoDeliveryFeeLeg")]
    public async Task ExternalPayment_CancelledTask_NoDeliveryFeeLeg()
    {
        await SeedTenantAsync();
        var orderId = await SeedResellerOrderAsync();
        await SeedDeliveryTaskAsync(orderId, _tenantId, DeliveryTaskStatus.Cancelled);

        await _service.ConfirmExternalPaymentAsync(orderId, 115000m, "VQR-CANCEL-1");

        var txs = await _context.WalletTransactions.IgnoreQueryFilters()
            .Where(t => t.RelatedOrderId == orderId).ToListAsync();

        Assert.DoesNotContain(txs, t => t.Type == WalletTransactionType.DeliveryFee);
        Assert.Contains(txs, t => t.Type == WalletTransactionType.ExternalPayment);
        Assert.Contains(txs, t => t.Type == WalletTransactionType.Settlement);
    }

    // ===== TC-10 S7: wallet audit trail =====

    // B4-7: every wallet tx creation emits an audit record via IAuditTrailService.
    [Fact(DisplayName = "B4-7: CreateTransaction_WritesWalletAuditLog")]
    public async Task CreateTransaction_WritesWalletAuditLog()
    {
        var audit = new Mock<IAuditTrailService>();
        var service = new WalletService(_context, _tenantProvider, NullLogger<WalletService>.Instance,
            auditTrailService: audit.Object);

        var tx = await service.CreateTransactionAsync(
            ShipperId, WalletTransactionType.CODCollection, 50000m, "Audit me");

        audit.Verify(a => a.LogCreateAsync(
            AuditableEntityType.WalletTransaction,
            tx.Id,
            It.Is<string>(json => json.Contains("CODCollection") && json.Contains("50000")),
            It.IsAny<string?>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    // ===== TC-10 S5: commission display uses CommissionBaseAmount =====

    // B4-8: zero-rate referral — the old CommissionAmount/CommissionRate math divided by
    // 0 and showed OrderTotal=0; the snapshot base must surface instead.
    [Fact(DisplayName = "B4-8: GetCommissions_OrderTotal_UsesBaseAmount")]
    public async Task GetCommissions_OrderTotal_UsesBaseAmount()
    {
        var riskScoringService = new RiskScoringService();
        var fraudFlagService = new FraudFlagService(_context, NullLogger<FraudFlagService>.Instance);
        var salesmanService = new SalesmanService(_context, riskScoringService, fraudFlagService,
            NullLogger<SalesmanService>.Instance);

        var productId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var referral = new SalesReferral(_tenantId, SalesmanId, "SM001", productId);
        // Zero-rate config: commission = 0 but the base (order total) is still 100K.
        referral.AttachToOrder(orderId, Guid.NewGuid(), 100000m, 0m, CommissionBase.OnOrderTotal);
        _context.SalesReferrals.Add(referral);
        await _context.SaveChangesAsync();

        var summary = await salesmanService.GetCommissionsAsync(SalesmanId);

        var record = Assert.Single(summary.CommissionRecords);
        Assert.Equal(100000m, record.OrderTotal); // base snapshot, not 0/0
        Assert.Equal(100000m, summary.TotalSales);
        Assert.Equal(0m, record.CommissionAmount);
    }

    // B4-9 (RV follow-up): order-linked wallet txs must carry the ORDER's tenant —
    // production RV showed every tx written with TenantId=Guid.Empty because the
    // request-scoped provider is empty on Gateway community/admin endpoints, which
    // made the admin settlements tenant filter dead.
    [Fact(DisplayName = "B4-9: WalletTx_TenantId_IsOrderTenant_NotProvider")]
    public async Task WalletTx_TenantId_IsOrderTenant_NotProvider()
    {
        var orderTenant = new TenantId(Guid.NewGuid()); // differs from provider tenant
        var orderId = await SeedMarketplaceOrderAsync(orderTenant);
        await SeedDeliveryTaskAsync(orderId, orderTenant);

        await _service.ConfirmCodAsync(ShipperId, orderId, 100000m);

        var txs = await _context.WalletTransactions.IgnoreQueryFilters()
            .Where(t => t.RelatedOrderId == orderId).ToListAsync();
        Assert.NotEmpty(txs);
        Assert.All(txs, t => Assert.Equal(orderTenant.Value, t.TenantId.Value));
    }

    private sealed class StubTenantProvider : ITenantProvider
    {
        public StubTenantProvider(Guid tenantId) => TenantId = tenantId;
        public Guid TenantId { get; private set; }
        public string? CurrentUser => "test";
        public bool HasTenant => true;
        public void SetTenant(Guid tenantId) => TenantId = tenantId;
    }
}
