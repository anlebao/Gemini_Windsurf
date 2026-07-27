using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace VanAn.Gateway.Controllers
{
    /// <summary>
    /// Loyalty-C WS-B/C: Gateway forward controller for Mission system (customer-facing endpoints).
    /// Forwards X-Customer-Token from KhachLink to ShopERP's MissionsController.
    /// Admin endpoints (mission CRUD) accessed directly via ShopERP admin UI (cookie auth).
    /// Routes forwarded:
    ///   GET /api/missions/active          — browse active missions
    ///   GET /api/missions/my/progress     — customer's mission progress
    ///   GET /api/missions/my/completions  — customer's completion history
    /// </summary>
    [ApiController]
    [Route("api/missions")]
    [AllowAnonymous]
    public class MissionsController(IHttpClientFactory httpClientFactory, ILogger<MissionsController> logger) : ControllerBase
    {
        private readonly IHttpClientFactory _httpClientFactory = httpClientFactory;
        private readonly ILogger<MissionsController> _logger = logger;

        [HttpGet("active")]
        public Task<IActionResult> GetActiveMissions() => ForwardAsync(HttpMethod.Get, "/api/missions/active");

        [HttpGet("my/progress")]
        public Task<IActionResult> GetMyProgress() => ForwardAsync(HttpMethod.Get, "/api/missions/my/progress");

        [HttpGet("my/completions")]
        public Task<IActionResult> GetMyCompletions() => ForwardAsync(HttpMethod.Get, "/api/missions/my/completions");

        private async Task<IActionResult> ForwardAsync(HttpMethod method, string path, bool includeBody = false)
        {
            try
            {
                var client = _httpClientFactory.CreateClient("shoperp");
                // AF-P1-T3: Forward query string (e.g., ?page=2&pageSize=20 for paginated completions)
                var reqMsg = new HttpRequestMessage(method, path + Request.QueryString.Value);

                // Forward customer token
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
