using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace VanAn.Gateway.Controllers
{
    /// <summary>
    /// KhachLink Profile Contextual UX v2: Gateway forward controller for
    /// /api/customer-context/* → ShopERP CustomerContextController.
    /// Forwards X-Customer-Token + X-Customer-Device-Id headers.
    /// </summary>
    [ApiController]
    [Route("api/customer-context")]
    [AllowAnonymous]
    public class CustomerContextController(IHttpClientFactory httpClientFactory, ILogger<CustomerContextController> logger) : ControllerBase
    {
        private readonly IHttpClientFactory _httpClientFactory = httpClientFactory;
        private readonly ILogger<CustomerContextController> _logger = logger;

        [HttpGet("me")]
        public Task<IActionResult> GetMyContext() => ForwardAsync(HttpMethod.Get, "/api/customer-context/me");

        [HttpPost("me/dismiss")]
        public Task<IActionResult> DismissCard() => ForwardAsync(HttpMethod.Post, "/api/customer-context/me/dismiss", includeBody: true);

        [HttpDelete("me/dismiss/{cardId}")]
        public Task<IActionResult> RestoreCard(string cardId) => ForwardAsync(HttpMethod.Delete, $"/api/customer-context/me/dismiss/{Uri.EscapeDataString(cardId)}");

        private async Task<IActionResult> ForwardAsync(HttpMethod method, string path, bool includeBody = false)
        {
            try
            {
                var client = _httpClientFactory.CreateClient("shoperp");
                var reqMsg = new HttpRequestMessage(method, path);

                if (Request.Headers.TryGetValue("X-Customer-Token", out var token))
                    reqMsg.Headers.Add("X-Customer-Token", token.ToString());
                if (Request.Headers.TryGetValue("X-Customer-Device-Id", out var deviceId))
                    reqMsg.Headers.Add("X-Customer-Device-Id", deviceId.ToString());

                if (includeBody && Request.ContentLength > 0)
                {
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
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
            {
                _logger.LogWarning(ex, "ShopERP unreachable while forwarding {Method} {Path}", method, path);
                return StatusCode(503, new { error = "Dịch vụ đang bảo trì, vui lòng thử lại sau." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error forwarding {Method} {Path} to ShopERP", method, path);
                return StatusCode(500, new { error = "Internal server error" });
            }
        }
    }
}
