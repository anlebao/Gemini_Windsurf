using VanAn.Shared.Domain;
using VanAn.CoreHub.Services;
using VanAn.KhachLink.Models;

namespace VanAn.KhachLink.Services;

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

public class OfflineOrderService : IOfflineOrderService
{
    private readonly IIndexedDBService _indexedDBService;
    private readonly IOrderService _orderService;
    private readonly ILogger<OfflineOrderService> _logger;
    private readonly IServiceProvider _serviceProvider;
    
    public OfflineOrderService(
        IIndexedDBService indexedDBService,
        IOrderService orderService,
        ILogger<OfflineOrderService> logger,
        IServiceProvider serviceProvider)
    {
        _indexedDBService = indexedDBService;
        _orderService = orderService;
        _logger = logger;
        _serviceProvider = serviceProvider;
    }
    
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
            var allOrders = await _indexedDBService.GetAllAsync<OfflineOrderDto>();
            return allOrders.Where(o => !o.IsSynced && o.CanRetrySync).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get pending orders");
            return new List<OfflineOrderDto>();
        }
    }
    
    public async Task<bool> SyncOrdersAsync()
    {
        try
        {
            var pendingOrders = await GetPendingOrdersAsync();
            var allSuccess = true;
            
            foreach (var order in pendingOrders)
            {
                var result = await SyncSingleOrderAsync(order.Id);
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
        var result = new SyncResult { OrderId = orderId };
        
        try
        {
            var offlineOrder = await _indexedDBService.GetAsync<OfflineOrderDto>($"order_{orderId}");
            if (offlineOrder == null)
            {
                result.ErrorMessage = "Order not found in offline storage";
                return result;
            }
            
            // Convert to domain and sync
            var domainOrder = offlineOrder.ToDomain();
            var tenantId = Guid.Parse(offlineOrder.ShopId);
            
            try
            {
                var syncedOrder = await _orderService.CreateOrderAsync(domainOrder, tenantId);
                
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
