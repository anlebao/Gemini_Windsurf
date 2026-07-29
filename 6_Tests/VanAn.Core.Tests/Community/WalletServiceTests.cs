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
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<VanAnDbContext>()
            .UseSqlite(_connection)
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

    private async Task<Guid> SeedOrderAsync(decimal? codAmount = null, string paymentMethod = "COD")
    {
        var orderId = Guid.NewGuid();
        var order = new Order(new TenantId(TenantId), null, 0);
        SetProp(order, "Id", orderId);
        SetProp(order, "OrderId", new OrderId(orderId));
        SetProp(order, "OrderType", "DELIVERY");
        SetProp(order, "Status", new OrderStatusId("delivering"));
        SetProp(order, "TotalAmount", 100000m);
        SetProp(order, "PaymentMethod", paymentMethod);
        SetProp(order, "ShipperId", ShipperId);
        if (codAmount.HasValue)
            SetProp(order, "CodAmount", codAmount.Value);
        _context.Orders.Add(order);
        await _context.SaveChangesAsync();
        return orderId;
    }

    private async Task SeedDeliveryTaskAsync(Guid orderId)
    {
        var task = new DeliveryTask(new TenantId(TenantId), orderId, ShipperId, 10.8, 106.7, 10.9, 106.8);
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

    private sealed class StubTenantProvider : ITenantProvider
    {
        public StubTenantProvider(Guid tenantId) => TenantId = tenantId;
        public Guid TenantId { get; }
        public string? CurrentUser => "test";
        public bool HasTenant => true;
        public void SetTenant(Guid tenantId) { }
    }
}
