using VanAn.Shared.Domain;

namespace VanAn.CoreHub.Services;

/// <summary>
/// VALCN v2.0 Phase 7 — Network Dashboard (investor-facing cross-tenant aggregate metrics).
/// Read-only, SystemAdmin-only, 10-minute cache. No domain modification.
/// </summary>
public interface INetworkDashboardService
{
    /// <summary>
    /// Get cross-tenant aggregate metrics for the given date range.
    /// 8 metrics: GMV, ActiveTenants, ActiveCustomers, RepeatRate, PlatformRevenue, LoyaltyCost, LoyaltyROI, ContributionProfit.
    /// Cache: 10 minutes (investor-facing — no real-time requirement).
    /// </summary>
    Task<NetworkDashboardMetrics> GetMetricsAsync(DateTime startDate, DateTime endDate, CancellationToken ct = default);
}

/// <summary>
/// Date range for dashboard queries.
/// </summary>
public record DateRange(DateTime Start, DateTime End);

/// <summary>
/// 8 cross-tenant aggregate metrics (fix I3 — Ops Cost excluded, fix I4 — Tier Distribution removed).
/// </summary>
public record NetworkDashboardMetrics(
    decimal Gmv,                    // SUM all tenant orders
    int ActiveTenants,              // COUNT tenants with orders this period
    int ActiveCustomers,            // COUNT distinct customers
    decimal RepeatRate,             // % customers with >1 order (0-1)
    decimal PlatformRevenue,        // SUM PlatformFeeAmount (Phase 2)
    decimal LoyaltyCost,            // SUM points issued × point value (fallback: 1000 VND/point — INV-009 deferred)
    decimal LoyaltyRoi,             // (repeatGmv - loyaltyCost) / loyaltyCost — fix C4
    decimal ContributionProfit      // PlatformRevenue - LoyaltyCost (Ops Cost excluded — fix I3, defer v3.0)
);
