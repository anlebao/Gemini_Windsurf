using WebPush;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using System.Text.Json;

namespace VanAn.CoreHub.Services
{
    /// <summary>
    /// Wave 9: Push Notification Service for Web Push notifications.
    /// Handles VAPID authentication and push notification delivery for order status updates.
    /// 
    /// Session 2: Basic push infrastructure (NATS subscription to be added in Session 3)
    /// </summary>
    public class PushNotificationService
    {
        private readonly ILogger<PushNotificationService> _logger;
        private readonly string _vapidPrivateKey;
        private readonly string _vapidPublicKey;
        private readonly string _vapidSubject;

        public PushNotificationService(IConfiguration configuration, ILogger<PushNotificationService> logger)
        {
            _logger = logger;
            
            // VAPID private key from environment variable (security requirement)
            _vapidPrivateKey = Environment.GetEnvironmentVariable("VAPID_PRIVATE_KEY") 
                ?? throw new InvalidOperationException("VAPID_PRIVATE_KEY environment variable is required");
            
            // VAPID public key from configuration (can be in source code)
            _vapidPublicKey = configuration["PushNotifications:VapidPublicKey"] 
                ?? throw new InvalidOperationException("PushNotifications:VapidPublicKey configuration is required");
            
            // VAPID subject (contact email for push notifications)
            _vapidSubject = configuration["PushNotifications:VapidSubject"] 
                ?? "mailto:admin@vanan.com";
            
            _logger.LogInformation("PushNotificationService initialized with VAPID subject: {Subject}", _vapidSubject);
        }

        /// <summary>
        /// Send a push notification for order status change.
        /// Session 2: Basic implementation (NATS integration to be added in Session 3)
        /// </summary>
        /// <param name="subscriptionJson">Push subscription JSON from client</param>
        /// <param name="orderId">Order ID for the notification</param>
        /// <param name="newStatus">New order status</param>
        /// <param name="customerName">Customer name (optional)</param>
        /// <returns>True if notification sent successfully, false otherwise</returns>
        public async Task<bool> SendOrderStatusNotificationAsync(
            string subscriptionJson, 
            Guid orderId, 
            string newStatus, 
            string? customerName = null)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(subscriptionJson))
                {
                    _logger.LogWarning("Cannot send push notification: subscription JSON is empty");
                    return false;
                }

                // Deserialize push subscription
                var subscription = JsonSerializer.Deserialize<PushSubscription>(subscriptionJson);
                if (subscription == null)
                {
                    _logger.LogWarning("Failed to deserialize push subscription JSON");
                    return false;
                }

                // Create notification payload
                var payload = CreateOrderStatusPayload(orderId, newStatus, customerName);

                // Configure VAPID details
                var vapidDetails = new VapidDetails(_vapidSubject, _vapidPublicKey, _vapidPrivateKey);

                // Send push notification
                var webPushClient = new WebPushClient();
                await webPushClient.SendNotificationAsync(subscription, payload, vapidDetails);

                _logger.LogInformation("Push notification sent successfully for OrderId: {OrderId}, Status: {Status}", 
                    orderId, newStatus);
                return true;
            }
            catch (Exception ex)
            {
                // Log failure but don't throw - push notifications are best-effort
                _logger.LogError(ex, "Failed to send push notification for OrderId: {OrderId}", orderId);
                return false;
            }
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
    }
}