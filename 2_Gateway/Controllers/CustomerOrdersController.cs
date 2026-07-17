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
                // Preserve Content-Type from upstream (ShopERP returns application/json).
                // Without this, StatusCode() defaults to text/plain, breaking KhachLink
                // ReadFromJsonAsync<OrdersResult>() which checks Content-Type for "json".
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
                _logger.LogError(ex, "Error forwarding GetMyOrders to ShopERP");
                return StatusCode(500, new { error = "Internal server error" });
            }
        }

        /// <summary>
        /// Wave 9: Forward order status endpoint for polling.
        /// Lightweight endpoint returns { orderId, status } for real-time updates.
        /// </summary>
        [HttpGet("{id}/status")]
        [AllowAnonymous]
        public async Task<IActionResult> GetOrderStatus(Guid id)
        {
            try
            {
                var client = _httpClientFactory.CreateClient("shoperp");
                var reqMsg = new HttpRequestMessage(HttpMethod.Get, $"/api/orders/{id}/status");
                
                // Forward X-Customer-Token header for authentication (consistent with Wave 17 pattern)
                if (Request.Headers.TryGetValue("X-Customer-Token", out var token))
                    reqMsg.Headers.Add("X-Customer-Token", token.ToString());

                var response = await client.SendAsync(reqMsg);
                var content = await response.Content.ReadAsStringAsync();
                
                _logger.LogDebug("Forwarded order status request for OrderId: {OrderId}, StatusCode: {StatusCode}", 
                    id, response.StatusCode);
                
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
                _logger.LogError(ex, "Error forwarding GetOrderStatus to ShopERP for OrderId: {OrderId}", id);
                return StatusCode(500, new { error = "Internal server error" });
            }
        }
    }
}
