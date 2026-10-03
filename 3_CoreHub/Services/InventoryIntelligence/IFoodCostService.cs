using VanAn.Shared.Domain;

namespace VanAn.CoreHub.Services.InventoryIntelligence
{
    /// <summary>Food cost 1 món: chi phí nguyên liệu cho 1 đơn vị bán.</summary>
    public sealed record FoodCostPerItem(Guid ProductId, decimal FoodCostPerUnit, int UnitsSold, decimal TotalFoodCost, decimal Revenue);

    /// <summary>Báo cáo Food Cost / COGS / Waste Ratio cho 1 ca (SRS §5.2).</summary>
    public sealed record FoodCostReport
    {
        public decimal Cogs { get; init; }              // Σ ActualConsumption × PricePerUnit
        public decimal TheoreticalCost { get; init; }   // Σ TheoreticalConsumption × PricePerUnit
        public decimal WasteCost { get; init; }         // Cogs − TheoreticalCost
        public decimal WasteRatioPercent { get; init; } // WasteCost / TheoreticalCost × 100
        public decimal Sales { get; init; }
        public decimal FoodCostPercent { get; init; }   // Cogs / Sales × 100
        public IReadOnlyList<FoodCostPerItem> Items { get; init; } = [];
    }

    /// <summary>
    /// VA-IIE (Sprint B, P2.6): Food Cost / COGS / Waste Ratio theo món/ca (SRS §3.4, §5.2).
    /// FoodCostPerUnit = Σ(RecipeLine.Quantity × Ingredient.PricePerUnit) × (1 + WasteFactor).
    /// COGS ca = Σ(ActualConsumption × PricePerUnit); Waste = COGS − TheoreticalCost.
    /// </summary>
    public interface IFoodCostService
    {
        /// <summary>Tính food cost cho 1 ca từ dữ liệu đã load (pure — không persist).</summary>
        FoodCostReport Calculate(
            Shift shift,
            IReadOnlyList<TheoreticalConsumption> consumptions,
            IReadOnlyDictionary<Guid, Ingredient> ingredients,
            IReadOnlyDictionary<Guid, Recipe> recipesByProduct,
            IReadOnlyDictionary<Guid, int> unitsSoldByProduct,
            IReadOnlyDictionary<Guid, decimal> revenueByProduct,
            decimal totalSales);

        /// <summary>Load dữ liệu + tính food cost cho ca (theo ShiftId).</summary>
        Task<FoodCostReport> GetShiftFoodCostAsync(Guid shiftId, CancellationToken ct = default);
    }
}
