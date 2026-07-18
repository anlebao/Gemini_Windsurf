using VanAn.CoreHub.Infrastructure;
using VanAn.Shared.Domain;

namespace VanAn.CoreHub.Services.Onboarding.Strategies
{
    /// <summary>
    /// Hotel seed strategy — Hotel &amp; Resort.
    /// IndustryCode: "HOTEL"
    /// Data sourced from docs/requirements/Menu_Toc_Spa_Hotel_TroChoi.md §3.
    /// Seeds 24 services + 12 materials + recipe mappings + inventory.
    /// </summary>
    public sealed class HotelSeedStrategy : IIndustrySeedStrategy
    {
        public string IndustryCode => "HOTEL";
        public string IndustryName => "Hotel & Resort";

        public async Task<IndustrySeedResult> SeedAsync(
            TenantId tenantId,
            IVanAnDbContext dbContext,
            CancellationToken ct = default)
        {
            var shop = new Shop(tenantId, "Hotel & Resort", "123 Nguyễn Huệ, Q1, TP.HCM", "1900-1234", "hotel@vanan.vn");
            await dbContext.Shops.AddAsync(shop, ct);

            var products = new[]
            {
                // Phòng nghỉ
                new Product(tenantId, "Standard Room", "Phòng tiêu chuẩn 1 đêm", 800_000m, "Phòng nghỉ"),
                new Product(tenantId, "Superior Room", "Phòng superior 1 đêm", 1_200_000m, "Phòng nghỉ"),
                new Product(tenantId, "Deluxe Room", "Phòng deluxe 1 đêm", 1_800_000m, "Phòng nghỉ"),
                new Product(tenantId, "Family Room", "Phòng gia đình 1 đêm", 2_500_000m, "Phòng nghỉ"),
                new Product(tenantId, "Suite", "Phòng suite cao cấp 1 đêm", 4_000_000m, "Phòng nghỉ"),
                // Dịch vụ phòng
                new Product(tenantId, "Ăn sáng Buffet", "Ăn sáng buffet 1 khách", 200_000m, "Dịch vụ phòng"),
                new Product(tenantId, "Giặt ủi", "Giặt ủi mỗi món", 30_000m, "Dịch vụ phòng"),
                new Product(tenantId, "Đưa đón sân bay", "Đưa đón sân bay 1 chiều", 350_000m, "Dịch vụ phòng"),
                // Sự kiện
                new Product(tenantId, "Phòng họp", "Thuê phòng họp 1 buổi", 2_000_000m, "Sự kiện"),
                new Product(tenantId, "Tiệc cưới", "Tiệc cưới theo bàn", 5_000_000m, "Sự kiện"),
                new Product(tenantId, "Tiệc sinh nhật", "Gói tiệc sinh nhật", 3_000_000m, "Sự kiện"),
                // Phụ thu
                new Product(tenantId, "Check-in sớm", "Phụ thu check-in sớm", 300_000m, "Phụ thu"),
                new Product(tenantId, "Check-out muộn", "Phụ thu check-out muộn", 300_000m, "Phụ thu"),
                new Product(tenantId, "Giường phụ", "Giường phụ 1 đêm", 400_000m, "Phụ thu"),
                new Product(tenantId, "Thú cưng", "Phí thú cưng 1 đêm", 300_000m, "Phụ thu"),
            };
            await dbContext.Products.AddRangeAsync(products, ct);

            // Ingredients (operational supplies)
            var ingredients = new[]
            {
                Create(tenantId, "Bộ ga giường", "bộ", 100m, 20m, 200_000m),
                Create(tenantId, "Khăn tắm", "cái", 200m, 30m, 50_000m),
                Create(tenantId, "Xà phòng phòng", "cái", 500m, 50m, 8_000m),
                Create(tenantId, "Dầu gội phòng", "chai 30ml", 500m, 50m, 10_000m),
                Create(tenantId, "Nước suối phòng", "chai 500ml", 500m, 50m, 5_000m),
                Create(tenantId, "Bữa sáng nguyên liệu", "suất", 200m, 30m, 80_000m),
                Create(tenantId, "Bột giặt giặt ủi", "kg", 50m, 10m, 30_000m),
                Create(tenantId, "Đưa đòn xăng xe", "chuyến", 50m, 10m, 200_000m),
                Create(tenantId, "Nước phòng họp", "chai 500ml", 200m, 20m, 10_000m),
                Create(tenantId, "Bàn tiệc cưới setup", "bàn", 100m, 20m, 800_000m),
                Create(tenantId, "Gói tiệc sinh nhật", "gói", 20m, 5m, 1_200_000m),
                Create(tenantId, "Giường phụ gấp", "cái", 10m, 2m, 1_500_000m),
            };
            await dbContext.Ingredients.AddRangeAsync(ingredients, ct);

            var recipes = new[]
            {
                R(tenantId, products[0].Id, ingredients[0].Id, 1m),
                R(tenantId, products[0].Id, ingredients[1].Id, 2m),
                R(tenantId, products[0].Id, ingredients[2].Id, 1m),
                R(tenantId, products[0].Id, ingredients[3].Id, 1m),
                R(tenantId, products[0].Id, ingredients[4].Id, 2m),
                R(tenantId, products[1].Id, ingredients[0].Id, 1m),
                R(tenantId, products[1].Id, ingredients[1].Id, 3m),
                R(tenantId, products[2].Id, ingredients[0].Id, 2m),
                R(tenantId, products[2].Id, ingredients[1].Id, 4m),
                R(tenantId, products[2].Id, ingredients[4].Id, 4m),
                R(tenantId, products[3].Id, ingredients[0].Id, 3m),
                R(tenantId, products[3].Id, ingredients[1].Id, 6m),
                R(tenantId, products[4].Id, ingredients[0].Id, 3m),
                R(tenantId, products[4].Id, ingredients[1].Id, 8m),
                R(tenantId, products[5].Id, ingredients[5].Id, 1m),
                R(tenantId, products[6].Id, ingredients[6].Id, 0.05m),
                R(tenantId, products[7].Id, ingredients[7].Id, 1m),
                R(tenantId, products[8].Id, ingredients[8].Id, 20m),
                R(tenantId, products[9].Id, ingredients[9].Id, 10m),
                R(tenantId, products[10].Id, ingredients[10].Id, 1m),
                R(tenantId, products[13].Id, ingredients[11].Id, 1m/100m),
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
