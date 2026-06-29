using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using VanAn.CoreHub.Infrastructure;
using VanAn.Shared.Domain;

namespace VanAn.CoreHub.Services
{
    /// <summary>
    /// Service for generating product recommendations based on customer order history.
    /// Uses frequency-based algorithm: products bought more frequently are recommended higher.
    /// </summary>
    public class CustomerRecommendationService(VanAnDbContext dbContext, IMemoryCache cache, ILogger<CustomerRecommendationService> logger)
    {
        private readonly VanAnDbContext _dbContext = dbContext;
        private readonly IMemoryCache _cache = cache;
        private readonly ILogger<CustomerRecommendationService> _logger = logger;
        private readonly TimeSpan _cacheExpiration = TimeSpan.FromMinutes(5);

        /// <summary>
        /// Get recommended products for a customer based on their order history.
        /// Uses frequency-based algorithm with caching.
        /// </summary>
        /// <param name="customerId">Customer ID</param>
        /// <param name="tenantId">Tenant ID for multi-tenancy</param>
        /// <param name="topN">Number of recommendations to return (default: 10)</param>
        /// <returns>List of recommended products with frequency score</returns>
        public async Task<List<RecommendationResult>> GetRecommendedProductsAsync(Guid customerId, Guid tenantId, int topN = 10)
        {
            string cacheKey = $"recommendations_{customerId}_{tenantId}";

            // Try to get from cache first
            if (_cache.TryGetValue(cacheKey, out List<RecommendationResult>? cachedResults))
            {
                _logger.LogDebug("Recommendations found in cache for customer {CustomerId}", customerId);
                return cachedResults ?? [];
            }

            _logger.LogDebug("Generating recommendations for customer {CustomerId}", customerId);

            try
            {
                // Query order history for the customer
                var orderItemsQuery = _dbContext.OrderItems
                    .Include(oi => oi.Order)
                    .Where(oi => oi.Order.CustomerId == customerId && 
                                 oi.TenantId == new TenantId(tenantId) &&
                                 !oi.IsDeleted);

                var orderItems = await orderItemsQuery.ToListAsync();

                // If no order history, return empty list (caller will handle fallback)
                if (!orderItems.Any())
                {
                    _logger.LogDebug("No order history found for customer {CustomerId}", customerId);
                    return [];
                }

                // Calculate product frequency
                var productFrequency = orderItems
                    .GroupBy(oi => oi.ProductId)
                    .Select(g => new
                    {
                        ProductId = g.Key,
                        Frequency = g.Sum(oi => oi.Quantity),
                        TotalSpent = g.Sum(oi => oi.TotalAmount)
                    })
                    .OrderByDescending(p => p.Frequency)
                    .Take(topN)
                    .ToList();

                // Get product details
                var productIds = productFrequency.Select(p => p.ProductId).ToList();
                var products = await _dbContext.Products
                    .Where(p => productIds.Contains(p.ProductId.Value) && 
                                p.IsActive && 
                                !p.IsDeleted &&
                                p.TenantId == new TenantId(tenantId))
                    .ToListAsync();

                // Build recommendation results
                var results = productFrequency
                    .Join(products,
                        freq => freq.ProductId,
                        product => product.ProductId.Value,
                        (freq, product) => new RecommendationResult
                        {
                            ProductId = product.ProductId.Value,
                            Name = product.Name,
                            Description = product.Description,
                            Price = product.Price,
                            Category = product.Category,
                            ImageUrl = product.ImageUrl,
                            VatRate = product.VatRate,
                            FrequencyScore = freq.Frequency,
                            TotalSpent = freq.TotalSpent,
                            RecommendationReason = $"Đã mua {freq.Frequency} lần"
                        })
                    .OrderByDescending(r => r.FrequencyScore)
                    .ToList();

                // Cache the results
                var cacheOptions = new MemoryCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = _cacheExpiration,
                    SlidingExpiration = TimeSpan.FromMinutes(2)
                };

                _cache.Set(cacheKey, results, cacheOptions);

                _logger.LogDebug("Generated {Count} recommendations for customer {CustomerId}", results.Count, customerId);
                return results;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating recommendations for customer {CustomerId}", customerId);
                return [];
            }
        }

        /// <summary>
        /// Invalidate recommendation cache for a specific customer
        /// </summary>
        public void InvalidateCustomerCache(Guid customerId, Guid tenantId)
        {
            string cacheKey = $"recommendations_{customerId}_{tenantId}";
            _cache.Remove(cacheKey);
            _logger.LogDebug("Invalidated recommendation cache for customer {CustomerId}", customerId);
        }

        /// <summary>
        /// Invalidate all recommendation caches
        /// </summary>
        public void InvalidateAllCache()
        {
            // Note: IMemoryCache doesn't support pattern-based removal
            // This would require tracking all cache keys or using a dedicated cache instance
            _logger.LogDebug("Global recommendation cache invalidation requested (limited with IMemoryCache)");
        }
    }

    /// <summary>
    /// Recommendation result with product details and frequency score
    /// </summary>
    public class RecommendationResult
    {
        public Guid ProductId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public decimal Price { get; set; }
        public string? Category { get; set; }
        public string? ImageUrl { get; set; }
        public decimal VatRate { get; set; }
        public int FrequencyScore { get; set; }
        public decimal TotalSpent { get; set; }
        public string RecommendationReason { get; set; } = string.Empty;
    }
}