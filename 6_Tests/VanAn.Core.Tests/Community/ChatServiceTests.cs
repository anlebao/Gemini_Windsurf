using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using VanAn.CoreHub.Infrastructure;
using VanAn.CoreHub.Services;
using VanAn.Shared.Domain;
using VanAn.Shared.Domain.Aggregates.TenantAggregate;
using Xunit;
using Tenant = VanAn.Shared.Domain.Aggregates.TenantAggregate.Tenant;

using Microsoft.Extensions.DependencyInjection;
namespace VanAn.Core.Tests.Community;

/// <summary>
/// CC-S3 (Sprint 3): ChatService unit tests — Conversation + Message persistence.
/// 8 test cases per detailed plan Section 4. Uses SQLite in-memory (kept open per test).
/// </summary>
public class ChatServiceTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly VanAnDbContext _context;
    private readonly ChatService _service;
    private static readonly Guid ShipperId = Guid.NewGuid();
    private static readonly Guid CustomerId = Guid.NewGuid();
    private static readonly Guid TenantId = Guid.Parse("00000000-0000-0000-0000-000000000001");

    public ChatServiceTests()
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
        _service = new ChatService(_context, NullLogger<ChatService>.Instance);
    }

    public void Dispose()
    {
        _context.Dispose();
        _connection.Dispose();
    }

    private static Order CreateDeliveryOrder(Guid id, Guid tenantId, string status, Guid customerId)
    {
        var order = new Order(new TenantId(tenantId), null, 0);
        SetProp(order, "Id", id);
        SetProp(order, "OrderId", new OrderId(id));
        SetProp(order, "OrderType", "DELIVERY");
        SetProp(order, "Status", new OrderStatusId(status));
        SetProp(order, "TotalAmount", 100000m);
        SetProp(order, "CustomerId", customerId);
        return order;
    }

    private static void SetProp<T>(T obj, string propName, object value)
    {
        typeof(T).GetProperty(propName)?.SetValue(obj, value);
    }

    private async Task<(Guid orderId, DeliveryTask task)> SeedOrderWithDeliveryTaskAsync()
    {
        // Seed Customer first (FK constraint: Order.CustomerId → Customers.Id)
        var customer = new Customer(new TenantId(TenantId), "Test Customer", "0901234567");
        SetProp(customer, "Id", CustomerId);
        _context.Customers.Add(customer);

        var orderId = Guid.NewGuid();
        var order = CreateDeliveryOrder(orderId, TenantId, "delivering", CustomerId);
        _context.Orders.Add(order);

        var task = new DeliveryTask(new TenantId(TenantId), orderId, ShipperId, 10.8, 106.7, 10.81, 106.71);
        _context.DeliveryTasks.Add(task);

        await _context.SaveChangesAsync();
        return (orderId, task);
    }

    /// <summary>
    /// Seed a DELIVERY order WITHOUT a DeliveryTask (simulates order before shipper accepts).
    /// </summary>
    private async Task<Guid> SeedOrderWithoutDeliveryTaskAsync()
    {
        var customer = new Customer(new TenantId(TenantId), "Test Customer", "0901234567");
        SetProp(customer, "Id", CustomerId);
        _context.Customers.Add(customer);

        var orderId = Guid.NewGuid();
        var order = CreateDeliveryOrder(orderId, TenantId, "confirmed", CustomerId);
        _context.Orders.Add(order);

        await _context.SaveChangesAsync();
        return orderId;
    }

    // === T1: GetOrCreateConversation_CreatesIfNotExists ===
    [Fact(DisplayName = "T1: GetOrCreateConversation_CreatesIfNotExists")]
    public async Task GetOrCreateConversation_CreatesIfNotExists()
    {
        var (orderId, task) = await SeedOrderWithDeliveryTaskAsync();

        var conversation = await _service.GetOrCreateConversationAsync(orderId);

        Assert.NotNull(conversation);
        Assert.Equal(orderId, conversation!.OrderId);
        Assert.Equal(ShipperId, conversation.ShipperId);
        Assert.Equal(CustomerId, conversation.CustomerId);
    }

    // === T2: GetOrCreateConversation_ReturnsExisting ===
    [Fact(DisplayName = "T2: GetOrCreateConversation_ReturnsExisting")]
    public async Task GetOrCreateConversation_ReturnsExisting()
    {
        var (orderId, task) = await SeedOrderWithDeliveryTaskAsync();

        var conv1 = await _service.GetOrCreateConversationAsync(orderId);
        var conv2 = await _service.GetOrCreateConversationAsync(orderId);

        Assert.NotNull(conv1);
        Assert.NotNull(conv2);
        Assert.Equal(conv1!.Id, conv2!.Id);
    }

    // === T3: SendMessage_CreatesMessage ===
    [Fact(DisplayName = "T3: SendMessage_CreatesMessage")]
    public async Task SendMessage_CreatesMessage()
    {
        var (orderId, task) = await SeedOrderWithDeliveryTaskAsync();

        var message = await _service.SendMessageAsync(orderId, ShipperId, "Hello customer!");

        Assert.NotNull(message);
        Assert.Equal("Hello customer!", message!.Content);
        Assert.Equal(ShipperId, message.SenderId);
        Assert.False(message.IsRead);
    }

    // === T4: SendMessage_NoDeliveryTask_ReturnsNull ===
    [Fact(DisplayName = "T4: SendMessage_NoDeliveryTask_ReturnsNull")]
    public async Task SendMessage_NoDeliveryTask_ReturnsNull()
    {
        var fakeOrderId = Guid.NewGuid();

        var message = await _service.SendMessageAsync(fakeOrderId, ShipperId, "Hello");

        Assert.Null(message);
    }

    // === T5: SendMessage_InvalidSender_Throws ===
    [Fact(DisplayName = "T5: SendMessage_InvalidSender_Throws")]
    public async Task SendMessage_InvalidSender_Throws()
    {
        var (orderId, task) = await SeedOrderWithDeliveryTaskAsync();
        var randomUser = Guid.NewGuid();

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            _service.SendMessageAsync(orderId, randomUser, "Hello"));
    }

    // === T6: GetHistory_ReturnsChronological ===
    [Fact(DisplayName = "T6: GetHistory_ReturnsChronological")]
    public async Task GetHistory_ReturnsChronological()
    {
        var (orderId, task) = await SeedOrderWithDeliveryTaskAsync();

        await _service.SendMessageAsync(orderId, ShipperId, "Message 1");
        await Task.Delay(50);
        await _service.SendMessageAsync(orderId, CustomerId, "Message 2");
        await Task.Delay(50);
        await _service.SendMessageAsync(orderId, ShipperId, "Message 3");

        var history = await _service.GetHistoryAsync(orderId);

        Assert.Equal(3, history.Count);
        Assert.True(history[0].SentAt <= history[1].SentAt);
        Assert.True(history[1].SentAt <= history[2].SentAt);
        Assert.Equal("Message 1", history[0].Content);
        Assert.Equal("Message 3", history[2].Content);
    }

    // === T7: MarkAsRead_UpdatesIsRead ===
    [Fact(DisplayName = "T7: MarkAsRead_UpdatesIsRead")]
    public async Task MarkAsRead_UpdatesIsRead()
    {
        var (orderId, task) = await SeedOrderWithDeliveryTaskAsync();
        var message = await _service.SendMessageAsync(orderId, ShipperId, "Read me");

        Assert.NotNull(message);
        Assert.False(message!.IsRead);

        await _service.MarkAsReadAsync(message.Id);

        var updated = await _context.Messages.IgnoreQueryFilters().FirstOrDefaultAsync(m => m.Id == message.Id);
        Assert.NotNull(updated);
        Assert.True(updated!.IsRead);
    }

    // === T8: HasActiveDeliveryTask_NoTask_ReturnsFalse ===
    [Fact(DisplayName = "T8: HasActiveDeliveryTask_NoTask_ReturnsFalse")]
    public async Task HasActiveDeliveryTask_NoTask_ReturnsFalse()
    {
        var fakeOrderId = Guid.NewGuid();

        var result = await _service.HasActiveDeliveryTaskAsync(fakeOrderId);

        Assert.False(result);
    }

    // === T9: GetOrCreateConversation_BeforeShipperAccept_CreatesWithPlaceholderShipper ===
    [Fact(DisplayName = "T9: GetOrCreateConversation_BeforeShipperAccept_CreatesWithPlaceholderShipper")]
    public async Task GetOrCreateConversation_BeforeShipperAccept_CreatesWithPlaceholderShipper()
    {
        // Order exists but no DeliveryTask yet (shipper has not accepted).
        var orderId = await SeedOrderWithoutDeliveryTaskAsync();

        var conversation = await _service.GetOrCreateConversationAsync(orderId);

        Assert.NotNull(conversation);
        Assert.Equal(orderId, conversation!.OrderId);
        Assert.Equal(Guid.Empty, conversation.ShipperId); // placeholder — shipper not assigned yet
        Assert.Equal(CustomerId, conversation.CustomerId);
    }

    // === T10: SendMessage_CustomerBeforeShipperAccept_Succeeds ===
    [Fact(DisplayName = "T10: SendMessage_CustomerBeforeShipperAccept_Succeeds")]
    public async Task SendMessage_CustomerBeforeShipperAccept_Succeeds()
    {
        // Customer can chat before shipper accepts (no DeliveryTask).
        var orderId = await SeedOrderWithoutDeliveryTaskAsync();

        var message = await _service.SendMessageAsync(orderId, CustomerId, "When will my order arrive?");

        Assert.NotNull(message);
        Assert.Equal("When will my order arrive?", message!.Content);
        Assert.Equal(CustomerId, message.SenderId);
    }

    // === T11: SendMessage_ShipperBeforeAccept_Rejected ===
    [Fact(DisplayName = "T11: SendMessage_ShipperBeforeAccept_Rejected")]
    public async Task SendMessage_ShipperBeforeAccept_Rejected()
    {
        // Shipper cannot chat before accepting (ShipperId=Guid.Empty placeholder).
        var orderId = await SeedOrderWithoutDeliveryTaskAsync();

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            _service.SendMessageAsync(orderId, ShipperId, "Hello"));
    }

    // === T12: GetHistory_BeforeShipperAccept_ReturnsMessages ===
    [Fact(DisplayName = "T12: GetHistory_BeforeShipperAccept_ReturnsMessages")]
    public async Task GetHistory_BeforeShipperAccept_ReturnsMessages()
    {
        var orderId = await SeedOrderWithoutDeliveryTaskAsync();

        await _service.SendMessageAsync(orderId, CustomerId, "Pre-accept message");
        var history = await _service.GetHistoryAsync(orderId);

        Assert.Single(history);
        Assert.Equal("Pre-accept message", history[0].Content);
    }

    // === T13: GetOrCreateConversation_NonDeliveryOrder_ReturnsNull ===
    [Fact(DisplayName = "T13: GetOrCreateConversation_NonDeliveryOrder_ReturnsNull")]
    public async Task GetOrCreateConversation_NonDeliveryOrder_ReturnsNull()
    {
        // Seed a DINEIN order (not DELIVERY) — chat should not be available.
        var customer = new Customer(new TenantId(TenantId), "Test Customer", "0901234567");
        SetProp(customer, "Id", CustomerId);
        _context.Customers.Add(customer);

        var orderId = Guid.NewGuid();
        var order = CreateDeliveryOrder(orderId, TenantId, "confirmed", CustomerId);
        SetProp(order, "OrderType", "DINEIN"); // override to non-DELIVERY
        _context.Orders.Add(order);
        await _context.SaveChangesAsync();

        var conversation = await _service.GetOrCreateConversationAsync(orderId);

        Assert.Null(conversation);
    }

    // === T14: Conversation_AssignShipper_UpdatesPlaceholderShipperId ===
    [Fact(DisplayName = "T14: Conversation_AssignShipper_UpdatesPlaceholderShipperId")]
    public async Task Conversation_AssignShipper_UpdatesPlaceholderShipperId()
    {
        // Simulate: customer chats before shipper accepts, then shipper accepts.
        var orderId = await SeedOrderWithoutDeliveryTaskAsync();

        // Customer sends message → conversation created with placeholder ShipperId=Guid.Empty
        await _service.SendMessageAsync(orderId, CustomerId, "Hello");
        var conversation = await _context.Conversations.IgnoreQueryFilters()
            .FirstOrDefaultAsync(c => c.OrderId == orderId);
        Assert.NotNull(conversation);
        Assert.Equal(Guid.Empty, conversation!.ShipperId);

        // Shipper accepts → update ShipperId
        conversation.AssignShipper(ShipperId);
        await _context.SaveChangesAsync();

        // Reload and verify
        var reloaded = await _context.Conversations.IgnoreQueryFilters()
            .FirstOrDefaultAsync(c => c.OrderId == orderId);
        Assert.NotNull(reloaded);
        Assert.Equal(ShipperId, reloaded!.ShipperId);

        // Now shipper can send messages
        var shipperMessage = await _service.SendMessageAsync(orderId, ShipperId, "On my way!");
        Assert.NotNull(shipperMessage);
        Assert.Equal(ShipperId, shipperMessage!.SenderId);
    }

    // === T15: Conversation_AssignShipper_AlreadyAssigned_Throws ===
    [Fact(DisplayName = "T15: Conversation_AssignShipper_AlreadyAssigned_Throws")]
    public async Task Conversation_AssignShipper_AlreadyAssigned_Throws()
    {
        var (orderId, task) = await SeedOrderWithDeliveryTaskAsync();
        var conversation = await _service.GetOrCreateConversationAsync(orderId);
        Assert.NotNull(conversation);
        Assert.Equal(ShipperId, conversation!.ShipperId); // already assigned

        // Try to assign to a different shipper → should throw
        var differentShipper = Guid.NewGuid();
        Assert.Throws<InvalidOperationException>(() => conversation.AssignShipper(differentShipper));
    }
}
