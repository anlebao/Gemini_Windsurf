using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VanAn.ShopERP.Filters;
using VanAn.ShopERP.Services;
using VanAn.CoreHub.Domain.Repositories;
using VanAn.CoreHub.Infrastructure;
using VanAn.CoreHub.Services;
using VanAn.Shared.Domain;

namespace VanAn.ShopERP.Controllers
{
    /// <summary>
    /// W17-T4: Push Notification subscription endpoint.
    /// Wave 9: Now persists subscription to PushSubscription table (separate table per user decision).
    /// Phase 5: Added DELETE push/subscribe (unsubscribe) + POST push/track (click tracking).
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    [AllowAnonymous]
    [ResolveCustomerTenant]
    public class NotificationsController(
        ICustomerTokenService customerTokenService,
        IPushSubscriptionRepository pushSubscriptionRepository,
        IVanAnDbContext dbContext,
        ILogger<NotificationsController> logger) : ControllerBase
    {
        private readonly ICustomerTokenService _customerTokenService = customerTokenService;
        private readonly IPushSubscriptionRepository _pushSubscriptionRepository = pushSubscriptionRepository;
        private readonly IVanAnDbContext _dbContext = dbContext;
        private readonly ILogger<NotificationsController> _logger = logger;

        /// <summary>POST /api/notifications/push/subscribe — persist push subscription (Wave 9).</summary>
        [HttpPost("push/subscribe")]
        public async Task<IActionResult> Subscribe(
            [FromHeader(Name = "X-Customer-Token")] string? token,
            [FromBody] PushSubscriptionRequest request)
        {
            var customerId = _customerTokenService.ValidateToken(token ?? "");
            if (!customerId.HasValue)
                return Unauthorized(new { error = "Token không hợp lệ." });

            try
            {
                var subscriptionJson = System.Text.Json.JsonSerializer.Serialize(request);
                var userAgent = Request.Headers["User-Agent"].ToString();

                var subscription = await _pushSubscriptionRepository.GetOrCreateAsync(
                    customerId.Value,
                    subscriptionJson,
                    userAgent);

                _logger.LogInformation(
                    "Push subscription persisted for customer {CustomerId}, subscription ID: {SubscriptionId}",
                    customerId.Value,
                    subscription.PushSubscriptionId);

                return Ok(new {
                    message = "Đã đăng ký nhận thông báo.",
                    subscriptionId = subscription.PushSubscriptionId
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error persisting push subscription for customer {CustomerId}", customerId.Value);
                return StatusCode(500, new { error = "Lỗi khi đăng ký nhận thông báo." });
            }
        }

        /// <summary>
        /// Phase 5: DELETE /api/notifications/push/subscribe — unsubscribe (soft-delete all subscriptions for customer).
        /// Called when user toggles OFF push notifications in Profile.razor.
        /// </summary>
        [HttpDelete("push/subscribe")]
        public async Task<IActionResult> Unsubscribe(
            [FromHeader(Name = "X-Customer-Token")] string? token)
        {
            var customerId = _customerTokenService.ValidateToken(token ?? "");
            if (!customerId.HasValue)
                return Unauthorized(new { error = "Token không hợp lệ." });

            try
            {
                var subscriptions = await _pushSubscriptionRepository.GetByCustomerIdAsync(customerId.Value);
                int deleted = 0;
                foreach (var sub in subscriptions)
                {
                    bool ok = await _pushSubscriptionRepository.SoftDeleteAsync(sub.PushSubscriptionId);
                    if (ok) deleted++;
                }

                _logger.LogInformation(
                    "Unsubscribed customer {CustomerId} — removed {Count} subscription(s)",
                    customerId.Value, deleted);

                return Ok(new {
                    message = "Đã hủy đăng ký nhận thông báo.",
                    removedCount = deleted
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error unsubscribing customer {CustomerId}", customerId.Value);
                return StatusCode(500, new { error = "Lỗi khi hủy đăng ký nhận thông báo." });
            }
        }

        /// <summary>
        /// Phase 5: POST /api/notifications/push/track — record click on push notification.
        /// Called by service worker notificationclick event via navigator.sendBeacon.
        /// </summary>
        [HttpPost("push/track")]
        [AllowAnonymous]
        public async Task<IActionResult> TrackClick([FromBody] PushClickTrackRequest request)
        {
            if (request.NotificationId == Guid.Empty)
                return BadRequest(new { error = "NotificationId is required." });

            try
            {
                var delivery = await _dbContext.PushNotificationDeliveries
                    .FirstOrDefaultAsync(d => d.NotificationId == request.NotificationId);

                if (delivery == null)
                {
                    _logger.LogWarning("PushClickTrack: delivery not found for NotificationId={NotificationId}", request.NotificationId);
                    return NotFound(new { error = "Delivery record not found." });
                }

                delivery.MarkAsClicked();
                await _dbContext.SaveChangesAsync();

                _logger.LogInformation(
                    "PushClickTrack: marked delivery {DeliveryId} as clicked (NotificationId={NotificationId})",
                    delivery.Id, request.NotificationId);

                return Ok(new { message = "Click tracked." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error tracking push click for NotificationId={NotificationId}", request.NotificationId);
                return StatusCode(500, new { error = "Lỗi khi ghi nhận click." });
            }
        }
    }

    public class PushSubscriptionRequest
    {
        public string? Endpoint { get; set; }
        public PushKeys? Keys { get; set; }
    }

    public class PushKeys
    {
        public string? P256dh { get; set; }
        public string? Auth { get; set; }
    }

    /// <summary>
    /// Phase 5: Request body for POST /api/notifications/push/track (click tracking).
    /// </summary>
    public class PushClickTrackRequest
    {
        public Guid NotificationId { get; set; }
        public string? ActionUrl { get; set; }
    }
}
