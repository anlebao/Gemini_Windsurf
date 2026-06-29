using Microsoft.JSInterop;
using VanAn.KhachLink.Models;

namespace VanAn.KhachLink.Services
{
    /// <summary>
    /// Service for tracking and retrieving recently viewed products using localStorage
    /// </summary>
    public class RecentlyViewedService(IJSRuntime jsRuntime)
    {
        private readonly IJSRuntime _jsRuntime = jsRuntime;
        private const string StorageKey = "khachlink_recently_viewed";
        private const int MaxItems = 10;

        /// <summary>
        /// Add a product to recently viewed list
        /// </summary>
        public async Task AddToRecentlyViewedAsync(ProductDto product)
        {
            try
            {
                var recentlyViewed = await GetRecentlyViewedAsync();

                // Remove if already exists (to move to top)
                recentlyViewed = recentlyViewed.Where(p => p.ProductId != product.ProductId).ToList();

                // Add to front
                recentlyViewed.Insert(0, product);

                // Keep only top N items
                if (recentlyViewed.Count > MaxItems)
                {
                    recentlyViewed = recentlyViewed.Take(MaxItems).ToList();
                }

                await SaveRecentlyViewedAsync(recentlyViewed);
            }
            catch (Exception ex)
            {
                // Silently fail - localStorage might not be available in all environments
                Console.WriteLine($"Error adding to recently viewed: {ex.Message}");
            }
        }

        /// <summary>
        /// Get recently viewed products
        /// </summary>
        public async Task<List<ProductDto>> GetRecentlyViewedAsync()
        {
            try
            {
                var json = await _jsRuntime.InvokeAsync<string>("localStorage.getItem", StorageKey);
                
                if (string.IsNullOrEmpty(json))
                {
                    return new List<ProductDto>();
                }

                var products = System.Text.Json.JsonSerializer.Deserialize<List<ProductDto>>(json);
                return products ?? new List<ProductDto>();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error getting recently viewed: {ex.Message}");
                return new List<ProductDto>();
            }
        }

        /// <summary>
        /// Clear recently viewed products
        /// </summary>
        public async Task ClearRecentlyViewedAsync()
        {
            try
            {
                await _jsRuntime.InvokeVoidAsync("localStorage.removeItem", StorageKey);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error clearing recently viewed: {ex.Message}");
            }
        }

        /// <summary>
        /// Save recently viewed products to localStorage
        /// </summary>
        private async Task SaveRecentlyViewedAsync(List<ProductDto> products)
        {
            try
            {
                var json = System.Text.Json.JsonSerializer.Serialize(products);
                await _jsRuntime.InvokeVoidAsync("localStorage.setItem", StorageKey, json);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error saving recently viewed: {ex.Message}");
            }
        }
    }
}