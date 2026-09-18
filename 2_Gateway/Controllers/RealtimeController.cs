using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
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
    IRealtimeSubjectResolver subjectResolver,
    RealtimeIdentityResolver identityResolver,
    IHubContext<MessagingHub> messagingHub,
    IHubContext<TrackingHub> trackingHub,
    IServiceProvider services,
    ILogger<RealtimeController> logger) : ControllerBase
{
    private const int MaxContentLength = 2000;
    private const int DefaultHistoryTake = 100;

    private readonly IRealtimeMessagingService _messaging = messagingService;
    private readonly ILiveLocationService _liveLocation = liveLocationService;
    private readonly IRealtimeSubjectResolver _subjectResolver = subjectResolver;
    private readonly RealtimeIdentityResolver _identityResolver = identityResolver;
    private readonly IHubContext<MessagingHub> _messagingHub = messagingHub;
    private readonly IHubContext<TrackingHub> _trackingHub = trackingHub;
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

        if (!await CanAccessAsync(subjectType, subjectId, identity.UserId, ct))
            return Forbidden(subjectType, subjectId);

        var conversation = await _messaging.GetConversationAsync(subjectType, subjectId, ct);
        if (conversation == null)
            return NotFound(new { error = "Chưa có cuộc trò chuyện cho chủ thể này." });

        try
        {
            var message = await _messaging.SendMessageAsync(conversation.Id, identity.UserId, body.Content, ct);
            if (message == null)
                return NotFound(new { error = "Không tìm thấy cuộc trò chuyện." });

            await _messagingHub.Clients.Group(RealtimeGroups.Messaging(subjectType, subjectId))
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

        if (!await CanAccessAsync(type, id, identity.UserId, ct))
            return Forbidden(type, id);

        var conversation = await _messaging.GetConversationAsync(type, id, ct);
        if (conversation == null)
            return NotFound(new { error = "Chưa có cuộc trò chuyện cho chủ thể này." });

        var messages = await _messaging.GetHistoryAsync(conversation.Id, take, ct);

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

        if (!await CanAccessAsync(type, id, identity.UserId, ct))
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

        if (!await CanAccessAsync(type, id, identity.UserId, ct))
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

    private async Task<RealtimeIdentity?> ResolveIdentityAsync(CancellationToken ct)
        => await _identityResolver.ResolveAsync(HttpContext, ct);

    private Task<bool> CanAccessAsync(RealtimeSubjectType type, Guid id, Guid userId, CancellationToken ct)
        => RealtimeAuthorizerLookup.CanAccessAsync(_services, type, id, userId, ct);

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
