namespace VanAn.Shared.Domain
{
    /// <summary>
    /// Data Provider Context - Critical for concurrent safety
    /// Provides tenant, period, and request context for data operations
    /// </summary>
    public record DataProviderContext(
        TenantId TenantId,
        AccountingPeriod Period,
        string? RequestId = null
    )
    {
        public string RequestId { get; init; } = RequestId ?? Guid.NewGuid().ToString();
        public DateTime CreatedAt { get; init; } = DateTime.UtcNow;
        
        /// <summary>
        /// Create context for specific tenant and period
        /// </summary>
        public static DataProviderContext Create(TenantId tenantId, AccountingPeriod period)
        {
            return new DataProviderContext(tenantId, period);
        }
        
        /// <summary>
        /// Create context with custom request ID
        /// </summary>
        public static DataProviderContext Create(TenantId tenantId, AccountingPeriod period, string requestId)
        {
            return new DataProviderContext(tenantId, period, requestId);
        }
        
        /// <summary>
        /// Check if context is still valid (not too old)
        /// </summary>
        public bool IsValid(TimeSpan maxAge = default)
        {
            var age = maxAge == default ? TimeSpan.FromMinutes(30) : maxAge;
            return DateTime.UtcNow - CreatedAt <= age;
        }
        
        /// <summary>
        /// Get cache key for this context
        /// </summary>
        public string GetCacheKey(string prefix = "")
        {
            return $"{prefix}_{TenantId.Value}_{Period.Year}_{Period.Month}_{RequestId}";
        }
    }
}
