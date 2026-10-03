using VanAn.Shared.Domain;

namespace VanAn.CoreHub.Services.InventoryIntelligence
{
    /// <summary>Phân loại variance (SRS §3.4).</summary>
    public enum VarianceClassification
    {
        Normal = 0,   // Variance ≈ 0 (trong dung sai)
        Loss = 1,     // Variance > 0 — hao hụt (thực > lý thuyết)
        Anomaly = 2   // Variance < 0 — bất thường (thực < lý thuyết — sai kiểm kê / gian lận ẩn)
    }

    /// <summary>
    /// VA-IIE (Sprint B, P2.4): Variance Analysis — Actual = Opening + MidShiftStockIn − Closing;
    /// Variance = Actual − Theoretical; phân loại hao hụt/bất thường/bình thường (SRS §3.4).
    /// Tạo + persist <see cref="TheoreticalConsumption"/> rows (cache, không tính lại — SRS §7.3).
    /// </summary>
    public interface IVarianceAnalysisService
    {
        /// <summary>
        /// Tính variance cho ca và persist các <see cref="TheoreticalConsumption"/>.
        /// </summary>
        Task<IReadOnlyList<TheoreticalConsumption>> AnalyzeShiftAsync(
            Shift shift,
            IReadOnlyList<InventoryCount> counts,
            IReadOnlyDictionary<Guid, decimal> theoreticalByIngredient,
            CancellationToken ct = default);

        /// <summary>Pure calc — Actual = Opening + MidShift − Closing (dùng chung test/UI).</summary>
        static decimal CalculateActual(IReadOnlyList<InventoryCount> countsForIngredient)
        {
            decimal opening = 0m;
            decimal closing = 0m;
            decimal restock = 0m;
            foreach (InventoryCount count in countsForIngredient)
            {
                if (count.CountType == CountType.Opening)
                {
                    opening = count.Quantity;
                    restock += count.MidShiftStockIn ?? 0m;
                }
                else if (count.CountType == CountType.Closing)
                {
                    closing = count.Quantity;
                }
            }
            return opening + restock - closing;
        }

        /// <summary>Pure classification (SRS §3.4).</summary>
        static VarianceClassification Classify(decimal variance, decimal tolerancePercent = 0m)
        {
            if (variance > tolerancePercent)
            {
                return VarianceClassification.Loss;
            }
            if (variance < -tolerancePercent)
            {
                return VarianceClassification.Anomaly;
            }
            return VarianceClassification.Normal;
        }
    }
}
