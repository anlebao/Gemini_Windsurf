using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using VanAn.CoreHub.Infrastructure;
using VanAn.Shared.Domain;

namespace VanAn.CoreHub.Services.InventoryIntelligence
{
    /// <summary>
    /// VA-IIE Phase 4 (2026-10-05): Profitability per Item / per Shift (SRS §5.2).
    /// Reuse FoodCostService (Sales/Cogs/Items đã có) — thêm tên sản phẩm + lợi nhuận tuyệt đối/%.
    /// </summary>
    public sealed class ProfitabilityService(
        IFoodCostService foodCostService,
        IVanAnDbContext context,
        ILogger<ProfitabilityService> logger) : IProfitabilityService
    {
        private readonly IFoodCostService _foodCostService = foodCostService;
        private readonly IVanAnDbContext _context = context;
        private readonly ILogger<ProfitabilityService> _logger = logger;

        public async Task<ProfitabilityReport?> GetShiftProfitabilityAsync(Guid shiftId, CancellationToken ct = default)
        {
            FoodCostReport report;
            try
            {
                report = await _foodCostService.GetShiftFoodCostAsync(shiftId, ct);
            }
            catch (InvalidOperationException)
            {
                _logger.LogWarning("Profitability: shift {ShiftId} không tồn tại", shiftId);
                return null;
            }

            IReadOnlyList<Guid> productIds = report.Items.Select(i => i.ProductId).Distinct().ToList();
            Dictionary<Guid, Product> products = productIds.Count == 0
                ? []
                : await _context.Products
                    .Where(p => productIds.Contains(p.Id))
                    .ToDictionaryAsync(p => p.Id, ct);

            List<ProfitabilityPerItem> items = report.Items
                .Select(i =>
                {
                    decimal profit = i.Revenue - i.TotalFoodCost;
                    decimal profitPercent = i.Revenue > 0m ? (profit / i.Revenue) * 100m : 0m;
                    string name = products.TryGetValue(i.ProductId, out Product? p) ? p.Name : i.ProductId.ToString();
                    return new ProfitabilityPerItem(i.ProductId, name, i.UnitsSold, i.Revenue, i.TotalFoodCost, profit, profitPercent);
                })
                .OrderByDescending(i => i.Profit)
                .ToList();

            decimal shiftProfit = report.Sales - report.Cogs;
            decimal shiftProfitPercent = report.Sales > 0m ? (shiftProfit / report.Sales) * 100m : 0m;

            _logger.LogDebug("Profitability computed for shift {ShiftId}: profit={Profit} ({Percent:F1}%)",
                shiftId, shiftProfit, shiftProfitPercent);

            return new ProfitabilityReport(shiftId, report.Sales, report.Cogs, shiftProfit, shiftProfitPercent, items);
        }
    }
}
