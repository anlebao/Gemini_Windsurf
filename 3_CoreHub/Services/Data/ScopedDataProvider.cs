using VanAn.Shared.Domain;
using VanAn.CoreHub.Services.Cache;
using VanAn.CoreHub.Services.PreAggregation;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Caching.Memory;

namespace VanAn.CoreHub.Services.Data
{
    /// <summary>
    /// Scoped Data Provider Implementation
    /// Thread-safe, context-aware data access with multi-level caching
    /// </summary>
    public class ScopedDataProvider(
        IPreAggregationService preAggregationService,
        IBookResultCache cache,
        ILogger<ScopedDataProvider> logger,
        IMemoryCache localCache) : IDataProvider
    {
        private readonly IPreAggregationService _preAggregationService = preAggregationService;
        private readonly IBookResultCache _cache = cache;
        private readonly ILogger<ScopedDataProvider> _logger = logger;
        private readonly IMemoryCache _localCache = localCache; // Per-request cache

        public async Task<Dictionary<string, decimal>> GetPreAggregatedDataAsync(DataProviderContext context)
        {
            string cacheKey = context.GetCacheKey("preagg");

            // Check local cache first (per-request)
            if (_localCache.TryGetValue(cacheKey, out Dictionary<string, decimal> cachedData))
            {
                _logger.LogDebug("Using local cache for tenant {TenantId} in request {RequestId}",
                    context.TenantId.Value, context.RequestId);
                return cachedData;
            }

            // Check distributed cache
            string distributedCacheKey = context.GetCacheKey("preagg_dist");
            GenericHKDBook? distributedCached = await _cache.GetBookAsync(distributedCacheKey);

            if (distributedCached?.NumericValues != null)
            {
                Dictionary<string, decimal> data = distributedCached.NumericValues.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);

                // Cache locally for this request
                _localCache.Set(cacheKey, data, TimeSpan.FromMinutes(5));

                _logger.LogDebug("Using distributed cache for tenant {TenantId} in request {RequestId}",
                    context.TenantId.Value, context.RequestId);

                return data;
            }

            // Get from pre-aggregation service
            Dictionary<string, decimal> preAggregatedData = await _preAggregationService.GetAccountAggregatesAsync(context.TenantId, context.Period);

            // Cache both locally and distributed
            _localCache.Set(cacheKey, preAggregatedData, TimeSpan.FromMinutes(5));

            GenericHKDBook cacheBook = new()
            {
                TenantId = context.TenantId,
                Period = context.Period,
                NumericValues = preAggregatedData.ToDictionary(kvp => kvp.Key, kvp => kvp.Value)
            };

            await _cache.SetBookAsync(distributedCacheKey, cacheBook, TimeSpan.FromMinutes(30));

            _logger.LogInformation("Pre-aggregated data for tenant {TenantId} in request {RequestId}: {Count} values",
                context.TenantId.Value, context.RequestId, preAggregatedData.Count);

            return preAggregatedData;
        }

        public decimal GetAccountSum(DataProviderContext context, string accountPattern, string side)
        {
            string cacheKey = context.GetCacheKey($"account_sum_{accountPattern}_{side}");

            if (_localCache.TryGetValue(cacheKey, out decimal cachedValue))
            {
                _logger.LogDebug("Using cached account sum for {Pattern} {Side} in request {RequestId}",
                    accountPattern, side, context.RequestId);
                return cachedValue;
            }

            // Get from pre-aggregated data
            Dictionary<string, decimal> data = GetPreAggregatedDataAsync(context).GetAwaiter().GetResult();
            string key = $"Account_{accountPattern}_{side}";

            if (!data.TryGetValue(key, out decimal value))
            {
                _logger.LogWarning("Account sum not found for pattern {Pattern} and side {Side} in request {RequestId}",
                    accountPattern, side, context.RequestId);
                return 0;
            }

            _localCache.Set(cacheKey, value, TimeSpan.FromMinutes(5));

            _logger.LogDebug("Account sum for {Pattern} {Side} in request {RequestId}: {Value}",
                accountPattern, side, context.RequestId, value);

            return value;
        }

        public decimal GetAccountBalance(DataProviderContext context, string accountPattern)
        {
            string cacheKey = context.GetCacheKey($"account_balance_{accountPattern}");

            if (_localCache.TryGetValue(cacheKey, out decimal cachedBalance))
            {
                _logger.LogDebug("Using cached account balance for {Pattern} in request {RequestId}",
                    accountPattern, context.RequestId);
                return cachedBalance;
            }

            decimal credit = GetAccountSum(context, accountPattern, "Credit");
            decimal debit = GetAccountSum(context, accountPattern, "Debit");
            decimal balance = debit - credit;

            _localCache.Set(cacheKey, balance, TimeSpan.FromMinutes(5));

            _logger.LogDebug("Account balance for {Pattern} in request {RequestId}: {Balance} (Debit: {Debit}, Credit: {Credit})",
                accountPattern, context.RequestId, balance, debit, credit);

            return balance;
        }

        public decimal GetPeriodTotal(DataProviderContext context, string accountPattern)
        {
            // For period total, we'll use the balance as the total
            return GetAccountBalance(context, accountPattern);
        }
    }
}
