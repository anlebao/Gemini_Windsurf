using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VanAn.ShopERP.Services;

namespace VanAn.ShopERP.Controllers
{
    /// <summary>
    /// W17-T4: Push Notification subscription endpoint.
    /// Note: Customer.PushSubscriptionJson field deferred to Wave 18 — logs subscription only.
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    [AllowAnonymous]
    public class NotificationsController(
        ICustomerTokenService customerTokenService,
        ILogger<NotificationsController> logger) : ControllerBase
    {
        private readonly ICustomerTokenService _customerTokenService = customerTokenService;
        private readonly ILogger<NotificationsController> _logger = logger;

        /// <summary>POST /api/notifications/push/subscribe — log push subscription (Wave 18 will persist).</summary>
        [HttpPost("push/subscribe")]
        public IActionResult Subscribe(
            [FromHeader(Name = "X-Customer-Token")] string? token,
            [FromBody] PushSubscriptionRequest request)
        {
            var customerId = _customerTokenService.ValidateToken(token ?? "");
            if (!customerId.HasValue)
                return Unauthorized(new { error = "Token không hợp lệ." });

            // W17: Log only — Customer.PushSubscriptionJson field pending Wave 18 approval
            _logger.LogInformation(
                "Push subscription received for customer {CustomerId}, endpoint: {Endpoint}",
                customerId.Value,
                request.Endpoint?.Length > 20 ? request.Endpoint[..20] + "..." : request.Endpoint);

            return Ok(new { message = "Đã đăng ký nhận thông báo." });
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
