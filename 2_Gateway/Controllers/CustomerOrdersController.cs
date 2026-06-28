using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace VanAn.Gateway.Controllers
{
    /// <summary>
    /// W17-T3: Gateway forward controller for Customer Order History.
    /// </summary>
    [ApiController]
    [Route("api/customerorders")]
    [AllowAnonymous]
    public class CustomerOrdersController(IHttpClientFactory httpClientFactory, ILogger<CustomerOrdersController> logger) : ControllerBase
    {
        private readonly IHttpClientFactory _httpClientFactory = httpClientFactory;
        private readonly ILogger<CustomerOrdersController> _logger = logger;

        [HttpGet]
        public async Task<IActionResult> GetMyOrders(
            [FromQuery] string? status,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 10)
        {
            try
            {
                var client = _httpClientFactory.CreateClient("shoperp");
                var reqMsg = new HttpRequestMessage(HttpMethod.Get,
                    $"/api/customerorders?status={status}&page={page}&pageSize={pageSize}");
                if (Request.Headers.TryGetValue("X-Customer-Token", out var token))
                    reqMsg.Headers.Add("X-Customer-Token", token.ToString());

                var response = await client.SendAsync(reqMsg);
                var content = await response.Content.ReadAsStringAsync();
                return StatusCode((int)response.StatusCode, content);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error forwarding GetMyOrders to ShopERP");
                return StatusCode(500, new { error = "Internal server error" });
            }
        }
    }
}
