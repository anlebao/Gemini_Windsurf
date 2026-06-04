using VanAn.Shared.Domain;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace VanAn.CoreHub.Services.Cache
{
    /// <summary>
    /// Cache for HKD book generation results
    /// Provides caching for generated books to improve performance
    /// </summary>
    public class BookResultCache(IMemoryCache cache, ILogger<BookResultCache> logger) : IBookResultCache
    {
        private readonly IMemoryCache _cache = cache;
        private readonly ILogger<BookResultCache> _logger = logger;
        private readonly BookCacheStatistics _statistics = new();

        /// <summary>
        /// Get cached book
        /// </summary>
        public async Task<GenericHKDBook?> GetBookAsync(string cacheKey)
        {
            if (_cache.TryGetValue(cacheKey, out byte[]? cachedData))
            {
                try
                {
                    GenericHKDBook? book = JsonSerializer.Deserialize<GenericHKDBook>(cachedData);
                    if (book != null)
                    {
                        _statistics.RecordHit();
                        _logger.LogDebug("Book found in cache: {CacheKey}", cacheKey);
                        return book;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error deserializing cached book: {CacheKey}", cacheKey);
                }
            }

            _statistics.RecordMiss();
            _logger.LogDebug("Book not found in cache: {CacheKey}", cacheKey);
            return null;
        }

        /// <summary>
        /// Set book in cache
        /// </summary>
        public async Task SetBookAsync(string cacheKey, GenericHKDBook book, TimeSpan expiration)
        {
            try
            {
                byte[] serializedData = JsonSerializer.SerializeToUtf8Bytes(book);

                MemoryCacheEntryOptions cacheOptions = new()
                {
                    AbsoluteExpirationRelativeToNow = expiration,
                    SlidingExpiration = expiration / 2,
                    Size = serializedData.Length // Track memory usage
                };

                _ = _cache.Set(cacheKey, serializedData, cacheOptions);
                _statistics.RecordSet(serializedData.Length);

                _logger.LogDebug("Book cached: {CacheKey} ({Size} bytes, expires in {Expiration} minutes)",
                    cacheKey, serializedData.Length, expiration.TotalMinutes);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error caching book: {CacheKey}", cacheKey);
            }
        }

        /// <summary>
        /// Remove book from cache
        /// </summary>
        public async Task RemoveBookAsync(string cacheKey)
        {
            _cache.Remove(cacheKey);
            _statistics.RecordRemoval();

            _logger.LogDebug("Book removed from cache: {CacheKey}", cacheKey);
        }

        /// <summary>
        /// Clear all books from cache
        /// </summary>
        public async Task ClearCacheAsync()
        {
            // Note: IMemoryCache doesn't have a direct clear method
            // This would require tracking all cache keys or using Compaction
            if (_cache is MemoryCache memoryCache)
            {
                memoryCache.Compact(1.0); // Remove all entries
            }

            _statistics.Reset();
            _logger.LogDebug("Book cache cleared");
        }

        /// <summary>
        /// Get cache statistics
        /// </summary>
        public BookCacheStatistics GetStatistics()
        {
            return _statistics.Clone();
        }

        /// <summary>
        /// Check if book exists in cache
        /// </summary>
        public async Task<bool> ContainsBookAsync(string cacheKey)
        {
            return _cache.TryGetValue(cacheKey, out _);
        }

        /// <summary>
        /// Get multiple books by cache keys
        /// </summary>
        public async Task<Dictionary<string, GenericHKDBook?>> GetBooksAsync(IEnumerable<string> cacheKeys)
        {
            Dictionary<string, GenericHKDBook?> results = [];

            foreach (string cacheKey in cacheKeys)
            {
                GenericHKDBook? book = await GetBookAsync(cacheKey);
                results[cacheKey] = book;
            }

            return results;
        }

        /// <summary>
        /// Set multiple books in cache
        /// </summary>
        public async Task SetBooksAsync(Dictionary<string, GenericHKDBook> books, TimeSpan expiration)
        {
            IEnumerable<Task> tasks = books.Select(async kvp =>
                await SetBookAsync(kvp.Key, kvp.Value, expiration));

            await Task.WhenAll(tasks);
        }

        /// <summary>
        /// Remove expired entries and optimize cache
        /// </summary>
        public async Task OptimizeCacheAsync()
        {
            if (_cache is MemoryCache memoryCache)
            {
                // Compact to remove expired entries
                memoryCache.Compact(0.9); // Keep 90% of memory
            }

            _logger.LogDebug("Book cache optimized");
        }
    }

    /// <summary>
    /// Interface for book result cache
    /// </summary>
    public interface IBookResultCache
    {
        Task<GenericHKDBook?> GetBookAsync(string cacheKey);
        Task SetBookAsync(string cacheKey, GenericHKDBook book, TimeSpan expiration);
        Task RemoveBookAsync(string cacheKey);
        Task ClearCacheAsync();
        BookCacheStatistics GetStatistics();
        Task<bool> ContainsBookAsync(string cacheKey);
        Task<Dictionary<string, GenericHKDBook?>> GetBooksAsync(IEnumerable<string> cacheKeys);
        Task SetBooksAsync(Dictionary<string, GenericHKDBook> books, TimeSpan expiration);
        Task OptimizeCacheAsync();
    }

    /// <summary>
    /// Book cache statistics
    /// </summary>
    public class BookCacheStatistics
    {
        private readonly object _lock = new();

        public long Hits { get; private set; }
        public long Misses { get; private set; }
        public long Sets { get; private set; }
        public long Removals { get; private set; }
        public long TotalBytes { get; private set; }
        public decimal HitRate => (Hits + Misses) > 0 ? (decimal)Hits / (Hits + Misses) * 100 : 0;
        public DateTime LastUpdated { get; private set; } = DateTime.UtcNow;

        internal void RecordHit()
        {
            lock (_lock)
            {
                Hits++;
                LastUpdated = DateTime.UtcNow;
            }
        }

        internal void RecordMiss()
        {
            lock (_lock)
            {
                Misses++;
                LastUpdated = DateTime.UtcNow;
            }
        }

        internal void RecordSet(long bytes)
        {
            lock (_lock)
            {
                Sets++;
                TotalBytes += bytes;
                LastUpdated = DateTime.UtcNow;
            }
        }

        internal void RecordRemoval()
        {
            lock (_lock)
            {
                Removals++;
                LastUpdated = DateTime.UtcNow;
            }
        }

        internal void Reset()
        {
            lock (_lock)
            {
                Hits = 0;
                Misses = 0;
                Sets = 0;
                Removals = 0;
                TotalBytes = 0;
                LastUpdated = DateTime.UtcNow;
            }
        }

        public BookCacheStatistics Clone()
        {
            lock (_lock)
            {
                return new BookCacheStatistics
                {
                    Hits = Hits,
                    Misses = Misses,
                    Sets = Sets,
                    Removals = Removals,
                    TotalBytes = TotalBytes,
                    LastUpdated = LastUpdated
                };
            }
        }
    }
}
