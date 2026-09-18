using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using VanAn.CoreHub.Infrastructure;
using VanAn.CoreHub.Services;
using VanAn.Gateway.Hubs;
using VanAn.Gateway.Realtime;
using VanAn.Shared.Domain;

namespace VanAn.Gateway.Controllers;

/// <summary>
/// Realtime Platform P3 (2026-09-17): module-neutral HTTP surface for the realtime platform.
///
///   POST /api/realtime/conversations/messages                          — send a message
///   GET  /api/realtime/conversations/{subjectType}/{subjectId}         — conversation history
///   POST /api/realtime/location/ping                                   — record a GPS ping
///   GET  /api/realtime/location/{subjectType}/{subjectId}/latest       — latest ping
///
/// Where <c>/api/community/chat|location/*</c> is bound to orders and to the Shipper role, these
/// endpoints take the subject as a parameter and authorize through the module's registered
/// <see cref="IRealtimeParticipantAuthorizer"/>. That is what makes
/// <c>GET .../location/Order/{orderId}/latest</c> answerable for the customer who owns the order —
/// the shipper-only role check that broke the buyer map (D2) has no place here (GW-8).
///
/// Auth: any <see cref="IRealtimeTokenValidator"/> (customer token, guest device id, staff JWT).
/// Unknown subject types are denied, not defaulted.
/// </summary>
[ApiController]
[Route("api/realtime")]
public class RealtimeController(
    IRealtimeMessagingService messagingService,
    ILiveLocationService liveLocationService,
    IChatService chatService,
    IRealtimeSubjectResolver subjectResolver,
    RealtimeIdentityResolver identityResolver,
    IHubContext<MessagingHub> messagingHub,
    IHubContext<TrackingHub> trackingHub,
    IVanAnDbContext dbContext,
    IServiceProvider services,
    ILogger<RealtimeController> logger) : ControllerBase
{
    private const int MaxContentLength = 2000;
    private const int DefaultHistoryTake = 100;

    private readonly IRealtimeMessagingService _messaging = messagingService;
    private readonly ILiveLocationService _liveLocation = liveLocationService;
    private readonly IChatService _chatService = chatService;
    private readonly IRealtimeSubjectResolver _subjectResolver = subjectResolver;
    private readonly RealtimeIdentityResolver _identityResolver = identityResolver;
    private readonly IHubContext<MessagingHub> _messagingHub = messagingHub;
    private readonly IHubContext<TrackingHub> _trackingHub = trackingHub;
    private readonly IVanAnDbContext _dbContext = dbContext;
    private readonly IServiceProvider _services = services;
    private readonly ILogger<RealtimeController> _logger = logger;

    /// <summary>Send a message to the conversation of a subject.</summary>
    [HttpPost("conversations/messages")]
    public async Task<IActionResult> SendMessage([FromBody] SendRealtimeMessageRequest body, CancellationToken ct)
    {
        var identity = await ResolveIdentityAsync(ct);
        if (identity == null)
            return Unauthorized(new { error = "Cần X-Customer-Token, X-Customer-Device-Id hoặc Bearer token." });

        if (body == null || string.IsNullOrWhiteSpace(body.Content))
            return BadRequest(new { error = "Nội dung tin nhắn không được để trống." });

        if (body.Content.Length > MaxContentLength)
            return BadRequest(new { error = $"Nội dung tin nhắn không được vượt quá {MaxContentLength} ký tự." });

        if (!TryParseSubject(body.SubjectType, body.SubjectId.ToString(), out var subjectType, out var subjectId, out var parseError))
            return parseError!;

        // P5 fix (P6): a fresh Shop chat has no anchor for the authorizer — the visitor creates
        // the conversation first (create-only — existing conversations are never joined here),
        // then the authorizer gates the result. Order keeps authorize-first semantics (an
        // unauthorized caller never touches the messaging service).
        Conversation? conversation;
        if (subjectType == RealtimeSubjectType.Shop)
        {
            conversation = await GetOrEnsureShopConversationAsync(subjectId, identity, ct);
            if (conversation == null)
                return NotFound(new { error = "Chưa có cuộc trò chuyện cho chủ thể này." });
            if (!await CanAccessAsync(subjectType, subjectId, identity, ct))
                return Forbidden(subjectType, subjectId);
        }
        else
        {
            if (!await CanAccessAsync(subjectType, subjectId, identity, ct))
                return Forbidden(subjectType, subjectId);
            conversation = await GetOrEnsureConversationAsync(subjectType, subjectId, identity, ct);
            if (conversation == null)
                return NotFound(new { error = "Chưa có cuộc trò chuyện cho chủ thể này." });
        }

        // P5/P6: the shop side is a tenant, not a user — a sender's id is not a conversation
        // party by default, so every Shop sender (staff AND customer) is added as a participant
        // before the sender check below. P6 RV: the same applies to a guest DEVICE on an order —
        // the order conversation is keyed to the customer (Conversation.CustomerId), so a device
        // authorized via Order.CustomerDeviceId is not a party yet. Access was already granted by
        // the authorizer above, so this never widens it.
        if (subjectType == RealtimeSubjectType.Shop || identity.Kind == RealtimeIdentityKind.Device)
            await _messaging.EnsureParticipantAsync(conversation, identity.UserId, identity.RoleCode, ct);

        try
        {
            var message = await _messaging.SendMessageAsync(conversation.Id, identity.UserId, body.Content, ct);
            if (message == null)
                return NotFound(new { error = "Không tìm thấy cuộc trò chuyện." });

            // P6: the shared group carries the thread to staff (inbox); the sender's per-user
            // group carries it to the customer who sent it — never to other customers.
            await _messagingHub.Clients.Group(RealtimeGroups.Messaging(subjectType, subjectId))
                .SendAsync("ReceiveMessage", message.Id.ToString(), message.SenderId.ToString(),
                    message.Content, message.SentAt.ToString("O"), ct);
            if (subjectType == RealtimeSubjectType.Shop)
                await _messagingHub.Clients.Group(RealtimeGroups.MessagingUser(subjectId, message.SenderId))
                    .SendAsync("ReceiveMessage", message.Id.ToString(), message.SenderId.ToString(),
                        message.Content, message.SentAt.ToString("O"), ct);

            return Ok(new
            {
                conversationId = conversation.Id,
                messageId = message.Id,
                sentAt = message.SentAt.ToString("O")
            });
        }
        catch (UnauthorizedAccessException)
        {
            return Forbidden(subjectType, subjectId);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>Conversation history for a subject, oldest first.</summary>
    [HttpGet("conversations/{subjectType}/{subjectId}")]
    public async Task<IActionResult> GetConversation(string subjectType, string subjectId, [FromQuery] int take = DefaultHistoryTake, CancellationToken ct = default)
    {
        var identity = await ResolveIdentityAsync(ct);
        if (identity == null)
            return Unauthorized(new { error = "Cần X-Customer-Token, X-Customer-Device-Id hoặc Bearer token." });

        if (!TryParseSubject(subjectType, subjectId, out var type, out var id, out var parseError))
            return parseError!;

        // P5 fix (P6): see SendMessage — Shop ensures (create-only) before authorizing;
        // Order authorizes first (an unauthorized caller never touches the messaging service).
        Conversation? conversation;
        if (type == RealtimeSubjectType.Shop)
        {
            conversation = await GetOrEnsureShopConversationAsync(id, identity, ct);
            if (conversation == null)
                return NotFound(new { error = "Chưa có cuộc trò chuyện cho chủ thể này." });
            if (!await CanAccessAsync(type, id, identity, ct))
                return Forbidden(type, id);
        }
        else
        {
            if (!await CanAccessAsync(type, id, identity, ct))
                return Forbidden(type, id);
            conversation = await GetOrEnsureConversationAsync(type, id, identity, ct);
            if (conversation == null)
                return NotFound(new { error = "Chưa có cuộc trò chuyện cho chủ thể này." });
        }

        var messages = await _messaging.GetHistoryAsync(conversation.Id, take, ct);

        // P6: the shop conversation is a shared thread — a customer only sees their own messages
        // plus the shop's replies (staff participants, role Shop); staff see the whole thread.
        if (type == RealtimeSubjectType.Shop && identity.Kind != RealtimeIdentityKind.Staff)
        {
            var shopSenderIds = await _dbContext.ConversationParticipants
                .IgnoreQueryFilters()
                .AsNoTracking()
                .Where(p => p.ConversationId == conversation.Id
                         && p.RoleCode == RealtimeParticipantRole.Shop && p.IsActive)
                .Select(p => p.ParticipantId)
                .ToListAsync(ct);

            messages = messages
                .Where(m => m.SenderId == identity.UserId || shopSenderIds.Contains(m.SenderId))
                .ToList();
        }

        return Ok(new
        {
            conversationId = conversation.Id,
            subjectType = type.ToString(),
            subjectId = id,
            participantId = identity.UserId,
            messages = messages.Select(m => new
            {
                id = m.Id,
                senderId = m.SenderId,
                content = m.Content,
                sentAt = m.SentAt,
                isRead = m.IsRead
            })
        });
    }

    /// <summary>
    /// P5: GET /api/realtime/shop/conversations — the shop inbox. Lists every customer
    /// conversation of the caller's shop (staff JWT tenant_id claim), newest first, with a
    /// last-message preview + resolved customer name. Customers who never opened a conversation
    /// yet simply don't appear until the first message.
    /// </summary>
    [HttpGet("shop/conversations")]
    public async Task<IActionResult> GetShopConversations(CancellationToken ct = default)
    {
        var identity = await ResolveIdentityAsync(ct);
        if (identity == null)
            return Unauthorized(new { error = "Cần Bearer token (staff)." });

        if (identity.Kind != RealtimeIdentityKind.Staff || !identity.TenantId.HasValue || identity.TenantId.Value == Guid.Empty)
            return StatusCode(403, new { error = "Chỉ chủ shop mới xem được hộp thư." });

        var shopTenantId = identity.TenantId.Value;
        var conversations = await _messaging.GetConversationsAsync(RealtimeSubjectType.Shop, shopTenantId, 100, ct);

        // Resolve customer display names in one pass (guest device ids are not Customers).
        var customerIds = conversations
            .Where(c => c.CustomerId != Guid.Empty)
            .Select(c => c.CustomerId)
            .Distinct()
            .ToList();
        var names = new Dictionary<Guid, string>();
        if (customerIds.Count > 0)
        {
            names = await _dbContext.Customers
                .IgnoreQueryFilters()
                .AsNoTracking()
                .Where(c => customerIds.Contains(c.Id))
                .ToDictionaryAsync(c => c.Id, c => c.FullName, ct);
        }

        var items = new List<ShopConversationItem>();
        foreach (var c in conversations)
        {
            var last = (await _messaging.GetHistoryAsync(c.Id, 1, ct)).LastOrDefault();
            items.Add(new ShopConversationItem
            {
                ConversationId = c.Id,
                SubjectId = c.SubjectId,
                CustomerId = c.CustomerId,
                CustomerName = c.CustomerId != Guid.Empty && names.TryGetValue(c.CustomerId, out var n) ? n : null,
                LastMessage = last?.Content,
                LastMessageAt = last?.SentAt,
                LastSenderId = last?.SenderId
            });
        }

        return Ok(new
        {
            tenantId = shopTenantId,
            conversations = items.OrderByDescending(i => i.LastMessageAt ?? DateTime.MinValue)
        });
    }

    private sealed class ShopConversationItem
    {
        public Guid ConversationId { get; init; }
        public Guid SubjectId { get; init; }
        public Guid CustomerId { get; init; }
        public string? CustomerName { get; init; }
        public string? LastMessage { get; init; }
        public DateTime? LastMessageAt { get; init; }
        public Guid? LastSenderId { get; init; }
    }

    /// <summary>Record a GPS ping for a subject and push it to the subject's tracking group.</summary>
    [HttpPost("location/ping")]
    public async Task<IActionResult> RecordPing([FromBody] RealtimePingRequest body, CancellationToken ct)
    {
        var identity = await ResolveIdentityAsync(ct);
        if (identity == null)
            return Unauthorized(new { error = "Cần X-Customer-Token, X-Customer-Device-Id hoặc Bearer token." });

        if (body == null)
            return BadRequest(new { error = "Dữ liệu không hợp lệ." });

        if (!TryParseSubject(body.SubjectType, body.SubjectId.ToString(), out var type, out var id, out var parseError))
            return parseError!;

        if (body.Lat is < -90 or > 90 || body.Lng is < -180 or > 180)
            return BadRequest(new { error = "Tọa độ không hợp lệ." });

        // (0, 0) is the Gulf of Guinea, not a location — it is what an unset/default coordinate looks
        // like and it silently centres maps in the ocean (D8). Reject it instead of persisting noise.
        if (body.Lat == 0 && body.Lng == 0)
            return BadRequest(new { error = "Tọa độ không hợp lệ. Vui lòng bật GPS." });

        if (!await CanAccessAsync(type, id, identity, ct))
            return Forbidden(type, id);

        var tenantId = await _subjectResolver.ResolveTenantAsync(type, id, ct);
        if (tenantId == null)
            return BadRequest(new { error = $"Không xác định được chủ thể {type}/{id}." });

        var ping = await _liveLocation.RecordPingAsync(tenantId, type, id, identity.UserId, body.Lat, body.Lng, ct);
        if (ping == null)
            return BadRequest(new { error = "Không ghi được vị trí." });

        await _trackingHub.Clients.Group(RealtimeGroups.Tracking(type, id))
            .SendAsync("LocationUpdate", id.ToString(), body.Lat, body.Lng, ping.RecordedAt.ToString("O"), ct);

        return Ok(new { recordedAt = ping.RecordedAt.ToString("O") });
    }

    /// <summary>
    /// Latest known position of a subject. Returns nulls (not 404) when nothing has been recorded yet,
    /// so a caller can render "not moving yet" without treating it as a failure.
    /// </summary>
    [HttpGet("location/{subjectType}/{subjectId}/latest")]
    public async Task<IActionResult> GetLatestLocation(string subjectType, string subjectId, CancellationToken ct)
    {
        var identity = await ResolveIdentityAsync(ct);
        if (identity == null)
            return Unauthorized(new { error = "Cần X-Customer-Token, X-Customer-Device-Id hoặc Bearer token." });

        if (!TryParseSubject(subjectType, subjectId, out var type, out var id, out var parseError))
            return parseError!;

        if (!await CanAccessAsync(type, id, identity, ct))
            return Forbidden(type, id);

        var ping = await _liveLocation.GetLatestAsync(type, id, ct);

        return Ok(new
        {
            subjectType = type.ToString(),
            subjectId = id,
            lat = ping?.Latitude,
            lng = ping?.Longitude,
            trackerId = ping?.TrackerId,
            recordedAt = ping?.RecordedAt
        });
    }

    /// <summary>
    /// Find a subject's conversation — or create it when the subject is an Order and it does not
    /// exist yet. Order conversations are created lazily by the legacy adapter
    /// (<see cref="IChatService.GetOrCreateConversationAsync"/>) — a fresh order has no
    /// conversation until a party opens chat, and the placeholder ShipperId is filled in when the
    /// shipper accepts (P4; GW-6 keeps IChatService as the order adapter).
    /// Shop conversations go through <see cref="GetOrEnsureShopConversationAsync"/> instead.
    /// </summary>
    private async Task<Conversation?> GetOrEnsureConversationAsync(
        RealtimeSubjectType subjectType,
        Guid subjectId,
        RealtimeIdentity identity,
        CancellationToken ct)
    {
        var conversation = await _messaging.GetConversationAsync(subjectType, subjectId, ct);
        if (conversation != null)
            return conversation;

        if (subjectType == RealtimeSubjectType.Order)
        {
            var guestDeviceId = identity.Kind == RealtimeIdentityKind.Device ? identity.UserId : (Guid?)null;
            return await _chatService.GetOrCreateConversationAsync(subjectId, guestDeviceId);
        }

        return null;
    }

    /// <summary>
    /// P5 fix (P6): ensure a Shop conversation for the caller — create-only semantics.
    /// A fresh shop chat has no prior anchor for the authorizer (the shop is a tenant, not a
    /// user), so the visitor creates the conversation with themselves as initiator, and the
    /// controller then authorizes the result. When the conversation already exists it is returned
    /// WITHOUT adding the caller as a participant — joining someone else's conversation still
    /// requires the authorizer (participant row / staff tenant), so no stranger can read a
    /// customer's chat by calling this endpoint. Staff never creates: they only reply to
    /// conversations the customer started.
    /// </summary>
    private async Task<Conversation?> GetOrEnsureShopConversationAsync(
        Guid subjectId,
        RealtimeIdentity identity,
        CancellationToken ct)
    {
        var conversation = await _messaging.GetConversationAsync(RealtimeSubjectType.Shop, subjectId, ct);
        if (conversation != null)
            return conversation;

        // Staff never creates — they only reply to conversations customers started.
        if (identity.Kind == RealtimeIdentityKind.Staff)
            return null;

        var tenantId = await _subjectResolver.ResolveTenantAsync(RealtimeSubjectType.Shop, subjectId, ct);
        if (tenantId == null)
            return null;

        // Counterpart is the shop itself (its tenant id doubles as the shop participant id).
        return await _messaging.EnsureConversationAsync(
            tenantId, RealtimeSubjectType.Shop, subjectId, identity.UserId, subjectId,
            identity.RoleCode, RealtimeParticipantRole.Shop, ct);
    }

    private async Task<RealtimeIdentity?> ResolveIdentityAsync(CancellationToken ct)
        => await _identityResolver.ResolveAsync(HttpContext, ct);

    private Task<bool> CanAccessAsync(RealtimeSubjectType type, Guid id, RealtimeIdentity identity, CancellationToken ct)
        => RealtimeAuthorizerLookup.CanAccessAsync(_services, type, id, identity.UserId, identity.TenantId, ct);

    private ObjectResult Forbidden(RealtimeSubjectType type, Guid subjectId)
    {
        _logger.LogDebug("RealtimeController: denied access to {SubjectType}/{SubjectId}", type, subjectId);
        return StatusCode(403, new { error = "Bạn không có quyền truy cập chủ thể này." });
    }

    /// <summary>
    /// Validates the (subjectType, subjectId) pair. Takes the subject id as a string because the
    /// same pair arrives as a route segment (always a string) and as a JSON guid — parsing once here
    /// keeps both entry points rejecting the same inputs with the same messages.
    /// </summary>
    private static bool TryParseSubject(
        string? subjectType,
        string? subjectIdRaw,
        out RealtimeSubjectType type,
        out Guid id,
        out IActionResult? error)
    {
        type = default;
        id = Guid.Empty;
        error = null;

        if (string.IsNullOrWhiteSpace(subjectType)
            || !Enum.TryParse<RealtimeSubjectType>(subjectType, ignoreCase: true, out type)
            || !Enum.IsDefined(type))
        {
            error = new BadRequestObjectResult(new { error = $"subjectType '{subjectType}' không hợp lệ." });
            return false;
        }

        if (!Guid.TryParse(subjectIdRaw, out id) || id == Guid.Empty)
        {
            error = new BadRequestObjectResult(new { error = "subjectId không hợp lệ." });
            return false;
        }

        return true;
    }

    public class SendRealtimeMessageRequest
    {
        public string SubjectType { get; set; } = string.Empty;
        public Guid SubjectId { get; set; }
        public string Content { get; set; } = string.Empty;
    }

    public class RealtimePingRequest
    {
        public string SubjectType { get; set; } = string.Empty;
        public Guid SubjectId { get; set; }
        public double Lat { get; set; }
        public double Lng { get; set; }
    }
}
