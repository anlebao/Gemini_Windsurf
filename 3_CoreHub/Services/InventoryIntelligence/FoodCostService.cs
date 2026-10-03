using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using VanAn.CoreHub.Infrastructure;
using VanAn.Shared.Domain;

namespace VanAn.CoreHub.Services.InventoryIntelligence
{
    /// <summary>
    /// VA-IIE (Sprint B, P2.6): Food Cost / COGS / Waste Ratio (SRS §3.4, §5.2).
    /// </summary>
    public sealed class FoodCostService(IVanAnDbContext context, ILogger<FoodCostService> logger) : IFoodCostService
    {
        private readonly IVanAnDbContext _context = context;
        private readonly ILogger<FoodCostService> _logger = logger;

        public FoodCostReport Calculate(
            Shift shift,
            IReadOnlyList<TheoreticalConsumption> consumptions,
            IReadOnlyDictionary<Guid, Ingredient> ingredients,
            IReadOnlyDictionary<Guid, Recipe> recipesByProduct,
            IReadOnlyDictionary<Guid, int> unitsSoldByProduct,
            IReadOnlyDictionary<Guid, decimal> revenueByProduct,
            decimal totalSales)
        {
            // Per-item food cost: Σ(line.Quantity × PricePerUnit) × (1 + WasteFactor)
            List<FoodCostPerItem> items = [];
            foreach ((Guid productId, Recipe recipe) in recipesByProduct)
            {
                decimal costPerUnit = 0m;
                foreach (RecipeLine line in recipe.Lines)
                {
                    if (ingredients.TryGetValue(line.IngredientId, out Ingredient? ingredient))
                    {
                        costPerUnit += line.Quantity * ingredient.PricePerUnit;
                    }
                }
                costPerUnit *= 1m + recipe.WasteFactor;

                int units = unitsSoldByProduct.GetValueOrDefault(productId);
                if (units <= 0)
                {
                    continue; // chỉ báo món đã bán trong ca
                }
                items.Add(new FoodCostPerItem(
                    productId,
                    costPerUnit,
                    units,
                    costPerUnit * units,
                    revenueByProduct.GetValueOrDefault(productId)));
            }

            // COGS = Actual consumption × price/unit; Theoretical cost = Theoretical × price/unit
            decimal cogs = 0m;
            decimal theoreticalCost = 0m;
            foreach (TheoreticalConsumption c in consumptions)
            {
                if (!ingredients.TryGetValue(c.IngredientId, out Ingredient? ingredient))
                {
                    continue;
                }
                cogs += c.ActualQuantity * ingredient.PricePerUnit;
                theoreticalCost += c.TheoreticalQuantity * ingredient.PricePerUnit;
            }

            decimal wasteCost = cogs - theoreticalCost;
            decimal wasteRatio = theoreticalCost == 0m ? 0m : (wasteCost / theoreticalCost) * 100m;
            decimal foodCostPercent = totalSales == 0m ? 0m : (cogs / totalSales) * 100m;

            return new FoodCostReport
            {
                Cogs = cogs,
                TheoreticalCost = theoreticalCost,
                WasteCost = wasteCost,
                WasteRatioPercent = wasteRatio,
                Sales = totalSales,
                FoodCostPercent = foodCostPercent,
                Items = items
            };
        }

        public async Task<FoodCostReport> GetShiftFoodCostAsync(Guid shiftId, CancellationToken ct = default)
        {
            Shift? shift = await _context.Shifts.FirstOrDefaultAsync(s => s.Id == shiftId, ct)
                ?? throw new InvalidOperationException($"Shift {shiftId} không tồn tại");

            IReadOnlyList<TheoreticalConsumption> consumptions = await _context.TheoreticalConsumptions
                .Where(t => t.ShiftId == shiftId)
                .ToListAsync(ct);

            IReadOnlyList<Guid> ingredientIds = consumptions.Select(c => c.IngredientId).Distinct().ToList();
            Dictionary<Guid, Ingredient> ingredients = await _context.Ingredients
                .Where(i => ingredientIds.Contains(i.Id))
                .ToDictionaryAsync(i => i.Id, ct);

            // Orders + recipes cho per-item food cost
            DateTime end = shift.EndTime ?? DateTime.UtcNow;
            List<Order> orders = await _context.Orders
                .Include(o => o.Items)
                .Where(o => o.CreatedAt >= shift.StartTime && o.CreatedAt <= end)
                .ToListAsync(ct);

            Dictionary<Guid, int> unitsSold = [];
            Dictionary<Guid, decimal> revenue = [];
            decimal totalSales = 0m;
            foreach (Order order in orders)
            {
                if (order.Status != OrderStatusId.Completed)
                {
                    continue;
                }
                totalSales += order.TotalPrice;
                foreach (OrderItem item in order.Items)
                {
                    unitsSold[item.ProductId] = unitsSold.GetValueOrDefault(item.ProductId) + item.Quantity;
                    revenue[item.ProductId] = revenue.GetValueOrDefault(item.ProductId) + (item.Quantity * item.UnitPrice);
                }
            }

            List<Recipe> recipes = await _context.Recipes
                .Include(r => r.Lines)
                .Where(r => r.IsActive && unitsSold.Keys.Contains(r.ProductId))
                .ToListAsync(ct);
            Dictionary<Guid, Recipe> recipesByProduct = recipes.ToDictionary(r => r.ProductId);

            return Calculate(shift, consumptions, ingredients, recipesByProduct, unitsSold, revenue, totalSales);
        }
    }
}
