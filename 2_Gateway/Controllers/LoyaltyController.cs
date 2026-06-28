using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace VanAn.Gateway.Controllers
{
    /// <summary>
    /// W17-T2: Gateway forward controller for Loyalty Dashboard.
    /// Forwards X-Customer-Token from KhachLink to ShopERP's LoyaltyController.
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
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
                return StatusCode((int)response.StatusCode, content);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error forwarding GetMyLoyalty to ShopERP");
                return StatusCode(500, new { error = "Internal server error" });
            }
        }
    }
}
