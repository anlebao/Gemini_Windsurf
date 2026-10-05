using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using VanAn.CoreHub.Infrastructure;
using VanAn.Shared.Domain;

namespace VanAn.CoreHub.Services.InventoryIntelligence
{
    /// <summary>
    /// VA-IIE (Sprint B, P2.3): tiêu hao lý thuyết = Σ(RecipeLine.Quantity × UnitsSold × (1 + WasteFactor)).
    /// Version resolve theo thời điểm bán (EffectiveFrom ≤ OrderTime, version cao nhất) — SRS §3.2.3.
    /// Ca &gt; 500 đơn → aggregate theo product trước (tránh OOM — SRS §7.3).
    /// </summary>
    public sealed class TheoreticalConsumptionService(IVanAnDbContext context, ILogger<TheoreticalConsumptionService> logger) : ITheoreticalConsumptionService
    {
        private const int BatchOrderThreshold = 500;

        private readonly IVanAnDbContext _context = context;
        private readonly ILogger<TheoreticalConsumptionService> _logger = logger;

        public async Task<IReadOnlyDictionary<Guid, decimal>> CalculateForShiftAsync(Shift shift, CancellationToken ct = default)
        {
            DateTime end = shift.EndTime ?? DateTime.UtcNow;

            // 1. Order Completed trong ca (kèm items) — SRS §3.3. Multi-tenancy: filter theo shift.TenantId
            //    (catalog chung 1 SQLite nhiều tenant — RV 2026-10-04).
            List<(Guid ProductId, decimal Quantity, DateTime OrderTime)> items = [];
            var orders = await _context.Orders
                .Include(o => o.Items)
                .Where(o => o.CreatedAt >= shift.StartTime && o.CreatedAt <= end && o.TenantId == shift.TenantId)
                .ToListAsync(ct);

            foreach (Order order in orders)
            {
                if (order.Status != OrderStatusId.Completed)
                {
                    continue;
                }
                foreach (OrderItem item in order.Items)
                {
                    if (item.Quantity <= 0)
                    {
                        continue;
                    }
                    items.Add((item.ProductId, item.Quantity, order.CreatedAt));
                }
            }

            if (items.Count == 0)
            {
                return new Dictionary<Guid, decimal>();
            }

            // 2. Load tất cả recipe (kèm lines) cho các product đã bán — 1 query, tránh N+1.
            IReadOnlyList<Guid> productIds = items.Select(i => i.ProductId).Distinct().ToList();
            List<Recipe> recipes = await _context.Recipes
                .Include(r => r.Lines)
                .Where(r => productIds.Contains(r.ProductId) && !r.IsDeleted)
                .ToListAsync(ct);

            Dictionary<Guid, IReadOnlyList<Recipe>> byProduct = recipes
                .GroupBy(r => r.ProductId)
                .ToDictionary(g => g.Key, g => (IReadOnlyList<Recipe>)g.OrderBy(r => r.EffectiveFrom).ThenBy(r => r.Version).ToList());

            return CalculateFromItems(items, byProduct);
        }

        public IReadOnlyDictionary<Guid, decimal> CalculateFromItems(
            IReadOnlyList<(Guid ProductId, decimal Quantity, DateTime OrderTime)> items,
            IReadOnlyDictionary<Guid, IReadOnlyList<Recipe>> recipesByProduct)
        {
            Dictionary<Guid, decimal> consumption = [];

            // Batch-safe: > 500 đơn → aggregate theo (product, version window) thay vì per-item (SRS §7.3).
            if (items.Count > BatchOrderThreshold)
            {
                foreach (IGrouping<Guid, (Guid ProductId, decimal Quantity, DateTime OrderTime)> productGroup in items.GroupBy(i => i.ProductId))
                {
                    if (!recipesByProduct.TryGetValue(productGroup.Key, out IReadOnlyList<Recipe>? versions) || versions.Count == 0)
                    {
                        continue; // RECIPE_MISSING — alert engine xử lý
                    }
                    Recipe? recipe = ResolveVersionAt(versions, productGroup.Min(i => i.OrderTime));
                    if (recipe is null)
                    {
                        continue;
                    }
                    decimal totalUnits = productGroup.Sum(i => i.Quantity);
                    AddConsumption(consumption, recipe, totalUnits);
                }
            }
            else
            {
                foreach ((Guid productId, decimal quantity, DateTime orderTime) in items)
                {
                    if (!recipesByProduct.TryGetValue(productId, out IReadOnlyList<Recipe>? versions) || versions.Count == 0)
                    {
                        continue; // RECIPE_MISSING — alert engine xử lý
                    }
                    Recipe? recipe = ResolveVersionAt(versions, orderTime);
                    if (recipe is null)
                    {
                        continue;
                    }
                    AddConsumption(consumption, recipe, quantity);
                }
            }

            _logger.LogDebug("Theoretical consumption computed for {ItemCount} items → {IngredientCount} ingredients", items.Count, consumption.Count);
            return consumption;
        }

        private static void AddConsumption(Dictionary<Guid, decimal> consumption, Recipe recipe, decimal units)
        {
            foreach (RecipeLine line in recipe.Lines)
            {
                decimal amount = line.Quantity * units * (1m + recipe.WasteFactor);
                consumption[line.IngredientId] = consumption.GetValueOrDefault(line.IngredientId) + amount;
            }
        }

        /// <summary>Version active tại thời điểm bán: EffectiveFrom ≤ time, version cao nhất.</summary>
        private static Recipe? ResolveVersionAt(IReadOnlyList<Recipe> versionsSortedByEffectiveFrom, DateTime time)
        {
            Recipe? best = null;
            foreach (Recipe recipe in versionsSortedByEffectiveFrom)
            {
                if (recipe.EffectiveFrom <= time)
                {
                    best = recipe;
                }
                else
                {
                    break; // danh sách đã sort theo EffectiveFrom
                }
            }
            return best;
        }
    }
}
