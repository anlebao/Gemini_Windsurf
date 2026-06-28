using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace VanAn.Gateway.Controllers
{
    /// <summary>W17-T4: Gateway forward controller for Push Notification subscription.</summary>
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
    }
}
