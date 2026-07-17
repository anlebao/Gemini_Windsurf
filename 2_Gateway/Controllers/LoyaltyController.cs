using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace VanAn.Gateway.Controllers
{
    /// <summary>
    /// W17-T2: Gateway forward controller for Loyalty Dashboard.
    /// Forwards X-Customer-Token from KhachLink to ShopERP's LoyaltyController.
    /// Tiered Auth Phase 2: adds POST /api/loyalty/redeem forwarding.
    /// </summary>
    [ApiController]
    [Route("api/loyalty")]
    [AllowAnonymous]
    public class LoyaltyController(IHttpClientFactory httpClientFactory, ILogger<LoyaltyController> logger) : ControllerBase
    {
        private readonly IHttpClientFactory _httpClientFactory = httpClientFactory;
        private readonly ILogger<LoyaltyController> _logger = logger;

        [HttpGet("my")]
        public async Task<IActionResult> GetMyLoyalty()
        {
            try
            {
                var client = _httpClientFactory.CreateClient("shoperp");
                var reqMsg = new HttpRequestMessage(HttpMethod.Get, "/api/loyalty/my");
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
                _logger.LogError(ex, "Error forwarding GetMyLoyalty to ShopERP");
                return StatusCode(500, new { error = "Internal server error" });
            }
        }

        /// <summary>
        /// Tiered Auth Phase 2: Forward POST /api/loyalty/redeem to ShopERP.
        /// Passes X-Customer-Token header and request body through.
        /// </summary>
        [HttpPost("redeem")]
        public async Task<IActionResult> Redeem()
        {
            try
            {
                var client = _httpClientFactory.CreateClient("shoperp");
                var reqMsg = new HttpRequestMessage(HttpMethod.Post, "/api/loyalty/redeem");
                if (Request.Headers.TryGetValue("X-Customer-Token", out var token))
                    reqMsg.Headers.Add("X-Customer-Token", token.ToString());

                if (Request.ContentLength > 0)
                {
                    reqMsg.Content = new StreamContent(Request.Body);
                    reqMsg.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(
                        Request.ContentType ?? "application/json");
                }

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
                _logger.LogError(ex, "Error forwarding Redeem to ShopERP");
                return StatusCode(500, new { error = "Internal server error" });
            }
        }
    }
}
