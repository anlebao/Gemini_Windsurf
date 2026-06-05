using VanAn.Shared.Domain;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace VanAn.CoreHub.Services.Cache
{
    /// <summary>
    /// In-memory cache for HKD book templates
    /// Provides fast access to frequently used templates
    /// </summary>
    public class MemoryTemplateCache(IMemoryCache cache, ILogger<MemoryTemplateCache> logger) : IMemoryTemplateCache
    {
        private readonly IMemoryCache _cache = cache;
        private readonly ILogger<MemoryTemplateCache> _logger = logger;

        /// <summary>
        /// Get cached template
        /// </summary>
        public HKDBookTemplate? GetTemplate(string templateCode)
        {
            string cacheKey = $"template_{templateCode}";

            if (_cache.TryGetValue(cacheKey, out HKDBookTemplate? template))
            {
                _logger.LogDebug("Template {TemplateCode} found in cache", templateCode);
                return template;
            }

            _logger.LogDebug("Template {TemplateCode} not found in cache", templateCode);
            return null;
        }

        /// <summary>
        /// Set template in cache
        /// </summary>
        public void SetTemplate(string templateCode, HKDBookTemplate template, TimeSpan expiration)
        {
            string cacheKey = $"template_{templateCode}";

            MemoryCacheEntryOptions cacheOptions = new()
            {
                AbsoluteExpirationRelativeToNow = expiration,
                SlidingExpiration = expiration / 2
            };

            _ = _cache.Set(cacheKey, template, cacheOptions);

            _logger.LogDebug("Template {TemplateCode} cached for {Expiration} minutes",
                templateCode, expiration.TotalMinutes);
        }

        /// <summary>
        /// Remove template from cache
        /// </summary>
        public void RemoveTemplate(string templateCode)
        {
            string cacheKey = $"template_{templateCode}";
            _cache.Remove(cacheKey);

            _logger.LogDebug("Template {TemplateCode} removed from cache", templateCode);
        }

        /// <summary>
        /// Clear all templates from cache
        /// </summary>
        public void ClearTemplates()
        {
            // Note: IMemoryCache doesn't have a direct way to clear by pattern
            // This would require tracking all cache keys or using a separate cache instance
            _logger.LogDebug("Template cache clear requested (not fully implemented with IMemoryCache)");
        }

        /// <summary>
        /// Get cache statistics
        /// </summary>
        public MemoryCacheStatistics GetStatistics()
        {
            // IMemoryCache doesn't expose detailed statistics
            // This is a placeholder implementation
            return new MemoryCacheStatistics
            {
                TotalItems = 0, // Would need to track this manually
                TotalMemoryUsage = 0, // Not available from IMemoryCache
                HitRate = 0, // Would need to track hits/misses manually
                LastCleanup = DateTime.UtcNow
            };
        }

        /// <summary>
        /// Check if template exists in cache
        /// </summary>
        public bool ContainsTemplate(string templateCode)
        {
            string cacheKey = $"template_{templateCode}";
            return _cache.TryGetValue(cacheKey, out _);
        }
    }

    /// <summary>
    /// Interface for memory template cache
    /// </summary>
    public interface IMemoryTemplateCache
    {
        HKDBookTemplate? GetTemplate(string templateCode);
        void SetTemplate(string templateCode, HKDBookTemplate template, TimeSpan expiration);
        void RemoveTemplate(string templateCode);
        void ClearTemplates();
        MemoryCacheStatistics GetStatistics();
        bool ContainsTemplate(string templateCode);
    }

    /// <summary>
    /// Memory cache statistics
    /// </summary>
    public record MemoryCacheStatistics
    {
        public int TotalItems { get; init; }
        public long TotalMemoryUsage { get; init; }
        public decimal HitRate { get; init; }
        public DateTime LastCleanup { get; init; }
    }
}
