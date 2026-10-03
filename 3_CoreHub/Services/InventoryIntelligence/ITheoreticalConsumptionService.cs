using VanAn.Shared.Domain;

namespace VanAn.CoreHub.Services.InventoryIntelligence
{
    /// <summary>
    /// VA-IIE (Sprint B, P2.3): tính tiêu hao lý thuyết từ POS OrderItems × Recipe version active tại thời điểm bán
    /// × (1 + WasteFactor) (SRS §3.3, §7.3). Real-time khi đóng ca; batch-safe cho ca &gt; 500 đơn.
    /// Kết quả persist qua <see cref="TheoreticalConsumption"/> (cache, không tính lại — SRS §7.3).
    /// </summary>
    public interface ITheoreticalConsumptionService
    {
        /// <summary>
        /// Tính tiêu hao lý thuyết cho 1 ca: đọc Order Completed trong [StartTime, EndTime],
        /// resolve Recipe version active tại thời điểm bán, aggregate theo ingredient.
        /// </summary>
        Task<IReadOnlyDictionary<Guid, decimal>> CalculateForShiftAsync(Shift shift, CancellationToken ct = default);

        /// <summary>
        /// Tính tiêu hao từ order items đã load sẵn (dùng chung cho test/integration) — recipe theo <paramref name="recipesByProduct"/>.
        /// </summary>
        IReadOnlyDictionary<Guid, decimal> CalculateFromItems(
            IReadOnlyList<(Guid ProductId, decimal Quantity, DateTime OrderTime)> items,
            IReadOnlyDictionary<Guid, IReadOnlyList<Recipe>> recipesByProduct);
    }
}
