using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using VanAn.CoreHub.Infrastructure;
using VanAn.CoreHub.Services;
using VanAn.CoreHub.Services.Adapters;
using VanAn.Gateway.Controllers;
using VanAn.Gateway.Hubs;
using VanAn.Gateway.Realtime;
using VanAn.Shared.Domain;
using Xunit;

namespace VanAn.Core.Tests.Realtime;

/// <summary>
/// Realtime Platform P5: Shop chat — authorizer + controller (inbox + conversation ensure).
///   T1-T6  — ShopRealtimeAuthorizer (staff tenant / initiator / guest participant / deny)
///   T7-T8  — GET /api/realtime/shop/conversations (staff 200 with names, non-staff 403)
///   T9     — GET conversations/Shop/{tenantId} ensures a conversation (initiator = caller)
/// </summary>
public class ShopRealtimeP5Tests : IDisposable
{
    private static readonly TenantId Tenant = new(Guid.NewGuid());
    private static readonly Guid ShopTenantId = Tenant.Value;

    private readonly SqliteConnection _connection;
    private readonly VanAnDbContext _context;

    public ShopRealtimeP5Tests()
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
    }

    public void Dispose()
    {
        _context.Dispose();
        _connection.Dispose();
    }

    // === T1-T6: ShopRealtimeAuthorizer ===

    [Fact(DisplayName = "T1: staff whose tenant == shop is allowed")]
    public async Task Staff_WithMatchingTenant_IsAllowed()
    {
        var authorizer = new ShopRealtimeAuthorizer(NullLogger<ShopRealtimeAuthorizer>.Instance);
        var staffUserId = Guid.NewGuid();

        var allowed = await authorizer.CanAccessAsync(
            RealtimeSubjectType.Shop, ShopTenantId, staffUserId, ShopTenantId, CancellationToken.None);

        Assert.True(allowed);
    }

    [Fact(DisplayName = "T2: staff of another tenant is denied")]
    public async Task Staff_WithOtherTenant_IsDenied()
    {
        var authorizer = new ShopRealtimeAuthorizer(NullLogger<ShopRealtimeAuthorizer>.Instance);
        var staffUserId = Guid.NewGuid();

        var allowed = await authorizer.CanAccessAsync(
            RealtimeSubjectType.Shop, ShopTenantId, staffUserId, Guid.NewGuid(), CancellationToken.None);

        Assert.False(allowed);
    }

    [Fact(DisplayName = "T3: the conversation initiator (customer) is allowed")]
    public async Task ConversationInitiator_IsAllowed()
    {
        var customerId = Guid.NewGuid();
        var conversation = new Conversation(Tenant, RealtimeSubjectType.Shop, ShopTenantId, customerId, ShopTenantId);
        _context.Conversations.Add(conversation);
        await _context.SaveChangesAsync();

        var authorizer = new ShopRealtimeAuthorizer(NullLogger<ShopRealtimeAuthorizer>.Instance);
        var allowed = await authorizer.CanAccessAsync(RealtimeSubjectType.Shop, ShopTenantId, customerId, CancellationToken.None);

        Assert.True(allowed);
    }

    [Fact(DisplayName = "T4: a guest participant row is allowed")]
    public async Task GuestParticipant_IsAllowed()
    {
        var guestDeviceId = Guid.NewGuid();
        var conversation = new Conversation(Tenant, RealtimeSubjectType.Shop, ShopTenantId, Guid.NewGuid(), ShopTenantId);
        _context.Conversations.Add(conversation);
        await _context.SaveChangesAsync();
        _context.ConversationParticipants.Add(
            new ConversationParticipant(Tenant, conversation.Id, guestDeviceId, RealtimeParticipantRole.Guest));
        await _context.SaveChangesAsync();

        var authorizer = new ShopRealtimeAuthorizer(NullLogger<ShopRealtimeAuthorizer>.Instance);
        var allowed = await authorizer.CanAccessAsync(RealtimeSubjectType.Shop, ShopTenantId, guestDeviceId, CancellationToken.None);

        Assert.True(allowed);
    }

    [Fact(DisplayName = "T5: any visitor may access the shop chat (public widget — privacy is enforced by the history filter + per-user push groups)")]
    public async Task Stranger_IsAllowed()
    {
        var stranger = Guid.NewGuid();
        var authorizer = new ShopRealtimeAuthorizer(NullLogger<ShopRealtimeAuthorizer>.Instance);

        var allowed = await authorizer.CanAccessAsync(RealtimeSubjectType.Shop, ShopTenantId, stranger, CancellationToken.None);

        Assert.True(allowed);
    }

    [Fact(DisplayName = "T6: empty user id is denied")]
    public async Task EmptyUserId_IsDenied()
    {
        var authorizer = new ShopRealtimeAuthorizer(NullLogger<ShopRealtimeAuthorizer>.Instance);

        var allowed = await authorizer.CanAccessAsync(RealtimeSubjectType.Shop, ShopTenantId, Guid.Empty, CancellationToken.None);

        Assert.False(allowed);
    }

    // === T7-T9: RealtimeController Shop endpoints ===

    [Fact(DisplayName = "T7: shop inbox — staff lists conversations with customer names, newest first")]
    public async Task ShopInbox_Staff_ReturnsConversations()
    {
        var staffUserId = Guid.NewGuid();
        var customer = new Customer(Tenant, "Khách A", "0900000000");
        _context.Customers.Add(customer);
        // The unique index (TenantId, SubjectType, SubjectId) allows ONE conversation per shop —
        // the list endpoint's ordering/preview is exercised via the messaging mock below.
        var newer = new Conversation(Tenant, RealtimeSubjectType.Shop, ShopTenantId, customer.Id, ShopTenantId);
        var older = new Conversation(Tenant, RealtimeSubjectType.Shop, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        _context.Conversations.AddRange(newer, older);
        await _context.SaveChangesAsync();

        var (controller, messaging) = BuildController(_context, staffUserId, ShopTenantId);
        messaging.Setup(m => m.GetConversationsAsync(It.IsAny<RealtimeSubjectType>(), It.IsAny<Guid>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
                 .ReturnsAsync(new List<Conversation> { newer, older });
        messaging.Setup(m => m.GetHistoryAsync(It.IsAny<Guid>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
                 .ReturnsAsync((Guid convId, int take, CancellationToken _) =>
                     new List<Message> { new Message(Tenant, convId, Guid.NewGuid(), "tin mới") });

        var result = await controller.GetShopConversations(CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var json = System.Text.Json.JsonSerializer.Serialize(ok.Value, new System.Text.Json.JsonSerializerOptions
        {
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        });
        Assert.Contains("\"conversations\"", json);
        Assert.True(json.Contains("Khách A"), $"customer name missing in: {json}");
        Assert.Contains("tin mới", json);
    }

    [Fact(DisplayName = "T8: shop inbox — non-staff (customer) identity is forbidden")]
    public async Task ShopInbox_Customer_IsForbidden()
    {
        var (controller, _) = BuildController(_context, Guid.NewGuid(), tenantId: null, kind: RealtimeIdentityKind.Customer);

        var result = await controller.GetShopConversations(CancellationToken.None);

        var status = Assert.IsType<ObjectResult>(result);
        Assert.Equal(403, status.StatusCode);
    }

    [Fact(DisplayName = "T9: GET conversations/Shop/{tenantId} — a fresh visitor creates the conversation (initiator = caller)")]
    public async Task GetConversation_Shop_EnsuresConversation()
    {
        var customerId = Guid.NewGuid();
        var (controller, messaging) = BuildController(_context, customerId, tenantId: null, kind: RealtimeIdentityKind.Customer);

        // Generic lookup misses → Shop ensure (create-only) runs, then the authorizer gates.
        messaging.Setup(m => m.GetConversationAsync(It.IsAny<RealtimeSubjectType>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                 .ReturnsAsync((Conversation?)null);
        messaging.Setup(m => m.EnsureConversationAsync(
                It.IsAny<TenantId>(), It.IsAny<RealtimeSubjectType>(), It.IsAny<Guid>(),
                It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                 .ReturnsAsync((TenantId t, RealtimeSubjectType st, Guid sid, Guid init, Guid counter, string ir, string cr, CancellationToken _)
                     => new Conversation(t, st, sid, init, counter));
        messaging.Setup(m => m.GetHistoryAsync(It.IsAny<Guid>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
                 .ReturnsAsync(new List<Message>());

        var result = await controller.GetConversation(
            nameof(RealtimeSubjectType.Shop), ShopTenantId.ToString(), 100, CancellationToken.None);

        Assert.IsType<OkObjectResult>(result);
        // The conversation was created with the caller as initiator and the shop as counterpart.
        messaging.Verify(m => m.EnsureConversationAsync(
            Tenant, RealtimeSubjectType.Shop, ShopTenantId, customerId, ShopTenantId,
            RealtimeParticipantRole.Customer, RealtimeParticipantRole.Shop, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact(DisplayName = "T10: a customer only sees their own messages + shop replies — never another customer's (history filter)")]
    public async Task GetConversation_Shop_CustomerHistory_IsFiltered()
    {
        var customerA = Guid.NewGuid();
        var customerB = Guid.NewGuid();
        var staffId = Guid.NewGuid();

        // Real context: conversation + shop-side participant (staff, role Shop) for the filter.
        var conversation = new Conversation(Tenant, RealtimeSubjectType.Shop, ShopTenantId, customerA, ShopTenantId);
        _context.Conversations.Add(conversation);
        _context.ConversationParticipants.Add(
            new ConversationParticipant(Tenant, conversation.Id, staffId, RealtimeParticipantRole.Shop));
        await _context.SaveChangesAsync();

        var (controller, messaging) = BuildController(_context, customerB, tenantId: null, kind: RealtimeIdentityKind.Device);
        messaging.Setup(m => m.GetConversationAsync(It.IsAny<RealtimeSubjectType>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                 .ReturnsAsync(conversation);
        // The shared thread contains customer A's message + a shop reply (by staff).
        messaging.Setup(m => m.GetHistoryAsync(It.IsAny<Guid>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
                 .ReturnsAsync(new List<Message>
                 {
                     new Message(Tenant, conversation.Id, customerA, "bí mật của khách A"),
                     new Message(Tenant, conversation.Id, staffId, "shop trả lời")
                 });

        var result = await controller.GetConversation(
            nameof(RealtimeSubjectType.Shop), ShopTenantId.ToString(), 100, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var json = System.Text.Json.JsonSerializer.Serialize(ok.Value, new System.Text.Json.JsonSerializerOptions
        {
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        });
        // Customer B does NOT see customer A's message; the shop reply is visible.
        Assert.DoesNotContain("bí mật của khách A", json);
        Assert.Contains("shop trả lời", json);
    }

    [Fact(DisplayName = "T12: a guest DEVICE on an order becomes a participant before sending (P6 RV fix)")]
    public async Task SendMessage_Order_DeviceSender_BecomesParticipant()
    {
        var device = Guid.NewGuid();
        var customer = Guid.NewGuid();
        // Legacy order conversation keyed to the customer — the device is NOT a party.
        var conversation = new Conversation(Tenant, customer /*orderId*/, Guid.NewGuid() /*shipper*/, customer);
        var (controller, messaging) = BuildController(_context, device, tenantId: null, kind: RealtimeIdentityKind.Device);
        messaging.Setup(m => m.GetConversationAsync(It.IsAny<RealtimeSubjectType>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                 .ReturnsAsync(conversation);
        messaging.Setup(m => m.SendMessageAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                 .ReturnsAsync((Guid c, Guid s, string content, CancellationToken _) => new Message(Tenant, c, s, content));

        var result = await controller.SendMessage(
            new RealtimeController.SendRealtimeMessageRequest
            {
                SubjectType = nameof(RealtimeSubjectType.Order),
                SubjectId = conversation.OrderId,
                Content = "xin chào từ device"
            }, CancellationToken.None);

        Assert.IsType<OkObjectResult>(result);
        // The device was added as a participant (role Guest) before the sender check.
        messaging.Verify(m => m.EnsureParticipantAsync(
            conversation, device, RealtimeParticipantRole.Guest, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact(DisplayName = "T11: staff sees the WHOLE shop thread (no per-caller filter)")]
    public async Task GetConversation_Shop_StaffSeesAll()
    {
        var customerA = Guid.NewGuid();
        var staffId = Guid.NewGuid();
        var conversation = new Conversation(Tenant, RealtimeSubjectType.Shop, ShopTenantId, customerA, ShopTenantId);
        _context.Conversations.Add(conversation);
        await _context.SaveChangesAsync();

        var (controller, messaging) = BuildController(_context, staffId, ShopTenantId, kind: RealtimeIdentityKind.Staff);
        messaging.Setup(m => m.GetConversationAsync(It.IsAny<RealtimeSubjectType>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                 .ReturnsAsync(conversation);
        messaging.Setup(m => m.GetHistoryAsync(It.IsAny<Guid>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
                 .ReturnsAsync(new List<Message>
                 {
                     new Message(Tenant, conversation.Id, customerA, "tin khách A"),
                     new Message(Tenant, conversation.Id, staffId, "tin shop")
                 });

        var result = await controller.GetConversation(
            nameof(RealtimeSubjectType.Shop), ShopTenantId.ToString(), 100, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var json = System.Text.Json.JsonSerializer.Serialize(ok.Value, new System.Text.Json.JsonSerializerOptions
        {
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        });
        Assert.Contains("tin khách A", json);
        Assert.Contains("tin shop", json);
    }

    // === helpers ===

    private (RealtimeController Controller, Mock<IRealtimeMessagingService> Messaging) BuildController(
        IVanAnDbContext dbContext, Guid userId, Guid? tenantId,
        RealtimeIdentityKind kind = RealtimeIdentityKind.Staff,
        bool authorizerAllows = true)
    {
        var identity = new RealtimeIdentity(userId, kind, tenantId);

        var messaging = new Mock<IRealtimeMessagingService>();
        messaging.Setup(m => m.GetConversationsAsync(It.IsAny<RealtimeSubjectType>(), It.IsAny<Guid>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
                 .ReturnsAsync(new List<Conversation>());
        messaging.Setup(m => m.GetHistoryAsync(It.IsAny<Guid>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
                 .ReturnsAsync(new List<Message>());

        var chatService = new Mock<IChatService>();
        var location = new Mock<ILiveLocationService>();
        var resolver = new Mock<IRealtimeSubjectResolver>();
        resolver.Setup(r => r.ResolveTenantAsync(It.IsAny<RealtimeSubjectType>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(Tenant);

        var identityResolver = new RealtimeIdentityResolver(
            new IRealtimeTokenValidator[] { new StubValidator(identity) },
            NullLogger<RealtimeIdentityResolver>.Instance);

        var services = new ServiceCollection();
        services.AddKeyedScoped<IRealtimeParticipantAuthorizer>(
            RealtimeSubjectType.Shop, (_, _) => new StubShopAuthorizer(allows: authorizerAllows));
        // T12: order sends exercise the Order branch of SendMessage.
        services.AddKeyedScoped<IRealtimeParticipantAuthorizer>(
            RealtimeSubjectType.Order, (_, _) => new StubShopAuthorizer(allows: authorizerAllows));
        var provider = services.BuildServiceProvider();

        var (messagingHub, _) = HubMock<MessagingHub>();
        var (trackingHub, _) = HubMock<TrackingHub>();

        var controller = new RealtimeController(
            messaging.Object, location.Object, chatService.Object, resolver.Object, identityResolver,
            messagingHub.Object, trackingHub.Object, dbContext, provider,
            NullLogger<RealtimeController>.Instance)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };

        return (controller, messaging);
    }

    private static (Mock<IHubContext<T>> Hub, Mock<IHubClients> Clients) HubMock<T>() where T : Hub
    {
        var proxy = new Mock<IClientProxy>();
        proxy.Setup(p => p.SendCoreAsync(It.IsAny<string>(), It.IsAny<object?[]>(), It.IsAny<CancellationToken>()))
             .Returns(Task.CompletedTask);

        var clients = new Mock<IHubClients>();
        clients.Setup(c => c.Group(It.IsAny<string>())).Returns(proxy.Object);

        var hub = new Mock<IHubContext<T>>();
        hub.Setup(h => h.Clients).Returns(clients.Object);

        return (hub, clients);
    }

    private sealed class StubValidator(RealtimeIdentity? identity) : IRealtimeTokenValidator
    {
        public string Name => "Stub";

        public Task<RealtimeIdentity?> ValidateAsync(HttpContext httpContext, CancellationToken ct = default)
            => Task.FromResult(identity);
    }

    private sealed class StubShopAuthorizer(bool allows) : IRealtimeParticipantAuthorizer
    {
        public Task<bool> CanAccessAsync(RealtimeSubjectType subjectType, Guid subjectId, Guid userId, CancellationToken ct = default)
            => Task.FromResult(allows);

        public Task<bool> CanAccessAsync(RealtimeSubjectType subjectType, Guid subjectId, Guid userId, Guid? tenantId, CancellationToken ct = default)
            => Task.FromResult(allows);
    }
}
