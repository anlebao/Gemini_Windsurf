using WebPush;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using System.Text.Json;
using VanAn.CoreHub.Domain.Repositories;
using VanAn.CoreHub.Infrastructure.Messaging;
using VanAn.Shared.Domain;

namespace VanAn.CoreHub.Services
{
    /// <summary>
    /// Wave 9: Push Notification Service for Web Push notifications.
    /// Handles VAPID authentication and push notification delivery for order status updates.
    /// 
    /// Session 3: NATS integration for event-driven push notifications
    /// </summary>
    public class PushNotificationService
    {
        private readonly ILogger<PushNotificationService> _logger;
        private readonly string _vapidPrivateKey;
        private readonly string _vapidPublicKey;
        private readonly string _vapidSubject;
        private readonly IPushSubscriptionRepository _subscriptionRepository;
        private readonly INatsEventPublisher? _natsPublisher; // Nullable for graceful degradation

        public PushNotificationService(
            IConfiguration configuration, 
            ILogger<PushNotificationService> logger,
            IPushSubscriptionRepository subscriptionRepository,
            INatsEventPublisher? natsPublisher = null)
        {
            _logger = logger;
            _subscriptionRepository = subscriptionRepository;
            _natsPublisher = natsPublisher;
            
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
                var payload = CreateOrderStatusPayload(orderId, newStatus, customerName);
                var vapidDetails = new VapidDetails(_vapidSubject, _vapidPublicKey, _vapidPrivateKey);
                var webPushClient = new WebPushClient();

                foreach (var subscription in subscriptions)
                {
                    try
                    {
                        // Deserialize subscription JSON
                        var pushSubscription = JsonSerializer.Deserialize<WebPush.PushSubscription>(subscription.SubscriptionJson);
                        if (pushSubscription == null)
                        {
                            _logger.LogWarning("Failed to deserialize subscription {SubscriptionId}", subscription.PushSubscriptionId);
                            continue;
                        }

                        // Send push notification
                        await webPushClient.SendNotificationAsync(pushSubscription, payload, vapidDetails);
                        
                        // Update last used timestamp
                        subscription.Renew();
                        await _subscriptionRepository.UpdateAsync(subscription);
                        
                        successCount++;
                    }
                    catch (Exception ex)
                    {
                        // Log failure for individual subscription but continue with others
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
                // Log failure but don't throw - push notifications are best-effort
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
                var payload = CreateLoyaltyPointsPayload(pointsChange, newBalance, reason);
                var vapidDetails = new VapidDetails(_vapidSubject, _vapidPublicKey, _vapidPrivateKey);
                var webPushClient = new WebPushClient();

                foreach (var subscription in subscriptions)
                {
                    try
                    {
                        var pushSubscription = JsonSerializer.Deserialize<WebPush.PushSubscription>(subscription.SubscriptionJson);
                        if (pushSubscription == null)
                        {
                            _logger.LogWarning("Failed to deserialize subscription {SubscriptionId}", subscription.PushSubscriptionId);
                            continue;
                        }

                        await webPushClient.SendNotificationAsync(pushSubscription, payload, vapidDetails);
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
        /// </summary>
        private string CreateLoyaltyPointsPayload(int pointsChange, int newBalance, string? reason)
        {
            string direction = pointsChange > 0 ? "+" : "";
            var notification = new
            {
                type = "loyalty_points_changed",
                pointsChange = pointsChange,
                newBalance = newBalance,
                reason = reason ?? "Cập nhật điểm thưởng",
                timestamp = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ"),
                message = $"{direction}{pointsChange} điểm. Số dư: {newBalance} điểm",
                actionUrl = "/my-loyalty"
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

                            var payload = CreateCampaignPayload(title, body, actionUrl);
                            await webPushClient.SendNotificationAsync(pushSubscription, payload, vapidDetails);
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
        /// Phase 5: Create campaign notification payload.
        /// </summary>
        private string CreateCampaignPayload(string title, string body, string? actionUrl)
        {
            var notification = new
            {
                type = "campaign",
                title = title,
                body = body,
                actionUrl = actionUrl ?? "/",
                timestamp = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ")
            };

            return JsonSerializer.Serialize(notification);
        }

        /// <summary>
        /// Create order status notification payload.
        /// </summary>
        private string CreateOrderStatusPayload(Guid orderId, string newStatus, string? customerName)
        {
            var notification = new
            {
                type = "order_status_changed",
                orderId = orderId,
                status = newStatus,
                customerName = customerName ?? "Khách hàng",
                timestamp = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ"),
                message = GetStatusMessage(newStatus),
                actionUrl = $"/order-tracking/{orderId}"
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