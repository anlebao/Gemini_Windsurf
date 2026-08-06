using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace VanAn.Gateway.Controllers
{
    /// <summary>
    /// Loyalty-C WS-B: Gateway forward controller for Customer Profile endpoints.
    /// Forwards birthday entry, PWA install, and social share endpoints from KhachLink to ShopERP.
    /// All endpoints pass X-Customer-Token header through (authenticated via ShopERP token validation).
    /// </summary>
    [ApiController]
    [Route("api/customer-profile")]
    [AllowAnonymous]
    public class CustomerProfileController(IHttpClientFactory httpClientFactory, ILogger<CustomerProfileController> logger) : ControllerBase
    {
        private readonly IHttpClientFactory _httpClientFactory = httpClientFactory;
        private readonly ILogger<CustomerProfileController> _logger = logger;

        /// <summary>Forward POST /api/customer-profile/birthday to ShopERP.</summary>
        [HttpPost("birthday")]
        public async Task<IActionResult> SetBirthday()
        {
            return await ForwardPostWithToken("/api/customer-profile/birthday");
        }

        /// <summary>Forward POST /api/customer-profile/pwa-installed to ShopERP.</summary>
        [HttpPost("pwa-installed")]
        public async Task<IActionResult> MarkPwaInstalled()
        {
            return await ForwardPostWithToken("/api/customer-profile/pwa-installed");
        }

        /// <summary>Forward POST /api/customer-profile/share to ShopERP.</summary>
        [HttpPost("share")]
        public async Task<IActionResult> SubmitShare()
        {
            return await ForwardPostWithToken("/api/customer-profile/share");
        }

        private async Task<IActionResult> ForwardPostWithToken(string shopErpPath)
        {
            try
            {
                var client = _httpClientFactory.CreateClient("shoperp");
                var reqMsg = new HttpRequestMessage(HttpMethod.Post, shopErpPath);
                if (Request.Headers.TryGetValue("X-Customer-Token", out var token))
                    reqMsg.Headers.Add("X-Customer-Token", token.ToString());

                if (Request.ContentLength > 0)
                {
                    // FIX #106: Strip charset from Content-Type — MediaTypeHeaderValue rejects
                    // "; charset=utf-8" (same bug as Redemption/Loyalty controllers).
                    var mediaType = (Request.ContentType ?? "application/json").Split(';', StringSplitOptions.TrimEntries)[0];
                    reqMsg.Content = new StreamContent(Request.Body);
                    reqMsg.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(mediaType);
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
                _logger.LogError(ex, "Error forwarding {Path} to ShopERP", shopErpPath);
                return StatusCode(500, new { error = "Internal server error" });
            }
        }
    }
}
