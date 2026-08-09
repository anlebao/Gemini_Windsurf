using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using VanAn.CoreHub.Infrastructure;
using VanAn.Shared.Domain;

namespace VanAn.CoreHub.Services;

/// <summary>
/// VALCN v2.0 Phase 7 — Network Dashboard implementation.
/// Cross-tenant aggregate metrics (investor-facing). Read-only, 10-minute cache.
///
/// DRIFT from task card (resolved during ANALYZE):
///   - LoyaltyGlobalConfig.PointValue MISSING (INV-009 deferred to v3.0) → fallback constant 1000 VND/point.
///   - No ILoyaltyIssuanceRecordRepository → direct IVanAnDbContext.LoyaltyIssuanceRecords.
///   - Order.CustomerId is Guid? (nullable) → filter nulls before grouping.
///   - Uses OrderService.GetAllOrdersByDateRangeAsync pattern (IgnoreQueryFilters for cross-tenant).
/// </summary>
public class NetworkDashboardService : INetworkDashboardService
{
    private readonly IVanAnDbContext _dbContext;
    private readonly IMemoryCache _cache;
    private readonly ILogger<NetworkDashboardService> _logger;

    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(10);

    // INV-009 deferred: LoyaltyGlobalConfig has no PointValue field (VND per point).
    // Fallback: 1000 VND/point — TODO: replace with LoyaltyGlobalConfig.PointValue when INV-009 is implemented (v3.0).
    private const decimal FallbackPointValueVnd = 1000m;

    public NetworkDashboardService(
        IVanAnDbContext dbContext,
        IMemoryCache cache,
        ILogger<NetworkDashboardService> logger)
    {
        _dbContext = dbContext;
        _cache = cache;
        _logger = logger;
    }

    public async Task<NetworkDashboardMetrics> GetMetricsAsync(DateTime startDate, DateTime endDate, CancellationToken ct = default)
    {
        string cacheKey = $"network-dashboard-{startDate:yyyyMMdd}-{endDate:yyyyMMdd}";
        if (_cache.TryGetValue(cacheKey, out NetworkDashboardMetrics? cached) && cached != null)
        {
            _logger.LogDebug("NetworkDashboard: cache hit for {Range}", cacheKey);
            return cached;
        }

        // Cross-tenant query (IgnoreQueryFilters — verified pattern in OrderService.cs:98)
        var orders = await _dbContext.Orders
            .IgnoreQueryFilters()
            .Where(o => o.CreatedAt.Date >= startDate.Date && o.CreatedAt.Date <= endDate.Date)
            .ToListAsync(ct);

        var gmv = orders.Sum(o => o.TotalAmount);
        var activeTenants = orders.Select(o => o.TenantId.Value).Distinct().Count();

        // Order.CustomerId is Guid? — filter nulls for customer-based metrics
        var ordersWithCustomer = orders.Where(o => o.CustomerId.HasValue).ToList();
        var activeCustomers = ordersWithCustomer.Select(o => o.CustomerId!.Value).Distinct().Count();

        // FIX C4: Repeat rate + repeat GMV calculated correctly
        var ordersByCustomer = ordersWithCustomer.GroupBy(o => o.CustomerId!.Value);
        var repeatCustomerIds = ordersByCustomer.Where(g => g.Count() > 1).Select(g => g.Key).ToHashSet();
        var repeatRate = activeCustomers > 0 ? (decimal)repeatCustomerIds.Count / activeCustomers : 0;
        var repeatGmv = ordersWithCustomer.Where(o => repeatCustomerIds.Contains(o.CustomerId!.Value)).Sum(o => o.TotalAmount);

        // Platform revenue from Phase 2 field (nullable — null = not calculated, treat as 0)
        var platformRevenue = orders.Sum(o => o.PlatformFeeAmount ?? 0);

        // Loyalty cost = SUM(LoyaltyIssuanceRecord.PointsIssued) × pointValue (fallback 1000 VND/point — INV-009)
        var totalPointsIssued = await _dbContext.LoyaltyIssuanceRecords
            .IgnoreQueryFilters()
            .Where(r => r.IssuedAt >= startDate && r.IssuedAt <= endDate && !r.IsReversed)
            .SumAsync(r => (int?)r.PointsIssued, ct) ?? 0;
        var loyaltyCost = totalPointsIssued * FallbackPointValueVnd;

        // FIX C4: LoyaltyROI = (repeatGmv - loyaltyCost) / loyaltyCost
        var loyaltyRoi = loyaltyCost > 0 ? (repeatGmv - loyaltyCost) / loyaltyCost : 0;

        // FIX I3: ContributionProfit = PlatformRevenue - LoyaltyCost (Ops Cost excluded, defer v3.0)
        var contributionProfit = platformRevenue - loyaltyCost;

        var metrics = new NetworkDashboardMetrics(
            gmv, activeTenants, activeCustomers, repeatRate,
            platformRevenue, loyaltyCost, loyaltyRoi, contributionProfit);

        _cache.Set(cacheKey, metrics, CacheTtl);
        _logger.LogInformation("NetworkDashboard: computed metrics for {Start} to {End} — GMV={Gmv}, Tenants={Tenants}, Customers={Customers}",
            startDate, endDate, gmv, activeTenants, activeCustomers);

        return metrics;
    }
}
