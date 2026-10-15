namespace VanAn.CoreHub.Services;

/// <summary>
/// Multi-channel notification service (Wave 1 W1-T4).
/// Implementations: <see cref="CompositeNotificationService"/> (email/SMS direct, push no-op)
/// and <see cref="OutboxNotificationService"/> (outbox-backed, atomic persistence).
/// NOTIFICATION HUB (2026-10-15): inbox đã đọc/chưa đọc cho owner + khách dùng
/// <see cref="INotificationCenterService"/> (entity TenantNotification/PushNotificationDelivery) —
/// interface này GIỮ NGUYÊN (channel email/SMS/push cũ).
/// </summary>
public interface INotificationService
{
    /// <summary>Send email notification.</summary>
    Task<bool> SendEmailAsync(string email, string subject, string message);

    /// <summary>Send SMS notification.</summary>
    Task<bool> SendSMSAsync(string phoneNumber, string message);

    /// <summary>Send push notification to a customer.</summary>
    Task<bool> SendPushNotificationAsync(Guid customerId, string title, string message);
}
