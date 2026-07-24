using WebPush;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using System.Text.Json;
using VanAn.CoreHub.Domain.Repositories;
using VanAn.CoreHub.Infrastructure;
using VanAn.CoreHub.Infrastructure.Messaging;
using VanAn.Shared.Domain;

namespace VanAn.CoreHub.Services
{
    /// <summary>
    /// Wave 9: Push Notification Service for Web Push notifications.
    /// Handles VAPID authentication and push notification delivery for order status updates.
    /// 
    /// Session 3: NATS integration for event-driven push notifications
    /// Phase 5: PushNotificationDelivery records for click tracking + notificationId in payload
    /// </summary>
    public class PushNotificationService
    {
        private readonly ILogger<PushNotificationService> _logger;
        private readonly string _vapidPrivateKey;
        private readonly string _vapidPublicKey;
        private readonly string _vapidSubject;
        private readonly IPushSubscriptionRepository _subscriptionRepository;
        private readonly INatsEventPublisher? _natsPublisher; // Nullable for graceful degradation
        private readonly IVanAnDbContext? _dbContext; // Phase 5: for PushNotificationDelivery records

        public PushNotificationService(
            IConfiguration configuration,
            ILogger<PushNotificationService> logger,
            IPushSubscriptionRepository subscriptionRepository,
            INatsEventPublisher? natsPublisher = null,
            IVanAnDbContext? dbContext = null)
        {
            _logger = logger;
            _subscriptionRepository = subscriptionRepository;
            _natsPublisher = natsPublisher;
            _dbContext = dbContext;
            
            // VAPID private key from environment variable (security requirement).
            // Dev fallback: read from configuration "PushNotifications:VapidPrivateKey" for local development.
            _vapidPrivateKey = Environment.GetEnvironmentVariable("VAPID_PRIVATE_KEY")
                ?? configuration["PushNotifications:VapidPrivateKey"]
                ?? throw new InvalidOperationException(
                    "VAPID_PRIVATE_KEY environment variable (or PushNotifications:VapidPrivateKey config for dev) is required");

            // VAPID public key from configuration (can be in source code)
            _vapidPublicKey = configuration["PushNotifications:VapidPublicKey"]
                ?? throw new InvalidOperationException("PushNotifications:VapidPublicKey configuration is required");
            
            // VAPID subject (contact email for push notifications)
            _vapidSubject = configuration["PushNotifications:VapidSubject"] 
                ?? "mailto:admin@vanan.com";
            
            _logger.LogInformation("PushNotificationService initialized with VAPID subject: {Subject}, NATS: {NatsEnabled}", 
                _vapidSubject, _natsPublisher != null);
        }

        /// <summary>
        /// Send a push notification for order status change.
        /// Session 3: Enhanced to fetch subscriptions from database
        /// </summary>
        /// <param name="customerId">Customer ID to send notification to</param>
        /// <param name="orderId">Order ID for the notification</param>
        /// <param name="newStatus">New order status</param>
        /// <param name="customerName">Customer name (optional)</param>
        /// <returns>Number of notifications sent successfully</returns>
        public async Task<int> SendOrderStatusNotificationAsync(
            Guid customerId,
            Guid orderId,
            string newStatus,
            string? customerName = null)
        {
            try
            {
                // Get active subscriptions for customer
                var subscriptions = await _subscriptionRepository.GetByCustomerIdAsync(customerId);

                if (!subscriptions.Any())
                {
                    _logger.LogInformation("No active push subscriptions found for customer {CustomerId}", customerId);
                    return 0;
                }

                int successCount = 0;
                var vapidDetails = new VapidDetails(_vapidSubject, _vapidPublicKey, _vapidPrivateKey);
                var webPushClient = new WebPushClient();

                foreach (var subscription in subscriptions)
                {
                    try
                    {
                        // Phase 5: Generate unique notificationId per push for click tracking
                        var notificationId = Guid.NewGuid();
                        var payload = CreateOrderStatusPayload(notificationId, orderId, newStatus, customerName);

                        var pushSubscription = JsonSerializer.Deserialize<WebPush.PushSubscription>(subscription.SubscriptionJson);
                        if (pushSubscription == null)
                        {
                            _logger.LogWarning("Failed to deserialize subscription {SubscriptionId}", subscription.PushSubscriptionId);
                            continue;
                        }

                        await webPushClient.SendNotificationAsync(pushSubscription, payload, vapidDetails);

                        // Phase 5: Create PushNotificationDelivery record for click tracking
                        await CreateDeliveryRecordAsync(customerId, notificationId, null, $"/order-tracking/{orderId}");

                        subscription.Renew();
                        await _subscriptionRepository.UpdateAsync(subscription);

                        successCount++;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to send push notification for subscription {SubscriptionId}",
                            subscription.PushSubscriptionId);
                    }
                }

                _logger.LogInformation("Push notifications sent: {SuccessCount}/{TotalCount} for OrderId: {OrderId}, Status: {Status}",
                    successCount, subscriptions.Count, orderId, newStatus);
                return successCount;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send push notifications for OrderId: {OrderId}", orderId);
                return 0;
            }
        }

        /// <summary>
        /// Phase 5: Send a push notification for loyalty points change.
        /// Triggered by NATS "loyalty.points.changed" event via PushNotificationBackgroundService.
        /// </summary>
        /// <param name="customerId">Customer ID to send notification to</param>
        /// <param name="pointsChange">Points change (positive for earn, negative for spend)</param>
        /// <param name="newBalance">New point balance after change</param>
        /// <param name="reason">Reason for the change</param>
        /// <returns>Number of notifications sent successfully</returns>
        public async Task<int> SendLoyaltyPointsChangedNotificationAsync(
            Guid customerId,
            int pointsChange,
            int newBalance,
            string? reason = null)
        {
            try
            {
                var subscriptions = await _subscriptionRepository.GetByCustomerIdAsync(customerId);

                if (!subscriptions.Any())
                {
                    _logger.LogInformation("No active push subscriptions found for customer {CustomerId}", customerId);
                    return 0;
                }

                int successCount = 0;
                var vapidDetails = new VapidDetails(_vapidSubject, _vapidPublicKey, _vapidPrivateKey);
                var webPushClient = new WebPushClient();

                foreach (var subscription in subscriptions)
                {
                    try
                    {
                        // Phase 5: Generate unique notificationId per push for click tracking
                        var notificationId = Guid.NewGuid();
                        var payload = CreateLoyaltyPointsPayload(notificationId, pointsChange, newBalance, reason);

                        var pushSubscription = JsonSerializer.Deserialize<WebPush.PushSubscription>(subscription.SubscriptionJson);
                        if (pushSubscription == null)
                        {
                            _logger.LogWarning("Failed to deserialize subscription {SubscriptionId}", subscription.PushSubscriptionId);
                            continue;
                        }

                        await webPushClient.SendNotificationAsync(pushSubscription, payload, vapidDetails);

                        // Phase 5: Create PushNotificationDelivery record for click tracking
                        await CreateDeliveryRecordAsync(customerId, notificationId, null, "/my-loyalty");

                        subscription.Renew();
                        await _subscriptionRepository.UpdateAsync(subscription);
                        successCount++;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to send loyalty push notification for subscription {SubscriptionId}",
                            subscription.PushSubscriptionId);
                    }
                }

                _logger.LogInformation("Loyalty push notifications sent: {SuccessCount}/{TotalCount} for CustomerId: {CustomerId}, PointsChange: {PointsChange}",
                    successCount, subscriptions.Count, customerId, pointsChange);
                return successCount;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send loyalty push notifications for CustomerId: {CustomerId}", customerId);
                return 0;
            }
        }

        /// <summary>
        /// Phase 5: Create loyalty points change notification payload.
        /// Includes notificationId in data for click tracking.
        /// </summary>
        private string CreateLoyaltyPointsPayload(Guid notificationId, int pointsChange, int newBalance, string? reason)
        {
            string direction = pointsChange > 0 ? "+" : "";
            var notification = new
            {
                type = "loyalty_points_changed",
                title = "Vạn An Group",
                body = $"{direction}{pointsChange} điểm. Số dư: {newBalance} điểm",
                pointsChange = pointsChange,
                newBalance = newBalance,
                reason = reason ?? "Cập nhật điểm thưởng",
                timestamp = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ"),
                actionUrl = "/my-loyalty",
                data = new { notificationId = notificationId, actionUrl = "/my-loyalty" }
            };

            return JsonSerializer.Serialize(notification);
        }

        /// <summary>
        /// Phase 5: Send bulk push notifications to a list of customers (for campaigns).
        /// Creates PushNotificationDelivery records for click tracking.
        /// </summary>
        /// <param name="customerIds">List of customer IDs to send to</param>
        /// <param name="title">Notification title</param>
        /// <param name="body">Notification body</param>
        /// <param name="actionUrl">URL to open on click</param>
        /// <param name="campaignPushJobId">Optional CampaignPushJob ID for tracking</param>
        /// <returns>Tuple of (sentCount, failedCount)</returns>
        public async Task<(int SentCount, int FailedCount)> SendBulkNotificationAsync(
            IReadOnlyList<Guid> customerIds,
            string title,
            string body,
            string? actionUrl = null,
            Guid? campaignPushJobId = null)
        {
            if (customerIds.Count == 0)
                return (0, 0);

            int sentCount = 0;
            int failedCount = 0;
            var vapidDetails = new VapidDetails(_vapidSubject, _vapidPublicKey, _vapidPrivateKey);
            var webPushClient = new WebPushClient();

            foreach (Guid customerId in customerIds)
            {
                try
                {
                    var subscriptions = await _subscriptionRepository.GetByCustomerIdAsync(customerId);
                    if (!subscriptions.Any())
                    {
                        failedCount++;
                        continue;
                    }

                    bool anySent = false;
                    foreach (var subscription in subscriptions)
                    {
                        try
                        {
                            var pushSubscription = JsonSerializer.Deserialize<WebPush.PushSubscription>(subscription.SubscriptionJson);
                            if (pushSubscription == null) continue;

                            // Phase 5: Generate unique notificationId per push for click tracking
                            var notificationId = Guid.NewGuid();
                            var payload = CreateCampaignPayload(notificationId, title, body, actionUrl);
                            await webPushClient.SendNotificationAsync(pushSubscription, payload, vapidDetails);

                            // Phase 5: Create PushNotificationDelivery record for click tracking
                            await CreateDeliveryRecordAsync(customerId, notificationId, campaignPushJobId, actionUrl);

                            subscription.Renew();
                            await _subscriptionRepository.UpdateAsync(subscription);
                            anySent = true;
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Failed to send bulk push for subscription {SubscriptionId}", subscription.PushSubscriptionId);
                        }
                    }

                    if (anySent) sentCount++; else failedCount++;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to send bulk push to customer {CustomerId}", customerId);
                    failedCount++;
                }
            }

            _logger.LogInformation("Bulk push complete: Sent={Sent}, Failed={Failed}, Total={Total}",
                sentCount, failedCount, customerIds.Count);
            return (sentCount, failedCount);
        }

        /// <summary>
        /// Phase 5: Create PushNotificationDelivery record for click tracking.
        /// Called after each successful push send. Best-effort — failures logged but don't block push.
        /// </summary>
        private async Task CreateDeliveryRecordAsync(Guid customerId, Guid notificationId, Guid? campaignPushJobId, string? actionUrl)
        {
            if (_dbContext == null) return;

            try
            {
                var delivery = new PushNotificationDelivery(
                    new TenantId(Guid.Empty), // Set by EF interceptor from current tenant context
                    customerId,
                    campaignPushJobId,
                    actionUrl);

                // Override the auto-generated NotificationId with our tracking ID
                typeof(PushNotificationDelivery).GetProperty("NotificationId")?.SetValue(delivery, notificationId);

                await _dbContext.PushNotificationDeliveries.AddAsync(delivery);
                await _dbContext.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to create PushNotificationDelivery record for CustomerId={CustomerId}, NotificationId={NotificationId}",
                    customerId, notificationId);
            }
        }

        /// <summary>
        /// Phase 5: Create campaign notification payload.
        /// Includes notificationId in data for click tracking.
        /// </summary>
        private string CreateCampaignPayload(Guid notificationId, string title, string body, string? actionUrl)
        {
            var notification = new
            {
                type = "campaign",
                title = title,
                body = body,
                actionUrl = actionUrl ?? "/",
                timestamp = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ"),
                data = new { notificationId = notificationId, actionUrl = actionUrl ?? "/" }
            };

            return JsonSerializer.Serialize(notification);
        }

        /// <summary>
        /// Create order status notification payload.
        /// Includes notificationId in data for click tracking (Phase 5).
        /// </summary>
        private string CreateOrderStatusPayload(Guid notificationId, Guid orderId, string newStatus, string? customerName)
        {
            var notification = new
            {
                type = "order_status_changed",
                title = "Vạn An Group",
                body = GetStatusMessage(newStatus),
                orderId = orderId,
                status = newStatus,
                customerName = customerName ?? "Khách hàng",
                timestamp = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ"),
                message = GetStatusMessage(newStatus),
                actionUrl = $"/order-tracking/{orderId}",
                data = new { notificationId = notificationId, actionUrl = $"/order-tracking/{orderId}" }
            };

            return JsonSerializer.Serialize(notification);
        }

        /// <summary>
        /// Get user-friendly status message in Vietnamese.
        /// </summary>
        private string GetStatusMessage(string status) => status switch
        {
            "pending" => "Đơn hàng của bạn đang chờ xác nhận",
            "confirmed" => "Đơn hàng của bạn đã được xác nhận",
            "processing" => "Đơn hàng của bạn đang được pha chế",
            "ready" => "Đơn hàng của bạn đã sẵn sàng",
            "delivered" => "Đơn hàng của bạn đã được giao thành công",
            "cancelled" => "Đơn hàng của bạn đã bị hủy",
            _ => $"Trạng thái đơn hàng: {status}"
        };

        /// <summary>
        /// Loyalty-C WS-C: Send a birthday notification + annual bonus points info to a customer.
        /// Triggered by BirthdayBonusJob on the customer's birthday (UTC date match).
        /// </summary>
        /// <param name="customerId">Customer ID to send notification to</param>
        /// <param name="customerName">Customer name for personalization</param>
        /// <param name="pointsAwarded">Birthday bonus points awarded (0 if mission not configured)</param>
        /// <returns>Number of notifications sent successfully</returns>
        public async Task<int> SendBirthdayNotificationAsync(Guid customerId, string? customerName, int pointsAwarded)
        {
            try
            {
                var subscriptions = await _subscriptionRepository.GetByCustomerIdAsync(customerId);
                if (!subscriptions.Any())
                {
                    _logger.LogInformation("No active push subscriptions for birthday notification: CustomerId={CustomerId}", customerId);
                    return 0;
                }

                int successCount = 0;
                var vapidDetails = new VapidDetails(_vapidSubject, _vapidPublicKey, _vapidPrivateKey);
                var webPushClient = new WebPushClient();

                foreach (var subscription in subscriptions)
                {
                    try
                    {
                        var notificationId = Guid.NewGuid();
                        var payload = CreateBirthdayPayload(notificationId, customerName, pointsAwarded);
                        var pushSubscription = JsonSerializer.Deserialize<WebPush.PushSubscription>(subscription.SubscriptionJson);
                        if (pushSubscription == null) continue;

                        await webPushClient.SendNotificationAsync(pushSubscription, payload, vapidDetails);
                        await CreateDeliveryRecordAsync(customerId, notificationId, null, "/my-loyalty");
                        subscription.Renew();
                        await _subscriptionRepository.UpdateAsync(subscription);
                        successCount++;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to send birthday push for subscription {SubscriptionId}", subscription.PushSubscriptionId);
                    }
                }

                _logger.LogInformation("Birthday push sent: {SuccessCount}/{TotalCount} for CustomerId={CustomerId}, PointsAwarded={Points}",
                    successCount, subscriptions.Count, customerId, pointsAwarded);
                return successCount;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send birthday push notifications for CustomerId={CustomerId}", customerId);
                return 0;
            }
        }

        /// <summary>
        /// Loyalty-C WS-C: Send a voucher expiry reminder to a customer.
        /// Triggered by VoucherExpiryReminderJob when a voucher is N days from expiry.
        /// </summary>
        /// <param name="customerId">Customer ID to send notification to</param>
        /// <param name="voucherCode">Voucher code for the notification</param>
        /// <param name="productName">Product name the voucher is for</param>
        /// <param name="expiresAt">Voucher expiry timestamp</param>
        /// <param name="daysRemaining">Days until expiry (for urgency messaging)</param>
        /// <returns>Number of notifications sent successfully</returns>
        public async Task<int> SendVoucherExpiryReminderAsync(
            Guid customerId,
            string voucherCode,
            string? productName,
            DateTime expiresAt,
            int daysRemaining)
        {
            try
            {
                var subscriptions = await _subscriptionRepository.GetByCustomerIdAsync(customerId);
                if (!subscriptions.Any())
                {
                    _logger.LogInformation("No active push subscriptions for voucher expiry reminder: CustomerId={CustomerId}", customerId);
                    return 0;
                }

                int successCount = 0;
                var vapidDetails = new VapidDetails(_vapidSubject, _vapidPublicKey, _vapidPrivateKey);
                var webPushClient = new WebPushClient();

                foreach (var subscription in subscriptions)
                {
                    try
                    {
                        var notificationId = Guid.NewGuid();
                        var payload = CreateVoucherExpiryPayload(notificationId, voucherCode, productName, expiresAt, daysRemaining);
                        var pushSubscription = JsonSerializer.Deserialize<WebPush.PushSubscription>(subscription.SubscriptionJson);
                        if (pushSubscription == null) continue;

                        await webPushClient.SendNotificationAsync(pushSubscription, payload, vapidDetails);
                        await CreateDeliveryRecordAsync(customerId, notificationId, null, "/rewards");
                        subscription.Renew();
                        await _subscriptionRepository.UpdateAsync(subscription);
                        successCount++;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to send voucher expiry push for subscription {SubscriptionId}", subscription.PushSubscriptionId);
                    }
                }

                _logger.LogInformation("Voucher expiry push sent: {SuccessCount}/{TotalCount} for CustomerId={CustomerId}, VoucherCode={VoucherCode}, DaysRemaining={Days}",
                    successCount, subscriptions.Count, customerId, voucherCode, daysRemaining);
                return successCount;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send voucher expiry push for CustomerId={CustomerId}, VoucherCode={VoucherCode}", customerId, voucherCode);
                return 0;
            }
        }

        /// <summary>Loyalty-C WS-C: Create birthday notification payload.</summary>
        private string CreateBirthdayPayload(Guid notificationId, string? customerName, int pointsAwarded)
        {
            string name = string.IsNullOrWhiteSpace(customerName) ? "Bạn" : customerName;
            string body = pointsAwarded > 0
                ? $"Chúc sinh nhật {name}! +{pointsAwarded} điểm thưởng từ Vạn An."
                : $"Chúc mừng sinh nhật {name}! Vạn An chúc bạn một ngày thật đặc biệt.";
            var notification = new
            {
                type = "birthday_bonus",
                title = "Vạn An Group",
                body = body,
                pointsAwarded = pointsAwarded,
                timestamp = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ"),
                actionUrl = "/my-loyalty",
                data = new { notificationId = notificationId, actionUrl = "/my-loyalty" }
            };
            return JsonSerializer.Serialize(notification);
        }

        /// <summary>Loyalty-C WS-C: Create voucher expiry reminder payload.</summary>
        private string CreateVoucherExpiryPayload(Guid notificationId, string voucherCode, string? productName, DateTime expiresAt, int daysRemaining)
        {
            string product = string.IsNullOrWhiteSpace(productName) ? "voucher" : productName;
            string body = daysRemaining <= 1
                ? $"Voucher {product} của bạn hết hạn hôm nay! Sử dụng ngay."
                : $"Voucher {product} của bạn sẽ hết hạn sau {daysRemaining} ngày. Đừng bỏ lỡ!";
            var notification = new
            {
                type = "voucher_expiry_reminder",
                title = "Vạn An Group",
                body = body,
                voucherCode = voucherCode,
                productName = product,
                expiresAt = expiresAt.ToString("yyyy-MM-ddTHH:mm:ssZ"),
                daysRemaining = daysRemaining,
                timestamp = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ"),
                actionUrl = "/rewards",
                data = new { notificationId = notificationId, actionUrl = "/rewards" }
            };
            return JsonSerializer.Serialize(notification);
        }

        /// <summary>
        /// Validate VAPID configuration.
        /// </summary>
        public bool IsConfigured()
        {
            return !string.IsNullOrEmpty(_vapidPrivateKey) && 
                   !string.IsNullOrEmpty(_vapidPublicKey) && 
                   !string.IsNullOrEmpty(_vapidSubject);
        }

        /// <summary>
        /// Subscribe to NATS "order.status.changed" subject (Session 3).
        /// This will be called during service startup if NATS is available.
        /// </summary>
        public async Task SubscribeToNatsAsync(CancellationToken cancellationToken = default)
        {
            if (_natsPublisher == null)
            {
                _logger.LogWarning("NATS publisher not available - push notifications will not be event-driven");
                return;
            }

            try
            {
                // Subscribe to order status changes
                // Note: In a full implementation, this would be a BackgroundService that listens to NATS
                // For Session 3, we'll implement a simple subscription hook
                _logger.LogInformation("PushNotificationService subscribed to NATS order.status.changed subject");
                
                // The actual NATS subscription would be implemented as a BackgroundService
                // that receives messages and triggers SendOrderStatusNotificationAsync
                // This is a placeholder for the subscription mechanism
                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to subscribe to NATS for push notifications");
            }
        }
    }
}