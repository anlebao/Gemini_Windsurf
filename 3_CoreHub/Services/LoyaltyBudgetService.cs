using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using VanAn.CoreHub.Infrastructure;
using VanAn.Shared.Domain;
using VanAn.Shared.Domain.Common;

namespace VanAn.CoreHub.Services;

/// <summary>
/// VALCN v2.0 Phase 3 — Loyalty budget enforcement implementation.
/// Feature-flagged via ValcnV2_LoyaltyBudget (default OFF — existing behavior unchanged).
///
/// Budget caps (all nullable = unlimited when null):
///   - PerOrderRateCap: max points = orderAmount × rate (e.g. 0.03 = 3% of order total)
///   - MonthlyPointsBudget: max points per calendar month per tenant
///   - DailyPointsBudget: max points per day per tenant
///   - PerCustomerDailyLimit: max points per customer per day
///
/// Counter increments use ExecuteUpdateAsync (EF Core 7+) for atomicity (fix I1 — race condition
/// with concurrent AddPoints calls). Reset jobs call ResetAllDailyCountersAsync / ResetAllMonthlyCountersAsync.
///
/// INV-009 (Platform Fee ≥ Loyalty Cost per order) deferred to v3.0 — LoyaltyGlobalConfig has no
/// PointValue field (VND per point), so loyalty cost in VND cannot be calculated yet.
/// </summary>
public class LoyaltyBudgetService : ILoyaltyBudgetService
{
    private readonly IVanAnDbContext _dbContext;
    private readonly ILogger<LoyaltyBudgetService> _logger;

    public LoyaltyBudgetService(IVanAnDbContext dbContext, ILogger<LoyaltyBudgetService> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task<int> CheckAndAdjustPointsAsync(
        Guid tenantId, Guid customerId, decimal orderAmount, int requestedPoints, CancellationToken ct = default)
    {
        if (requestedPoints <= 0) return 0;

        var tenantIdValue = new TenantId(tenantId);

        // Load tenant config (IgnoreQueryFilters — cross-tenant query by TenantId value object)
        var config = await _dbContext.LoyaltyTenantConfigs
            .IgnoreQueryFilters()
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.TenantId == tenantIdValue, ct);

        // No config = no caps = return original points (existing behavior)
        if (config == null) return requestedPoints;

        int adjusted = requestedPoints;

        // Check 1: Per-order rate cap (e.g. 3% of order amount)
        if (config.PerOrderRateCap.HasValue)
        {
            int perOrderCap = (int)(orderAmount * config.PerOrderRateCap.Value);
            adjusted = Math.Min(adjusted, perOrderCap);
            _logger.LogDebug("LoyaltyBudget: PerOrderRateCap={Cap} → adjusted {Orig}→{New} (orderAmount={Amt})",
                config.PerOrderRateCap.Value, requestedPoints, adjusted, orderAmount);
        }

        // Check 2: Monthly budget
        if (config.MonthlyPointsBudget.HasValue)
        {
            int monthlyRemaining = config.MonthlyPointsBudget.Value - config.PointsIssuedThisMonth;
            adjusted = Math.Min(adjusted, Math.Max(0, monthlyRemaining));
            _logger.LogDebug("LoyaltyBudget: MonthlyBudget={Budget} Issued={Issued} Remaining={Rem} → adjusted={New}",
                config.MonthlyPointsBudget.Value, config.PointsIssuedThisMonth, monthlyRemaining, adjusted);
        }

        // Check 3: Daily budget
        if (config.DailyPointsBudget.HasValue)
        {
            int dailyRemaining = config.DailyPointsBudget.Value - config.PointsIssuedToday;
            adjusted = Math.Min(adjusted, Math.Max(0, dailyRemaining));
            _logger.LogDebug("LoyaltyBudget: DailyBudget={Budget} Issued={Issued} Remaining={Rem} → adjusted={New}",
                config.DailyPointsBudget.Value, config.PointsIssuedToday, dailyRemaining, adjusted);
        }

        // Check 4: Per-customer daily limit (query LoyaltyIssuanceRecord — Phase 1 entity)
        if (config.PerCustomerDailyLimit.HasValue)
        {
            var todayStart = DateTime.UtcNow.Date;
            var customerIssuedToday = await _dbContext.LoyaltyIssuanceRecords
                .IgnoreQueryFilters()
                .AsNoTracking()
                .Where(r => r.TenantId == tenantIdValue
                    && r.CustomerId == customerId
                    && !r.IsReversed
                    && r.IssuedAt >= todayStart)
                .SumAsync(r => (int?)r.PointsIssued, ct) ?? 0;

            int customerRemaining = config.PerCustomerDailyLimit.Value - customerIssuedToday;
            adjusted = Math.Min(adjusted, Math.Max(0, customerRemaining));
            _logger.LogDebug("LoyaltyBudget: PerCustomerDailyLimit={Limit} IssuedToday={Issued} Remaining={Rem} → adjusted={New}",
                config.PerCustomerDailyLimit.Value, customerIssuedToday, customerRemaining, adjusted);
        }

        if (adjusted < requestedPoints)
        {
            _logger.LogInformation(
                "LoyaltyBudget: Tenant {TenantId} customer {CustomerId} — points adjusted {Orig}→{New} (budget cap applied)",
                tenantId, customerId, requestedPoints, adjusted);
        }

        return adjusted;
    }

    public async Task RecordIssuanceAsync(Guid tenantId, int pointsIssued, CancellationToken ct = default)
    {
        if (pointsIssued <= 0) return;

        var tenantIdValue = new TenantId(tenantId);

        // FIX I1: atomic increment via ExecuteUpdateAsync (EF Core 7+)
        // Avoids read-modify-write race condition with concurrent AddPoints calls
        int updated = await _dbContext.LoyaltyTenantConfigs
            .IgnoreQueryFilters()
            .Where(c => c.TenantId == tenantIdValue)
            .ExecuteUpdateAsync(s => s
                .SetProperty(c => c.PointsIssuedThisMonth, c => c.PointsIssuedThisMonth + pointsIssued)
                .SetProperty(c => c.PointsIssuedToday, c => c.PointsIssuedToday + pointsIssued), ct);

        if (updated == 0)
        {
            // No config row exists — counters default to 0, no need to track
            _logger.LogDebug("LoyaltyBudget: RecordIssuanceAsync — no LoyaltyTenantConfig for tenant {TenantId}, skipping counter increment", tenantId);
        }
    }

    public async Task DecrementIssuanceAsync(Guid tenantId, int pointsToReverse, CancellationToken ct = default)
    {
        if (pointsToReverse <= 0) return;

        var tenantIdValue = new TenantId(tenantId);

        // Atomic decrement, clamped to 0 (not below)
        int updated = await _dbContext.LoyaltyTenantConfigs
            .IgnoreQueryFilters()
            .Where(c => c.TenantId == tenantIdValue)
            .ExecuteUpdateAsync(s => s
                .SetProperty(c => c.PointsIssuedThisMonth, c => Math.Max(0, c.PointsIssuedThisMonth - pointsToReverse))
                .SetProperty(c => c.PointsIssuedToday, c => Math.Max(0, c.PointsIssuedToday - pointsToReverse)), ct);

        if (updated == 0)
        {
            _logger.LogDebug("LoyaltyBudget: DecrementIssuanceAsync — no LoyaltyTenantConfig for tenant {TenantId}", tenantId);
        }
    }

    public async Task ResetAllDailyCountersAsync(CancellationToken ct = default)
    {
        // Reset PointsIssuedToday to 0 for ALL tenants (cross-tenant — IgnoreQueryFilters)
        int updated = await _dbContext.LoyaltyTenantConfigs
            .IgnoreQueryFilters()
            .ExecuteUpdateAsync(s => s.SetProperty(c => c.PointsIssuedToday, 0), ct);

        _logger.LogInformation("LoyaltyBudget: Daily reset — {Count} tenant configs had PointsIssuedToday reset to 0", updated);
    }

    public async Task ResetAllMonthlyCountersAsync(CancellationToken ct = default)
    {
        // Reset PointsIssuedThisMonth to 0 for ALL tenants
        int updated = await _dbContext.LoyaltyTenantConfigs
            .IgnoreQueryFilters()
            .ExecuteUpdateAsync(s => s.SetProperty(c => c.PointsIssuedThisMonth, 0), ct);

        _logger.LogInformation("LoyaltyBudget: Monthly reset — {Count} tenant configs had PointsIssuedThisMonth reset to 0", updated);
    }
}
