using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using VanAn.CoreHub.Infrastructure;
using VanAn.CoreHub.Infrastructure.Messaging;
using VanAn.CoreHub.Services;
using VanAn.Shared.Services;
using VanAn.Shared.Domain;
using VanAn.Shared.Domain.Aggregates.TenantAggregate;
using VanAn.Shared.Domain.Common;
using Xunit;
using Tenant = VanAn.Shared.Domain.Aggregates.TenantAggregate.Tenant;

using Microsoft.Extensions.DependencyInjection;
namespace VanAn.Core.Tests.Community;

/// <summary>
/// CC-S5 (Sprint 5): WalletService unit tests — wallet, COD, advance, settlement, immutability, balance chain.
/// 17 test cases per detailed plan Section 4 + shop-confirmed advance additions.
/// Uses SQLite in-memory (WalletService.CreateTransactionAsync handles SQLite via LINQ fallback).
/// </summary>
public class WalletServiceTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly VanAnDbContext _context;
    private readonly WalletService _service;
    private readonly StubTenantProvider _tenantProvider;
    private static readonly Guid TenantId = Guid.Parse("00000000-0000-0000-0000-000000000001");
    private static readonly Guid ShipperId = Guid.NewGuid();
    private static readonly Guid CustomerId = Guid.NewGuid();

    public WalletServiceTests()
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

        _tenantProvider = new StubTenantProvider(TenantId);
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
        var tenant = Tenant.CreateCompany(new TenantId(TenantId), "Shop A",
            TenantSettings.Empty().WithCoordinates(10.8, 106.7));
        _context.Tenants.Add(tenant);
        await _context.SaveChangesAsync();
    }

    private async Task<Guid> SeedOrderAsync(
        decimal? codAmount = null,
        string paymentMethod = "COD",
        string status = "delivering",
        string paymentStatus = "Pending")
    {
        var orderId = Guid.NewGuid();
        var order = new Order(new TenantId(TenantId), null, 0);
        SetProp(order, "Id", orderId);
        SetProp(order, "OrderId", new OrderId(orderId));
        SetProp(order, "OrderType", "DELIVERY");
        SetProp(order, "Status", new OrderStatusId(status));
        SetProp(order, "TotalAmount", 100000m);
        SetProp(order, "PaymentMethod", paymentMethod);
        SetProp(order, "PaymentStatus", paymentStatus);
        SetProp(order, "ShipperId", ShipperId);
        if (codAmount.HasValue)
            SetProp(order, "CodAmount", codAmount.Value);
        _context.Orders.Add(order);
        await _context.SaveChangesAsync();
        return orderId;
    }

    private async Task SeedDeliveryTaskAsync(Guid orderId, DeliveryTaskStatus status = DeliveryTaskStatus.OutForDelivery)
    {
        var task = new DeliveryTask(new TenantId(TenantId), orderId, ShipperId, 10.8, 106.7, 10.9, 106.8);
        SetProp(task, "Status", status);
        _context.DeliveryTasks.Add(task);
        await _context.SaveChangesAsync();
    }

    // === T1: GetWallet_Empty_ReturnsZero ===
    [Fact(DisplayName = "T1: GetWallet_Empty_ReturnsZero")]
    public async Task GetWallet_Empty_ReturnsZero()
    {
        var wallet = await _service.GetWalletAsync(ShipperId);
        Assert.Equal(0m, wallet.Balance);
        Assert.Empty(wallet.Transactions);
    }

    // === T2: GetWallet_WithTransactions_ReturnsBalance ===
    [Fact(DisplayName = "T2: GetWallet_WithTransactions_ReturnsBalance")]
    public async Task GetWallet_WithTransactions_ReturnsBalance()
    {
        await _service.CreateTransactionAsync(ShipperId, WalletTransactionType.CODCollection, 50000m, "Test COD");
        var wallet = await _service.GetWalletAsync(ShipperId);
        Assert.Equal(50000m, wallet.Balance);
        Assert.Single(wallet.Transactions);
    }

    // === T3: GetWallet_SortsByCreatedAtDesc ===
    [Fact(DisplayName = "T3: GetWallet_SortsByCreatedAtDesc")]
    public async Task GetWallet_SortsByCreatedAtDesc()
    {
        await _service.CreateTransactionAsync(ShipperId, WalletTransactionType.CODCollection, 50000m, "First");
        await Task.Delay(50); // Ensure CreatedAt differs
        await _service.CreateTransactionAsync(ShipperId, WalletTransactionType.Commission, 30000m, "Second");

        var wallet = await _service.GetWalletAsync(ShipperId);
        Assert.Equal(2, wallet.Transactions.Count);
        Assert.True(wallet.Transactions[0].CreatedAt >= wallet.Transactions[1].CreatedAt);
    }

    // === T4: ConfirmCod_CreatesTransaction ===
    [Fact(DisplayName = "T4: ConfirmCod_CreatesTransaction")]
    public async Task ConfirmCod_CreatesTransaction()
    {
        await SeedTenantAsync();
        var orderId = await SeedOrderAsync(codAmount: 50000m);
        await SeedDeliveryTaskAsync(orderId);

        var tx = await _service.ConfirmCodAsync(ShipperId, orderId, 50000m);

        Assert.Equal(WalletTransactionType.CODCollection, tx.Type);
        Assert.Equal(50000m, tx.Amount);
        Assert.Equal(50000m, tx.BalanceAfter);
        Assert.Equal(orderId, tx.RelatedOrderId);
    }

    // === T5: ConfirmCod_SetsOrderCodCollectedAt ===
    [Fact(DisplayName = "T5: ConfirmCod_SetsOrderCodCollectedAt")]
    public async Task ConfirmCod_SetsOrderCodCollectedAt()
    {
        await SeedTenantAsync();
        var orderId = await SeedOrderAsync(codAmount: 50000m);
        await SeedDeliveryTaskAsync(orderId);

        await _service.ConfirmCodAsync(ShipperId, orderId, 50000m);

        var order = await _context.Orders.IgnoreQueryFilters().FirstAsync(o => o.Id == orderId);
        Assert.NotNull(order.CodCollectedAt);
        Assert.Equal(50000m, order.CodAmount);
    }

    // === T6: ConfirmCod_CreatesSettlement ===
    [Fact(DisplayName = "T6: ConfirmCod_CreatesSettlement")]
    public async Task ConfirmCod_CreatesSettlement()
    {
        await SeedTenantAsync();
        var orderId = await SeedOrderAsync(codAmount: 50000m);
        await SeedDeliveryTaskAsync(orderId);

        await _service.ConfirmCodAsync(ShipperId, orderId, 50000m);

        // Shop wallet should have a Settlement tx with -amount
        var shopWallet = await _service.GetWalletAsync(TenantId);
        Assert.Single(shopWallet.Transactions);
        var settlement = shopWallet.Transactions[0];
        Assert.Equal("Settlement", settlement.Type);
        Assert.Equal(-50000m, settlement.Amount);
    }

    // === T7: ConfirmCod_AlreadyConfirmed_Throws ===
    [Fact(DisplayName = "T7: ConfirmCod_AlreadyConfirmed_Throws")]
    public async Task ConfirmCod_AlreadyConfirmed_Throws()
    {
        await SeedTenantAsync();
        var orderId = await SeedOrderAsync(codAmount: 50000m);
        await SeedDeliveryTaskAsync(orderId);

        await _service.ConfirmCodAsync(ShipperId, orderId, 50000m);

        // Second confirm should throw
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service.ConfirmCodAsync(ShipperId, orderId, 50000m));
    }

    // === T8: ConfirmCod_NotShipper_Throws ===
    [Fact(DisplayName = "T8: ConfirmCod_NotShipper_Throws")]
    public async Task ConfirmCod_NotShipper_Throws()
    {
        await SeedTenantAsync();
        var orderId = await SeedOrderAsync(codAmount: 50000m);
        await SeedDeliveryTaskAsync(orderId);

        var wrongShipper = Guid.NewGuid();
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            _service.ConfirmCodAsync(wrongShipper, orderId, 50000m));
    }

    // === T9: ConfirmCod_WrongAmount_Throws ===
    [Fact(DisplayName = "T9: ConfirmCod_WrongAmount_Throws")]
    public async Task ConfirmCod_WrongAmount_Throws()
    {
        await SeedTenantAsync();
        var orderId = await SeedOrderAsync(codAmount: 50000m);
        await SeedDeliveryTaskAsync(orderId);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service.ConfirmCodAsync(ShipperId, orderId, 99999m));
    }

    // === T10: ConfirmAdvance_CreatesTransaction ===
    [Fact(DisplayName = "T10: ConfirmAdvance_CreatesTransaction")]
    public async Task ConfirmAdvance_CreatesTransaction()
    {
        await SeedTenantAsync();
        var orderId = await SeedOrderAsync();
        await SeedDeliveryTaskAsync(orderId);

        var tx = await _service.ConfirmAdvanceAsync(ShipperId, orderId, 30000m);

        Assert.Equal(WalletTransactionType.AdvancePayment, tx.Type);
        Assert.Equal(-30000m, tx.Amount);
        Assert.Equal(-30000m, tx.BalanceAfter);
    }

    // === T11: ConfirmAdvance_BalanceGoesNegative ===
    [Fact(DisplayName = "T11: ConfirmAdvance_BalanceGoesNegative")]
    public async Task ConfirmAdvance_BalanceGoesNegative()
    {
        await SeedTenantAsync();
        var orderId = await SeedOrderAsync();
        await SeedDeliveryTaskAsync(orderId);

        await _service.ConfirmAdvanceAsync(ShipperId, orderId, 30000m);
        var balance = await _service.GetBalanceAsync(ShipperId);

        Assert.Equal(-30000m, balance);
    }

    // === T12: GetBalance_NoTransactions_ReturnsZero ===
    [Fact(DisplayName = "T12: GetBalance_NoTransactions_ReturnsZero")]
    public async Task GetBalance_NoTransactions_ReturnsZero()
    {
        var balance = await _service.GetBalanceAsync(Guid.NewGuid());
        Assert.Equal(0m, balance);
    }

    // === T13: GetBalance_MultipleTransactions_ReturnsLast ===
    [Fact(DisplayName = "T13: GetBalance_MultipleTransactions_ReturnsLast")]
    public async Task GetBalance_MultipleTransactions_ReturnsLast()
    {
        await _service.CreateTransactionAsync(ShipperId, WalletTransactionType.CODCollection, 50000m, "First");
        await _service.CreateTransactionAsync(ShipperId, WalletTransactionType.Withdrawal, -20000m, "Second");

        var balance = await _service.GetBalanceAsync(ShipperId);
        Assert.Equal(30000m, balance);
    }

    // === T14: WalletTransaction_Immutable_NoUpdateMethod ===
    [Fact(DisplayName = "T14: WalletTransaction_Immutable_NoUpdateMethod")]
    public void WalletTransaction_Immutable_NoUpdateMethod()
    {
        var type = typeof(WalletTransaction);
        var publicMethods = type.GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance)
            .Where(m => m.DeclaringType == type && !m.IsSpecialName);

        // Should only have constructors, no update methods (no methods that modify state)
        var updateMethods = publicMethods.Where(m =>
            !m.Name.StartsWith("get_") &&
            !m.Name.StartsWith("set_") &&
            !m.Name.StartsWith("ctor"));

        Assert.Empty(updateMethods);
    }

    // === T15: WalletTransaction_BalanceAfter_ChainCorrect ===
    [Fact(DisplayName = "T15: WalletTransaction_BalanceAfter_ChainCorrect")]
    public async Task WalletTransaction_BalanceAfter_ChainCorrect()
    {
        // Sequence: 0 → +50k → 50k → -30k → 20k
        var tx1 = await _service.CreateTransactionAsync(ShipperId, WalletTransactionType.CODCollection, 50000m, "COD");
        Assert.Equal(50000m, tx1.BalanceAfter);

        var tx2 = await _service.CreateTransactionAsync(ShipperId, WalletTransactionType.AdvancePayment, -30000m, "Advance");
        Assert.Equal(20000m, tx2.BalanceAfter);

        var balance = await _service.GetBalanceAsync(ShipperId);
        Assert.Equal(20000m, balance);
    }

    // === T16: ConfirmAdvanceReceived_CreatesSettlementForShop (shop-confirmed flow) ===
    [Fact(DisplayName = "T16: ConfirmAdvanceReceived_CreatesSettlementForShop")]
    public async Task ConfirmAdvanceReceived_CreatesSettlementForShop()
    {
        await SeedTenantAsync();
        var orderId = await SeedOrderAsync();
        await SeedDeliveryTaskAsync(orderId);

        // Shipper creates advance
        var advanceTx = await _service.ConfirmAdvanceAsync(ShipperId, orderId, 30000m);

        // Shop confirms receipt
        var settlementTx = await _service.ConfirmAdvanceReceivedAsync(TenantId, advanceTx.Id);

        Assert.Equal(WalletTransactionType.Settlement, settlementTx.Type);
        Assert.Equal(30000m, settlementTx.Amount); // -(-30000) = +30000
        Assert.Equal(advanceTx.Id, settlementTx.RelatedTransactionId);
    }

    // === T17: ConfirmAdvanceReceived_AlreadyConfirmed_Throws (idempotency) ===
    [Fact(DisplayName = "T17: ConfirmAdvanceReceived_AlreadyConfirmed_Throws")]
    public async Task ConfirmAdvanceReceived_AlreadyConfirmed_Throws()
    {
        await SeedTenantAsync();
        var orderId = await SeedOrderAsync();
        await SeedDeliveryTaskAsync(orderId);

        var advanceTx = await _service.ConfirmAdvanceAsync(ShipperId, orderId, 30000m);
        await _service.ConfirmAdvanceReceivedAsync(TenantId, advanceTx.Id);

        // Second confirmation should throw
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service.ConfirmAdvanceReceivedAsync(TenantId, advanceTx.Id));
    }

    // === T18: GetPendingAdvances_ReturnsUnsettledAdvances ===
    [Fact(DisplayName = "T18: GetPendingAdvances_ReturnsUnsettledAdvances")]
    public async Task GetPendingAdvances_ReturnsUnsettledAdvances()
    {
        await SeedTenantAsync();
        var orderId = await SeedOrderAsync();
        await SeedDeliveryTaskAsync(orderId);

        var advanceTx = await _service.ConfirmAdvanceAsync(ShipperId, orderId, 30000m);

        // Should show as pending
        var pending = await _service.GetPendingAdvancesAsync(TenantId);
        Assert.Single(pending);
        Assert.Equal(advanceTx.Id, pending[0].TransactionId);
        Assert.Equal(30000m, pending[0].Amount);
        Assert.Equal(ShipperId, pending[0].ShipperId);

        // After shop confirms, should be empty
        await _service.ConfirmAdvanceReceivedAsync(TenantId, advanceTx.Id);
        var pendingAfter = await _service.GetPendingAdvancesAsync(TenantId);
        Assert.Empty(pendingAfter);
    }

    // === T19: ReverseTransaction_CreatesReversalEntry ===
    [Fact(DisplayName = "T19: ReverseTransaction_CreatesReversalEntry")]
    public async Task ReverseTransaction_CreatesReversalEntry()
    {
        var original = await _service.CreateTransactionAsync(ShipperId, WalletTransactionType.CODCollection, 50000m, "Original COD");
        var reversal = await _service.ReverseTransactionAsync(ShipperId, original.Id);

        Assert.Equal(WalletTransactionType.Reversal, reversal.Type);
        Assert.Equal(-50000m, reversal.Amount);
        Assert.Equal(original.Id, reversal.RelatedTransactionId);
        Assert.Equal(0m, reversal.BalanceAfter); // 50k - 50k = 0
    }

    // ===== Settlement Batch-1 (TC-01 → TC-04) =====

    // === T20: ConfirmCod_TaskNotOutForDelivery_Throws (TC-01) ===
    [Fact(DisplayName = "T20: ConfirmCod_TaskNotOutForDelivery_Throws")]
    public async Task ConfirmCod_TaskNotOutForDelivery_Throws()
    {
        await SeedTenantAsync();
        var orderId = await SeedOrderAsync(codAmount: 50000m);
        await SeedDeliveryTaskAsync(orderId, DeliveryTaskStatus.Assigned); // not yet out for delivery

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service.ConfirmCodAsync(ShipperId, orderId, 50000m));

        // No partial wallet state
        Assert.Empty(await _context.WalletTransactions.IgnoreQueryFilters().Where(t => t.RelatedOrderId == orderId).ToListAsync());
    }

    // === T21: ConfirmCod_AlreadyPaid_Throws (TC-01 — double-count hole) ===
    [Fact(DisplayName = "T21: ConfirmCod_AlreadyPaid_Throws")]
    public async Task ConfirmCod_AlreadyPaid_Throws()
    {
        await SeedTenantAsync();
        var orderId = await SeedOrderAsync(codAmount: 50000m, paymentMethod: "VIETQR", paymentStatus: "Paid");
        await SeedDeliveryTaskAsync(orderId);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service.ConfirmCodAsync(ShipperId, orderId, 50000m));
    }

    // === T22: ConfirmCod_CancelledOrder_Throws (TC-01) ===
    [Fact(DisplayName = "T22: ConfirmCod_CancelledOrder_Throws")]
    public async Task ConfirmCod_CancelledOrder_Throws()
    {
        await SeedTenantAsync();
        var orderId = await SeedOrderAsync(codAmount: 50000m, status: "cancelled");
        await SeedDeliveryTaskAsync(orderId, DeliveryTaskStatus.Delivered);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service.ConfirmCodAsync(ShipperId, orderId, 50000m));
    }

    // === T23: ConfirmCod_ExpectedIsTotalAmount_WhenNoCodSnapshot (TC-01 — shipper cannot self-declare) ===
    [Fact(DisplayName = "T23: ConfirmCod_ExpectedIsTotalAmount_WhenNoCodSnapshot")]
    public async Task ConfirmCod_ExpectedIsTotalAmount_WhenNoCodSnapshot()
    {
        await SeedTenantAsync();
        var orderId = await SeedOrderAsync(); // CodAmount null → expected = TotalAmount = 100000
        await SeedDeliveryTaskAsync(orderId);

        // Shipper declares 50000 — must be rejected against authoritative TotalAmount
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service.ConfirmCodAsync(ShipperId, orderId, 50000m));

        // Correct amount works
        var tx = await _service.ConfirmCodAsync(ShipperId, orderId, 100000m);
        Assert.Equal(100000m, tx.Amount);
    }

    // === T24: ConfirmAdvance_Duplicate_Throws (TC-02 — no money minting) ===
    [Fact(DisplayName = "T24: ConfirmAdvance_Duplicate_Throws")]
    public async Task ConfirmAdvance_Duplicate_Throws()
    {
        await SeedTenantAsync();
        var orderId = await SeedOrderAsync();
        await SeedDeliveryTaskAsync(orderId);

        await _service.ConfirmAdvanceAsync(ShipperId, orderId, 30000m);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service.ConfirmAdvanceAsync(ShipperId, orderId, 30000m));

        var advances = await _context.WalletTransactions.IgnoreQueryFilters()
            .Where(t => t.RelatedOrderId == orderId && t.Type == WalletTransactionType.AdvancePayment)
            .ToListAsync();
        Assert.Single(advances);
    }

    // === T25: ConfirmAdvanceReceived_CrossTenant_Throws (TC-02) ===
    [Fact(DisplayName = "T25: ConfirmAdvanceReceived_CrossTenant_Throws")]
    public async Task ConfirmAdvanceReceived_CrossTenant_Throws()
    {
        await SeedTenantAsync();
        var orderId = await SeedOrderAsync();
        await SeedDeliveryTaskAsync(orderId);

        var advanceTx = await _service.ConfirmAdvanceAsync(ShipperId, orderId, 30000m);

        // A different tenant must not be able to confirm this shop's advance
        var otherTenantId = Guid.NewGuid();
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            _service.ConfirmAdvanceReceivedAsync(otherTenantId, advanceTx.Id));

        // The real tenant can still confirm afterwards
        var settlement = await _service.ConfirmAdvanceReceivedAsync(TenantId, advanceTx.Id);
        Assert.Equal(30000m, settlement.Amount);
    }

    // ===== Settlement Batch-2 (TC-06): COD collect → Paid + payment event + accounting =====

    // === T26: ConfirmCod_MarksOrderPaid ===
    [Fact(DisplayName = "T26: ConfirmCod_MarksOrderPaid_COD")]
    public async Task ConfirmCod_MarksOrderPaid()
    {
        await SeedTenantAsync();
        var orderId = await SeedOrderAsync(codAmount: 50000m);
        await SeedDeliveryTaskAsync(orderId);

        var tx = await _service.ConfirmCodAsync(ShipperId, orderId, 50000m);

        var order = await _context.Orders.IgnoreQueryFilters().FirstAsync(o => o.Id == orderId);
        Assert.Equal("Paid", order.PaymentStatus);
        Assert.Equal("COD", order.PaymentMethod);
        Assert.Equal(tx.Id.ToString(), order.VietQR_TransactionId); // ref = CODCollection wallet tx
    }

    // === T27: ConfirmCod_EnqueuesOrderPaymentConfirmed (atomic outbox → ShopERP replica) ===
    [Fact(DisplayName = "T27: ConfirmCod_EnqueuesOrderPaymentConfirmed")]
    public async Task ConfirmCod_EnqueuesOrderPaymentConfirmed()
    {
        var outbox = new Mock<IOutboxRepository>();
        var service = new WalletService(_context, _tenantProvider, NullLogger<WalletService>.Instance,
            outboxRepository: outbox.Object);

        await SeedTenantAsync();
        var orderId = await SeedOrderAsync(codAmount: 50000m);
        await SeedDeliveryTaskAsync(orderId);

        await service.ConfirmCodAsync(ShipperId, orderId, 50000m);

        outbox.Verify(o => o.EnqueueAsync(
            It.Is<OutboxEvent>(e => e.EventType == "OrderPaymentConfirmed" && e.CorrelationId == orderId),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    // === T28: ConfirmCod_GeneratesAccountingEntriesOnLocalBookset ===
    [Fact(DisplayName = "T28: ConfirmCod_GeneratesAccountingEntries")]
    public async Task ConfirmCod_GeneratesAccountingEntries()
    {
        var orderService = new Mock<IOrderService>();
        var service = new WalletService(_context, _tenantProvider, NullLogger<WalletService>.Instance,
            orderService: orderService.Object);

        await SeedTenantAsync();
        var orderId = await SeedOrderAsync(codAmount: 50000m);
        await SeedDeliveryTaskAsync(orderId);

        await service.ConfirmCodAsync(ShipperId, orderId, 50000m);

        orderService.Verify(o => o.GenerateAccountingEntriesAsync(
            It.Is<Order>(x => x.Id == orderId),
            It.Is<TenantId>(t => t.Value == TenantId)), Times.Once);
    }

    // === T29: ConfirmCod_AccountingSyncDisabled_SkipsEntries (toggle honored, Paid still set) ===
    [Fact(DisplayName = "T29: ConfirmCod_AccountingSyncDisabled_SkipsEntries")]
    public async Task ConfirmCod_AccountingSyncDisabled_SkipsEntries()
    {
        var orderService = new Mock<IOrderService>();
        var featureSettings = new Mock<IShopFeatureSettingsService>();
        featureSettings
            .Setup(f => f.IsEnabledAsync(TenantId, nameof(ShopFeatureSettingsDto.Accounting_Sync_Enabled), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        var service = new WalletService(_context, _tenantProvider, NullLogger<WalletService>.Instance,
            orderService.Object, shopFeatureSettingsService: featureSettings.Object);

        await SeedTenantAsync();
        var orderId = await SeedOrderAsync(codAmount: 50000m);
        await SeedDeliveryTaskAsync(orderId);

        await service.ConfirmCodAsync(ShipperId, orderId, 50000m);

        orderService.Verify(o => o.GenerateAccountingEntriesAsync(It.IsAny<Order>(), It.IsAny<TenantId>()), Times.Never);
        var order = await _context.Orders.IgnoreQueryFilters().FirstAsync(o => o.Id == orderId);
        Assert.Equal("Paid", order.PaymentStatus); // payment state unaffected by the toggle
    }

    // === T30: ConfirmCod_AccountingFailure_DoesNotFailConfirm (best-effort after commit) ===
    [Fact(DisplayName = "T30: ConfirmCod_AccountingFailure_DoesNotFailConfirm")]
    public async Task ConfirmCod_AccountingFailure_DoesNotFailConfirm()
    {
        var orderService = new Mock<IOrderService>();
        orderService
            .Setup(o => o.GenerateAccountingEntriesAsync(It.IsAny<Order>(), It.IsAny<TenantId>()))
            .ThrowsAsync(new Exception("accounting boom"));
        var service = new WalletService(_context, _tenantProvider, NullLogger<WalletService>.Instance,
            orderService.Object);

        await SeedTenantAsync();
        var orderId = await SeedOrderAsync(codAmount: 50000m);
        await SeedDeliveryTaskAsync(orderId);

        // Accounting failure must NOT fail the COD confirm — payment state is durable.
        var tx = await service.ConfirmCodAsync(ShipperId, orderId, 50000m);
        Assert.Equal(WalletTransactionType.CODCollection, tx.Type);

        var order = await _context.Orders.IgnoreQueryFilters().FirstAsync(o => o.Id == orderId);
        Assert.Equal("Paid", order.PaymentStatus);
    }

    // ============================================================
    // Settlement Batch-3 (TC-08): Shipper COD remittance — per-order
    // ============================================================

    // === T31: RemitCod_Marketplace_ClosesLedgerLoop ===
    [Fact(DisplayName = "T31: RemitCod_Marketplace_ClosesLedgerLoop")]
    public async Task RemitCod_Marketplace_ClosesLedgerLoop()
    {
        await SeedTenantAsync();
        var orderId = await SeedOrderAsync(codAmount: 65000m);
        await SeedDeliveryTaskAsync(orderId);
        await _service.ConfirmCodAsync(ShipperId, orderId, 65000m);

        var remitTx = await _service.RemitCodAsync(ShipperId, orderId);
        Assert.Equal(WalletTransactionType.Remittance, remitTx.Type);
        Assert.Equal(-65000m, remitTx.Amount);
        Assert.Equal(0m, remitTx.BalanceAfter); // shipper: +65k collect −65k remit = 0

        // Shop wallet (owner = TenantId): −65k Settlement at collect +65k Settlement at remit = 0
        var shopTxs = await _context.WalletTransactions.IgnoreQueryFilters()
            .Where(w => w.OwnerId == TenantId).OrderBy(w => w.CreatedAt).ToListAsync();
        Assert.Equal(2, shopTxs.Count);
        Assert.Equal(-65000m, shopTxs[0].Amount);
        Assert.Equal(65000m, shopTxs[1].Amount);
        Assert.Equal(0m, shopTxs[1].BalanceAfter);
        Assert.Equal(remitTx.Id, shopTxs[1].RelatedTransactionId);
    }

    // === T32: RemitCod_DoubleRemit_Throws ===
    [Fact(DisplayName = "T32: RemitCod_DoubleRemit_Throws")]
    public async Task RemitCod_DoubleRemit_Throws()
    {
        await SeedTenantAsync();
        var orderId = await SeedOrderAsync(codAmount: 65000m);
        await SeedDeliveryTaskAsync(orderId);
        await _service.ConfirmCodAsync(ShipperId, orderId, 65000m);
        await _service.RemitCodAsync(ShipperId, orderId);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _service.RemitCodAsync(ShipperId, orderId));
    }

    // === T33: RemitCod_BeforeCollection_Throws ===
    [Fact(DisplayName = "T33: RemitCod_BeforeCollection_Throws")]
    public async Task RemitCod_BeforeCollection_Throws()
    {
        await SeedTenantAsync();
        var orderId = await SeedOrderAsync(codAmount: 65000m);
        await SeedDeliveryTaskAsync(orderId);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _service.RemitCodAsync(ShipperId, orderId));
    }

    // === T34: RemitCod_NotShipper_Throws ===
    [Fact(DisplayName = "T34: RemitCod_NotShipper_Throws")]
    public async Task RemitCod_NotShipper_Throws()
    {
        await SeedTenantAsync();
        var orderId = await SeedOrderAsync(codAmount: 65000m);
        await SeedDeliveryTaskAsync(orderId);
        await _service.ConfirmCodAsync(ShipperId, orderId, 65000m);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => _service.RemitCodAsync(Guid.NewGuid(), orderId));
    }

    // === T35: GetPendingRemittances_ListsUntilRemitted ===
    [Fact(DisplayName = "T35: GetPendingRemittances_ListsUntilRemitted")]
    public async Task GetPendingRemittances_ListsUntilRemitted()
    {
        await SeedTenantAsync();
        var order1 = await SeedOrderAsync(codAmount: 65000m);
        var order2 = await SeedOrderAsync(codAmount: 50000m);
        await SeedDeliveryTaskAsync(order1);
        await SeedDeliveryTaskAsync(order2);
        await _service.ConfirmCodAsync(ShipperId, order1, 65000m);
        await _service.ConfirmCodAsync(ShipperId, order2, 50000m);

        var pending = await _service.GetPendingRemittancesAsync(ShipperId);
        Assert.Equal(2, pending.Count);

        await _service.RemitCodAsync(ShipperId, order1);
        pending = await _service.GetPendingRemittancesAsync(ShipperId);
        Assert.Single(pending);
        Assert.Equal(order2, pending[0].OrderId);

        // CodHeld flows into the wallet summary (TC-09 available balance)
        var wallet = await _service.GetWalletAsync(ShipperId);
        Assert.Equal(50000m, wallet.CodHeld);
        Assert.Equal(0m, wallet.AvailableBalance); // 50k balance − 50k held
    }

    // ============================================================
    // Settlement Batch-3 (TC-09): WithdrawalRequest lifecycle
    // ============================================================

    private static readonly Guid AdminId = Guid.NewGuid();

    // === T36: RequestWithdrawal_BelowMinimum_Throws ===
    [Fact(DisplayName = "T36: RequestWithdrawal_BelowMinimum_Throws")]
    public async Task RequestWithdrawal_BelowMinimum_Throws()
    {
        await Assert.ThrowsAsync<ArgumentException>(
            () => _service.RequestWithdrawalAsync(ShipperId, 400000m));
    }

    // === T37: RequestWithdrawal_InsufficientBalance_Throws ===
    [Fact(DisplayName = "T37: RequestWithdrawal_InsufficientBalance_Throws")]
    public async Task RequestWithdrawal_InsufficientBalance_Throws()
    {
        await _service.CreateTransactionAsync(ShipperId, WalletTransactionType.Commission, 600000m, "Commission");

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _service.RequestWithdrawalAsync(ShipperId, 700000m));
    }

    // === T38: RequestWithdrawal_HeldCodNotWithdrawable ===
    [Fact(DisplayName = "T38: RequestWithdrawal_HeldCodNotWithdrawable")]
    public async Task RequestWithdrawal_HeldCodNotWithdrawable()
    {
        await SeedTenantAsync();
        var orderId = await SeedOrderAsync(codAmount: 65000m);
        await SeedDeliveryTaskAsync(orderId);
        await _service.ConfirmCodAsync(ShipperId, orderId, 65000m);
        // Shipper also earned commission — total balance 665k but 65k is held COD.
        await _service.CreateTransactionAsync(ShipperId, WalletTransactionType.Commission, 600000m, "Commission");

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _service.RequestWithdrawalAsync(ShipperId, 650000m)); // > 600k available

        var request = await _service.RequestWithdrawalAsync(ShipperId, 600000m);
        Assert.Equal(VanAn.Shared.Domain.Aggregates.WalletAggregate.WithdrawalStatus.Pending, request.Status);
    }

    // === T39: RequestWithdrawal_OpenRequestBlocksNew ===
    [Fact(DisplayName = "T39: RequestWithdrawal_OpenRequestBlocksNew")]
    public async Task RequestWithdrawal_OpenRequestBlocksNew()
    {
        await _service.CreateTransactionAsync(ShipperId, WalletTransactionType.Commission, 2000000m, "Commission");
        await _service.RequestWithdrawalAsync(ShipperId, 500000m);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _service.RequestWithdrawalAsync(ShipperId, 500000m));
    }

    // === T40: Withdrawal_FullLifecycle_Paid ===
    [Fact(DisplayName = "T40: Withdrawal_FullLifecycle_Paid")]
    public async Task Withdrawal_FullLifecycle_Paid()
    {
        await _service.CreateTransactionAsync(ShipperId, WalletTransactionType.Commission, 600000m, "Commission");

        var request = await _service.RequestWithdrawalAsync(ShipperId, 500000m);
        await _service.ApproveWithdrawalAsync(request.Id, AdminId);

        var paid = await _service.MarkWithdrawalPaidAsync(request.Id, AdminId, "FT26001-VCB");
        Assert.Equal(VanAn.Shared.Domain.Aggregates.WalletAggregate.WithdrawalStatus.Paid, paid.Status);
        Assert.Equal("FT26001-VCB", paid.BankReference);
        Assert.NotNull(paid.WalletTransactionId);

        var walletTx = await _context.WalletTransactions.IgnoreQueryFilters()
            .FirstAsync(w => w.Id == paid.WalletTransactionId!.Value);
        Assert.Equal(WalletTransactionType.Withdrawal, walletTx.Type);
        Assert.Equal(-500000m, walletTx.Amount);
        Assert.Equal(100000m, walletTx.BalanceAfter);
    }

    // === T41: Withdrawal_DoublePay_Throws ===
    [Fact(DisplayName = "T41: Withdrawal_DoublePay_Throws")]
    public async Task Withdrawal_DoublePay_Throws()
    {
        await _service.CreateTransactionAsync(ShipperId, WalletTransactionType.Commission, 600000m, "Commission");
        var request = await _service.RequestWithdrawalAsync(ShipperId, 500000m);
        await _service.ApproveWithdrawalAsync(request.Id, AdminId);
        await _service.MarkWithdrawalPaidAsync(request.Id, AdminId, "FT26001-VCB");

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _service.MarkWithdrawalPaidAsync(request.Id, AdminId, "FT26002-VCB"));
    }

    // === T42: Withdrawal_Reject_NoTx ===
    [Fact(DisplayName = "T42: Withdrawal_Reject_NoTx")]
    public async Task Withdrawal_Reject_NoTx()
    {
        await _service.CreateTransactionAsync(ShipperId, WalletTransactionType.Commission, 600000m, "Commission");
        var request = await _service.RequestWithdrawalAsync(ShipperId, 500000m);

        await _service.RejectWithdrawalAsync(request.Id, AdminId, "KYC chưa hoàn tất");

        var reloaded = await _context.WithdrawalRequests.IgnoreQueryFilters().FirstAsync(r => r.Id == request.Id);
        Assert.Equal(VanAn.Shared.Domain.Aggregates.WalletAggregate.WithdrawalStatus.Rejected, reloaded.Status);
        Assert.Equal("KYC chưa hoàn tất", reloaded.RejectReason);

        var wallet = await _service.GetWalletAsync(ShipperId);
        Assert.Equal(600000m, wallet.Balance); // no debit
        Assert.DoesNotContain(wallet.Transactions, t => t.Type == nameof(WalletTransactionType.Withdrawal));
    }

    // === T43: Withdrawal_CancelOwn_Pending ===
    [Fact(DisplayName = "T43: Withdrawal_CancelOwn_Pending")]
    public async Task Withdrawal_CancelOwn_Pending()
    {
        await _service.CreateTransactionAsync(ShipperId, WalletTransactionType.Commission, 600000m, "Commission");
        var request = await _service.RequestWithdrawalAsync(ShipperId, 500000m);

        await _service.CancelWithdrawalAsync(ShipperId, request.Id);
        var reloaded = await _context.WithdrawalRequests.IgnoreQueryFilters().FirstAsync(r => r.Id == request.Id);
        Assert.Equal(VanAn.Shared.Domain.Aggregates.WalletAggregate.WithdrawalStatus.Cancelled, reloaded.Status);

        // After cancel the owner can request again
        var second = await _service.RequestWithdrawalAsync(ShipperId, 500000m);
        Assert.Equal(VanAn.Shared.Domain.Aggregates.WalletAggregate.WithdrawalStatus.Pending, second.Status);
    }

    // === T44: Withdrawal_PayWithoutApprove_Throws ===
    [Fact(DisplayName = "T44: Withdrawal_PayWithoutApprove_Throws")]
    public async Task Withdrawal_PayWithoutApprove_Throws()
    {
        await _service.CreateTransactionAsync(ShipperId, WalletTransactionType.Commission, 600000m, "Commission");
        var request = await _service.RequestWithdrawalAsync(ShipperId, 500000m);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _service.MarkWithdrawalPaidAsync(request.Id, AdminId, "FT26001-VCB"));
    }

    // === T45: Withdrawal_CancelNotOwner_Throws ===
    [Fact(DisplayName = "T45: Withdrawal_CancelNotOwner_Throws")]
    public async Task Withdrawal_CancelNotOwner_Throws()
    {
        await _service.CreateTransactionAsync(ShipperId, WalletTransactionType.Commission, 600000m, "Commission");
        var request = await _service.RequestWithdrawalAsync(ShipperId, 500000m);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => _service.CancelWithdrawalAsync(Guid.NewGuid(), request.Id));
    }

    // === T46: BalanceAfter chains across tenant contexts (RV finding — FromSqlRaw
    // composed inside the EF tenant filter: latest tx under another tenant was
    // filtered out after LIMIT 1, silently resetting balanceBefore to 0) ===
    [Fact(DisplayName = "T46: BalanceAfter_ChainsAcrossTenantContexts")]
    public async Task BalanceAfter_ChainsAcrossTenantContexts()
    {
        await _service.CreateTransactionAsync(ShipperId, WalletTransactionType.Commission, 100000m, "T1 commission");

        _tenantProvider.SetTenant(Guid.NewGuid());
        var tx2 = await _service.CreateTransactionAsync(ShipperId, WalletTransactionType.Withdrawal, -40000m, "payout");

        Assert.Equal(60000m, tx2.BalanceAfter);
        _tenantProvider.SetTenant(TenantId);
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
