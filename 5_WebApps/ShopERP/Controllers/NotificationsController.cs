using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VanAn.ShopERP.Services;
using VanAn.CoreHub.Domain.Repositories;
using VanAn.Shared.Domain;

namespace VanAn.ShopERP.Controllers
{
    /// <summary>
    /// W17-T4: Push Notification subscription endpoint.
    /// Wave 9: Now persists subscription to PushSubscription table (separate table per user decision).
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    [AllowAnonymous]
    public class NotificationsController(
        ICustomerTokenService customerTokenService,
        IPushSubscriptionRepository pushSubscriptionRepository,
        ILogger<NotificationsController> logger) : ControllerBase
    {
        private readonly ICustomerTokenService _customerTokenService = customerTokenService;
        private readonly IPushSubscriptionRepository _pushSubscriptionRepository = pushSubscriptionRepository;
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
                // Convert push subscription request to JSON format
                var subscriptionJson = System.Text.Json.JsonSerializer.Serialize(request);
                var userAgent = Request.Headers["User-Agent"].ToString();

                // Use upsert pattern (get or create)
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
}
