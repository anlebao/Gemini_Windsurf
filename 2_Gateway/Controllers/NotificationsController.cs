using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VanAn.CoreHub.Services;
using VanAn.Gateway.Realtime;
using VanAn.Shared.Domain;

namespace VanAn.Gateway.Controllers;

/// <summary>
/// Notification Hub (2026-10-15, user directive): API thông báo.
///   Owner (staff JWT — tenant_id claim, pattern RealtimeController):
///     GET  /api/v1/notifications/owner?category=&unreadOnly=&take=
///     GET  /api/v1/notifications/owner/unread-count
///     POST /api/v1/notifications/owner/{id}/read
///     POST /api/v1/notifications/owner/read-all
///   Khách (X-Customer-Token / device — realtime identity):
///     GET  /api/v1/notifications/customer?take=
///     GET  /api/v1/notifications/customer/unread-count
///     POST /api/v1/notifications/customer/{notificationId}/read
///     POST /api/v1/notifications/customer/read-all
/// Dữ liệu PG (Gateway source of truth — TenantNotification / PushNotificationDelivery).
/// </summary>
[ApiController]
[Route("api/v1/notifications")]
[AllowAnonymous]
public class NotificationsController(
    INotificationCenterService notificationCenter,
    RealtimeIdentityResolver identityResolver,
    ILogger<NotificationsController> logger) : ControllerBase
{
    private readonly INotificationCenterService _notificationCenter = notificationCenter;
    private readonly RealtimeIdentityResolver _identityResolver = identityResolver;
    private readonly ILogger<NotificationsController> _logger = logger;

    // ── Owner ──────────────────────────────────────────────────────────────

    [HttpGet("owner")]
    public async Task<IActionResult> ListOwner([FromQuery] string? category, [FromQuery] bool unreadOnly = false, [FromQuery] int take = 100, CancellationToken ct = default)
    {
        var (ok, tenantId) = ResolveOwnerTenant();
        if (!ok) return Unauthorized(new { error = "Cần Bearer token (staff) với tenant_id." });

        NotificationCategory? cat = null;
        if (!string.IsNullOrWhiteSpace(category) && Enum.TryParse<NotificationCategory>(category, ignoreCase: true, out var parsed))
            cat = parsed;

        var items = await _notificationCenter.ListForOwnerAsync(tenantId.Value, cat, unreadOnly, Math.Min(take, 200), ct);
        return Ok(new
        {
            tenantId = tenantId.Value,
            items = items.Select(n => new
            {
                id = n.Id,
                category = n.Category.ToString(),
                title = n.Title,
                body = n.Body,
                actionUrl = n.ActionUrl,
                severity = n.Severity.ToString(),
                isRead = n.IsRead,
                createdAt = n.CreatedAt
            })
        });
    }

    [HttpGet("owner/unread-count")]
    public async Task<IActionResult> UnreadCountOwner(CancellationToken ct = default)
    {
        var (ok, tenantId) = ResolveOwnerTenant();
        if (!ok) return Unauthorized(new { error = "Cần Bearer token (staff) với tenant_id." });

        int count = await _notificationCenter.UnreadCountForOwnerAsync(tenantId.Value, ct);
        return Ok(new { tenantId = tenantId.Value, unreadCount = count });
    }

    [HttpPost("owner/{id:guid}/read")]
    public async Task<IActionResult> MarkOwnerRead(Guid id, CancellationToken ct = default)
    {
        var (ok, tenantId) = ResolveOwnerTenant();
        if (!ok) return Unauthorized(new { error = "Cần Bearer token (staff) với tenant_id." });

        await _notificationCenter.MarkOwnerReadAsync(id, tenantId.Value, ct);
        return Ok(new { id, read = true });
    }

    [HttpPost("owner/read-all")]
    public async Task<IActionResult> MarkAllOwnerRead(CancellationToken ct = default)
    {
        var (ok, tenantId) = ResolveOwnerTenant();
        if (!ok) return Unauthorized(new { error = "Cần Bearer token (staff) với tenant_id." });

        await _notificationCenter.MarkAllOwnerReadAsync(tenantId.Value, ct);
        return Ok(new { readAll = true });
    }

    // ── Khách ──────────────────────────────────────────────────────────────

    [HttpGet("customer")]
    public async Task<IActionResult> ListCustomer([FromQuery] int take = 100, CancellationToken ct = default)
    {
        var identity = await ResolveCustomerIdentityAsync(ct);
        if (identity == null)
            return Unauthorized(new { error = "Cần X-Customer-Token hoặc X-Customer-Device-Id." });

        var items = await _notificationCenter.ListForCustomerAsync(identity.Value, Math.Min(take, 200), ct);
        return Ok(new
        {
            customerId = identity.Value,
            items = items.Select(d => new
            {
                notificationId = d.NotificationId,
                category = d.Category?.ToString(),
                title = d.Title,
                body = d.Body,
                actionUrl = d.ActionUrl,
                isRead = d.IsRead,
                createdAt = d.CreatedAt
            })
        });
    }

    [HttpGet("customer/unread-count")]
    public async Task<IActionResult> UnreadCountCustomer(CancellationToken ct = default)
    {
        var identity = await ResolveCustomerIdentityAsync(ct);
        if (identity == null)
            return Unauthorized(new { error = "Cần X-Customer-Token hoặc X-Customer-Device-Id." });

        int count = await _notificationCenter.UnreadCountForCustomerAsync(identity.Value, ct);
        return Ok(new { customerId = identity.Value, unreadCount = count });
    }

    [HttpPost("customer/{notificationId:guid}/read")]
    public async Task<IActionResult> MarkCustomerRead(Guid notificationId, CancellationToken ct = default)
    {
        var identity = await ResolveCustomerIdentityAsync(ct);
        if (identity == null)
            return Unauthorized(new { error = "Cần X-Customer-Token hoặc X-Customer-Device-Id." });

        await _notificationCenter.MarkCustomerReadAsync(notificationId, identity.Value, ct);
        return Ok(new { notificationId, read = true });
    }

    [HttpPost("customer/read-all")]
    public async Task<IActionResult> MarkAllCustomerRead(CancellationToken ct = default)
    {
        var identity = await ResolveCustomerIdentityAsync(ct);
        if (identity == null)
            return Unauthorized(new { error = "Cần X-Customer-Token hoặc X-Customer-Device-Id." });

        await _notificationCenter.MarkAllCustomerReadAsync(identity.Value, ct);
        return Ok(new { readAll = true });
    }

    // ── Helpers ────────────────────────────────────────────────────────────

    private (bool ok, TenantId? tenantId) ResolveOwnerTenant()
    {
        string? tenantIdStr = User.FindFirst("tenant_id")?.Value ?? User.FindFirst("TenantId")?.Value;
        if (Guid.TryParse(tenantIdStr, out Guid tid) && tid != Guid.Empty)
            return (true, new TenantId(tid));
        return (false, null);
    }

    private async Task<Guid?> ResolveCustomerIdentityAsync(CancellationToken ct)
    {
        var identity = await _identityResolver.ResolveAsync(HttpContext, ct);
        if (identity == null) return null;
        // Chỉ customer (hoặc guest device có customerId) — device-only không có customer id để gán inbox.
        if (identity.Kind != RealtimeIdentityKind.Customer)
            return null;
        return identity.UserId != Guid.Empty ? identity.UserId : null;
    }
}
