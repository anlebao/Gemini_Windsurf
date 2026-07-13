using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace VanAn.Gateway.Controllers
{
    /// <summary>
    /// Tiered Auth Phase 2: Gateway forward controller for Customer Identity upgrade endpoints.
    /// Forwards X-Customer-Token from KhachLink to ShopERP's CustomerIdentityController.
    /// </summary>
    [ApiController]
    [Route("api/customer-identity")]
    [AllowAnonymous]
    public class CustomerIdentityController(IHttpClientFactory httpClientFactory, ILogger<CustomerIdentityController> logger) : ControllerBase
    {
        private readonly IHttpClientFactory _httpClientFactory = httpClientFactory;
        private readonly ILogger<CustomerIdentityController> _logger = logger;

        /// <summary>
        /// Forward POST /api/customer-identity/upgrade/send-otp to ShopERP.
        /// Passes X-Customer-Token header through.
        /// </summary>
        [HttpPost("upgrade/send-otp")]
        public async Task<IActionResult> SendUpgradeOtp()
        {
            try
            {
                var client = _httpClientFactory.CreateClient("shoperp");
                var reqMsg = new HttpRequestMessage(HttpMethod.Post, "/api/customer-identity/upgrade/send-otp");
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

                // Forward X-Dev-OTP header from ShopERP response (dev mode only)
                if (response.Headers.TryGetValues("X-Dev-OTP", out var devOtp))
                    Response.Headers["X-Dev-OTP"] = devOtp.FirstOrDefault();

                return StatusCode((int)response.StatusCode, content);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error forwarding SendUpgradeOtp to ShopERP");
                return StatusCode(500, new { error = "Internal server error" });
            }
        }

        /// <summary>
        /// Forward POST /api/customer-identity/upgrade/verify-otp to ShopERP.
        /// Passes X-Customer-Token header and request body through.
        /// </summary>
        [HttpPost("upgrade/verify-otp")]
        public async Task<IActionResult> VerifyUpgradeOtp()
        {
            try
            {
                var client = _httpClientFactory.CreateClient("shoperp");
                var reqMsg = new HttpRequestMessage(HttpMethod.Post, "/api/customer-identity/upgrade/verify-otp");
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
                return StatusCode((int)response.StatusCode, content);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error forwarding VerifyUpgradeOtp to ShopERP");
                return StatusCode(500, new { error = "Internal server error" });
            }
        }
    }
}
