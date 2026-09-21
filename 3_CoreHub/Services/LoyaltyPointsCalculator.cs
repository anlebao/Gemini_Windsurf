namespace VanAn.CoreHub.Services;

/// <summary>
/// Points formula mode — Silo uses the per-tenant rate; Alliance uses VND-per-point (Option A).
/// </summary>
public enum PointsFormulaMode
{
    Silo = 0,
    Alliance = 1
}

/// <summary>
/// Resolved points formula passed to <see cref="LoyaltyPointsCalculator"/>.
/// Resolution order (unchanged, done by the callers): tenant settings (ShopFeatureSettings)
/// → PG LoyaltyGlobalConfig (int % → /100) → appsettings. The calculator itself is pure.
/// </summary>
public readonly record struct PointsFormula(
    decimal Rate,
    int MinPoints,
    int? MaxPoints,
    PointsFormulaMode Mode,
    int VndPerPoint = LoyaltyPointsCalculator.DefaultVndPerPoint);

/// <summary>
/// Loyalty Points Integrity (Batch 4, Phase 4, T4.1) — SINGLE points formula shared by the
/// award path (OrderWorkflowService), the tracking banner (PublicOrdersController) and the
/// checkout estimate (Gateway /api/loyalty/estimate). Fixes RC2 (3 divergent formulas).
///
/// Decision D1 (APPROVED 2026-09-20): base = NET revenue = SubTotal − DiscountAmount
/// (VAT + shipping excluded; Order.SubTotal does NOT shrink with discount — it only reduces
/// TotalAmount, so the base must be computed explicitly).
///   - Silo:     base × rate, clamped to [MinPoints, MaxPoints]
///   - Alliance: base / VndPerPoint (Option A — loyalty_points_visibility Phase 3)
///
/// Clamp min/max lives HERE only (single source). Rate ≤ 0 falls back to 10%; VndPerPoint ≤ 0
/// falls back to 1000. NetRevenue ≤ 0 (free order) → 0 points, min clamp does not apply.
/// </summary>
public static class LoyaltyPointsCalculator
{
    /// <summary>Default Silo rate when config resolves to 0 (appsettings LoyaltyPoints default).</summary>
    public const decimal DefaultRate = 0.1m;

    /// <summary>Default VND per point for Alliance mode (Option A — 1 point = 1000 VND).</summary>
    public const int DefaultVndPerPoint = 1000;

    /// <summary>D1 net revenue — clamped ≥ 0 (discount larger than subtotal → 0).</summary>
    public static decimal NetRevenue(decimal subTotal, decimal discountAmount)
        => subTotal - discountAmount > 0 ? subTotal - discountAmount : 0m;

    /// <summary>Compute points for a net revenue base using the resolved formula (D1).</summary>
    public static int Calculate(decimal netRevenue, PointsFormula formula)
    {
        if (netRevenue <= 0)
        {
            // Free order (discount = subtotal) — no points; min clamp must NOT raise it.
            return 0;
        }

        int points = formula.Mode == PointsFormulaMode.Alliance
            ? (int)(netRevenue / (formula.VndPerPoint > 0 ? formula.VndPerPoint : DefaultVndPerPoint))
            : (int)(netRevenue * (formula.Rate > 0 ? formula.Rate : DefaultRate));

        if (points < formula.MinPoints)
        {
            points = formula.MinPoints;
        }

        if (formula.MaxPoints.HasValue && points > formula.MaxPoints.Value)
        {
            points = formula.MaxPoints.Value;
        }

        return points;
    }
}
