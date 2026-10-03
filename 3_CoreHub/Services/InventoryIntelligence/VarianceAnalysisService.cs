using Microsoft.Extensions.Logging;
using VanAn.CoreHub.Infrastructure;
using VanAn.Shared.Domain;

namespace VanAn.CoreHub.Services.InventoryIntelligence
{
    /// <summary>
    /// VA-IIE (Sprint B, P2.4): Actual = Opening + MidShiftStockIn − Closing; Variance = Actual − Theoretical.
    /// Persist <see cref="TheoreticalConsumption"/> (1 row per ingredient per shift — cache, SRS §7.3).
    /// </summary>
    public sealed class VarianceAnalysisService(IVanAnDbContext context, ILogger<VarianceAnalysisService> logger) : IVarianceAnalysisService
    {
        private readonly IVanAnDbContext _context = context;
        private readonly ILogger<VarianceAnalysisService> _logger = logger;

        public async Task<IReadOnlyList<TheoreticalConsumption>> AnalyzeShiftAsync(
            Shift shift,
            IReadOnlyList<InventoryCount> counts,
            IReadOnlyDictionary<Guid, decimal> theoreticalByIngredient,
            CancellationToken ct = default)
        {
            List<TheoreticalConsumption> result = [];

            IEnumerable<Guid> ingredientIds = counts
                .Select(c => c.IngredientId)
                .Concat(theoreticalByIngredient.Keys)
                .Distinct();

            foreach (Guid ingredientId in ingredientIds)
            {
                IReadOnlyList<InventoryCount> forIngredient = counts.Where(c => c.IngredientId == ingredientId).ToList();
                decimal actual = IVarianceAnalysisService.CalculateActual(forIngredient);
                decimal theoretical = theoreticalByIngredient.GetValueOrDefault(ingredientId);

                // Chỉ persist khi có dữ liệu ý nghĩa (có kiểm kê HOẶC có tiêu hao lý thuyết).
                if (forIngredient.Count == 0 && theoretical == 0m)
                {
                    continue;
                }

                result.Add(new TheoreticalConsumption(shift.TenantId, shift.Id, ingredientId, theoretical, actual));
            }

            if (result.Count > 0)
            {
                _context.TheoreticalConsumptions.AddRange(result);
                _ = await _context.SaveChangesAsync(ct);
            }

            _logger.LogInformation("Variance analysis for shift {ShiftId}: {Count} ingredients", shift.Id, result.Count);
            return result;
        }
    }
}
