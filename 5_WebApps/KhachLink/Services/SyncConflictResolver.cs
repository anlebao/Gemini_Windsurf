using VanAn.Shared.Domain;
using VanAn.KhachLink.Models;

namespace VanAn.KhachLink.Services;

public interface ISyncConflictResolver
{
    Task<ConflictResolution> ResolveOrderConflictAsync(OfflineOrderDto offlineOrder, Order? serverOrder);
    Task<ConflictResolution> ResolveCartConflictAsync(List<OfflineOrderItemDto> offlineItems, List<CartItem> serverItems);
    Task<bool> ValidateDataIntegrityAsync(OfflineOrderDto order);
}

public class ConflictResolution
{
    public ResolutionAction Action { get; set; }
    public string? Reason { get; set; }
    public OfflineOrderDto? MergedOrder { get; set; }
    public bool Success => Action != ResolutionAction.Error;
}

public enum ResolutionAction
{
    UseOffline,
    UseServer,
    Merge,
    Skip,
    Error
}

public class SyncConflictResolver : ISyncConflictResolver
{
    private readonly ILogger<SyncConflictResolver> _logger;
    
    public SyncConflictResolver(ILogger<SyncConflictResolver> logger)
    {
        _logger = logger;
    }
    
    public async Task<ConflictResolution> ResolveOrderConflictAsync(OfflineOrderDto offlineOrder, Order? serverOrder)
    {
        try
        {
            // Case 1: No server order exists - use offline
            if (serverOrder == null)
            {
                return new ConflictResolution 
                { 
                    Action = ResolutionAction.UseOffline,
                    Reason = "No server order found"
                };
            }
            
            // Case 2: Server order is newer - use server
            if (serverOrder.CreatedAt > offlineOrder.CreatedAt)
            {
                return new ConflictResolution
                {
                    Action = ResolutionAction.UseServer,
                    Reason = "Server order is newer"
                };
            }
            
            // Case 3: Same timestamp - merge items
            if (Math.Abs((serverOrder.CreatedAt - offlineOrder.CreatedAt).TotalSeconds) < 5)
            {
                var mergedOrder = await MergeOrdersAsync(offlineOrder, serverOrder);
                return new ConflictResolution
                {
                    Action = ResolutionAction.Merge,
                    Reason = "Orders created at same time - merging items",
                    MergedOrder = mergedOrder
                };
            }
            
            // Case 4: Offline order is newer - use offline
            return new ConflictResolution
            {
                Action = ResolutionAction.UseOffline,
                Reason = "Offline order is newer"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to resolve order conflict for offline order: {OrderId}", offlineOrder.Id);
            return new ConflictResolution
            {
                Action = ResolutionAction.Error,
                Reason = ex.Message
            };
        }
    }
    
    public async Task<ConflictResolution> ResolveCartConflictAsync(List<OfflineOrderItemDto> offlineItems, List<CartItem> serverItems)
    {
        try
        {
            // Merge cart items - combine unique items
            var mergedItems = new List<OfflineOrderItemDto>();
            var processedProductIds = new HashSet<string>();
            
            // Add offline items first
            foreach (var offlineItem in offlineItems)
            {
                mergedItems.Add(offlineItem);
                processedProductIds.Add(offlineItem.ProductId);
            }
            
            // Add server items that aren't in offline
            foreach (var serverItem in serverItems)
            {
                if (!processedProductIds.Contains(serverItem.ProductId.ToString()))
                {
                    mergedItems.Add(new OfflineOrderItemDto
                    {
                        ProductId = serverItem.ProductId.ToString(),
                        Quantity = serverItem.Quantity,
                        UnitPrice = serverItem.UnitPrice,
                        TotalPrice = serverItem.TotalPrice
                    });
                }
            }
            
            return new ConflictResolution
            {
                Action = ResolutionAction.Merge,
                Reason = "Cart items merged successfully",
                MergedOrder = new OfflineOrderDto { Items = mergedItems }
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to resolve cart conflict");
            return new ConflictResolution
            {
                Action = ResolutionAction.Error,
                Reason = ex.Message
            };
        }
    }
    
    public async Task<bool> ValidateDataIntegrityAsync(OfflineOrderDto order)
    {
        try
        {
            // Validate required fields
            if (string.IsNullOrEmpty(order.Id) || string.IsNullOrEmpty(order.CustomerId) || string.IsNullOrEmpty(order.ShopId))
            {
                return false;
            }
            
            // Validate items
            if (order.Items == null || !order.Items.Any())
            {
                return false;
            }
            
            // Validate each item
            foreach (var item in order.Items)
            {
                if (string.IsNullOrEmpty(item.ProductId) || item.Quantity <= 0 || item.UnitPrice <= 0)
                {
                    return false;
                }
            }
            
            // Validate total amount
            var calculatedTotal = order.Items.Sum(i => i.TotalPrice);
            if (Math.Abs(calculatedTotal - order.TotalAmount) > 0.01m)
            {
                return false;
            }
            
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to validate data integrity for order: {OrderId}", order.Id);
            return false;
        }
    }
    
    private async Task<OfflineOrderDto> MergeOrdersAsync(OfflineOrderDto offlineOrder, Order serverOrder)
    {
        // Create merged order with server data as base
        var mergedOrder = new OfflineOrderDto
        {
            Id = serverOrder.Id.ToString(),
            CustomerId = serverOrder.CustomerId?.ToString() ?? string.Empty,
            ShopId = serverOrder.TenantId.Value.ToString(),
            TotalAmount = serverOrder.TotalAmount,
            Status = serverOrder.Status.Value,
            CreatedAtTimestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            Items = new List<OfflineOrderItemDto>()
        };
        
        // Merge items from both orders
        var processedProductIds = new HashSet<string>();
        
        // Add server items first
        foreach (var serverItem in serverOrder.Items)
        {
            mergedOrder.Items.Add(new OfflineOrderItemDto
            {
                ProductId = serverItem.ProductId.ToString(),
                Quantity = serverItem.Quantity,
                UnitPrice = serverItem.UnitPrice,
                TotalPrice = serverItem.TotalPrice
            });
            processedProductIds.Add(serverItem.ProductId.ToString());
        }
        
        // Add offline items that aren't in server
        foreach (var offlineItem in offlineOrder.Items)
        {
            if (!processedProductIds.Contains(offlineItem.ProductId))
            {
                mergedOrder.Items.Add(offlineItem);
            }
        }
        
        // Recalculate total
        mergedOrder.TotalAmount = mergedOrder.Items.Sum(i => i.TotalPrice);
        
        return mergedOrder;
    }
}
