using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace VanAn.Gateway.Controllers
{
    /// <summary>
    /// Gateway forward controller for Customer Identity endpoints.
    /// Forwards OTP login, profile, and identity upgrade endpoints from KhachLink to ShopERP.
    /// </summary>
    [ApiController]
    [Route("api/customer-identity")]
    [AllowAnonymous]
    public class CustomerIdentityController(IHttpClientFactory httpClientFactory, ILogger<CustomerIdentityController> logger) : ControllerBase
    {
        private readonly IHttpClientFactory _httpClientFactory = httpClientFactory;
        private readonly ILogger<CustomerIdentityController> _logger = logger;

        /// <summary>
        /// Forward POST /api/customer-identity/otp/send to ShopERP.
        /// Anonymous — sends OTP to a phone number for login.
        /// </summary>
        [HttpPost("otp/send")]
        public async Task<IActionResult> SendOtp()
        {
            try
            {
                var client = _httpClientFactory.CreateClient("shoperp");
                var reqMsg = new HttpRequestMessage(HttpMethod.Post, "/api/customer-identity/otp/send");
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

                // Forward X-Dev-OTP header from ShopERP response (dev mode only)
                if (response.Headers.TryGetValues("X-Dev-OTP", out var devOtp))
                    Response.Headers["X-Dev-OTP"] = devOtp.FirstOrDefault();

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
                _logger.LogError(ex, "Error forwarding SendOtp to ShopERP");
                return StatusCode(500, new { error = "Internal server error" });
            }
        }

        /// <summary>
        /// Forward POST /api/customer-identity/otp/verify to ShopERP.
        /// Anonymous — verifies OTP and returns customer token + info.
        /// </summary>
        [HttpPost("otp/verify")]
        public async Task<IActionResult> VerifyOtp()
        {
            try
            {
                var client = _httpClientFactory.CreateClient("shoperp");
                var reqMsg = new HttpRequestMessage(HttpMethod.Post, "/api/customer-identity/otp/verify");
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
                _logger.LogError(ex, "Error forwarding VerifyOtp to ShopERP");
                return StatusCode(500, new { error = "Internal server error" });
            }
        }

        /// <summary>
        /// Forward GET /api/customer-identity/me to ShopERP.
        /// Requires X-Customer-Token header.
        /// </summary>
        [HttpGet("me")]
        public async Task<IActionResult> GetMe()
        {
            try
            {
                var client = _httpClientFactory.CreateClient("shoperp");
                var reqMsg = new HttpRequestMessage(HttpMethod.Get, "/api/customer-identity/me");
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
                _logger.LogError(ex, "Error forwarding GetMe to ShopERP");
                return StatusCode(500, new { error = "Internal server error" });
            }
        }

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
                    // FIX #106: Strip charset from Content-Type — MediaTypeHeaderValue rejects
                    // "; charset=utf-8" (same bug as Redemption/Loyalty controllers).
                    var mediaType = (Request.ContentType ?? "application/json").Split(';', StringSplitOptions.TrimEntries)[0];
                    reqMsg.Content = new StreamContent(Request.Body);
                    reqMsg.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(mediaType);
                }

                var response = await client.SendAsync(reqMsg);
                var content = await response.Content.ReadAsStringAsync();

                // Forward X-Dev-OTP header from ShopERP response (dev mode only)
                if (response.Headers.TryGetValues("X-Dev-OTP", out var devOtp))
                    Response.Headers["X-Dev-OTP"] = devOtp.FirstOrDefault();

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
                _logger.LogError(ex, "Error forwarding VerifyUpgradeOtp to ShopERP");
                return StatusCode(500, new { error = "Internal server error" });
            }
        }
    }
}
