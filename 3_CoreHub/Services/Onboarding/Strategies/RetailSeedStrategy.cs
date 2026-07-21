using VanAn.CoreHub.Infrastructure;
using VanAn.Shared.Domain;

namespace VanAn.CoreHub.Services.Onboarding.Strategies
{
    /// <summary>
    /// Retail seed strategy — general retail store (FMCG, mixed goods).
    /// IndustryCode: "RETAIL" — maps to QuickSetup "Cửa hàng" template (c333).
    ///
    /// Seeds 18 products across common retail categories + 10 supply ingredients
    /// + recipe mappings + inventory. Data sourced from general retail reference.
    /// </summary>
    public sealed class RetailSeedStrategy : IIndustrySeedStrategy
    {
        public string IndustryCode => "RETAIL";
        public string IndustryName => "Retail Store";

        public async Task<IndustrySeedResult> SeedAsync(
            TenantId tenantId,
            IVanAnDbContext dbContext,
            CancellationToken ct = default)
        {
            // ── 1. Default Shop ───────────────────────────────────────────────────

            // ── 2. Products — across retail categories ────────────────────────────
            var products = new[]
            {
                // Đồ uống đóng gói
                new Product(tenantId, "Nước suối 500ml", "Nước khoáng tinh khiết", 5_000m, "Đồ uống"),
                new Product(tenantId, "Coca Cola lon", "Nước ngọt Coca Cola 330ml", 10_000m, "Đồ uống"),
                new Product(tenantId, "Pepsi lon", "Nước ngọt Pepsi 330ml", 10_000m, "Đồ uống"),
                new Product(tenantId, "Trà xanh không độ 0 độ", "Trà xanh không đường 330ml", 10_000m, "Đồ uống"),
                new Product(tenantId, "Sữa tươi Vinamilk", "Sữa tươi 100% 180ml", 8_000m, "Đồ uống"),
                // Bánh kẹo
                new Product(tenantId, "Bánh Oreo", "Bánh quy Oreo gói 137g", 15_000m, "Bánh kẹo"),
                new Product(tenantId, "Kẹo sữa Alpine", "Kẹo cứng sữa Alpine gói 200g", 25_000m, "Bánh kẹo"),
                new Product(tenantId, "Bánh mì sandwich", "Bánh mì sandwich mẹt 6 lát", 18_000m, "Bánh kẹo"),
                // Đồ dùng gia đình
                new Product(tenantId, "Nước rửa chén Sunlight", "Nước rửa chena Sunlight 500ml", 22_000m, "Đồ dùng gia đình"),
                new Product(tenantId, "Xà phòng Lifebuoy", "Xà phòng Lifebuoy 125g", 8_000m, "Đồ dùng gia đình"),
                new Product(tenantId, "Giấy vệ sinh Cleo", "Giấy vệ sinh Cleo 10 cuộn", 45_000m, "Đồ dùng gia đình"),
                new Product(tenantId, "Kem đánh răng Colgate", "Kem đánh răng Colgate 100g", 18_000m, "Đồ dùng gia đình"),
                // Đồ khô / thực phẩm
                new Product(tenantId, "Gạo ST25 5kg", "Gạo thơm ST25 túi 5kg", 120_000m, "Thực phẩm khô"),
                new Product(tenantId, "Đường tinh luyện 1kg", "Đường tinh luyện túi 1kg", 25_000m, "Thực phẩm khô"),
                new Product(tenantId, "Muối iod 250g", "Muối iod gói 250g", 5_000m, "Thực phẩm khô"),
                new Product(tenantId, "Dầu ăn Tường An 1L", "Dầu ăn Tường An chai 1L", 45_000m, "Thực phẩm khô"),
                // Văn phòng phẩm
                new Product(tenantId, "Vở học sinh 200 trang", "Vở ô ly 200 trang", 12_000m, "Văn phòng phẩm"),
                new Product(tenantId, "Bút bi Thiên Long", "Bút bi Thiên Long AVL", 5_000m, "Văn phòng phẩm"),
            };
            await dbContext.Products.AddRangeAsync(products, ct);

            // ── 3. Ingredients (wholesale purchase items) ─────────────────────────
            var ingredients = new[]
            {
                Create(tenantId, "Nước suối thùng 24 chai", "thùng", 50m, 10m, 100_000m),
                Create(tenantId, "Coca Cola thùng 24 lon", "thùng", 30m, 5m, 200_000m),
                Create(tenantId, "Pepsi thùng 24 lon", "thùng", 30m, 5m, 200_000m),
                Create(tenantId, "Trà xanh thùng 24 lon", "thùng", 30m, 5m, 200_000m),
                Create(tenantId, "Sữa tươi lốc 48 hộp", "lốc", 20m, 5m, 350_000m),
                Create(tenantId, "Bánh Oreo thùng 24 gói", "thùng", 10m, 2m, 300_000m),
                Create(tenantId, "Kẹo Alpine thùng 30 gói", "thùng", 10m, 2m, 500_000m),
                Create(tenantId, "Bánh mì sandwich lốc 10 mẹt", "lốc", 20m, 5m, 150_000m),
                Create(tenantId, "Nước rửa chena thùng 24 chai", "thùng", 10m, 2m, 400_000m),
                Create(tenantId, "Xà phòng thùng 120 bánh", "thùng", 10m, 2m, 600_000m),
                Create(tenantId, "Giấy vệ sinh lốc 20 gói", "lốc", 10m, 2m, 800_000m),
                Create(tenantId, "Kem đánh răng thùng 72 tuýp", "thùng", 5m, 1m, 1_200_000m),
                Create(tenantId, "Gạo ST25 bao 25kg", "bao", 50m, 10m, 550_000m),
                Create(tenantId, "Đường tinh luyện bao 50kg", "bao", 20m, 5m, 1_100_000m),
                Create(tenantId, "Muối iod bao 50kg", "bao", 10m, 2m, 800_000m),
                Create(tenantId, "Dầu ăn Tường An lốc 6 chai", "lốc", 20m, 5m, 250_000m),
                Create(tenantId, "Vở 200 trang lốc 50 cuốn", "lốc", 20m, 5m, 500_000m),
                Create(tenantId, "Bút bi Thiên Long hộp 50 cây", "hộp", 20m, 5m, 200_000m),
            };
            await dbContext.Ingredients.AddRangeAsync(ingredients, ct);

            // ── 4. Recipes (product ↔ ingredient — 1:1 wholesale→retail mapping) ──
            var recipes = new[]
            {
                CreateRecipe(tenantId, products[0].Id, ingredients[0].Id, 1m/24m),
                CreateRecipe(tenantId, products[1].Id, ingredients[1].Id, 1m/24m),
                CreateRecipe(tenantId, products[2].Id, ingredients[2].Id, 1m/24m),
                CreateRecipe(tenantId, products[3].Id, ingredients[3].Id, 1m/24m),
                CreateRecipe(tenantId, products[4].Id, ingredients[4].Id, 1m/48m),
                CreateRecipe(tenantId, products[5].Id, ingredients[5].Id, 1m/24m),
                CreateRecipe(tenantId, products[6].Id, ingredients[6].Id, 1m/30m),
                CreateRecipe(tenantId, products[7].Id, ingredients[7].Id, 1m/10m),
                CreateRecipe(tenantId, products[8].Id, ingredients[8].Id, 1m/24m),
                CreateRecipe(tenantId, products[9].Id, ingredients[9].Id, 1m/120m),
                CreateRecipe(tenantId, products[10].Id, ingredients[10].Id, 1m/20m),
                CreateRecipe(tenantId, products[11].Id, ingredients[11].Id, 1m/72m),
                CreateRecipe(tenantId, products[12].Id, ingredients[12].Id, 5m/25m),
                CreateRecipe(tenantId, products[13].Id, ingredients[13].Id, 1m/50m),
                CreateRecipe(tenantId, products[14].Id, ingredients[14].Id, 0.250m/50m),
                CreateRecipe(tenantId, products[15].Id, ingredients[15].Id, 1m/6m),
                CreateRecipe(tenantId, products[16].Id, ingredients[16].Id, 1m/50m),
                CreateRecipe(tenantId, products[17].Id, ingredients[17].Id, 1m/50m),
            };
            await dbContext.Recipes.AddRangeAsync(recipes, ct);

            // ── 5. Inventory (initial stock for each ingredient) ──────────────────
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

        private static Recipe CreateRecipe(TenantId tenantId, Guid productId, Guid ingredientId, decimal qty)
            => new(tenantId, productId, ingredientId, qty);
    }
}
