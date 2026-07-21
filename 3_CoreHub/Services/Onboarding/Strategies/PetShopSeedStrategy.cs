using VanAn.CoreHub.Infrastructure;
using VanAn.Shared.Domain;

namespace VanAn.CoreHub.Services.Onboarding.Strategies
{
    /// <summary>
    /// PetShop seed strategy — Pet shop services &amp; products.
    /// IndustryCode: "PETSHOP"
    /// Seeds 12 products + 10 ingredients.
    /// </summary>
    public sealed class PetShopSeedStrategy : IIndustrySeedStrategy
    {
        public string IndustryCode => "PETSHOP";
        public string IndustryName => "Pet Shop";

        public async Task<IndustrySeedResult> SeedAsync(
            TenantId tenantId,
            IVanAnDbContext dbContext,
            CancellationToken ct = default)
        {

            var products = new[]
            {
                // Tắm gội
                new Product(tenantId, "Tắm chó nhỏ", "Tắm chó dưới 10kg", 150_000m, "Tắm gội"),
                new Product(tenantId, "Tắm chó lớn", "Tắm chó trên 10kg", 250_000m, "Tắm gội"),
                new Product(tenantId, "Tắm mèo", "Tắm mèo ngắn lông", 130_000m, "Tắm gội"),
                new Product(tenantId, "Cắt tỉa lông chó", "Cắt tỉa lông chó tạo kiểu", 250_000m, "Cắt tỉa"),
                new Product(tenantId, "Cắt tỉa lông mèo", "Cắt tỉa lông mèo", 180_000m, "Cắt tỉa"),
                // Sản phẩm bán
                new Product(tenantId, "Thức ăn chó 1kg", "Hạt thức ăn chó 1kg", 120_000m, "Sản phẩm bán"),
                new Product(tenantId, "Thức ăn mèo 1kg", "Hạt thức ăn mèo 1kg", 140_000m, "Sản phẩm bán"),
                new Product(tenantId, "Cát vệ sinh mèo 10L", "Cát vệ sinh mèo 10L", 90_000m, "Sản phẩm bán"),
                new Product(tenantId, "Dây dắt chó", "Dây dắt chó da 1.2m", 80_000m, "Sản phẩm bán"),
                new Product(tenantId, "Khay vệ sinh mèo", "Khay vệ sinh mèo to", 120_000m, "Sản phẩm bán"),
                // Dịch vụ khác
                new Product(tenantId, "Spa pet full", "Gói tắm + cắt + vệ sinh", 500_000m, "Combo"),
                new Product(tenantId, "Khám sức khỏe pet", "Khám tổng quát pet", 200_000m, "Khám"),
            };
            await dbContext.Products.AddRangeAsync(products, ct);

            var ingredients = new[]
            {
                Create(tenantId, "Dầu tắm pet chuyên dụng", "chai 5L", 20m, 5m, 350_000m),
                Create(tenantId, "Dầu xả pet", "chai 5L", 20m, 5m, 350_000m),
                Create(tenantId, "Kéo cắt lông", "cái", 10m, 2m, 500_000m),
                Create(tenantId, "Khăn tắm pet", "cái", 50m, 10m, 30_000m),
                Create(tenantId, "Hạt thức ăn chó", "bao 20kg", 10m, 2m, 1_500_000m),
                Create(tenantId, "Hạt thức ăn mèo", "bao 20kg", 10m, 2m, 1_800_000m),
                Create(tenantId, "Cát vệ sinh", "bao 25L", 20m, 5m, 200_000m),
                Create(tenantId, "Dây dắt", "cái", 50m, 10m, 30_000m),
                Create(tenantId, "Khay vệ sinh", "cái", 30m, 5m, 70_000m),
                Create(tenantId, "Bộ khám pet", "bộ", 5m, 1m, 2_000_000m),
            };
            await dbContext.Ingredients.AddRangeAsync(ingredients, ct);

            var recipes = new[]
            {
                R(tenantId, products[0].Id, ingredients[0].Id, 0.05m),
                R(tenantId, products[0].Id, ingredients[3].Id, 2m),
                R(tenantId, products[1].Id, ingredients[0].Id, 0.1m),
                R(tenantId, products[1].Id, ingredients[3].Id, 3m),
                R(tenantId, products[2].Id, ingredients[0].Id, 0.05m),
                R(tenantId, products[2].Id, ingredients[3].Id, 2m),
                R(tenantId, products[3].Id, ingredients[2].Id, 1m/100m),
                R(tenantId, products[4].Id, ingredients[2].Id, 1m/100m),
                R(tenantId, products[5].Id, ingredients[4].Id, 1m/20m),
                R(tenantId, products[6].Id, ingredients[5].Id, 1m/20m),
                R(tenantId, products[7].Id, ingredients[6].Id, 10m/25m),
                R(tenantId, products[8].Id, ingredients[7].Id, 1m),
                R(tenantId, products[9].Id, ingredients[8].Id, 1m),
                R(tenantId, products[10].Id, ingredients[0].Id, 0.1m),
                R(tenantId, products[10].Id, ingredients[2].Id, 1m/100m),
                R(tenantId, products[10].Id, ingredients[3].Id, 3m),
                R(tenantId, products[11].Id, ingredients[9].Id, 1m/200m),
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
