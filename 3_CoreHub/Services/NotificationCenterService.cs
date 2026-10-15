using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using VanAn.CoreHub.Infrastructure;
using VanAn.CoreHub.Infrastructure.Messaging;
using VanAn.Shared.Domain;

namespace VanAn.CoreHub.Services;

/// <inheritdoc cref="INotificationCenterService" />
/// <summary>
/// Notification Hub (2026-10-15): implementation.
/// Ghi PG (VanAnDbContext — source of truth) TRỰC TIẾP — giống OrderService P6b
/// (trong ShopERP IVanAnDbContext = SQLite mirror; notifications là PG-only).
/// Mọi method fail-safe: exception được log + KHÔNG rethrow (không làm hỏng caller).
/// Publish "vanan.cloud.notification.created" (NATS) → Gateway NotificationHubNotifier
/// đẩy badge realtime tới NotificationsHub (fail-safe, fire-and-forget).
/// </summary>
public class NotificationCenterService(
    VanAnDbContext dbContext,
    ILogger<NotificationCenterService> logger,
    INatsEventPublisher? natsEventPublisher = null) : INotificationCenterService
{
    private const string CreatedSubject = "vanan.cloud.notification.created";

    private readonly VanAnDbContext _db = dbContext;
    private readonly ILogger<NotificationCenterService> _logger = logger;
    private readonly INatsEventPublisher? _natsPublisher = natsEventPublisher;

    // ── Owner ──────────────────────────────────────────────────────────────

    public async Task CreateForOwnerAsync(
        TenantId tenantId,
        NotificationCategory category,
        string title,
        string body,
        string? actionUrl = null,
        string? referenceType = null,
        Guid? referenceId = null,
        TenantNotificationSeverity severity = TenantNotificationSeverity.Info,
        CancellationToken ct = default)
    {
        try
        {
            // Idempotent: cùng nguồn (ReferenceType+ReferenceId) → bỏ qua (tránh spam khi NATS redelivery).
            if (!string.IsNullOrWhiteSpace(referenceType) && referenceId.HasValue && referenceId.Value != Guid.Empty)
            {
                bool exists = await _db.TenantNotifications
                    .IgnoreQueryFilters()
                    .AnyAsync(n => n.TenantId == tenantId
                                && n.ReferenceType == referenceType
                                && n.ReferenceId == referenceId, ct);
                if (exists) return;
            }

            var notification = new TenantNotification(tenantId, category, title, body, actionUrl, referenceType, referenceId, severity);
            _db.TenantNotifications.Add(notification);
            await _db.SaveChangesAsync(ct);
            await PublishCreatedAsync("owner", tenantId.Value, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "NotificationCenterService: failed to create owner notification for tenant {TenantId} ({Category})", tenantId.Value, category);
        }
    }

    public async Task<List<TenantNotification>> ListForOwnerAsync(
        TenantId tenantId,
        NotificationCategory? category = null,
        bool unreadOnly = false,
        int take = 100,
        CancellationToken ct = default)
    {
        try
        {
            var query = _db.TenantNotifications
                .IgnoreQueryFilters()
                .AsNoTracking()
                .Where(n => n.TenantId == tenantId);

            if (category.HasValue)
                query = query.Where(n => n.Category == category.Value);
            if (unreadOnly)
                query = query.Where(n => !n.IsRead);

            return await query
                .OrderByDescending(n => n.CreatedAt)
                .Take(take)
                .ToListAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "NotificationCenterService: failed to list owner notifications for tenant {TenantId}", tenantId.Value);
            return new List<TenantNotification>();
        }
    }

    public async Task<int> UnreadCountForOwnerAsync(TenantId tenantId, CancellationToken ct = default)
    {
        try
        {
            return await _db.TenantNotifications
                .IgnoreQueryFilters()
                .AsNoTracking()
                .CountAsync(n => n.TenantId == tenantId && !n.IsRead, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "NotificationCenterService: failed to count unread owner notifications for tenant {TenantId}", tenantId.Value);
            return 0;
        }
    }

    public async Task MarkOwnerReadAsync(Guid notificationId, TenantId tenantId, CancellationToken ct = default)
    {
        try
        {
            var notification = await _db.TenantNotifications
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(n => n.Id == notificationId && n.TenantId == tenantId, ct);
            if (notification is null) return;
            notification.MarkAsRead();
            await _db.SaveChangesAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "NotificationCenterService: failed to mark owner notification {Id} read", notificationId);
        }
    }

    public async Task MarkAllOwnerReadAsync(TenantId tenantId, CancellationToken ct = default)
    {
        try
        {
            var unread = await _db.TenantNotifications
                .IgnoreQueryFilters()
                .Where(n => n.TenantId == tenantId && !n.IsRead)
                .ToListAsync(ct);
            foreach (var n in unread)
                n.MarkAsRead();
            if (unread.Count > 0)
                await _db.SaveChangesAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "NotificationCenterService: failed to mark all owner notifications read for tenant {TenantId}", tenantId.Value);
        }
    }

    // ── Khách ──────────────────────────────────────────────────────────────

    public async Task CreateForCustomerAsync(
        Guid customerId,
        NotificationCategory category,
        string title,
        string body,
        string? actionUrl = null,
        string? referenceType = null,
        Guid? referenceId = null,
        CancellationToken ct = default)
    {
        try
        {
            // Idempotent theo nguồn (ReferenceType+ReferenceId) — tránh trùng khi redelivery.
            if (!string.IsNullOrWhiteSpace(referenceType) && referenceId.HasValue && referenceId.Value != Guid.Empty)
            {
                bool exists = await _db.PushNotificationDeliveries
                    .IgnoreQueryFilters()
                    .AnyAsync(d => d.CustomerId == customerId
                                && d.ReferenceType == referenceType
                                && d.ReferenceId == referenceId, ct);
                if (exists) return;
            }

            var delivery = new PushNotificationDelivery(
                new TenantId(Guid.Empty),
                customerId,
                null,
                actionUrl,
                category,
                title,
                body,
                referenceType,
                referenceId);
            _db.PushNotificationDeliveries.Add(delivery);
            await _db.SaveChangesAsync(ct);
            await PublishCreatedAsync("customer", null, customerId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "NotificationCenterService: failed to create customer notification for {CustomerId} ({Category})", customerId, category);
        }
    }

    public async Task<List<PushNotificationDelivery>> ListForCustomerAsync(Guid customerId, int take = 100, CancellationToken ct = default)
    {
        try
        {
            // Dedupe theo NotificationId — một thông báo có thể có nhiều delivery (nhiều subscription)
            // → lấy mới nhất mỗi NotificationId, sắp giảm dần CreatedAt.
            var rows = await _db.PushNotificationDeliveries
                .IgnoreQueryFilters()
                .AsNoTracking()
                .Where(d => d.CustomerId == customerId)
                .OrderByDescending(d => d.CreatedAt)
                .Take(take * 3)
                .ToListAsync(ct);

            return rows
                .GroupBy(d => d.NotificationId)
                .Select(g => g.First())
                .OrderByDescending(d => d.CreatedAt)
                .Take(take)
                .ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "NotificationCenterService: failed to list customer notifications for {CustomerId}", customerId);
            return new List<PushNotificationDelivery>();
        }
    }

    public async Task<int> UnreadCountForCustomerAsync(Guid customerId, CancellationToken ct = default)
    {
        try
        {
            var rows = await _db.PushNotificationDeliveries
                .IgnoreQueryFilters()
                .AsNoTracking()
                .Where(d => d.CustomerId == customerId && !d.IsRead)
                .OrderByDescending(d => d.CreatedAt)
                .Take(1000)
                .ToListAsync(ct);

            // Dedupe theo NotificationId (nhiều subscription → nhiều row cùng notification)
            return rows.GroupBy(d => d.NotificationId).Count();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "NotificationCenterService: failed to count unread customer notifications for {CustomerId}", customerId);
            return 0;
        }
    }

    public async Task MarkCustomerReadAsync(Guid notificationId, Guid customerId, CancellationToken ct = default)
    {
        try
        {
            var rows = await _db.PushNotificationDeliveries
                .IgnoreQueryFilters()
                .Where(d => d.CustomerId == customerId && d.NotificationId == notificationId)
                .ToListAsync(ct);
            foreach (var row in rows)
                row.MarkAsRead();
            if (rows.Count > 0)
                await _db.SaveChangesAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "NotificationCenterService: failed to mark customer notification {Id} read", notificationId);
        }
    }

    public async Task MarkAllCustomerReadAsync(Guid customerId, CancellationToken ct = default)
    {
        try
        {
            var unread = await _db.PushNotificationDeliveries
                .IgnoreQueryFilters()
                .Where(d => d.CustomerId == customerId && !d.IsRead)
                .ToListAsync(ct);
            foreach (var row in unread)
                row.MarkAsRead();
            if (unread.Count > 0)
                await _db.SaveChangesAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "NotificationCenterService: failed to mark all customer notifications read for {CustomerId}", customerId);
        }
    }

    /// <summary>Publish sự kiện thông báo mới → Gateway NotificationHubNotifier push badge realtime. Fail-safe.</summary>
    private async Task PublishCreatedAsync(string audience, Guid? tenantId, Guid? customerId)
    {
        if (_natsPublisher is null) return;
        try
        {
            var payload = new
            {
                Audience = audience,
                TenantId = tenantId,
                CustomerId = customerId,
                Timestamp = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffffffZ")
            };
            byte[] bytes = System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(payload);
            await _natsPublisher.PublishAsync(CreatedSubject, bytes);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "NotificationCenterService: failed to publish notification.created — badge realtime may lag (poll fallback)");
        }
    }
}
