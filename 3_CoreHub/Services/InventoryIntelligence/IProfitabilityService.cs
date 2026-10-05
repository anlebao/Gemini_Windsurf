using VanAn.Shared.Domain;

namespace VanAn.CoreHub.Services.InventoryIntelligence
{
    /// <summary>Lợi nhuận 1 món trong ca: Profit = Revenue − FoodCost (SRS §5.2 Profitability per Item).</summary>
    public sealed record ProfitabilityPerItem(
        Guid ProductId,
        string ProductName,
        int UnitsSold,
        decimal Revenue,
        decimal FoodCost,
        decimal Profit,
        decimal ProfitPercent);

    /// <summary>Lợi nhuận 1 ca: ShiftProfit = Sales − Cogs (SRS §5.2 Profitability per Shift).</summary>
    public sealed record ProfitabilityReport(
        Guid ShiftId,
        decimal Sales,
        decimal Cogs,
        decimal ShiftProfit,
        decimal ShiftProfitPercent,
        IReadOnlyList<ProfitabilityPerItem> Items);

    /// <summary>
    /// VA-IIE Phase 4 (2026-10-05): Profitability per Item / per Shift (SRS §5.2).
    /// Nguồn: FoodCostReport (đã có Sales/Cogs/Items) — thin wrapper thêm tên sản phẩm + lợi nhuận.
    /// </summary>
    public interface IProfitabilityService
    {
        /// <summary>Lợi nhuận ca theo ShiftId (pure read — không persist). Null nếu ca không tồn tại.</summary>
        Task<ProfitabilityReport?> GetShiftProfitabilityAsync(Guid shiftId, CancellationToken ct = default);
    }
}
