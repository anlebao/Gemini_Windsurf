using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using VanAn.CoreHub.Infrastructure;
using VanAn.Shared.Domain;
using VanAn.Shared.Domain.Common;
using VanAn.Shared.Services;

namespace VanAn.CoreHub.Services
{
    /// <summary>
    /// #185-1: Shared computation for the shop-owner loyalty dashboard (4 metrics).
    /// Used by both ShopERP LoyaltyController.GetDashboard and the LoyaltyDashboard.razor
    /// page (Blazor Server — calls this in-process instead of HTTP-ing its own API).
    /// </summary>
    public interface ILoyaltyDashboardStatsService
    {
        Task<LoyaltyDashboardStats> GetStatsAsync(Guid tenantId, CancellationToken ct = default);

        /// <summary>
        /// SystemAdmin (cross-tenant): aggregate loyalty stats across ALL tenants.
        /// Bypasses the global TenantId query filter (same pattern as CustomerRepository
        /// SystemAdmin lookups) and uses the global default loyalty rate.
        /// </summary>
        Task<LoyaltyDashboardStats> GetAllTenantsStatsAsync(CancellationToken ct = default);
    }

    public class LoyaltyDashboardStatsService(
        IVanAnDbContext dbContext,
        IShopFeatureSettingsService? shopFeatureSettingsService,
        IOptions<LoyaltyPointsConfig>? loyaltyPointsConfig) : ILoyaltyDashboardStatsService
    {
        private readonly IVanAnDbContext _dbContext = dbContext;
        private readonly IShopFeatureSettingsService? _shopFeatureSettingsService = shopFeatureSettingsService;
        private readonly IOptions<LoyaltyPointsConfig>? _loyaltyPointsConfig = loyaltyPointsConfig;

        public async Task<LoyaltyDashboardStats> GetStatsAsync(Guid tenantId, CancellationToken ct = default)
        {
            // Resolve loyalty rate (per-tenant override or global default)
            decimal rate = _loyaltyPointsConfig?.Value.PointsRate ?? 0.1m;
            if (_shopFeatureSettingsService != null)
            {
                try
                {
                    var settings = await _shopFeatureSettingsService.GetSettingsAsync(new TenantId(tenantId));
                    if (settings.Loyalty_PointsRate > 0m) rate = settings.Loyalty_PointsRate;
                }
                catch { /* fallback to global default */ }
            }

            return await ComputeStatsAsync(tenantId, ignoreFilters: false, rate, ct);
        }

        /// <inheritdoc />
        public async Task<LoyaltyDashboardStats> GetAllTenantsStatsAsync(CancellationToken ct = default)
        {
            // SystemAdmin aggregate: global default rate (per-tenant overrides vary — not applicable).
            decimal rate = _loyaltyPointsConfig?.Value.PointsRate ?? 0.1m;
            return await ComputeStatsAsync(tenantId: null, ignoreFilters: true, rate, ct);
        }

        private async Task<LoyaltyDashboardStats> ComputeStatsAsync(
            Guid? tenantId, bool ignoreFilters, decimal rate, CancellationToken ct)
        {
            IQueryable<LoyaltyRewards> rewardsQuery = _dbContext.LoyaltyRewards;
            IQueryable<RedemptionRecord> redemptionsQuery = _dbContext.RedemptionRecords;
            IQueryable<Order> ordersQuery = _dbContext.Orders;

            if (ignoreFilters)
            {
                // SystemAdmin all-tenants: bypass global TenantId query filter.
                rewardsQuery = rewardsQuery.IgnoreQueryFilters();
                redemptionsQuery = redemptionsQuery.IgnoreQueryFilters();
                ordersQuery = ordersQuery.IgnoreQueryFilters();
            }
            if (tenantId.HasValue)
            {
                var tenantIdVo = new TenantId(tenantId.Value);
                rewardsQuery = rewardsQuery.Where(lr => lr.TenantId == tenantIdVo);
                redemptionsQuery = redemptionsQuery.Where(r => r.TenantId == tenantIdVo);
                ordersQuery = ordersQuery.Where(o => o.TenantId == tenantIdVo);
            }

            // Metric 1: Points pending redemption (sum of all customer balances)
            int pendingRedemption = await rewardsQuery
                .Where(lr => lr.IsActive)
                .SumAsync(lr => (int?)lr.PointBalance, ct) ?? 0;

            // Metric 2: Points redeemed (Fulfilled only — Cancelled already refunded)
            int redeemed = await redemptionsQuery
                .Where(r => r.Status == "Fulfilled")
                .SumAsync(r => (int?)r.PointsSpent, ct) ?? 0;

            // Metric 3: Points in active campaigns (pending orders with TrackingCode, not yet delivered/completed)
            // "delivered" is a valid workflow status but not in OrderStatusId static props — use new OrderStatusId("delivered")
            var deliveredStatus = new OrderStatusId("delivered");
            var campaignOrderTotals = await ordersQuery
                .Where(o => o.TrackingCode != null
                    && o.Status != OrderStatusId.Completed
                    && o.Status != OrderStatusId.Cancelled
                    && o.Status != deliveredStatus)
                .Select(o => o.TotalAmount)
                .ToListAsync(ct);
            int pointsInCampaigns = campaignOrderTotals.Sum(a => (int)(a * rate));

            // Metric 4: Points reserved (ALL pending orders, not yet delivered/completed)
            var allPendingOrderTotals = await ordersQuery
                .Where(o => o.Status != OrderStatusId.Completed
                    && o.Status != OrderStatusId.Cancelled
                    && o.Status != deliveredStatus)
                .Select(o => o.TotalAmount)
                .ToListAsync(ct);
            int pointsReserved = allPendingOrderTotals.Sum(a => (int)(a * rate));

            return new LoyaltyDashboardStats
            {
                PointsPendingRedemption = pendingRedemption,
                PointsRedeemed = redeemed,
                PointsInCampaigns = pointsInCampaigns,
                PointsReserved = pointsReserved
            };
        }
    }

    /// <summary>#99-3: Shop owner loyalty dashboard stats (4 metrics).</summary>
    public class LoyaltyDashboardStats
    {
        /// <summary>Metric 1: Total points in customer wallets (not yet redeemed).</summary>
        public int PointsPendingRedemption { get; set; }
        /// <summary>Metric 2: Total points redeemed (Fulfilled vouchers only).</summary>
        public int PointsRedeemed { get; set; }
        /// <summary>Metric 3: Points estimated for active campaign orders (pending, with TrackingCode).</summary>
        public int PointsInCampaigns { get; set; }
        /// <summary>Metric 4: Points reserved for ALL pending orders (not yet completed/delivered).</summary>
        public int PointsReserved { get; set; }
    }
}
