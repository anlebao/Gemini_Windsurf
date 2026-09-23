using VanAn.Shared.Domain.Common;

namespace VanAn.CoreHub.Services;

/// <summary>
/// VALCN v2.0 Phase 3 — Loyalty budget enforcement.
/// Checks budget caps before AddPoints + records issuance (atomic counter increment)
/// + decrements on reversal (Phase 4). Feature-flagged via ValcnV2_LoyaltyBudget (default OFF).
/// </summary>
public interface ILoyaltyBudgetService
{
    /// <summary>
    /// Check budget caps and return adjusted points (may be lower than requested, or 0 if exhausted).
    /// Caps applied: PerOrderRateCap, MonthlyPointsBudget, DailyPointsBudget, PerCustomerDailyLimit.
    /// Does NOT modify counters — caller must call RecordIssuanceAsync after AddPoints succeeds.
    /// </summary>
    /// <param name="tenantId">Tenant ID.</param>
    /// <param name="customerId">Customer ID (for per-customer daily limit check).</param>
    /// <param name="orderAmount">Order total amount for the per-order rate cap. Null (non-order awards,
    /// e.g. mission/welcome) SKIPS the PerOrderRateCap — monthly/daily/per-customer caps still apply.</param>
    /// <param name="requestedPoints">Points calculated by existing formula (rate × amount, clamped to min/max).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Adjusted points (0 = budget exhausted, skip reward; &gt;0 = capped points to award).</returns>
    Task<int> CheckAndAdjustPointsAsync(Guid tenantId, Guid customerId, decimal? orderAmount, int requestedPoints, CancellationToken ct = default);

    /// <summary>
    /// Record points issuance — atomically increment PointsIssuedThisMonth + PointsIssuedToday counters.
    /// Uses ExecuteUpdateAsync (EF Core 7+) to avoid read-modify-write race condition (fix I1).
    /// Call AFTER AddPoints succeeds.
    /// </summary>
    Task RecordIssuanceAsync(Guid tenantId, int pointsIssued, CancellationToken ct = default);

    /// <summary>
    /// Decrement counters on reversal (Phase 4 — RefundOrchestrationService).
    /// Atomic via ExecuteUpdateAsync, clamped to 0 (not below).
    /// </summary>
    Task DecrementIssuanceAsync(Guid tenantId, int pointsToReverse, CancellationToken ct = default);

    /// <summary>
    /// Reset PointsIssuedToday to 0 for ALL tenants (called by LoyaltyBudgetDailyResetJob at 00:00 UTC).
    /// </summary>
    Task ResetAllDailyCountersAsync(CancellationToken ct = default);

    /// <summary>
    /// Reset PointsIssuedThisMonth to 0 for ALL tenants (called by LoyaltyBudgetMonthlyResetJob on 1st of month).
    /// </summary>
    Task ResetAllMonthlyCountersAsync(CancellationToken ct = default);

    /// <summary>
    /// #185-4: Read-only view of a tenant's budget caps + runtime counters
    /// (for owner-facing display in /settings/shop-features — caps are set by
    /// SystemAdmin in /admin/loyalty-config). Default = empty DTO (no caps).
    /// </summary>
    Task<LoyaltyTenantCapsDto> GetCapsAsync(Guid tenantId, CancellationToken ct = default)
        => Task.FromResult(new LoyaltyTenantCapsDto { TenantId = tenantId });
}

/// <summary>
/// #185-4: Read-only snapshot of per-tenant loyalty budget caps + counters.
/// All caps nullable (null = unlimited / no override configured).
/// </summary>
public sealed class LoyaltyTenantCapsDto
{
    public Guid TenantId { get; set; }
    public bool HasConfig { get; set; }
    public int? MonthlyPointsBudget { get; set; }
    public int? DailyPointsBudget { get; set; }
    public int? PerCustomerDailyLimit { get; set; }
    public decimal? PerOrderRateCap { get; set; }
    public int PointsIssuedThisMonth { get; set; }
    public int PointsIssuedToday { get; set; }
}
