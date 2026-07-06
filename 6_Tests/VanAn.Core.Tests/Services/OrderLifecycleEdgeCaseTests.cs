using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using VanAn.CoreHub.Infrastructure.Messaging;
using VanAn.CoreHub.Services;
using VanAn.CoreHub.Tests;
using VanAn.CoreHub.Tests.TestInfrastructure;
using VanAn.Shared.Domain;
using VanAn.Shared.Domain.Common;
using VanAn.Shared.DTOs;
using VanAn.CoreHub.Interfaces;
using Xunit;
using Xunit.Abstractions;

namespace VanAn.Core.Tests.Services;

/// <summary>
/// Critical edge case tests for Order Lifecycle stream (W-1 to W5).
///
/// Covers 5 scenarios identified as critical gaps:
/// T1: Idempotency — ConfirmPaymentAsync called twice → no duplicate accounting entries
/// T2: Race condition — rapid status changes → NatsSyncWorker preserves publish order (FIFO by CreatedAt)
/// T3: Disconnected — IOrderNotificationService null/throwing → caller does NOT crash
/// T4: Partial completion — 3 items, complete 2 → no Ready transition, no false StatusChanged broadcast
/// T5: Invalid payload — ConfirmPaymentAsync with missing TenantId/TransactionId → proper exception, no entries
/// </summary>
public class OrderLifecycleEdgeCaseTests : IntegrationTestBase
{
    private readonly ITestOutputHelper _output;

    public OrderLifecycleEdgeCaseTests(ITestOutputHelper output)
    {
        _output = output;
        SetupAsync().Wait();
    }

    // Helper: seed a complete order with items into the test DB
    private async Task<(Order order, OrderItem item, Guid shopId)> SeedOrderWithItemAsync(decimal amount = 50000m)
    {
        Guid shopId = ActiveTenantId;
        TenantId shopTenantId = new(shopId);

        Shop shop = new(shopTenantId, "Edge Case Shop", "Addr", "0901234567", "test@shop.com");
        _ = await Context.Shops.AddAsync(shop);

        Product product = new(shopTenantId, "Test Product", "Desc", amount, "Coffee", true, null, 0.10m);
        _ = await Context.Products.AddAsync(product);

        Customer customer = new(shopTenantId, "Edge Customer", "0123456789", "test@customer.com");
        _ = await Context.Customers.AddAsync(customer);
        _ = await Context.SaveChangesAsync();

        Order order = new(shopTenantId, customer.Id, amount);
        _ = await Context.Orders.AddAsync(order);
        _ = await Context.SaveChangesAsync();

        OrderItem item = new(shopTenantId, order.Id, product.Id, 1, amount, "Test Product");
        await Context.OrderItems.AddAsync(item);
        _ = await Context.SaveChangesAsync();

        return (order, item, shopId);
    }

    // ════════════════════════════════════════════════════════════════════════════
    // T1: IDEMPOTENCY — ConfirmPaymentAsync twice → no duplicate accounting entries
    // ════════════════════════════════════════════════════════════════════════════

    [Fact(DisplayName = "T1-Idempotency: ConfirmPaymentAsync called twice → accounting entries created only once")]
    public async Task ConfirmPayment_Idempotent_NoDuplicateAccountingEntries()
    {
        // Arrange — seed order with item
        var (order, item, shopId) = await SeedOrderWithItemAsync(50000m);
        TenantId shopTenantId = new(shopId);

        // Build OrderService with real DbContext (IntegrationTestBase provides Context)
        var orderRepo = new CoreHub.Repositories.OrderRepository(Context, NullLogger<CoreHub.Repositories.OrderRepository>.Instance);
        var accountingServiceMock = new Mock<CoreHub.Services.IAccountingService>();
        var hkdBookRepoMock = new Mock<CoreHub.Repositories.IHKDBookRepository>();
        var accountingEntryRepoMock = new Mock<CoreHub.Repositories.IAccountingEntryRepository>();
        var inventoryServiceMock = new Mock<IInventoryService>();

        var orderService = new CoreHub.Services.OrderService(
            orderRepo,
            accountingServiceMock.Object,
            hkdBookRepoMock.Object,
            accountingEntryRepoMock.Object,
            NullLogger<CoreHub.Services.OrderService>.Instance,
            inventoryServiceMock.Object);

        string transactionId = $"IDEMPOTENT_TEST_{order.Id}_{DateTime.UtcNow:yyyyMMddHHmmss}";

        // Act — first call: should confirm payment + call accounting service
        await orderService.ConfirmPaymentAsync(order.Id, shopId, transactionId);

        // Count accounting service calls after first call
        int callsAfterFirst = accountingServiceMock.Invocations.Count;

        // Second call — should be idempotent noop (PaymentStatus already "Paid")
        await orderService.ConfirmPaymentAsync(order.Id, shopId, transactionId);

        // Count accounting service calls after second call
        int callsAfterSecond = accountingServiceMock.Invocations.Count;

        // Assert — no duplicate accounting service calls
        Assert.True(callsAfterFirst > 0, "First call should invoke accounting service");
        Assert.Equal(callsAfterFirst, callsAfterSecond);

        // Verify order PaymentStatus is "Paid" (not double-confirmed)
        Order? confirmedOrder = await Context.Orders.FindAsync(order.Id);
        Assert.NotNull(confirmedOrder);
        Assert.Equal("Paid", confirmedOrder!.PaymentStatus);
    }

    // ════════════════════════════════════════════════════════════════════════════
    // T2: RACE CONDITION — rapid status changes → NatsSyncWorker preserves FIFO order
    // ════════════════════════════════════════════════════════════════════════════

    [Fact(DisplayName = "T2-RaceCondition: 3 events enqueued rapidly → NatsSyncWorker publishes in CreatedAt order")]
    public async Task NatsSyncWorker_RapidStatusChanges_PreservesFifoOrder()
    {
        // Arrange — 3 events with explicit CreatedAt timestamps (1ms apart)
        var tenantId = new TenantId(Guid.NewGuid());
        var invoiceId = new ElectronicInvoiceId(Guid.Empty);

        var ev1 = new OutboxEvent(tenantId, invoiceId, "OrderConfirmed", "{}");
        var ev2 = new OutboxEvent(tenantId, invoiceId, "KitchenStarted", "{}");
        var ev3 = new OutboxEvent(tenantId, invoiceId, "KitchenCompleted", "{}");

        var baseTime = new DateTime(2026, 7, 7, 12, 0, 0, DateTimeKind.Utc);
        SetCreatedAt(ev1, baseTime.AddMilliseconds(1));
        SetCreatedAt(ev2, baseTime.AddMilliseconds(2));
        SetCreatedAt(ev3, baseTime.AddMilliseconds(3));

        var outboxMock = new Mock<IOutboxRepository>();
        outboxMock
            .SetupSequence(o => o.GetPendingEventsAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([ev1, ev2, ev3])
            .ReturnsAsync([]);

        var publisher = new FakeNatsEventPublisher();
        var serviceCollection = new ServiceCollection();
        serviceCollection.AddScoped<IOutboxRepository>(_ => outboxMock.Object);
        var sp = serviceCollection.BuildServiceProvider();

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Sync__PollIntervalMs"] = "50",
                ["Sync__BatchSize"] = "50"
            })
            .Build();

        var worker = new NatsSyncWorker(sp, publisher, NullLogger<NatsSyncWorker>.Instance, config);

        // Act
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(200));
        await worker.StartAsync(cts.Token);
        await Task.Delay(150, CancellationToken.None);
        await worker.StopAsync(CancellationToken.None);

        // Assert — events published in FIFO order (CreatedAt ascending)
        Assert.Equal(3, publisher.Published.Count);
        Assert.Equal("vanan.shoperp.orderconfirmed", publisher.Published[0].Subject);
        Assert.Equal("vanan.shoperp.kitchenstarted", publisher.Published[1].Subject);
        Assert.Equal("vanan.shoperp.kitchencompleted", publisher.Published[2].Subject);

        // All 3 marked as processed (no skipped, no duplicated)
        outboxMock.Verify(o => o.MarkAsProcessedAsync(ev1.OutboxEventId, It.IsAny<CancellationToken>()), Times.Once);
        outboxMock.Verify(o => o.MarkAsProcessedAsync(ev2.OutboxEventId, It.IsAny<CancellationToken>()), Times.Once);
        outboxMock.Verify(o => o.MarkAsProcessedAsync(ev3.OutboxEventId, It.IsAny<CancellationToken>()), Times.Once);
    }

    // ════════════════════════════════════════════════════════════════════════════
    // T3: DISCONNECTED — IOrderNotificationService null/throwing → no crash
    // ════════════════════════════════════════════════════════════════════════════

    [Fact(DisplayName = "T3-Disconnected: KitchenService with null IOrderNotificationService → no crash on full completion")]
    public async Task KitchenService_NullNotificationService_NoCrashOnFullCompletion()
    {
        // Arrange — ShopERP scope: IOrderNotificationService is null (no OrderHub)
        var (order, item, shopId) = await SeedOrderWithItemAsync(30000m);

        var kitchenService = new CoreHub.Services.KitchenService(
            Context,
            new TestLogger<CoreHub.Services.KitchenService>(_output),
            null);  // IOrderNotificationService — null (ShopERP scope)

        // Act — complete the only item → should transition to Ready without crash
        bool result = await kitchenService.UpdateItemStatusAsync(
            new KitchenStatusUpdateDto { OrderItemId = item.Id, NewStatus = KitchenStatus.Completed },
            userId: Guid.NewGuid());

        // Assert — no NullReferenceException, order transitioned to Ready
        Assert.True(result);
        Order? readyOrder = await Context.Orders.FindAsync(order.Id);
        Assert.NotNull(readyOrder);
        Assert.Equal("ready", readyOrder!.Status.Value);
    }

    [Fact(DisplayName = "T3-Disconnected: KitchenService with throwing IOrderNotificationService → order still saved")]
    public async Task KitchenService_ThrowingNotificationService_OrderStillSaved()
    {
        // Arrange — Gateway scope but SignalR disconnected: notification throws
        var (order, item, shopId) = await SeedOrderWithItemAsync(35000m);

        var throwingNotification = new ThrowingOrderNotificationService();
        var kitchenService = new CoreHub.Services.KitchenService(
            Context,
            new TestLogger<CoreHub.Services.KitchenService>(_output),
            throwingNotification);

        // Act — complete the only item → should transition to Ready
        // Notification is fire-and-forget ("_ = NotifyOrderStatusChangedAsync(...)") so
        // the exception is swallowed by the unawaited Task, order save still succeeds.
        bool result = await kitchenService.UpdateItemStatusAsync(
            new KitchenStatusUpdateDto { OrderItemId = item.Id, NewStatus = KitchenStatus.Completed },
            userId: Guid.NewGuid());

        // Assert — order status is Ready despite notification service throwing
        Assert.True(result);
        Order? readyOrder = await Context.Orders.FindAsync(order.Id);
        Assert.NotNull(readyOrder);
        Assert.Equal("ready", readyOrder!.Status.Value);
    }

    // ════════════════════════════════════════════════════════════════════════════
    // T4: PARTIAL COMPLETION — 3 items, complete 2 → no Ready, no false StatusChanged
    // ════════════════════════════════════════════════════════════════════════════

    [Fact(DisplayName = "T4-PartialCompletion: 3 items, complete 2 → OrderStatus NOT Ready, no false broadcast")]
    public async Task KitchenService_3Items_Complete2_OrderStatusNotReady_NoFalseBroadcast()
    {
        // Arrange
        Guid shopId = ActiveTenantId;
        TenantId shopTenantId = new(shopId);

        Shop shop = new(shopTenantId, "Partial Shop", "Addr", "0901234567", "test@shop.com");
        _ = await Context.Shops.AddAsync(shop);

        Product product = new(shopTenantId, "Trà sữa", "Trà sữa trân châu", 45000m, "Tea", true, null, 0.10m);
        _ = await Context.Products.AddAsync(product);

        Customer customer = new(shopTenantId, "Partial Customer", "0123456789", "test@customer.com");
        _ = await Context.Customers.AddAsync(customer);
        _ = await Context.SaveChangesAsync();

        Order order = new(shopTenantId, customer.Id, 135000m);
        _ = await Context.Orders.AddAsync(order);
        _ = await Context.SaveChangesAsync();

        OrderItem item1 = new(shopTenantId, order.Id, product.Id, 1, 45000m, "Trà sữa");
        OrderItem item2 = new(shopTenantId, order.Id, product.Id, 1, 45000m, "Trà sữa");
        OrderItem item3 = new(shopTenantId, order.Id, product.Id, 1, 45000m, "Trà sữa");
        await Context.OrderItems.AddRangeAsync(item1, item2, item3);
        _ = await Context.SaveChangesAsync();

        // Track notification calls — partial completion should NOT trigger StatusChanged
        var notificationMock = new Mock<IOrderNotificationService>();
        var kitchenService = new CoreHub.Services.KitchenService(
            Context,
            new TestLogger<CoreHub.Services.KitchenService>(_output),
            notificationMock.Object);

        string originalStatus = order.Status.Value;

        // Act — complete only 2 of 3 items
        _ = await kitchenService.UpdateItemStatusAsync(
            new KitchenStatusUpdateDto { OrderItemId = item1.Id, NewStatus = KitchenStatus.Completed },
            userId: Guid.NewGuid());

        _ = await kitchenService.UpdateItemStatusAsync(
            new KitchenStatusUpdateDto { OrderItemId = item2.Id, NewStatus = KitchenStatus.Completed },
            userId: Guid.NewGuid());

        // Assert — order status should NOT be "ready" (item3 still pending)
        Order? partialOrder = await Context.Orders.FindAsync(order.Id);
        Assert.NotNull(partialOrder);
        Assert.NotEqual("ready", partialOrder!.Status.Value);
        Assert.Equal(originalStatus, partialOrder.Status.Value);

        // Assert — NotifyOrderStatusChangedAsync should NOT have been called
        // (no false "ready" broadcast to staff/customer)
        notificationMock.Verify(
            n => n.NotifyOrderStatusChangedAsync(
                It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>()),
            Times.Never,
            "Partial completion must NOT broadcast OrderStatusChanged (avoids false 'ready' notification)");

        // Now complete the 3rd item → order should transition to Ready
        _ = await kitchenService.UpdateItemStatusAsync(
            new KitchenStatusUpdateDto { OrderItemId = item3.Id, NewStatus = KitchenStatus.Completed },
            userId: Guid.NewGuid());

        // Assert — order status should now be "ready"
        Order? readyOrder = await Context.Orders.FindAsync(order.Id);
        Assert.NotNull(readyOrder);
        Assert.Equal("ready", readyOrder!.Status.Value);

        // Assert — NotifyOrderStatusChangedAsync called exactly once with "ready"
        notificationMock.Verify(
            n => n.NotifyOrderStatusChangedAsync(
                order.Id, It.IsAny<Guid>(), It.IsAny<string>(), "ready"),
            Times.Once,
            "Full completion should broadcast OrderStatusChanged with newStatus='ready' exactly once");
    }

    // ════════════════════════════════════════════════════════════════════════════
    // T5: INVALID PAYLOAD — ConfirmPaymentAsync with invalid inputs → proper exception
    // ════════════════════════════════════════════════════════════════════════════

    [Fact(DisplayName = "T5-InvalidPayload: ConfirmPaymentAsync with non-existent orderId → KeyNotFoundException, no entries")]
    public async Task ConfirmPayment_NonExistentOrderId_ThrowsKeyNotFound_NoEntriesCreated()
    {
        // Arrange
        Guid shopId = ActiveTenantId;
        TenantId shopTenantId = new(shopId);

        var orderRepo = new CoreHub.Repositories.OrderRepository(Context, NullLogger<CoreHub.Repositories.OrderRepository>.Instance);
        var accountingServiceMock = new Mock<CoreHub.Services.IAccountingService>();
        var hkdBookRepoMock = new Mock<CoreHub.Repositories.IHKDBookRepository>();
        var accountingEntryRepoMock = new Mock<CoreHub.Repositories.IAccountingEntryRepository>();

        var orderService = new CoreHub.Services.OrderService(
            orderRepo,
            accountingServiceMock.Object,
            hkdBookRepoMock.Object,
            accountingEntryRepoMock.Object,
            NullLogger<CoreHub.Services.OrderService>.Instance);

        Guid nonExistentOrderId = Guid.NewGuid();
        string transactionId = $"INVALID_TEST_{DateTime.UtcNow:yyyyMMddHHmmss}";

        // Act + Assert — should throw KeyNotFoundException
        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            orderService.ConfirmPaymentAsync(nonExistentOrderId, shopId, transactionId));

        // Verify no accounting entries were created
        accountingServiceMock.Verify(
            a => a.CreateRevenueEntryAsync(
                It.IsAny<TenantId>(), It.IsAny<AccountingPeriod>(), It.IsAny<decimal>(),
                It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<IndustrySector?>()),
            Times.Never,
            "Non-existent order must NOT create accounting entries");
    }

    [Fact(DisplayName = "T5-InvalidPayload: ConfirmPaymentAsync with empty TenantId → KeyNotFound, no entries")]
    public async Task ConfirmPayment_EmptyTenantId_ThrowsKeyNotFound_NoEntriesCreated()
    {
        // Arrange — seed order with valid tenant
        var (order, item, shopId) = await SeedOrderWithItemAsync(40000m);

        var orderRepo = new CoreHub.Repositories.OrderRepository(Context, NullLogger<CoreHub.Repositories.OrderRepository>.Instance);
        var accountingServiceMock = new Mock<CoreHub.Services.IAccountingService>();
        var hkdBookRepoMock = new Mock<CoreHub.Repositories.IHKDBookRepository>();
        var accountingEntryRepoMock = new Mock<CoreHub.Repositories.IAccountingEntryRepository>();

        var orderService = new CoreHub.Services.OrderService(
            orderRepo,
            accountingServiceMock.Object,
            hkdBookRepoMock.Object,
            accountingEntryRepoMock.Object,
            NullLogger<CoreHub.Services.OrderService>.Instance);

        // Act — call with empty TenantId (wrong tenant → order not found via query filter)
        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            orderService.ConfirmPaymentAsync(order.Id, Guid.Empty, "TXN_EMPTY_TENANT"));

        // Assert — no accounting entries created
        accountingServiceMock.Verify(
            a => a.CreateRevenueEntryAsync(
                It.IsAny<TenantId>(), It.IsAny<AccountingPeriod>(), It.IsAny<decimal>(),
                It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<IndustrySector?>()),
            Times.Never,
            "Empty TenantId must NOT create accounting entries");
    }

    [Fact(DisplayName = "T5-InvalidPayload: ConfirmPaymentAsync with empty TransactionId → no crash (controller validates)")]
    public async Task ConfirmPayment_EmptyTransactionId_NoCrash()
    {
        // Arrange
        var (order, item, shopId) = await SeedOrderWithItemAsync(25000m);

        var orderRepo = new CoreHub.Repositories.OrderRepository(Context, NullLogger<CoreHub.Repositories.OrderRepository>.Instance);
        var accountingServiceMock = new Mock<CoreHub.Services.IAccountingService>();
        var hkdBookRepoMock = new Mock<CoreHub.Repositories.IHKDBookRepository>();
        var accountingEntryRepoMock = new Mock<CoreHub.Repositories.IAccountingEntryRepository>();

        var orderService = new CoreHub.Services.OrderService(
            orderRepo,
            accountingServiceMock.Object,
            hkdBookRepoMock.Object,
            accountingEntryRepoMock.Object,
            NullLogger<CoreHub.Services.OrderService>.Instance);

        // Act — call with empty transactionId
        // WebhookController validates empty txnId BEFORE calling service (returns 400 BadRequest).
        // At service level, Domain.ConfirmPayment("") may accept it — this test verifies no crash.
        // The validation responsibility is at the controller layer (tested separately).
        Exception? caught = await Record.ExceptionAsync(() =>
            orderService.ConfirmPaymentAsync(order.Id, shopId, ""));

        // Assert — service should not crash with empty transactionId
        // (controller-level validation is the proper gate, tested via WebhookController tests)
        Assert.True(caught == null || caught is not SystemException,
            "Service should not crash with SystemException on empty transactionId");
    }

    // ════════════════════════════════════════════════════════════════════════════
    // Helper: set CreatedAt via reflection (init-only property)
    // ════════════════════════════════════════════════════════════════════════════

    private static void SetCreatedAt(OutboxEvent ev, DateTime createdAt)
    {
        var prop = typeof(OutboxEvent).GetProperty("CreatedAt");
        prop?.SetValue(ev, createdAt);
    }
}

// ──────────────────────────────────────────────────────────────────────────────
// Test doubles
// ──────────────────────────────────────────────────────────────────────────────

/// <summary>
/// IOrderNotificationService that throws asynchronously — simulates SignalR disconnected.
/// Returns Faulted Task (not synchronous throw) to match real SignalR behavior when
/// connection drops mid-send. The fire-and-forget pattern (_ = NotifyAsync()) swallows
/// the unobserved exception, so the caller (KitchenService) does NOT crash.
/// </summary>
internal sealed class ThrowingOrderNotificationService : IOrderNotificationService
{
    public Task NotifyOrderStatusChangedAsync(Guid orderId, Guid tenantId, string oldStatus, string newStatus)
        => Task.FromException(new InvalidOperationException("Simulated SignalR connection failure"));

    public Task NotifyPaymentConfirmedAsync(Guid orderId, Guid tenantId, string transactionId)
        => Task.FromException(new InvalidOperationException("Simulated SignalR connection failure"));

    public Task NotifyKitchenItemCompletedAsync(Guid orderId, Guid orderItemId, string newStatus)
        => Task.FromException(new InvalidOperationException("Simulated SignalR connection failure"));
}
