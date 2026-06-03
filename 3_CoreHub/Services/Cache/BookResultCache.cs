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
    public class BookResultCache : IBookResultCache
    {
        private readonly IMemoryCache _cache;
        private readonly ILogger<BookResultCache> _logger;
        private readonly BookCacheStatistics _statistics;
        
        public BookResultCache(IMemoryCache cache, ILogger<BookResultCache> logger)
        {
            _cache = cache;
            _logger = logger;
            _statistics = new BookCacheStatistics();
        }
        
        /// <summary>
        /// Get cached book
        /// </summary>
        public async Task<GenericHKDBook?> GetBookAsync(string cacheKey)
        {
            if (_cache.TryGetValue(cacheKey, out byte[]? cachedData))
            {
                try
                {
                    var book = JsonSerializer.Deserialize<GenericHKDBook>(cachedData);
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
                var serializedData = JsonSerializer.SerializeToUtf8Bytes(book);
                
                var cacheOptions = new MemoryCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = expiration,
                    SlidingExpiration = expiration / 2,
                    Size = serializedData.Length // Track memory usage
                };
                
                _cache.Set(cacheKey, serializedData, cacheOptions);
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
            var results = new Dictionary<string, GenericHKDBook?>();
            
            foreach (var cacheKey in cacheKeys)
            {
                var book = await GetBookAsync(cacheKey);
                results[cacheKey] = book;
            }
            
            return results;
        }
        
        /// <summary>
        /// Set multiple books in cache
        /// </summary>
        public async Task SetBooksAsync(Dictionary<string, GenericHKDBook> books, TimeSpan expiration)
        {
            var tasks = books.Select(async kvp => 
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
        private long _hits;
        private long _misses;
        private long _sets;
        private long _removals;
        private long _totalBytes;
        private readonly object _lock = new object();
        
        public long Hits => _hits;
        public long Misses => _misses;
        public long Sets => _sets;
        public long Removals => _removals;
        public long TotalBytes => _totalBytes;
        public decimal HitRate => (_hits + _misses) > 0 ? (decimal)_hits / (_hits + _misses) * 100 : 0;
        public DateTime LastUpdated { get; private set; } = DateTime.UtcNow;
        
        internal void RecordHit()
        {
            lock (_lock)
            {
                _hits++;
                LastUpdated = DateTime.UtcNow;
            }
        }
        
        internal void RecordMiss()
        {
            lock (_lock)
            {
                _misses++;
                LastUpdated = DateTime.UtcNow;
            }
        }
        
        internal void RecordSet(long bytes)
        {
            lock (_lock)
            {
                _sets++;
                _totalBytes += bytes;
                LastUpdated = DateTime.UtcNow;
            }
        }
        
        internal void RecordRemoval()
        {
            lock (_lock)
            {
                _removals++;
                LastUpdated = DateTime.UtcNow;
            }
        }
        
        internal void Reset()
        {
            lock (_lock)
            {
                _hits = 0;
                _misses = 0;
                _sets = 0;
                _removals = 0;
                _totalBytes = 0;
                LastUpdated = DateTime.UtcNow;
            }
        }
        
        public BookCacheStatistics Clone()
        {
            lock (_lock)
            {
                return new BookCacheStatistics
                {
                    _hits = this._hits,
                    _misses = this._misses,
                    _sets = this._sets,
                    _removals = this._removals,
                    _totalBytes = this._totalBytes,
                    LastUpdated = this.LastUpdated
                };
            }
        }
    }
}
