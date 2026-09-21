using VanAn.CoreHub.Services;
using Xunit;

namespace VanAn.Tests.Services;

/// <summary>
/// Loyalty Points Integrity (Batch 4, Phase 4, T4.1) — single points formula (D1).
/// Decision D1 (APPROVED 2026-09-20): base = NET revenue = SubTotal − DiscountAmount
/// (VAT + shipping excluded). Silo: base × rate clamped [Min, Max]; Alliance: base / VndPerPoint.
/// Spec: docs/plans/loyalty-integrity-detail-coding-plan.md §Phase 4.
/// </summary>
public class LoyaltyPointsCalculatorTests
{
    // ──────────────────────────────────────────────────────────
    // NetRevenue (D1)
    // ──────────────────────────────────────────────────────────

    [Fact(DisplayName = "CALC-1: NetRevenue = SubTotal − DiscountAmount (VAT + shipping excluded)")]
    public void NetRevenue_SubtractsDiscountOnly()
    {
        // SubTotal 100,000 + VAT 8,000 + shipping 15,000 − discount 10,000 — base must ignore
        // VAT and shipping entirely: only SubTotal − DiscountAmount.
        decimal net = LoyaltyPointsCalculator.NetRevenue(subTotal: 100_000m, discountAmount: 10_000m);
        Assert.Equal(90_000m, net);
    }

    [Fact(DisplayName = "CALC-2: NetRevenue clamps ≥ 0 when discount exceeds subtotal")]
    public void NetRevenue_DiscountExceedsSubtotal_ClampsToZero()
    {
        Assert.Equal(0m, LoyaltyPointsCalculator.NetRevenue(100_000m, 150_000m));
        Assert.Equal(0m, LoyaltyPointsCalculator.NetRevenue(100_000m, 100_000m));
    }

    // ──────────────────────────────────────────────────────────
    // Silo mode (rate)
    // ──────────────────────────────────────────────────────────

    [Fact(DisplayName = "CALC-3: Silo 10% of net revenue 90,000 → 9,000 points")]
    public void Calculate_Silo_RateAppliedToNetRevenue()
    {
        var formula = new PointsFormula(Rate: 0.1m, MinPoints: 10, MaxPoints: null, Mode: PointsFormulaMode.Silo);
        Assert.Equal(9_000, LoyaltyPointsCalculator.Calculate(90_000m, formula));
    }

    [Fact(DisplayName = "CALC-4: Silo min clamp — small order raises to MinPoints")]
    public void Calculate_Silo_MinClampApplies()
    {
        var formula = new PointsFormula(Rate: 0.1m, MinPoints: 10, MaxPoints: null, Mode: PointsFormulaMode.Silo);
        Assert.Equal(10, LoyaltyPointsCalculator.Calculate(50m, formula)); // 5 → floor 10
    }

    [Fact(DisplayName = "CALC-5: Silo max clamp — large order capped at MaxPoints")]
    public void Calculate_Silo_MaxClampApplies()
    {
        var formula = new PointsFormula(Rate: 0.1m, MinPoints: 10, MaxPoints: 500, Mode: PointsFormulaMode.Silo);
        Assert.Equal(500, LoyaltyPointsCalculator.Calculate(900_000m, formula)); // 90,000 → cap 500
    }

    [Fact(DisplayName = "CALC-6: Silo rate 0 → fallback default rate 10%")]
    public void Calculate_Silo_ZeroRateFallsBackToDefault()
    {
        var formula = new PointsFormula(Rate: 0m, MinPoints: 10, MaxPoints: null, Mode: PointsFormulaMode.Silo);
        Assert.Equal(9_000, LoyaltyPointsCalculator.Calculate(90_000m, formula)); // 90,000 × 0.1
    }

    // ──────────────────────────────────────────────────────────
    // Alliance mode (VND per point, Option A)
    // ──────────────────────────────────────────────────────────

    [Fact(DisplayName = "CALC-7: Alliance net revenue 90,000 / 1000 → 90 points")]
    public void Calculate_Alliance_DividesByVndPerPoint()
    {
        var formula = new PointsFormula(Rate: 0.1m, MinPoints: 10, MaxPoints: null, Mode: PointsFormulaMode.Alliance, VndPerPoint: 1000);
        Assert.Equal(90, LoyaltyPointsCalculator.Calculate(90_000m, formula));
    }

    [Fact(DisplayName = "CALC-8: Alliance VndPerPoint 0 → fallback default 1000")]
    public void Calculate_Alliance_ZeroVndPerPointFallsBackToDefault()
    {
        var formula = new PointsFormula(Rate: 0.1m, MinPoints: 10, MaxPoints: null, Mode: PointsFormulaMode.Alliance, VndPerPoint: 0);
        Assert.Equal(90, LoyaltyPointsCalculator.Calculate(90_000m, formula));
    }

    [Fact(DisplayName = "CALC-9: Alliance custom VndPerPoint 500 → 180 points for 90,000")]
    public void Calculate_Alliance_CustomVndPerPoint()
    {
        var formula = new PointsFormula(Rate: 0.1m, MinPoints: 10, MaxPoints: null, Mode: PointsFormulaMode.Alliance, VndPerPoint: 500);
        Assert.Equal(180, LoyaltyPointsCalculator.Calculate(90_000m, formula));
    }

    [Fact(DisplayName = "CALC-10: Alliance min clamp applies")]
    public void Calculate_Alliance_MinClampApplies()
    {
        var formula = new PointsFormula(Rate: 0.1m, MinPoints: 10, MaxPoints: null, Mode: PointsFormulaMode.Alliance, VndPerPoint: 1000);
        Assert.Equal(10, LoyaltyPointsCalculator.Calculate(2_000m, formula)); // 2 → floor 10
    }

    // ──────────────────────────────────────────────────────────
    // Zero / free orders
    // ──────────────────────────────────────────────────────────

    [Fact(DisplayName = "CALC-11: net revenue 0 (free order) → 0 points, min clamp does NOT apply")]
    public void Calculate_ZeroNetRevenue_ReturnsZero()
    {
        var formula = new PointsFormula(Rate: 0.1m, MinPoints: 10, MaxPoints: null, Mode: PointsFormulaMode.Silo);
        Assert.Equal(0, LoyaltyPointsCalculator.Calculate(0m, formula));

        var allianceFormula = new PointsFormula(Rate: 0.1m, MinPoints: 10, MaxPoints: null, Mode: PointsFormulaMode.Alliance);
        Assert.Equal(0, LoyaltyPointsCalculator.Calculate(0m, allianceFormula));
    }
}
