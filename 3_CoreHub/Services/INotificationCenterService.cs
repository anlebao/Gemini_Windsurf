using VanAn.Shared.Domain;

namespace VanAn.CoreHub.Services;

/// <summary>
/// Notification Hub (2026-10-15, user directive): tạo + đọc thông báo.
/// 2 đầu ra: OWNER (TenantNotification — ShopERP bell/trang /notifications)
/// và KHÁCH (PushNotificationDelivery — inbox in-app KhachLink, đồng thời là vết web push).
/// Fail-safe: mọi lỗi được log, KHÔNG bao giờ làm hỏng luồng nghiệp vụ gọi tới.
/// LƯU Ý: khác <see cref="INotificationService"/> (channel email/SMS/push cũ — W1-T4).
/// </summary>
public interface INotificationCenterService
{
    // ── Owner (TenantNotification — PG) ────────────────────────────────────

    /// <summary>Tạo thông báo owner tenant. Idempotent theo (ReferenceType+ReferenceId) nếu cung cấp.</summary>
    Task CreateForOwnerAsync(
        TenantId tenantId,
        NotificationCategory category,
        string title,
        string body,
        string? actionUrl = null,
        string? referenceType = null,
        Guid? referenceId = null,
        TenantNotificationSeverity severity = TenantNotificationSeverity.Info,
        CancellationToken ct = default);

    /// <summary>Danh sách thông báo owner (mới nhất trước), paged + filter category/unreadOnly.</summary>
    Task<List<TenantNotification>> ListForOwnerAsync(
        TenantId tenantId,
        NotificationCategory? category = null,
        bool unreadOnly = false,
        int take = 100,
        CancellationToken ct = default);

    /// <summary>Số thông báo owner CHƯA đọc (badge).</summary>
    Task<int> UnreadCountForOwnerAsync(TenantId tenantId, CancellationToken ct = default);

    /// <summary>Đánh dấu 1 thông báo owner đã đọc (idempotent).</summary>
    Task MarkOwnerReadAsync(Guid notificationId, TenantId tenantId, CancellationToken ct = default);

    /// <summary>Đánh dấu TẤT CẢ thông báo owner của tenant đã đọc.</summary>
    Task MarkAllOwnerReadAsync(TenantId tenantId, CancellationToken ct = default);

    // ── Khách (PushNotificationDelivery — PG) ──────────────────────────────

    /// <summary>Tạo bản ghi inbox in-app cho khách (KHÔNG gửi web push — ghi inbox thuần).</summary>
    Task CreateForCustomerAsync(
        Guid customerId,
        NotificationCategory category,
        string title,
        string body,
        string? actionUrl = null,
        string? referenceType = null,
        Guid? referenceId = null,
        CancellationToken ct = default);

    /// <summary>Danh sách thông báo của khách (mới nhất trước, dedupe theo NotificationId).</summary>
    Task<List<PushNotificationDelivery>> ListForCustomerAsync(Guid customerId, int take = 100, CancellationToken ct = default);

    /// <summary>Số thông báo khách CHƯA đọc (badge KhachLink).</summary>
    Task<int> UnreadCountForCustomerAsync(Guid customerId, CancellationToken ct = default);

    /// <summary>Đánh dấu 1 thông báo khách đã đọc (idempotent — đọc cả nhóm cùng NotificationId).</summary>
    Task MarkCustomerReadAsync(Guid notificationId, Guid customerId, CancellationToken ct = default);

    /// <summary>Đánh dấu TẤT CẢ thông báo của khách đã đọc.</summary>
    Task MarkAllCustomerReadAsync(Guid customerId, CancellationToken ct = default);
}
