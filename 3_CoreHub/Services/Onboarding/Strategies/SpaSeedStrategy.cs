using VanAn.CoreHub.Infrastructure;
using VanAn.Shared.Domain;

namespace VanAn.CoreHub.Services.Onboarding.Strategies
{
    /// <summary>
    /// SPA seed strategy — Spa, Massage, Chăm sóc da, Sức khỏe.
    /// IndustryCode: "SPA" — maps to QuickSetup "Spa & Beauty" template (b222).
    /// Data sourced from docs/requirements/Menu_Toc_Spa_Hotel_TroChoi.md §2.
    /// Seeds 22 services + 12 materials + recipe mappings + inventory.
    /// </summary>
    public sealed class SpaSeedStrategy : IIndustrySeedStrategy
    {
        public string IndustryCode => "SPA";
        public string IndustryName => "Spa & Beauty";

        public async Task<IndustrySeedResult> SeedAsync(
            TenantId tenantId,
            IVanAnDbContext dbContext,
            CancellationToken ct = default)
        {

            // ── Products (services) from Menu_Toc_Spa_Hotel_TroChoi.md §2 ─────────
            var products = new[]
            {
                // Massage
                new Product(tenantId, "Massage chân 60 phút", "Massage thư giãn chân 60 phút", 250_000m, "Massage"),
                new Product(tenantId, "Massage toàn thân 60 phút", "Massage toàn thân 60 phút", 350_000m, "Massage"),
                new Product(tenantId, "Massage đá nóng", "Massage với đá nóng nung lửa", 450_000m, "Massage"),
                new Product(tenantId, "Massage Thái", "Massage Thái truyền thống", 500_000m, "Massage"),
                new Product(tenantId, "Massage tinh dầu", "Massage với tinh dầu thiên nhiên", 450_000m, "Massage"),
                // Chăm sóc da
                new Product(tenantId, "Rửa mặt chuyên sâu", "Rửa mặt chuyên sâu lấy bụi bẩn", 250_000m, "Chăm sóc da"),
                new Product(tenantId, "Lấy nhân mụn", "Lấy nhân mụn chuẩn y khoa", 350_000m, "Chăm sóc da"),
                new Product(tenantId, "Chăm sóc da cơ bản", "Chăm sóc da cơ bản 60 phút", 500_000m, "Chăm sóc da"),
                new Product(tenantId, "Facial cao cấp", "Facial cao cấp serum đậm đặc", 800_000m, "Chăm sóc da"),
                new Product(tenantId, "Peel da", "Peel da hóa học tái tạo", 900_000m, "Chăm sóc da"),
                // Công nghệ
                new Product(tenantId, "Triệt lông nách", "Triệt lông nách bằng công nghệ", 400_000m, "Công nghệ"),
                new Product(tenantId, "Triệt lông chân", "Triệt lông chân toàn bộ", 900_000m, "Công nghệ"),
                new Product(tenantId, "HIFU nâng cơ", "HIFU nâng cơ trẻ hóa", 2_500_000m, "Công nghệ"),
                new Product(tenantId, "RF trẻ hóa da", "RF trẻ hóa da công nghệ cao", 1_800_000m, "Công nghệ"),
                new Product(tenantId, "Điện di Vitamin C", "Điện di Vitamin C sáng da", 700_000m, "Công nghệ"),
                // Chăm sóc sức khỏe
                new Product(tenantId, "Xông hơi", "Xông hơi thảo dược thư giãn", 150_000m, "Sức khỏe"),
                new Product(tenantId, "Ngâm chân thảo dược", "Ngâm chân thảo dược 30 phút", 120_000m, "Sức khỏe"),
                new Product(tenantId, "Cạo gió dưỡng sinh", "Cạo gió dưỡng sinh truyền thống", 180_000m, "Sức khỏe"),
                new Product(tenantId, "Giác hơi", "Giác hơi giải độc cơ thể", 250_000m, "Sức khỏe"),
                // Combo
                new Product(tenantId, "Massage + Xông hơi", "Combo massage toàn thân + xông hơi", 500_000m, "Combo"),
                new Product(tenantId, "Facial + Massage", "Combo facial + massage 120 phút", 850_000m, "Combo"),
                new Product(tenantId, "Spa VIP 3 giờ", "Gói VIP 3 giờ trọn gói", 1_800_000m, "Combo"),
            };
            await dbContext.Products.AddRangeAsync(products, ct);

            // ── Ingredients (materials) ──────────────────────────────────────────
            var tinhDau = Create(tenantId, "Tinh dầu massage", "chai 100ml", 30m, 5m, 80_000m);
            var daNong = Create(tenantId, "Đá nóng massage", "hòn", 30m, 5m, 50_000m);
            var kemMassage = Create(tenantId, "Kem massage Thái", "hũ 500g", 20m, 3m, 250_000m);
            var matNa = Create(tenantId, "Mặt nạ đất sét", "hộp 10 miếng", 50m, 10m, 180_000m);
            var serum = Create(tenantId, "Serum Vitamin C", "chai 30ml", 30m, 5m, 450_000m);
            var gelPeel = Create(tenantId, "Gel peel da", "tuýp 50ml", 15m, 3m, 800_000m);
            var khan = Create(tenantId, "Khăn spa mềm", "cái", 200m, 20m, 30_000m);
            var nuocHoaHong = Create(tenantId, "Nước hoa hồng tonic", "chai 200ml", 30m, 5m, 120_000m);
            var thuocGiacHoi = Create(tenantId, "Bộ giác hơi", "bộ", 10m, 2m, 200_000m);
            var daoGio = Create(tenantId, "Bộ cạo gió", "bộ", 10m, 2m, 150_000m);
            var khoiXongHoi = Create(tenantId, "Herb xông hơi thảo dược", "gói", 100m, 10m, 25_000m);
            var kemTrietLong = Create(tenantId, "Gel triệt lông", "tuýp 100ml", 20m, 3m, 350_000m);

            var ingredients = new[] { tinhDau, daNong, kemMassage, matNa, serum, gelPeel, khan, nuocHoaHong, thuocGiacHoi, daoGio, khoiXongHoi, kemTrietLong };
            await dbContext.Ingredients.AddRangeAsync(ingredients, ct);

            // ── Recipes (service ↔ materials) ────────────────────────────────────
            var recipes = new[]
            {
                // Massage services
                R(tenantId, products[0].Id, tinhDau.Id, 0.05m),
                R(tenantId, products[0].Id, khan.Id, 2m),
                R(tenantId, products[1].Id, tinhDau.Id, 0.1m),
                R(tenantId, products[1].Id, khan.Id, 2m),
                R(tenantId, products[2].Id, daNong.Id, 6m),
                R(tenantId, products[2].Id, tinhDau.Id, 0.1m),
                R(tenantId, products[3].Id, kemMassage.Id, 0.1m),
                R(tenantId, products[3].Id, khan.Id, 2m),
                R(tenantId, products[4].Id, tinhDau.Id, 0.15m),
                R(tenantId, products[4].Id, khan.Id, 2m),
                // Chăm sóc da
                R(tenantId, products[5].Id, nuocHoaHong.Id, 0.1m),
                R(tenantId, products[5].Id, khan.Id, 1m),
                R(tenantId, products[6].Id, nuocHoaHong.Id, 0.1m),
                R(tenantId, products[6].Id, khan.Id, 2m),
                R(tenantId, products[7].Id, matNa.Id, 1m),
                R(tenantId, products[7].Id, nuocHoaHong.Id, 0.1m),
                R(tenantId, products[8].Id, serum.Id, 0.05m),
                R(tenantId, products[8].Id, matNa.Id, 1m),
                R(tenantId, products[9].Id, gelPeel.Id, 0.05m),
                R(tenantId, products[9].Id, nuocHoaHong.Id, 0.1m),
                // Công nghệ
                R(tenantId, products[10].Id, kemTrietLong.Id, 0.05m),
                R(tenantId, products[11].Id, kemTrietLong.Id, 0.1m),
                R(tenantId, products[12].Id, serum.Id, 0.05m),
                R(tenantId, products[12].Id, khan.Id, 2m),
                R(tenantId, products[13].Id, serum.Id, 0.05m),
                R(tenantId, products[13].Id, khan.Id, 2m),
                R(tenantId, products[14].Id, serum.Id, 0.1m),
                // Sức khỏe
                R(tenantId, products[15].Id, khoiXongHoi.Id, 2m),
                R(tenantId, products[16].Id, khoiXongHoi.Id, 1m),
                R(tenantId, products[16].Id, khan.Id, 2m),
                R(tenantId, products[17].Id, daoGio.Id, 1m/30m),
                R(tenantId, products[18].Id, thuocGiacHoi.Id, 1m/20m),
                // Combo
                R(tenantId, products[19].Id, tinhDau.Id, 0.1m),
                R(tenantId, products[19].Id, khoiXongHoi.Id, 2m),
                R(tenantId, products[19].Id, khan.Id, 2m),
                R(tenantId, products[20].Id, serum.Id, 0.05m),
                R(tenantId, products[20].Id, matNa.Id, 1m),
                R(tenantId, products[20].Id, tinhDau.Id, 0.1m),
                R(tenantId, products[21].Id, tinhDau.Id, 0.15m),
                R(tenantId, products[21].Id, matNa.Id, 1m),
                R(tenantId, products[21].Id, serum.Id, 0.1m),
                R(tenantId, products[21].Id, daNong.Id, 6m),
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
