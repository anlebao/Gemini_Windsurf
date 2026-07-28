using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace VanAn.Gateway.Controllers
{
    /// <summary>
    /// W17-T4: Gateway forward controller for Push Notification subscription.
    /// Phase 5: Added DELETE push/subscribe (unsubscribe) + POST push/track (click tracking).
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    [AllowAnonymous]
    public class NotificationsController(IHttpClientFactory httpClientFactory, ILogger<NotificationsController> logger) : ControllerBase
    {
        private readonly IHttpClientFactory _httpClientFactory = httpClientFactory;
        private readonly ILogger<NotificationsController> _logger = logger;

        [HttpPost("push/subscribe")]
        public async Task<IActionResult> Subscribe([FromBody] object request)
        {
            try
            {
                var client = _httpClientFactory.CreateClient("shoperp");
                var reqMsg = new HttpRequestMessage(HttpMethod.Post, "/api/notifications/push/subscribe")
                {
                    Content = new StringContent(
                        System.Text.Json.JsonSerializer.Serialize(request),
                        System.Text.Encoding.UTF8,
                        "application/json")
                };
                if (Request.Headers.TryGetValue("X-Customer-Token", out var token))
                    reqMsg.Headers.Add("X-Customer-Token", token.ToString());

                var response = await client.SendAsync(reqMsg);
                var content = await response.Content.ReadAsStringAsync();
                return StatusCode((int)response.StatusCode, content);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error forwarding push subscribe to ShopERP");
                return StatusCode(500, new { error = "Internal server error" });
            }
        }

        /// <summary>
        /// Phase 5: DELETE /api/notifications/push/subscribe — forward unsubscribe to ShopERP.
        /// </summary>
        [HttpDelete("push/subscribe")]
        public async Task<IActionResult> Unsubscribe()
        {
            try
            {
                var client = _httpClientFactory.CreateClient("shoperp");
                var reqMsg = new HttpRequestMessage(HttpMethod.Delete, "/api/notifications/push/subscribe");
                if (Request.Headers.TryGetValue("X-Customer-Token", out var token))
                    reqMsg.Headers.Add("X-Customer-Token", token.ToString());

                var response = await client.SendAsync(reqMsg);
                var content = await response.Content.ReadAsStringAsync();
                return StatusCode((int)response.StatusCode, content);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error forwarding push unsubscribe to ShopERP");
                return StatusCode(500, new { error = "Internal server error" });
            }
        }

        /// <summary>
        /// GET /api/notifications/push/status — forward push subscription status check to ShopERP.
        /// Called by KhachLink Profile page on load to restore toggle state.
        /// </summary>
        [HttpGet("push/status")]
        public async Task<IActionResult> GetPushStatus()
        {
            try
            {
                var client = _httpClientFactory.CreateClient("shoperp");
                var reqMsg = new HttpRequestMessage(HttpMethod.Get, "/api/notifications/push/status");
                if (Request.Headers.TryGetValue("X-Customer-Token", out var token))
                    reqMsg.Headers.Add("X-Customer-Token", token.ToString());

                var response = await client.SendAsync(reqMsg);
                var content = await response.Content.ReadAsStringAsync();
                var contentType = response.Content.Headers.ContentType?.MediaType ?? "application/json";
                return new ContentResult
                {
                    StatusCode = (int)response.StatusCode,
                    Content = content,
                    ContentType = contentType
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error forwarding push status to ShopERP");
                return StatusCode(500, new { error = "Internal server error" });
            }
        }

        /// <summary>
        /// Phase 5: POST /api/notifications/push/track — forward click tracking to ShopERP.
        /// Called by service worker notificationclick event via navigator.sendBeacon.
        /// </summary>
        [HttpPost("push/track")]
        [AllowAnonymous]
        public async Task<IActionResult> TrackClick([FromBody] object request)
        {
            try
            {
                var client = _httpClientFactory.CreateClient("shoperp");
                var reqMsg = new HttpRequestMessage(HttpMethod.Post, "/api/notifications/push/track")
                {
                    Content = new StringContent(
                        System.Text.Json.JsonSerializer.Serialize(request),
                        System.Text.Encoding.UTF8,
                        "application/json")
                };

                var response = await client.SendAsync(reqMsg);
                var content = await response.Content.ReadAsStringAsync();
                return StatusCode((int)response.StatusCode, content);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error forwarding push track to ShopERP");
                return StatusCode(500, new { error = "Internal server error" });
            }
        }
    }
}
