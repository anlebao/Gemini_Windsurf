using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using VanAn.CoreHub.Infrastructure;
using VanAn.CoreHub.Services;
using VanAn.Gateway.Controllers;
using VanAn.Gateway.Hubs;
using VanAn.Gateway.Realtime;
using VanAn.Shared.Domain;
using Xunit;

namespace VanAn.Core.Tests.Realtime;

/// <summary>
/// Realtime Platform P3 (2026-09-17): /api/realtime/* behaviour — identity required, access decided
/// by the module authorizer (never by a role check), and pushes landing in the subject group.
/// </summary>
public class RealtimeControllerP3Tests
{
    private static readonly Guid TenantGuid = Guid.Parse("00000000-0000-0000-0000-000000000001");
    private static readonly TenantId Tenant = new(TenantGuid);
    private static readonly Guid SubjectId = Guid.NewGuid();
    private static readonly Guid CallerId = Guid.NewGuid();

    // === T16: no credential at all → 401, and nothing is queried ===
    [Fact(DisplayName = "T16: SendMessage_NoIdentity_Returns401")]
    public async Task SendMessage_NoIdentity_Returns401()
    {
        var (controller, _, _, _) = Build(authenticated: false);

        var result = await controller.SendMessage(
            new RealtimeController.SendRealtimeMessageRequest
            {
                SubjectType = nameof(RealtimeSubjectType.Order),
                SubjectId = SubjectId,
                Content = "hello"
            }, CancellationToken.None);

        Assert.IsType<UnauthorizedObjectResult>(result);
    }

    // === T17: an unknown subject type is rejected before any lookup ===
    [Fact(DisplayName = "T17: SendMessage_InvalidSubjectType_Returns400")]
    public async Task SendMessage_InvalidSubjectType_Returns400()
    {
        var (controller, _, _, _) = Build();

        var result = await controller.SendMessage(
            new RealtimeController.SendRealtimeMessageRequest
            {
                SubjectType = "NotASubject",
                SubjectId = SubjectId,
                Content = "hello"
            }, CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result);
    }

    // === T18: no authorizer for the subject → 403 (default deny), not a silent allow ===
    [Fact(DisplayName = "T18: GetConversation_NoAuthorizerRegistered_Returns403")]
    public async Task GetConversation_NoAuthorizerRegistered_Returns403()
    {
        // Identity resolves, but no authorizer is registered for the subject type — the platform
        // defaults to deny rather than trusting an unregistered module (P5 registers the Shop key).
        var (controller, _, _, _) = Build(registerOrderAuthorizer: false);

        var result = await controller.GetConversation(
            nameof(RealtimeSubjectType.Order), SubjectId.ToString(), 100, CancellationToken.None);

        var forbidden = Assert.IsType<ObjectResult>(result);
        Assert.Equal(403, forbidden.StatusCode);
    }

    // === T19: authorizer allows → history is returned for the caller ===
    [Fact(DisplayName = "T19: GetConversation_Authorized_ReturnsHistory")]
    public async Task GetConversation_Authorized_ReturnsHistory()
    {
        var conversation = new Conversation(Tenant, RealtimeSubjectType.Order, SubjectId, CallerId, Guid.Empty);

        var (controller, messaging, _, _) = Build(conversation: conversation);
        messaging.Setup(m => m.GetHistoryAsync(conversation.Id, 100, It.IsAny<CancellationToken>()))
                 .ReturnsAsync(new List<Message>
                 {
                     new(Tenant, conversation.Id, CallerId, "first")
                 });

        var result = await controller.GetConversation(
            nameof(RealtimeSubjectType.Order), SubjectId.ToString(), 100, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.NotNull(ok.Value);
    }

    // === T20: an unauthorized caller never reaches the messaging service ===
    [Fact(DisplayName = "T20: GetConversation_AuthorizerDenies_Returns403AndSkipsService")]
    public async Task GetConversation_AuthorizerDenies_Returns403AndSkipsService()
    {
        var (controller, messaging, _, _) = Build(allowedUser: Guid.NewGuid());

        var result = await controller.GetConversation(
            nameof(RealtimeSubjectType.Order), SubjectId.ToString(), 100, CancellationToken.None);

        var forbidden = Assert.IsType<ObjectResult>(result);
        Assert.Equal(403, forbidden.StatusCode);
        messaging.Verify(m => m.GetConversationAsync(
            It.IsAny<RealtimeSubjectType>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // === T21: (0,0) is rejected — it is the unset-default coordinate that blanked the maps (D8) ===
    [Fact(DisplayName = "T21: RecordPing_ZeroCoordinates_Returns400")]
    public async Task RecordPing_ZeroCoordinates_Returns400()
    {
        var (controller, _, location, _) = Build();

        var result = await controller.RecordPing(
            new RealtimeController.RealtimePingRequest
            {
                SubjectType = nameof(RealtimeSubjectType.Order),
                SubjectId = SubjectId,
                Lat = 0,
                Lng = 0
            }, CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result);
        location.Verify(l => l.RecordPingAsync(
            It.IsAny<TenantId>(), It.IsAny<RealtimeSubjectType>(), It.IsAny<Guid>(),
            It.IsAny<Guid?>(), It.IsAny<double>(), It.IsAny<double>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // === T22: a valid ping is stamped with the subject's tenant and pushed to the subject group ===
    [Fact(DisplayName = "T22: RecordPing_Authorized_PersistsAndPushesToSubjectGroup")]
    public async Task RecordPing_Authorized_PersistsAndPushesToSubjectGroup()
    {
        var (controller, _, location, trackingClients) = Build();

        location.Setup(l => l.RecordPingAsync(
                It.IsAny<TenantId>(), It.IsAny<RealtimeSubjectType>(), It.IsAny<Guid>(),
                It.IsAny<Guid?>(), It.IsAny<double>(), It.IsAny<double>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DeliveryTracking(Tenant, RealtimeSubjectType.Order, SubjectId, CallerId, 10.8, 106.7));

        var result = await controller.RecordPing(
            new RealtimeController.RealtimePingRequest
            {
                SubjectType = nameof(RealtimeSubjectType.Order),
                SubjectId = SubjectId,
                Lat = 10.8,
                Lng = 106.7
            }, CancellationToken.None);

        Assert.IsType<OkObjectResult>(result);

        // Tenant came from the subject resolver, not from the caller.
        location.Verify(l => l.RecordPingAsync(
            Tenant, RealtimeSubjectType.Order, SubjectId, CallerId, 10.8, 106.7, It.IsAny<CancellationToken>()),
            Times.Once);

        trackingClients.Verify(c => c.Group(RealtimeGroups.Tracking(RealtimeSubjectType.Order, SubjectId)), Times.Once);
    }

    // === T24 (P4): a fresh order has no generic conversation — the legacy adapter creates it ===
    [Fact(DisplayName = "T24: GetConversation_OrderSubject_NoConversation_EnsuresViaLegacyAdapter")]
    public async Task GetConversation_OrderSubject_NoConversation_EnsuresViaLegacyAdapter()
    {
        var (controller, _, _, _) = Build(conversation: null);

        var result = await controller.GetConversation(
            nameof(RealtimeSubjectType.Order), SubjectId.ToString(), 100, CancellationToken.None);

        // The fallback created a conversation → history is served, not 404.
        Assert.IsType<OkObjectResult>(result);
    }

    // === T25 (P4): a non-Order subject without a conversation stays 404 — no legacy fallback ===
    [Fact(DisplayName = "T25: GetConversation_NonOrderSubject_NoConversation_DoesNotFallback")]
    public async Task GetConversation_NonOrderSubject_NoConversation_DoesNotFallback()
    {
        var (controller, messaging, _, _) = Build(conversation: null, registerShopAuthorizer: true);

        var result = await controller.GetConversation(
            nameof(RealtimeSubjectType.Shop), SubjectId.ToString(), 100, CancellationToken.None);

        Assert.IsType<NotFoundObjectResult>(result);
        messaging.Verify(m => m.GetConversationAsync(It.IsAny<RealtimeSubjectType>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    // === T26 (P4): when the generic conversation exists, the legacy adapter is NOT consulted ===
    [Fact(DisplayName = "T26: GetConversation_OrderSubject_ExistingConversation_SkipsLegacyEnsure")]
    public async Task GetConversation_OrderSubject_ExistingConversation_SkipsLegacyEnsure()
    {
        var existing = new Conversation(Tenant, RealtimeSubjectType.Order, SubjectId, CallerId, Guid.Empty);
        var (controller, _, _, _) = Build(conversation: existing);

        var result = await controller.GetConversation(
            nameof(RealtimeSubjectType.Order), SubjectId.ToString(), 100, CancellationToken.None);

        Assert.IsType<OkObjectResult>(result);
    }

    // === T27 (P4): sending the first message on a fresh order creates the conversation too ===
    [Fact(DisplayName = "T27: SendMessage_OrderSubject_NoConversation_FallsBackToLegacyEnsure")]
    public async Task SendMessage_OrderSubject_NoConversation_FallsBackToLegacyEnsure()
    {
        var (controller, _, _, _) = Build(conversation: null);

        var result = await controller.SendMessage(
            new RealtimeController.SendRealtimeMessageRequest
            {
                SubjectType = nameof(RealtimeSubjectType.Order),
                SubjectId = SubjectId,
                Content = "Xin chào"
            }, CancellationToken.None);

        Assert.IsType<OkObjectResult>(result);
    }

    // === T23: "no ping yet" is an empty answer, not an error ===
    [Fact(DisplayName = "T23: GetLatestLocation_NoPing_Returns200WithNulls")]
    public async Task GetLatestLocation_NoPing_Returns200WithNulls()
    {
        var (controller, _, location, _) = Build();
        location.Setup(l => l.GetLatestAsync(It.IsAny<RealtimeSubjectType>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((DeliveryTracking?)null);

        var result = await controller.GetLatestLocation(
            nameof(RealtimeSubjectType.Order), SubjectId.ToString(), CancellationToken.None);

        Assert.IsType<OkObjectResult>(result);
    }

    private static (RealtimeController Controller,
                    Mock<IRealtimeMessagingService> Messaging,
                    Mock<ILiveLocationService> Location,
                    Mock<IHubClients> TrackingClients)
        Build(
            bool authenticated = true,
            Conversation? conversation = null,
            Guid? allowedUser = null,
            bool registerOrderAuthorizer = true,
            bool registerShopAuthorizer = false)
    {
        var resolved = authenticated ? new RealtimeIdentity(CallerId, RealtimeIdentityKind.Customer) : null;
        var allowed = allowedUser ?? CallerId;

        var messaging = new Mock<IRealtimeMessagingService>();
        messaging.Setup(m => m.GetConversationAsync(It.IsAny<RealtimeSubjectType>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                 .ReturnsAsync(conversation);
        messaging.Setup(m => m.SendMessageAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                 .ReturnsAsync((Guid c, Guid s, string content, CancellationToken _) => new Message(Tenant, c, s, content));
        messaging.Setup(m => m.GetHistoryAsync(It.IsAny<Guid>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
                 .ReturnsAsync(new List<Message>());

        var location = new Mock<ILiveLocationService>();
        location.Setup(l => l.GetLatestAsync(It.IsAny<RealtimeSubjectType>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((DeliveryTracking?)null);

        var resolver = new Mock<IRealtimeSubjectResolver>();
        resolver.Setup(r => r.ResolveTenantAsync(It.IsAny<RealtimeSubjectType>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(Tenant);

        // P5: the controller resolves customer display names for the inbox (not exercised here).
        var dbContext = new Mock<IVanAnDbContext>();

        // P4: order conversations fall back to the legacy adapter when the generic lookup misses.
        var chatService = new Mock<IChatService>();
        chatService.Setup(c => c.GetOrCreateConversationAsync(It.IsAny<Guid>(), It.IsAny<Guid?>()))
                   .ReturnsAsync((Guid orderId, Guid? _) => conversation ?? new Conversation(Tenant, RealtimeSubjectType.Order, orderId, CallerId, Guid.Empty));

        var identityResolver = new RealtimeIdentityResolver(
            new IRealtimeTokenValidator[] { new StubValidator(resolved) },
            NullLogger<RealtimeIdentityResolver>.Instance);

        var services = new ServiceCollection();
        if (registerOrderAuthorizer)
        {
            services.AddKeyedScoped<IRealtimeParticipantAuthorizer>(
                RealtimeSubjectType.Order, (_, _) => new SingleUserAuthorizer(allowed));
        }
        if (registerShopAuthorizer)
        {
            services.AddKeyedScoped<IRealtimeParticipantAuthorizer>(
                RealtimeSubjectType.Shop, (_, _) => new SingleUserAuthorizer(allowed));
        }

        var provider = services.BuildServiceProvider();

        var (messagingHub, _) = HubMock<MessagingHub>();
        var (trackingHub, trackingClients) = HubMock<TrackingHub>();

        var controller = new RealtimeController(
            messaging.Object,
            location.Object,
            chatService.Object,
            resolver.Object,
            identityResolver,
            messagingHub.Object,
            trackingHub.Object,
            dbContext.Object,
            provider,
            NullLogger<RealtimeController>.Instance)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };

        return (controller, messaging, location, trackingClients);
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

    private sealed class SingleUserAuthorizer(Guid allowedUser) : IRealtimeParticipantAuthorizer
    {
        public Task<bool> CanAccessAsync(RealtimeSubjectType subjectType, Guid subjectId, Guid userId, CancellationToken ct = default)
            => Task.FromResult(userId == allowedUser);
    }
}
