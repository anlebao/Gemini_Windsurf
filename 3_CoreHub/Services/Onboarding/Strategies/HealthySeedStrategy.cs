using VanAn.CoreHub.Infrastructure;
using VanAn.Shared.Domain;

namespace VanAn.CoreHub.Services.Onboarding.Strategies
{
    /// <summary>
    /// Healthy seed strategy — Healthy food, juice, smoothie.
    /// IndustryCode: "HEALTHY"
    /// Seeds 12 healthy food/drink products + 10 ingredients.
    /// </summary>
    public sealed class HealthySeedStrategy : IIndustrySeedStrategy
    {
        public string IndustryCode => "HEALTHY";
        public string IndustryName => "Healthy Food";

        public async Task<IndustrySeedResult> SeedAsync(
            TenantId tenantId,
            IVanAnDbContext dbContext,
            CancellationToken ct = default)
        {
            var shop = new Shop(tenantId, "Healthy Food & Juice", "123 Nguyễn Huệ, Q1, TP.HCM", "1900-1234", "healthy@vanan.vn");
            await dbContext.Shops.AddAsync(shop, ct);

            var products = new[]
            {
                new Product(tenantId, "Sinh tố bơ", "Sinh tố bơ tươi", 50_000m, "Sinh tố"),
                new Product(tenantId, "Sinh tố xoài", "Sinh tố xoài chín", 50_000m, "Sinh tố"),
                new Product(tenantId, "Sinh tố dâu", "Sinh tố dâu tây", 55_000m, "Sinh tố"),
                new Product(tenantId, "Nước ép cam", "Nước ép cam tươi", 40_000m, "Nước ép"),
                new Product(tenantId, "Nước ép cần tây", "Nước ép cần tây detox", 45_000m, "Nước ép"),
                new Product(tenantId, "Nước ép củ quả", "Mix củ quả detox", 55_000m, "Nước ép"),
                new Product(tenantId, "Salad gà", "Salad ức gà giảm cân", 75_000m, "Salad"),
                new Product(tenantId, "Salad cá ngừ", "Salad cá ngừ healthy", 85_000m, "Salad"),
                new Product(tenantId, "Salad chay", "Salad chay thanh đạm", 60_000m, "Salad"),
                new Product(tenantId, "Cơm gà luộc", "Cơm ức gà luộc healthy", 65_000m, "Cơm healthy"),
                new Product(tenantId, "Cơm ức gà xé", "Cơm ức gà xé phở", 70_000m, "Cơm healthy"),
                new Product(tenantId, "Yến mạch trái cây", "Yến mạch mix trái cây", 50_000m, "Yến mạch"),
            };
            await dbContext.Products.AddRangeAsync(products, ct);

            var ingredients = new[]
            {
                Create(tenantId, "Bơ tươi", "trái", 100m, 10m, 15_000m),
                Create(tenantId, "Xoài chín", "trái", 100m, 10m, 12_000m),
                Create(tenantId, "Dâu tây tươi", "kg", 50m, 5m, 200_000m),
                Create(tenantId, "Cam tươi", "trái", 200m, 20m, 8_000m),
                Create(tenantId, "Cần tây", "bó", 50m, 5m, 25_000m),
                Create(tenantId, "Ức gà", "kg", 50m, 10m, 110_000m),
                Create(tenantId, "Cá ngừ hộp", "hộp", 100m, 20m, 35_000m),
                Create(tenantId, "Rau salad mix", "gói", 100m, 20m, 30_000m),
                Create(tenantId, "Yến mạch", "kg", 50m, 10m, 80_000m),
                Create(tenantId, "Sữa hạt", "chai 1L", 30m, 5m, 120_000m),
            };
            await dbContext.Ingredients.AddRangeAsync(ingredients, ct);

            var recipes = new[]
            {
                R(tenantId, products[0].Id, ingredients[0].Id, 1m),
                R(tenantId, products[0].Id, ingredients[9].Id, 0.1m),
                R(tenantId, products[1].Id, ingredients[1].Id, 1m),
                R(tenantId, products[1].Id, ingredients[9].Id, 0.1m),
                R(tenantId, products[2].Id, ingredients[2].Id, 0.1m),
                R(tenantId, products[2].Id, ingredients[9].Id, 0.1m),
                R(tenantId, products[3].Id, ingredients[3].Id, 3m),
                R(tenantId, products[4].Id, ingredients[4].Id, 1m/5m),
                R(tenantId, products[5].Id, ingredients[3].Id, 1m),
                R(tenantId, products[5].Id, ingredients[4].Id, 0.2m),
                R(tenantId, products[5].Id, ingredients[2].Id, 0.1m),
                R(tenantId, products[6].Id, ingredients[5].Id, 0.15m),
                R(tenantId, products[6].Id, ingredients[7].Id, 1m),
                R(tenantId, products[7].Id, ingredients[6].Id, 1m),
                R(tenantId, products[7].Id, ingredients[7].Id, 1m),
                R(tenantId, products[8].Id, ingredients[7].Id, 1.5m),
                R(tenantId, products[9].Id, ingredients[5].Id, 0.2m),
                R(tenantId, products[10].Id, ingredients[5].Id, 0.2m),
                R(tenantId, products[11].Id, ingredients[8].Id, 0.05m),
                R(tenantId, products[11].Id, ingredients[2].Id, 0.05m),
            };
            await dbContext.Recipes.AddRangeAsync(recipes, ct);

            var inventories = ingredients.Select(i => new Inventory(tenantId, i.Id, 50m)).ToList();
            await dbContext.Inventories.AddRangeAsync(inventories, ct);

            return new IndustrySeedResult(
                ProductsCreated: products.Length,
                IngredientsCreated: ingredients.Length,
                RecipesCreated: recipes.Length,
                ShopsCreated: 1,
                Warnings: []);
        }

        private static Ingredient Create(TenantId tenantId, string name, string unit,
            decimal currentStock, decimal minStockThreshold, decimal pricePerUnit)
            => new(tenantId, name, unit, currentStock, minStockThreshold, pricePerUnit);

        private static Recipe R(TenantId tenantId, Guid productId, Guid ingredientId, decimal qty)
            => new(tenantId, productId, ingredientId, qty);
    }
}
