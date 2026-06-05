using VanAn.CoreHub.Services;
using VanAn.KhachLink.Models;

namespace VanAn.KhachLink.Services
{
    public interface IOfflineOrderService
    {
        Task<bool> CreateOrderAsync(OfflineOrderDto order);
        Task<List<OfflineOrderDto>> GetPendingOrdersAsync();
        Task<bool> SyncOrdersAsync();
        Task<SyncResult> SyncSingleOrderAsync(string orderId);
        Task<bool> DeleteOrderAsync(string orderId);
        Task<OfflineOrderDto?> GetOrderAsync(string orderId);
    }

    public class SyncResult
    {
        public bool Success { get; set; }
        public string? ErrorMessage { get; set; }
        public string OrderId { get; set; } = string.Empty;
        public int Attempts { get; set; }
    }

    public class OfflineOrderService(
        IIndexedDBService indexedDBService,
        IOrderService orderService,
        ILogger<OfflineOrderService> logger,
        IServiceProvider serviceProvider) : IOfflineOrderService
    {
        private readonly IIndexedDBService _indexedDBService = indexedDBService;
        private readonly IOrderService _orderService = orderService;
        private readonly ILogger<OfflineOrderService> _logger = logger;
        private readonly IServiceProvider _serviceProvider = serviceProvider;

        public async Task<bool> CreateOrderAsync(OfflineOrderDto order)
        {
            try
            {
                // Set creation timestamp
                order.CreatedAtTimestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

                // Store in IndexedDB
                await _indexedDBService.SetAsync($"order_{order.Id}", order);

                _logger.LogInformation("Offline order created: {OrderId}", order.Id);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to create offline order: {OrderId}", order.Id);
                return false;
            }
        }

        public async Task<List<OfflineOrderDto>> GetPendingOrdersAsync()
        {
            try
            {
                List<OfflineOrderDto> allOrders = await _indexedDBService.GetAllAsync<OfflineOrderDto>();
                return allOrders.Where(o => !o.IsSynced && o.CanRetrySync).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get pending orders");
                return [];
            }
        }

        public async Task<bool> SyncOrdersAsync()
        {
            try
            {
                List<OfflineOrderDto> pendingOrders = await GetPendingOrdersAsync();
                bool allSuccess = true;

                foreach (OfflineOrderDto order in pendingOrders)
                {
                    SyncResult result = await SyncSingleOrderAsync(order.Id);
                    if (!result.Success)
                    {
                        allSuccess = false;
                    }
                }

                return allSuccess;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to sync orders");
                return false;
            }
        }

        public async Task<SyncResult> SyncSingleOrderAsync(string orderId)
        {
            SyncResult result = new() { OrderId = orderId };

            try
            {
                OfflineOrderDto? offlineOrder = await _indexedDBService.GetAsync<OfflineOrderDto>($"order_{orderId}");
                if (offlineOrder == null)
                {
                    result.ErrorMessage = "Order not found in offline storage";
                    return result;
                }

                // Convert to domain and sync
                Shared.Domain.Order domainOrder = offlineOrder.ToDomain();
                Guid tenantId = Guid.Parse(offlineOrder.ShopId);

                try
                {
                    Shared.Domain.Order syncedOrder = await _orderService.CreateOrderAsync(domainOrder, tenantId);

                    // Mark as synced
                    offlineOrder.SyncedAtTimestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                    offlineOrder.LastSyncError = null;
                    await _indexedDBService.SetAsync($"order_{orderId}", offlineOrder);

                    result.Success = true;
                    _logger.LogInformation("Order synced successfully: {OrderId}", orderId);
                }
                catch (Exception ex)
                {
                    // Update sync attempt
                    offlineOrder.SyncAttempts++;
                    offlineOrder.LastSyncError = ex.Message;
                    await _indexedDBService.SetAsync($"order_{orderId}", offlineOrder);

                    result.Success = false;
                    result.ErrorMessage = ex.Message;
                    result.Attempts = offlineOrder.SyncAttempts;

                    _logger.LogWarning("Order sync failed: {OrderId}, Error: {Error}", orderId, ex.Message);
                }
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.ErrorMessage = ex.Message;
                _logger.LogError(ex, "Failed to sync order: {OrderId}", orderId);
            }

            return result;
        }

        public async Task<bool> DeleteOrderAsync(string orderId)
        {
            try
            {
                await _indexedDBService.RemoveAsync($"order_{orderId}");
                _logger.LogInformation("Offline order deleted: {OrderId}", orderId);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to delete offline order: {OrderId}", orderId);
                return false;
            }
        }

        public async Task<OfflineOrderDto?> GetOrderAsync(string orderId)
        {
            try
            {
                return await _indexedDBService.GetAsync<OfflineOrderDto>($"order_{orderId}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get offline order: {OrderId}", orderId);
                return null;
            }
        }
    }
}
