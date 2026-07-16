using VanAn.CoreHub.Services;
using VanAn.Shared.Domain;

namespace VanAn.KhachLink.Services.Http
{
    /// <summary>
    /// Thin HTTP client wrapper for CoreHub order workflow operations.
    /// KhachLink uses this adapter instead of referencing CoreHub services directly.
    /// </summary>
    public class OrderWorkflowHttpService : IOrderWorkflowService
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<OrderWorkflowHttpService> _logger;

        public OrderWorkflowHttpService(IHttpClientFactory httpClientFactory, ILogger<OrderWorkflowHttpService> logger)
        {
            _httpClient = httpClientFactory.CreateClient("gateway");
            _logger = logger;
        }

        public async Task<Order?> TransitionStatusAsync(Guid orderId, OrderStatusId newStatus, string? reason = null)
        {
            var request = new { Status = newStatus.Value, Reason = reason };
            var response = await _httpClient.PutAsJsonAsync($"api/orderworkflow/{orderId}/status", request);
            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                return null;
            }

            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<Order>();
        }

        public async Task<Order?> GetOrderAsync(Guid orderId)
        {
            var response = await _httpClient.GetAsync($"api/orderworkflow/{orderId}");
            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                return null;
            }

            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<Order>();
        }

        public async Task<List<Order>> GetOrdersByCustomerAsync(string customerDeviceId)
        {
            var response = await _httpClient.GetAsync($"api/orderworkflow/by-customer/{Uri.EscapeDataString(customerDeviceId)}");
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<List<Order>>() ?? [];
        }

        public async Task<List<Order>> GetOrdersByStatusAsync(OrderStatusId status)
        {
            var response = await _httpClient.GetAsync($"api/orderworkflow/by-status/{Uri.EscapeDataString(status.Value)}");
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<List<Order>>() ?? [];
        }

        public async Task<List<Order>> GetOrdersByStatusAsync(OrderStatusId status, Guid tenantId)
        {
            // tenantId is resolved server-side from JWT claims (ShopERP controller parses TenantId claim).
            // Query param kept for interface compatibility; server ignores it for security.
            var response = await _httpClient.GetAsync($"api/orderworkflow/by-status/{Uri.EscapeDataString(status.Value)}?tenantId={tenantId}");
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<List<Order>>() ?? [];
        }

        public async Task<bool> IsTransitionValidAsync(OrderStatusId currentStatus, OrderStatusId newStatus)
        {
            var response = await _httpClient.GetAsync(
                $"api/orderworkflow/transition-valid?current={Uri.EscapeDataString(currentStatus.Value)}&next={Uri.EscapeDataString(newStatus.Value)}");
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<bool>();
        }
    }
}
