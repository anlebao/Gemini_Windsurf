using VanAn.CoreHub.Services.InventoryIntelligence;
using VanAn.Shared.Domain;

namespace VanAn.Core.Tests.InventoryIntelligence
{
    /// <summary>
    /// Helper dựng ShiftAlertContext cho test alert rules (pure — không DB).
    /// </summary>
    internal static class AlertContextTestFactory
    {
        internal static readonly TenantId TenantId = new(Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"));

        internal static Ingredient Ingredient(string name = "Cà phê bột", string unit = "g", decimal minStock = 5_000m, decimal price = 200m,
            IngredientCategory category = IngredientCategory.RawMaterial, decimal? varianceThreshold = null)
            => new(TenantId, name, unit, 100_000m, minStock, price, category, varianceThreshold);

        internal static ShiftAlertContext Build(
            Shift? shift = null,
            IReadOnlyList<InventoryCount>? counts = null,
            IReadOnlyList<TheoreticalConsumption>? consumptions = null,
            IReadOnlyDictionary<Guid, Ingredient>? ingredients = null,
            IReadOnlyDictionary<Guid, Recipe>? recipesByProduct = null,
            IReadOnlyDictionary<Guid, decimal>? revenueByProduct = null,
            decimal totalSales = 0m,
            int totalUnitsSold = 0,
            DateTime? now = null)
        {
            shift ??= new Shift(TenantId, ShiftType.Morning, Guid.Parse("11111111-1111-1111-1111-111111111111"));
            counts ??= [];
            consumptions ??= [];
            ingredients ??= new Dictionary<Guid, Ingredient>();
            recipesByProduct ??= new Dictionary<Guid, Recipe>();
            revenueByProduct ??= new Dictionary<Guid, decimal>();

            Dictionary<Guid, decimal> opening = [];
            Dictionary<Guid, decimal> closing = [];
            Dictionary<Guid, decimal> restock = [];
            foreach (InventoryCount c in counts)
            {
                if (c.CountType == CountType.Opening)
                {
                    opening[c.IngredientId] = c.Quantity;
                    restock[c.IngredientId] = restock.GetValueOrDefault(c.IngredientId) + (c.MidShiftStockIn ?? 0m);
                }
                else if (c.CountType == CountType.Closing)
                {
                    closing[c.IngredientId] = c.Quantity;
                }
            }
            Dictionary<Guid, decimal> stockChange = [];
            foreach (Guid id in opening.Keys.Concat(closing.Keys).Distinct())
            {
                // Net change = Closing − Opening − Restock (âm = nguyên liệu giảm — SRS §4.1 rule 4/5).
                stockChange[id] = closing.GetValueOrDefault(id) - opening.GetValueOrDefault(id) - restock.GetValueOrDefault(id);
            }
            Dictionary<Guid, decimal> revenueByIngredient = [];
            foreach ((Guid productId, decimal revenue) in revenueByProduct)
            {
                if (recipesByProduct.TryGetValue(productId, out Recipe? recipe))
                {
                    foreach (RecipeLine line in recipe.Lines)
                    {
                        revenueByIngredient[line.IngredientId] = revenueByIngredient.GetValueOrDefault(line.IngredientId) + revenue;
                    }
                }
            }

            return new ShiftAlertContext
            {
                Shift = shift,
                InventoryCounts = counts,
                Consumptions = consumptions,
                Ingredients = ingredients,
                RecipesByProduct = recipesByProduct,
                RevenueByProduct = revenueByProduct,
                TotalSales = totalSales,
                TotalUnitsSold = totalUnitsSold,
                Now = now ?? DateTime.UtcNow,
                OpeningCountByIngredient = opening,
                ClosingCountByIngredient = closing,
                RestockByIngredient = restock,
                StockChangeByIngredient = stockChange,
                RevenueByIngredient = revenueByIngredient
            };
        }
    }
}
