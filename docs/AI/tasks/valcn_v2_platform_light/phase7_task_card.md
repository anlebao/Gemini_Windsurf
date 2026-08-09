# TASK CARD — Phase 7: Network Dashboard (investor-facing)

> **Status:** 📋 PENDING (requires Phase 2 + Phase 3 + Phase 4 complete)
> **Priority:** P1 — Investor pitch enabler (BOM Section 27 + 34)
> **Branch:** `feature/valcn-v2-phase7-network-dashboard`
> **Estimated sessions:** 2-3
> **Mode:** IMPLEMENT
> **Domain modification:** NO (read-only cross-tenant queries)

## Objective
`NetworkDashboardService` cross-tenant aggregate (8 metrics — bỏ Ops Cost fix I3, bỏ Tier Distribution fix I4) + admin UI `/admin/network-dashboard`. SystemAdmin-only, cache 10 phút. Investor-facing — chứng minh unit economics.

## Prerequisites
- [ ] Phase 2 complete — `Order.PlatformFeeAmount` set (for platform revenue)
- [ ] Phase 3 complete — loyalty budget tracked (for loyalty cost)
- [ ] Phase 4 complete — refund reversal working (for accurate net metrics)
- [ ] `dotnet build VanAn.sln` Release — 0 errors (baseline)

## Files to Modify/Create

| File | Status | Purpose |
|------|--------|---------|
| `3_CoreHub/Services/INetworkDashboardService.cs` | NEW | Interface |
| `3_CoreHub/Services/NetworkDashboardService.cs` | NEW | Cross-tenant aggregate queries |
| `2_Gateway/Controllers/NetworkDashboardController.cs` | NEW | API endpoint (SystemAdmin JWT) |
| `5_WebApps/ShopERP/Components/Pages/Admin/NetworkDashboard.razor` | NEW | Admin UI |
| `5_WebApps/ShopERP/Services/NetworkDashboardHttpService.cs` | NEW | HTTP client |
| DI registration | MODIFY | Register service + HTTP client |
| Tests | NEW | Dashboard query tests |

## Detailed Changes

### Change 1: NetworkDashboardMetrics record (8 metrics — fix I3, I4)
```csharp
public record NetworkDashboardMetrics(
    decimal Gmv,                    // SUM all tenant orders
    int ActiveTenants,              // COUNT tenants with orders this period
    int ActiveCustomers,            // COUNT distinct customers
    decimal RepeatRate,             // % customers with >1 order
    decimal PlatformRevenue,        // SUM PlatformFeeAmount (Phase 2)
    decimal LoyaltyCost,            // SUM points issued × point value
    decimal LoyaltyRoi,             // (repeatGmv - loyaltyCost) / loyaltyCost — fix C4
    decimal ContributionProfit      // PlatformRevenue - LoyaltyCost (Ops Cost excluded — fix I3, defer v3.0)
    // TierDistribution REMOVED (fix I4 — Phase 6 dropped)
    // OpsCost REMOVED (fix I3 — undefined, defer v3.0)
);
```

### Change 2: NetworkDashboardService — fix C4 LoyaltyROI formula
```csharp
public class NetworkDashboardService : INetworkDashboardService
{
    private readonly IOrderRepository _orderRepo;
    private readonly ILoyaltyIssuanceRecordRepository _issuanceRepo;  // Phase 1 — for loyalty cost
    private readonly IMemoryCache _cache;

    public async Task<NetworkDashboardMetrics> GetMetricsAsync(DateRange range, CancellationToken ct)
    {
        var cacheKey = $"network-dashboard-{range.Start:yyyyMMdd}-{range.End:yyyyMMdd}";
        if (_cache.TryGetValue(cacheKey, out NetworkDashboardMetrics cached))
            return cached;

        // Cross-tenant query (IgnoreQueryFilters — verified pattern in OrderService.cs:89)
        var orders = await _orderRepo.GetAllOrdersByDateRangeAsync(range.Start, range.End, ct);

        var gmv = orders.Sum(o => o.TotalAmount);
        var activeTenants = orders.Select(o => o.TenantId).Distinct().Count();
        var activeCustomers = orders.Select(o => o.CustomerId).Distinct().Count();

        // FIX C4: Repeat rate + repeat GMV calculated correctly
        var ordersByCustomer = orders.GroupBy(o => o.CustomerId);
        var repeatCustomerIds = ordersByCustomer.Where(g => g.Count() > 1).Select(g => g.Key).ToHashSet();
        var repeatRate = activeCustomers > 0 ? (decimal)repeatCustomerIds.Count / activeCustomers : 0;
        var repeatGmv = orders.Where(o => repeatCustomerIds.Contains(o.CustomerId)).Sum(o => o.TotalAmount);

        var platformRevenue = orders.Sum(o => o.PlatformFeeAmount ?? 0);

        // Loyalty cost = SUM(LoyaltyIssuanceRecord.PointsIssued × pointValue) — Phase 1 entity
        var totalPointsIssued = await _issuanceRepo.GetTotalPointsIssuedByDateRangeAsync(range, ct);
        var pointValue = await _loyaltyGlobalConfigRepo.GetPointValueAsync(ct);  // TODO: verify field name
        var loyaltyCost = totalPointsIssued * pointValue;

        // FIX C4: LoyaltyROI = (repeatGmv - loyaltyCost) / loyaltyCost
        var loyaltyRoi = loyaltyCost > 0 ? (repeatGmv - loyaltyCost) / loyaltyCost : 0;

        // FIX I3: ContributionProfit = PlatformRevenue - LoyaltyCost (Ops Cost excluded, defer v3.0)
        var contributionProfit = platformRevenue - loyaltyCost;

        var metrics = new NetworkDashboardMetrics(gmv, activeTenants, activeCustomers, repeatRate,
            platformRevenue, loyaltyCost, loyaltyRoi, contributionProfit);

        _cache.Set(cacheKey, metrics, TimeSpan.FromMinutes(10));
        return metrics;
    }
}
```

### Change 3: NetworkDashboardController (Gateway)
```csharp
[Authorize(Roles = "SystemAdmin")]  // class-level — W12-G7 pattern
[ApiController]
[Route("api/admin/network-dashboard")]
public class NetworkDashboardController : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<NetworkDashboardMetrics>> GetMetrics(
        [FromQuery] DateTime? from, [FromQuery] DateTime? to, CancellationToken ct)
    {
        var range = new DateRange(from ?? DateTime.UtcNow.AddDays(-30), to ?? DateTime.UtcNow);
        return Ok(await _dashboardService.GetMetricsAsync(range, ct));
    }
}
```

### Change 4: NetworkDashboard.razor (ShopERP admin UI)
- 8 metric cards (GMV, Active Tenants, Active Customers, Repeat Rate, Platform Revenue, Loyalty Cost, Loyalty ROI, Contribution Profit)
- Date range picker (default: last 30 days)
- Auto-refresh 10 min (match cache TTL)
- UI Platform components (mandatory)

## Verification
- [ ] `dotnet build VanAn.sln` Release — 0 errors
- [ ] `guard-check.ps1` — PASS
- [ ] Test: GetMetricsAsync returns 8 metrics
- [ ] Test: Cross-tenant query aggregates ALL tenants
- [ ] Test: **LoyaltyROI formula correct** (fix C4) — repeatGmv not totalGmv
- [ ] Test: Cache — second call within 10 min returns cached
- [ ] Test: API — 401 without JWT, 403 without SystemAdmin
- [ ] UI: `/admin/network-dashboard` renders 8 metric cards
- [ ] Architecture test: Controller has class-level `[Authorize]` (W12-G7)

## Rollback
`git revert <commit>` — dashboard removed. No data loss (read-only).

---

## ANALYZE UPDATE (to be filled during INVESTIGATE step)

### INVESTIGATE checklist
- [ ] Read `OrderService.GetAllOrdersByDateRangeAsync` (line 89) — cross-tenant query pattern
- [ ] Find `ILoyaltyIssuanceRecordRepository` (Phase 1) — `GetTotalPointsIssuedByDateRangeAsync` method or add
- [ ] Find `LoyaltyGlobalConfig` — point value field (for loyalty cost calculation)
- [ ] Read `DashboardService.GetPostgreSQLMetricsAsync` (line 23-84) — existing dashboard pattern
- [ ] Read `DashboardController` — existing admin API pattern
- [ ] Find UI Platform components for metric cards (governance compliance)
- [ ] Confirm `DateRange` type exists or define it

### Verified Accurate
- (fill after investigation)

### DRIFT
- (fill if investigation finds drift)
