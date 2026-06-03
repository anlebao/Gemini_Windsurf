using VanAn.Shared.Domain;
using VanAn.Shared.Services;
using VanAn.KhachLink.Models;

namespace VanAn.KhachLink.Services;

public interface IEnhancedCartService
{
    Task<bool> AddItemAsync(CartItem item);
    Task<bool> RemoveItemAsync(string productId);
    Task<bool> UpdateQuantityAsync(string productId, int quantity);
    Task<List<CartItem>> GetItemsAsync();
    Task<decimal> GetTotalAsync();
    Task<bool> ClearCartAsync();
    Task<SyncResult> SyncCartAsync();
    Task<bool> SaveCartOfflineAsync();
    Task<bool> LoadCartOfflineAsync();
}

public class EnhancedCartService : IEnhancedCartService
{
    private readonly IIndexedDBService _indexedDBService;
    private readonly ICartService _cartService;
    private readonly ISyncConflictResolver _conflictResolver;
    private readonly ILogger<EnhancedCartService> _logger;
    private readonly string _cartKey = "user_cart";
    
    public EnhancedCartService(
        IIndexedDBService indexedDBService,
        ICartService cartService,
        ISyncConflictResolver conflictResolver,
        ILogger<EnhancedCartService> logger)
    {
        _indexedDBService = indexedDBService;
        _cartService = cartService;
        _conflictResolver = conflictResolver;
        _logger = logger;
    }
    
    public async Task<bool> AddItemAsync(CartItem item)
    {
        try
        {
            // Add to local cart
            var items = await GetItemsAsync();
            var existingItem = items.FirstOrDefault(i => i.Id == item.Id);
            
            if (existingItem != null)
            {
                // CartItem is immutable, create new instance with updated quantity
                var index = items.IndexOf(existingItem);
                items[index] = existingItem with { Quantity = existingItem.Quantity + item.Quantity };
            }
            else
            {
                // Add new item
                items.Add(item);
            }
            
            // Save to IndexedDB
            await _indexedDBService.SetAsync(_cartKey, items);
            
            // Try to sync immediately if online
            _ = Task.Run(async () => await SyncCartAsync());
            
            _logger.LogInformation("Item added to cart: {ProductId}, Quantity: {Quantity}", item.Id, item.Quantity);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to add item to cart: {ProductId}", item.Id);
            return false;
        }
    }
    
    public async Task<bool> RemoveItemAsync(string productId)
    {
        try
        {
            var items = await GetItemsAsync();
            var item = items.FirstOrDefault(i => i.Id.ToString() == productId);
            
            if (item != null)
            {
                items.Remove(item);
                await _indexedDBService.SetAsync(_cartKey, items);
                
                // Try to sync immediately if online
                _ = Task.Run(async () => await SyncCartAsync());
                
                _logger.LogInformation("Item removed from cart: {ProductId}", productId);
                return true;
            }
            
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to remove item from cart: {ProductId}", productId);
            return false;
        }
    }
    
    public async Task<bool> UpdateQuantityAsync(string productId, int quantity)
    {
        try
        {
            if (quantity <= 0)
            {
                return await RemoveItemAsync(productId);
            }
            
            var items = await GetItemsAsync();
            var item = items.FirstOrDefault(i => i.Id.ToString() == productId);
            
            if (item != null)
            {
                // CartItem is immutable, create new instance with updated quantity
                var index = items.IndexOf(item);
                items[index] = item with { Quantity = quantity };
                
                await _indexedDBService.SetAsync(_cartKey, items);
                
                // Try to sync immediately if online
                _ = Task.Run(async () => await SyncCartAsync());
                
                _logger.LogInformation("Cart item quantity updated: {ProductId}, New Quantity: {Quantity}", productId, quantity);
                return true;
            }
            
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update cart item quantity: {ProductId}", productId);
            return false;
        }
    }
    
    public async Task<List<CartItem>> GetItemsAsync()
    {
        try
        {
            var items = await _indexedDBService.GetAsync<List<CartItem>>(_cartKey);
            return items ?? new List<CartItem>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get cart items");
            return new List<CartItem>();
        }
    }
    
    public async Task<decimal> GetTotalAsync()
    {
        try
        {
            var items = await GetItemsAsync();
            return items.Sum(i => i.TotalPrice);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to calculate cart total");
            return 0;
        }
    }
    
    public async Task<bool> ClearCartAsync()
    {
        try
        {
            await _indexedDBService.RemoveAsync(_cartKey);
            
            // Try to sync immediately if online
            _ = Task.Run(async () => await _cartService.ClearCartAsync());
            
            _logger.LogInformation("Cart cleared");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to clear cart");
            return false;
        }
    }
    
    public async Task<SyncResult> SyncCartAsync()
    {
        var result = new SyncResult();
        
        try
        {
            // Get offline cart
            var offlineItems = await GetItemsAsync();
            if (!offlineItems.Any())
            {
                result.Success = true;
                result.ErrorMessage = "No items to sync";
                return result;
            }
            
            // Get server cart
            var serverItems = await _cartService.GetItemsAsync();
            
            // Resolve conflicts
            var conflictResolution = await _conflictResolver.ResolveCartConflictAsync(
                offlineItems.Select(ToOfflineItemDto).ToList(),
                serverItems);
            
            if (!conflictResolution.Success)
            {
                result.Success = false;
                result.ErrorMessage = conflictResolution.Reason;
                return result;
            }
            
            // Sync resolved items to server
            if (conflictResolution.Action == ResolutionAction.Merge && conflictResolution.MergedOrder != null)
            {
                // Clear server cart first
                await _cartService.ClearCartAsync();
                
                // Add merged items to server
                foreach (var item in conflictResolution.MergedOrder.Items)
                {
                    await _cartService.AddItemAsync(new CartItem
                    {
                        Id = Guid.NewGuid(),
                        ProductId = Guid.Parse(item.ProductId),
                        ProductName = item.ProductName ?? string.Empty,
                        Description = string.Empty,
                        Quantity = item.Quantity,
                        UnitPrice = item.UnitPrice
                    });
                }
                
                // Update offline cart with merged items
                var mergedCartItems = conflictResolution.MergedOrder.Items.Select(ToCartItem).ToList();
                await _indexedDBService.SetAsync(_cartKey, mergedCartItems);
            }
            
            result.Success = true;
            _logger.LogInformation("Cart synced successfully with {ItemCount} items", offlineItems.Count);
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.ErrorMessage = ex.Message;
            _logger.LogError(ex, "Failed to sync cart");
        }
        
        return result;
    }
    
    public async Task<bool> SaveCartOfflineAsync()
    {
        try
        {
            var items = await _cartService.GetItemsAsync();
            await _indexedDBService.SetAsync(_cartKey, items);
            
            _logger.LogInformation("Cart saved offline with {ItemCount} items", items.Count);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save cart offline");
            return false;
        }
    }
    
    public async Task<bool> LoadCartOfflineAsync()
    {
        try
        {
            var offlineItems = await _indexedDBService.GetAsync<List<CartItem>>(_cartKey);
            if (offlineItems == null || !offlineItems.Any())
            {
                return true; // No offline cart to load
            }
            
            // Clear current server cart
            await _cartService.ClearCartAsync();
            
            // Load offline items to server cart
            foreach (var item in offlineItems)
            {
                await _cartService.AddItemAsync(item);
            }
            
            _logger.LogInformation("Cart loaded from offline with {ItemCount} items", offlineItems.Count);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load cart from offline");
            return false;
        }
    }
    
    private OfflineOrderItemDto ToOfflineItemDto(CartItem item)
    {
        return new OfflineOrderItemDto
        {
            ProductId = item.ProductId.ToString(),
            Quantity = item.Quantity,
            UnitPrice = item.UnitPrice,
            TotalPrice = item.TotalPrice,
            ProductName = item.ProductName
        };
    }
    
    private CartItem ToCartItem(OfflineOrderItemDto item)
    {
        return new CartItem
        {
            Id = Guid.NewGuid(),
            ProductId = Guid.Parse(item.ProductId),
            ProductName = item.ProductName ?? string.Empty,
            Description = string.Empty,
            Quantity = item.Quantity,
            UnitPrice = item.UnitPrice
        };
    }
}
