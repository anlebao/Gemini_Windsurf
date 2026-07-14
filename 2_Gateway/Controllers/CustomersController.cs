using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace VanAn.Gateway.Controllers
{
    /// <summary>
    /// W17-T1: Gateway forward controller for Customer Identity (OTP) endpoints.
    /// Forwards requests to ShopERP's CustomerIdentityController.
    /// AllowAnonymous: OTP endpoints are accessed by unauthenticated KhachLink users.
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    [AllowAnonymous]
    public class CustomersController(IHttpClientFactory httpClientFactory, ILogger<CustomersController> logger) : ControllerBase
    {
        private readonly IHttpClientFactory _httpClientFactory = httpClientFactory;
        private readonly ILogger<CustomersController> _logger = logger;

        [HttpPost("otp/send")]
        public async Task<IActionResult> SendOtp([FromBody] object request)
        {
            return await ForwardPost("/api/customer-identity/otp/send", request);
        }

        [HttpPost("otp/verify")]
        public async Task<IActionResult> VerifyOtp([FromBody] object request)
        {
            return await ForwardPost("/api/customer-identity/otp/verify", request);
        }

        [HttpGet("me")]
        public async Task<IActionResult> GetMe()
        {
            try
            {
                var client = _httpClientFactory.CreateClient("shoperp");
                var reqMsg = new HttpRequestMessage(HttpMethod.Get, "/api/customer-identity/me");
                // Forward X-Customer-Token header
                if (Request.Headers.TryGetValue("X-Customer-Token", out var token))
                    reqMsg.Headers.Add("X-Customer-Token", token.ToString());

                var response = await client.SendAsync(reqMsg);
                var content = await response.Content.ReadAsStringAsync();
                return StatusCode((int)response.StatusCode, content);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error forwarding GetMe to ShopERP");
                return StatusCode(500, new { error = "Internal server error" });
            }
        }

        private async Task<IActionResult> ForwardPost(string path, object body)
        {
            try
            {
                var client = _httpClientFactory.CreateClient("shoperp");
                var response = await client.PostAsJsonAsync(path, body);
                var content = await response.Content.ReadAsStringAsync();
                // Forward X-Dev-OTP header if present (development only)
                if (response.Headers.TryGetValues("X-Dev-OTP", out var otpValues))
                    Response.Headers["X-Dev-OTP"] = otpValues.FirstOrDefault();
                return Content(content, "application/json");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error forwarding {Path} to ShopERP", path);
                return StatusCode(500, new { error = "Internal server error" });
            }
        }
    }
}
