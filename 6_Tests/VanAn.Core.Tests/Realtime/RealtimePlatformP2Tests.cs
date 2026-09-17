using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using VanAn.CoreHub.Infrastructure;
using VanAn.CoreHub.Services;
using VanAn.CoreHub.Services.Adapters;
using VanAn.Shared.Domain;
using Xunit;

namespace VanAn.Core.Tests.Realtime;

/// <summary>
/// Realtime Platform P2 (2026-09-17): generic messaging + live location + order authorizer.
/// SQLite in-memory, kept open per test (same harness as ChatServiceTests).
/// </summary>
public class RealtimePlatformP2Tests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly VanAnDbContext _context;
    private readonly RealtimeMessagingService _messaging;
    private readonly LiveLocationService _location;
    private readonly OrderRealtimeAuthorizer _authorizer;

    private static readonly Guid TenantGuid = Guid.Parse("00000000-0000-0000-0000-000000000001");
    private static readonly Guid OtherTenantGuid = Guid.Parse("00000000-0000-0000-0000-000000000002");
    private static readonly TenantId Tenant = new(TenantGuid);
    private static readonly Guid CustomerId = Guid.NewGuid();
    private static readonly Guid ShopTenantId = Guid.NewGuid();
    private static readonly Guid GuestDeviceId = Guid.NewGuid();
    private static readonly Guid ShipperId = Guid.NewGuid();
    private static readonly Guid StrangerId = Guid.NewGuid();

    public RealtimePlatformP2Tests()
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

        _messaging = new RealtimeMessagingService(_context, NullLogger<RealtimeMessagingService>.Instance);
        _location = new LiveLocationService(_context, NullLogger<LiveLocationService>.Instance);
        _authorizer = new OrderRealtimeAuthorizer(_context, NullLogger<OrderRealtimeAuthorizer>.Instance);
    }

    public void Dispose()
    {
        _context.Dispose();
        _connection.Dispose();
    }

    // === T1: generic shop conversation is created with the subject keys ===
    [Fact(DisplayName = "T1: EnsureConversation_ShopSubject_CreatesWithSubjectKeys")]
    public async Task EnsureConversation_ShopSubject_CreatesWithSubjectKeys()
    {
        var conversation = await _messaging.EnsureConversationAsync(
            Tenant, RealtimeSubjectType.Shop, ShopTenantId, CustomerId, ShopTenantId);

        Assert.NotNull(conversation);
        Assert.Equal(RealtimeSubjectType.Shop.ToString(), conversation!.SubjectType);
        Assert.Equal(ShopTenantId, conversation.SubjectId);
        Assert.Equal(Guid.Empty, conversation.OrderId); // no order behind a shop chat
        Assert.Equal(CustomerId, conversation.CustomerId);
    }

    // === T2: EnsureConversation is idempotent per (tenant, subjectType, subjectId) ===
    [Fact(DisplayName = "T2: EnsureConversation_IsIdempotent")]
    public async Task EnsureConversation_IsIdempotent()
    {
        var first = await _messaging.EnsureConversationAsync(Tenant, RealtimeSubjectType.Shop, ShopTenantId, CustomerId, ShopTenantId);
        var second = await _messaging.EnsureConversationAsync(Tenant, RealtimeSubjectType.Shop, ShopTenantId, CustomerId, ShopTenantId);

        Assert.NotNull(first);
        Assert.Equal(first!.Id, second!.Id);
    }

    // === T3 (F2 regression): two non-order conversations in one tenant must not collide ===
    // Before P2 the unique index was on OrderId alone — every shop conversation carries
    // OrderId = Guid.Empty, so the second one violated the index.
    [Fact(DisplayName = "T3: EnsureConversation_TwoShopSubjectsInSameTenant_NoCollision")]
    public async Task EnsureConversation_TwoShopSubjectsInSameTenant_NoCollision()
    {
        var shopA = await _messaging.EnsureConversationAsync(Tenant, RealtimeSubjectType.Shop, Guid.NewGuid(), CustomerId, ShopTenantId);
        var shopB = await _messaging.EnsureConversationAsync(Tenant, RealtimeSubjectType.Shop, Guid.NewGuid(), CustomerId, ShopTenantId);

        Assert.NotNull(shopA);
        Assert.NotNull(shopB);
        Assert.NotEqual(shopA!.Id, shopB!.Id);
    }

    // === T4: participants are registered and authorize send/receive ===
    [Fact(DisplayName = "T4: EnsureConversation_RegistersParticipants_IsParticipant")]
    public async Task EnsureConversation_RegistersParticipants()
    {
        var conversation = await _messaging.EnsureConversationAsync(Tenant, RealtimeSubjectType.Shop, ShopTenantId, CustomerId, ShopTenantId);

        Assert.True(await _messaging.IsParticipantAsync(conversation!.Id, CustomerId));
        Assert.True(await _messaging.IsParticipantAsync(conversation.Id, ShopTenantId));
        Assert.False(await _messaging.IsParticipantAsync(conversation.Id, StrangerId));
    }

    // === T5: a non-participant cannot send ===
    [Fact(DisplayName = "T5: SendMessage_NonParticipant_Throws")]
    public async Task SendMessage_NonParticipant_Throws()
    {
        var conversation = await _messaging.EnsureConversationAsync(Tenant, RealtimeSubjectType.Shop, ShopTenantId, CustomerId, ShopTenantId);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => _messaging.SendMessageAsync(conversation!.Id, StrangerId, "hello?"));
    }

    // === T6: history is bounded and returned oldest-first ===
    [Fact(DisplayName = "T6: GetHistory_IsBoundedAndAscending")]
    public async Task GetHistory_IsBoundedAndAscending()
    {
        var conversation = await _messaging.EnsureConversationAsync(Tenant, RealtimeSubjectType.Shop, ShopTenantId, CustomerId, ShopTenantId);

        for (var i = 1; i <= 5; i++)
            await _messaging.SendMessageAsync(conversation!.Id, CustomerId, $"msg {i}");

        var history = await _messaging.GetHistoryAsync(conversation!.Id, take: 3);

        Assert.Equal(3, history.Count);
        Assert.Equal(new[] { "msg 3", "msg 4", "msg 5" }, history.Select(m => m.Content));
        Assert.True(history[0].SentAt <= history[^1].SentAt);
    }

    // === T7: generic location ping round-trip ===
    [Fact(DisplayName = "T7: RecordPing_ShopSubject_GetLatestAndHistory")]
    public async Task RecordPing_ShopSubject_GetLatestAndHistory()
    {
        await _location.RecordPingAsync(Tenant, RealtimeSubjectType.Shop, ShopTenantId, CustomerId, 10.80, 106.70);
        await _location.RecordPingAsync(Tenant, RealtimeSubjectType.Shop, ShopTenantId, CustomerId, 10.81, 106.71);

        var latest = await _location.GetLatestAsync(RealtimeSubjectType.Shop, ShopTenantId);
        var history = await _location.GetHistoryAsync(RealtimeSubjectType.Shop, ShopTenantId);

        Assert.NotNull(latest);
        Assert.Equal(10.81, latest!.Latitude);
        Assert.Equal(CustomerId, latest.TrackerId);
        Assert.Equal(Guid.Empty, latest.DeliveryTaskId); // generic subject has no DeliveryTask
        Assert.Equal(2, history.Count);
    }

    // === T8: delivery pings keep the legacy DeliveryTaskId column populated ===
    [Fact(DisplayName = "T8: RecordPing_DeliverySubject_KeepsLegacyDeliveryTaskId")]
    public async Task RecordPing_DeliverySubject_KeepsLegacyDeliveryTaskId()
    {
        var taskId = Guid.NewGuid();

        await _location.RecordPingAsync(Tenant, RealtimeSubjectType.Delivery, taskId, ShipperId, 10.82, 106.72);

        var latest = await _location.GetLatestAsync(RealtimeSubjectType.Delivery, taskId);
        Assert.NotNull(latest);
        Assert.Equal(taskId, latest!.DeliveryTaskId);
        Assert.Equal(taskId, latest.SubjectId);
        Assert.Equal(RealtimeSubjectType.Delivery.ToString(), latest.SubjectType);

        // Legacy query path (DeliveryWorkflowService.GetTrackingHistoryAsync) still matches.
        var legacy = await _context.DeliveryTrackings
            .IgnoreQueryFilters()
            .Where(t => t.DeliveryTaskId == taskId)
            .ToListAsync();
        Assert.Single(legacy);
    }

    // === T9: legacy Conversation ctor keeps SubjectId == OrderId (SC7 no-regression) ===
    [Fact(DisplayName = "T9: LegacyConversationCtor_BackfillsOrderSubject")]
    public void LegacyConversationCtor_BackfillsOrderSubject()
    {
        var orderId = Guid.NewGuid();

        var conversation = new Conversation(Tenant, orderId, ShipperId, CustomerId);

        Assert.Equal(RealtimeSubjectType.Order.ToString(), conversation.SubjectType);
        Assert.Equal(orderId, conversation.SubjectId);
        Assert.Equal(orderId, conversation.OrderId);
    }

    // === T10: order authorizer — shipper, owner, guest device allowed; stranger denied ===
    [Fact(DisplayName = "T10: OrderAuthorizer_AllowsShipperOwnerGuest_DeniesStranger")]
    public async Task OrderAuthorizer_AllowsShipperOwnerGuest_DeniesStranger()
    {
        var customer = new Customer(Tenant, "Test Customer", "0901234567");
        SetProp(customer, "Id", CustomerId);
        _context.Customers.Add(customer);

        var orderId = Guid.NewGuid();
        var order = new Order(Tenant, null, 0);
        SetProp(order, "Id", orderId);
        SetProp(order, "OrderId", new OrderId(orderId));
        SetProp(order, "OrderType", "DELIVERY");
        SetProp(order, "CustomerId", CustomerId);
        _context.Orders.Add(order);

        var task = new DeliveryTask(Tenant, orderId, ShipperId, 10.8, 106.7);
        _context.DeliveryTasks.Add(task);

        // Guest order (no CustomerId) linked by device id — P1 D6 identity model.
        var guestOrderId = Guid.NewGuid();
        var guestOrder = new Order(Tenant, null, 0);
        SetProp(guestOrder, "Id", guestOrderId);
        SetProp(guestOrder, "OrderId", new OrderId(guestOrderId));
        SetProp(guestOrder, "OrderType", "DELIVERY");
        SetProp(guestOrder, "CustomerDeviceId", GuestDeviceId.ToString());
        _context.Orders.Add(guestOrder);

        await _context.SaveChangesAsync();

        Assert.True(await _authorizer.CanAccessAsync(RealtimeSubjectType.Order, orderId, ShipperId));
        Assert.True(await _authorizer.CanAccessAsync(RealtimeSubjectType.Order, orderId, CustomerId));
        Assert.False(await _authorizer.CanAccessAsync(RealtimeSubjectType.Order, orderId, StrangerId));
        Assert.True(await _authorizer.CanAccessAsync(RealtimeSubjectType.Order, guestOrderId, GuestDeviceId));
        Assert.False(await _authorizer.CanAccessAsync(RealtimeSubjectType.Order, guestOrderId, StrangerId));
    }

    // === T11: multi-tenancy — same subject id in another tenant is a different conversation ===
    [Fact(DisplayName = "T11: EnsureConversation_IsTenantScoped")]
    public async Task EnsureConversation_IsTenantScoped()
    {
        var subjectId = Guid.NewGuid();

        var a = await _messaging.EnsureConversationAsync(Tenant, RealtimeSubjectType.Shop, subjectId, CustomerId, ShopTenantId);
        var b = await _messaging.EnsureConversationAsync(new TenantId(OtherTenantGuid), RealtimeSubjectType.Shop, subjectId, CustomerId, ShopTenantId);

        Assert.NotNull(a);
        Assert.NotNull(b);
        Assert.NotEqual(a!.Id, b!.Id);
    }

    private static void SetProp<T>(T obj, string propName, object value)
    {
        typeof(T).GetProperty(propName)?.SetValue(obj, value);
    }
}
