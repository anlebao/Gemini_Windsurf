using VanAn.CoreHub.Infrastructure;
using VanAn.Shared.Domain;

namespace VanAn.CoreHub.Services.Onboarding.Strategies
{
    /// <summary>
    /// Clothes & Fashion seed strategy — Salon tóc, Nail, Makeup.
    /// IndustryCode: "CLOTHES" — maps to QuickSetup "Thời trang" template (d444).
    /// Data sourced from docs/requirements/Menu_Toc_Spa_Hotel_TroChoi.md §1.
    /// Seeds 22 services + 12 materials + recipe mappings + inventory.
    /// </summary>
    public sealed class ClothesSeedStrategy : IIndustrySeedStrategy
    {
        public string IndustryCode => "CLOTHES";
        public string IndustryName => "Clothes & Fashion";

        public async Task<IndustrySeedResult> SeedAsync(
            TenantId tenantId,
            IVanAnDbContext dbContext,
            CancellationToken ct = default)
        {

            // ── Products (services) from Menu_Toc_Spa_Hotel_TroChoi.md §1 ─────────
            var products = new[]
            {
                // Cắt tóc
                new Product(tenantId, "Cắt tóc nam", "Cắt và tạo kiểu cơ bản", 80_000m, "Cắt tóc"),
                new Product(tenantId, "Cắt tóc nữ", "Cắt theo xu hướng", 180_000m, "Cắt tóc"),
                new Product(tenantId, "Cắt tóc trẻ em", "Phù hợp bé trai và bé gái", 70_000m, "Cắt tóc"),
                new Product(tenantId, "Gội + Cắt + Sấy", "Trọn gói chăm sóc tóc", 150_000m, "Cắt tóc"),
                new Product(tenantId, "Tạo kiểu tóc", "Uốn nhẹ hoặc tạo form", 120_000m, "Cắt tóc"),
                // Nhuộm - Uốn - Duỗi
                new Product(tenantId, "Nhuộm tóc ngắn", "Nhuộm tóc ngắn tẩy và màu", 600_000m, "Nhuộm-Uốn-Duỗi"),
                new Product(tenantId, "Nhuộm tóc dài", "Nhuộm tóc dài tẩy và màu", 900_000m, "Nhuộm-Uốn-Duỗi"),
                new Product(tenantId, "Uốn tóc", "Uốn tóc toàn bộ", 900_000m, "Nhuộm-Uốn-Duỗi"),
                new Product(tenantId, "Duỗi tóc", "Duỗi tóc phẳng mượt", 800_000m, "Nhuộm-Uốn-Duỗi"),
                new Product(tenantId, "Phục hồi Keratin", "Phục hồi Keratin tóc hư", 700_000m, "Nhuộm-Uốn-Duỗi"),
                // Gội đầu
                new Product(tenantId, "Gội đầu thường", "Gội đầu sạch và sấy", 50_000m, "Gội đầu"),
                new Product(tenantId, "Gội dưỡng sinh", "Gội đầu dưỡng sinh thảo mộc", 150_000m, "Gội đầu"),
                new Product(tenantId, "Massage đầu vai gáy", "Massage đầu vai gáy 30 phút", 100_000m, "Gội đầu"),
                new Product(tenantId, "Hấp dầu", "Hấp dầu ủ tóc phục hồi", 120_000m, "Gội đầu"),
                // Nail
                new Product(tenantId, "Sơn thường", "Sơn móng thường nhiều màu", 80_000m, "Nail"),
                new Product(tenantId, "Sơn gel", "Sơn gel bền lâu", 180_000m, "Nail"),
                new Product(tenantId, "Đắp gel", "Đắp gel móng giòn", 350_000m, "Nail"),
                new Product(tenantId, "Nối móng", "Nối móng dài mẫu đẹp", 500_000m, "Nail"),
                new Product(tenantId, "Vẽ nail nghệ thuật", "Vẽ nail nghệ thuật mẫu", 100_000m, "Nail"),
                // Makeup
                new Product(tenantId, "Makeup dự tiệc", "Trang điểm dự tiệc 90 phút", 500_000m, "Makeup"),
                new Product(tenantId, "Makeup cô dâu", "Trang điểm cô dâu trọn gói", 2_000_000m, "Makeup"),
                new Product(tenantId, "Làm tóc cô dâu", "Làm tóc cô dâu trọn gói", 1_500_000m, "Makeup"),
            };
            await dbContext.Products.AddRangeAsync(products, ct);

            // ── Ingredients (materials) ──────────────────────────────────────────
            var dauGoi = Create(tenantId, "Dầu gội đầu", "chai 1L", 30m, 5m, 250_000m);
            var dauXa = Create(tenantId, "Dầu xả", "chai 1L", 30m, 5m, 220_000m);
            var thuocNhuom = Create(tenantId, "Thuốc nhuộm tóc", "hộp", 50m, 10m, 120_000m);
            var thuocUon = Create(tenantId, "Thuốc uốn tóc", "hộp", 30m, 5m, 180_000m);
            var thuocDuoi = Create(tenantId, "Thuốc duỗi tóc", "hộp", 30m, 5m, 200_000m);
            var botKeratin = Create(tenantId, "Kem Keratin phục hồi", "hũ 200g", 20m, 3m, 600_000m);
            var sonMong = Create(tenantId, "Sơn móng gel", "hộp 12 màu", 50m, 10m, 200_000m);
            var gelNail = Create(tenantId, "Gel đắp móng", "hũ 50g", 20m, 3m, 350_000m);
            var thuyTinhNail = Create(tenantId, "Mẫu móng thủy tinh", "cái", 100m, 20m, 5_000m);
            var makeupBase = Create(tenantId, "Kem lót makeup", "hũ 30g", 30m, 5m, 450_000m);
            var phanChe = Create(tenantId, "Phấn che khuyết điểm", "hũ 10g", 30m, 5m, 500_000m);
            var khan = Create(tenantId, "Khăn Salon", "cái", 200m, 20m, 25_000m);

            var ingredients = new[] { dauGoi, dauXa, thuocNhuom, thuocUon, thuocDuoi, botKeratin, sonMong, gelNail, thuyTinhNail, makeupBase, phanChe, khan };
            await dbContext.Ingredients.AddRangeAsync(ingredients, ct);

            // ── Recipes ──────────────────────────────────────────────────────────
            var recipes = new[]
            {
                // Cắt tóc
                R(tenantId, products[0].Id, khan, 1m),
                R(tenantId, products[1].Id, khan, 1m),
                R(tenantId, products[2].Id, khan, 1m),
                R(tenantId, products[3].Id, dauGoi, 0.03m),
                R(tenantId, products[3].Id, khan, 1m),
                R(tenantId, products[4].Id, khan, 1m),
                // Nhuộm-uốn-duỗi
                R(tenantId, products[5].Id, thuocNhuom, 1m),
                R(tenantId, products[5].Id, khan, 2m),
                R(tenantId, products[6].Id, thuocNhuom, 2m),
                R(tenantId, products[6].Id, khan, 2m),
                R(tenantId, products[7].Id, thuocUon, 1m),
                R(tenantId, products[7].Id, khan, 2m),
                R(tenantId, products[8].Id, thuocDuoi, 1m),
                R(tenantId, products[8].Id, khan, 2m),
                R(tenantId, products[9].Id, botKeratin, 0.1m),
                R(tenantId, products[9].Id, khan, 2m),
                // Gội đầu
                R(tenantId, products[10].Id, dauGoi, 0.05m),
                R(tenantId, products[10].Id, khan, 1m),
                R(tenantId, products[11].Id, dauGoi, 0.05m),
                R(tenantId, products[11].Id, dauXa, 0.05m),
                R(tenantId, products[11].Id, khan, 2m),
                R(tenantId, products[12].Id, dauGoi, 0.03m),
                R(tenantId, products[12].Id, khan, 1m),
                R(tenantId, products[13].Id, dauGoi, 0.05m),
                R(tenantId, products[13].Id, botKeratin, 0.05m),
                R(tenantId, products[13].Id, khan, 2m),
                // Nail
                R(tenantId, products[14].Id, sonMong, 1m/12m),
                R(tenantId, products[15].Id, sonMong, 1m/6m),
                R(tenantId, products[16].Id, gelNail, 0.05m),
                R(tenantId, products[16].Id, thuyTinhNail, 1m),
                R(tenantId, products[17].Id, gelNail, 0.1m),
                R(tenantId, products[17].Id, thuyTinhNail, 10m),
                R(tenantId, products[18].Id, sonMong, 1m/6m),
                R(tenantId, products[18].Id, gelNail, 0.02m),
                // Makeup
                R(tenantId, products[19].Id, makeupBase, 0.02m),
                R(tenantId, products[19].Id, phanChe, 0.01m),
                R(tenantId, products[20].Id, makeupBase, 0.05m),
                R(tenantId, products[20].Id, phanChe, 0.02m),
                R(tenantId, products[20].Id, khan, 2m),
                R(tenantId, products[21].Id, thuocUon, 1m),
                R(tenantId, products[21].Id, khan, 2m),
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

        private static Recipe R(TenantId tenantId, Guid productId, Ingredient ingredient, decimal qty)
        {
            var recipe = new Recipe(tenantId, productId);
            recipe.AddLine(ingredient.Id, qty, ingredient.Unit);
            return recipe;
        }
    }
}
