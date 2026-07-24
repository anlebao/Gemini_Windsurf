using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace VanAn.Gateway.Controllers
{
    /// <summary>
    /// Loyalty-B: Gateway forward controller for Redemption system (customer-facing endpoints).
    /// Forwards X-Customer-Token from KhachLink to ShopERP's RedemptionController.
    /// Admin endpoints (catalog CRUD, fulfillment, history) accessed directly via ShopERP admin UI.
    /// Routes forwarded:
    ///   GET  /api/redemption/catalog/active       — browse active catalog
    ///   GET  /api/redemption/catalog/{id}         — catalog item detail
    ///   GET  /api/redemption/my/redemptions       — customer's redemption history
    ///   GET  /api/redemption/my/vouchers          — customer's vouchers
    ///   POST /api/redemption/redeem               — customer redeems catalog item
    /// </summary>
    [ApiController]
    [Route("api/redemption")]
    [AllowAnonymous]
    public class RedemptionController(IHttpClientFactory httpClientFactory, ILogger<RedemptionController> logger) : ControllerBase
    {
        private readonly IHttpClientFactory _httpClientFactory = httpClientFactory;
        private readonly ILogger<RedemptionController> _logger = logger;

        [HttpGet("catalog/active")]
        public Task<IActionResult> GetActiveCatalog() => ForwardAsync(HttpMethod.Get, "/api/redemption/catalog/active");

        [HttpGet("catalog/{id:guid}")]
        public Task<IActionResult> GetCatalogItem(Guid id) => ForwardAsync(HttpMethod.Get, $"/api/redemption/catalog/{id}");

        [HttpGet("my/redemptions")]
        public Task<IActionResult> GetMyRedemptions() => ForwardAsync(HttpMethod.Get, "/api/redemption/my/redemptions");

        [HttpGet("my/vouchers")]
        public Task<IActionResult> GetMyVouchers() => ForwardAsync(HttpMethod.Get, "/api/redemption/my/vouchers");

        [HttpPost("redeem")]
        public Task<IActionResult> Redeem() => ForwardAsync(HttpMethod.Post, "/api/redemption/redeem", includeBody: true);

        private async Task<IActionResult> ForwardAsync(HttpMethod method, string path, bool includeBody = false)
        {
            try
            {
                var client = _httpClientFactory.CreateClient("shoperp");
                var reqMsg = new HttpRequestMessage(method, path);

                // Forward customer token (both header formats — X-Customer-Token + Authorization Bearer)
                if (Request.Headers.TryGetValue("X-Customer-Token", out var token))
                    reqMsg.Headers.Add("X-Customer-Token", token.ToString());
                if (Request.Headers.TryGetValue("Authorization", out var auth))
                    reqMsg.Headers.Add("Authorization", auth.ToString());

                if (includeBody && Request.ContentLength > 0)
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
                _logger.LogError(ex, "Error forwarding {Method} {Path} to ShopERP", method, path);
                return StatusCode(500, new { error = "Internal server error" });
            }
        }
    }
}
