using VanAn.CoreHub.Infrastructure;
using VanAn.Shared.Domain;

namespace VanAn.CoreHub.Services.Onboarding.Strategies
{
    /// <summary>
    /// Barber seed strategy — Barber shop, hair cut for men.
    /// IndustryCode: "BARBER"
    /// Data sourced from docs/requirements/Menu_Toc_Spa_Hotel_TroChoi.md §1 (subset).
    /// Seeds 12 services + 10 materials + recipe mappings + inventory.
    /// </summary>
    public sealed class BarberSeedStrategy : IIndustrySeedStrategy
    {
        public string IndustryCode => "BARBER";
        public string IndustryName => "Barber Shop";

        public async Task<IndustrySeedResult> SeedAsync(
            TenantId tenantId,
            IVanAnDbContext dbContext,
            CancellationToken ct = default)
        {

            var products = new[]
            {
                new Product(tenantId, "Cắt tóc nam", "Cắt tóc nam cơ bản", 80_000m, "Cắt tóc"),
                new Product(tenantId, "Cắt tóc trẻ em", "Cắt tóc bé", 70_000m, "Cắt tóc"),
                new Product(tenantId, "Gội + Cắt + Sấy", "Trọn gói", 150_000m, "Cắt tóc"),
                new Product(tenantId, "Tạo kiểu tóc", "Tạo kiểu vuốt", 120_000m, "Cắt tóc"),
                new Product(tenantId, "Cắt râu", "Cắt râu và tạo dạng", 50_000m, "Râu"),
                new Product(tenantId, "Gội đầu dưỡng sinh", "Gội đầu nam dưỡng sinh", 150_000m, "Gội đầu"),
                new Product(tenantId, "Massage đầu vai gáy", "Massage đầu vai gáy", 100_000m, "Gội đầu"),
                new Product(tenantId, "Nhuộm tóc nam", "Nhuộm tóc nam ngắn", 600_000m, "Nhuộm"),
                new Product(tenantId, "Uốn tóc nam", "Uốn tóc nam nhẹ", 900_000m, "Nhuộm"),
                new Product(tenantId, "Duỗi tóc nam", "Duỗi tóc nam", 800_000m, "Nhuộm"),
                new Product(tenantId, "Phục hồi Keratin", "Phục hồi Keratin", 700_000m, "Nhuộm"),
                new Product(tenantId, "Gói VIP cắt + nhuộm", "Combo VIP cắt + nhuộm", 1_500_000m, "Combo"),
            };
            await dbContext.Products.AddRangeAsync(products, ct);

            var ingredients = new[]
            {
                Create(tenantId, "Dầu gội đầu", "chai 1L", 30m, 5m, 250_000m),
                Create(tenantId, "Dầu xả", "chai 1L", 30m, 5m, 220_000m),
                Create(tenantId, "Thuốc nhuộm tóc", "hộp", 50m, 10m, 120_000m),
                Create(tenantId, "Thuốc uốn tóc", "hộp", 30m, 5m, 180_000m),
                Create(tenantId, "Thuốc duỗi tóc", "hộp", 30m, 5m, 200_000m),
                Create(tenantId, "Kem Keratin phục hồi", "hũ 200g", 20m, 3m, 600_000m),
                Create(tenantId, "Sáp vuốt tóc", "hũ 100g", 50m, 10m, 150_000m),
                Create(tenantId, "Dao cạo râu", "cái", 100m, 20m, 10_000m),
                Create(tenantId, "Kem cạo râu", "tuýp 100ml", 50m, 10m, 80_000m),
                Create(tenantId, "Khăn Barber", "cái", 200m, 20m, 25_000m),
            };
            await dbContext.Ingredients.AddRangeAsync(ingredients, ct);

            var recipes = new[]
            {
                R(tenantId, products[0].Id, ingredients[9].Id, 1m),
                R(tenantId, products[1].Id, ingredients[9].Id, 1m),
                R(tenantId, products[2].Id, ingredients[0].Id, 0.03m),
                R(tenantId, products[2].Id, ingredients[9].Id, 1m),
                R(tenantId, products[3].Id, ingredients[6].Id, 0.01m),
                R(tenantId, products[3].Id, ingredients[9].Id, 1m),
                R(tenantId, products[4].Id, ingredients[7].Id, 1m/30m),
                R(tenantId, products[4].Id, ingredients[8].Id, 0.02m),
                R(tenantId, products[5].Id, ingredients[0].Id, 0.05m),
                R(tenantId, products[5].Id, ingredients[1].Id, 0.05m),
                R(tenantId, products[5].Id, ingredients[9].Id, 2m),
                R(tenantId, products[6].Id, ingredients[0].Id, 0.03m),
                R(tenantId, products[7].Id, ingredients[2].Id, 1m),
                R(tenantId, products[7].Id, ingredients[9].Id, 2m),
                R(tenantId, products[8].Id, ingredients[3].Id, 1m),
                R(tenantId, products[9].Id, ingredients[4].Id, 1m),
                R(tenantId, products[10].Id, ingredients[5].Id, 0.1m),
                R(tenantId, products[11].Id, ingredients[2].Id, 1m),
                R(tenantId, products[11].Id, ingredients[9].Id, 2m),
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
