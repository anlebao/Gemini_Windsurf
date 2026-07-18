using VanAn.CoreHub.Infrastructure;
using VanAn.Shared.Domain;

namespace VanAn.CoreHub.Services.Onboarding.Strategies
{
    /// <summary>
    /// F&amp;B (Food &amp; Beverage) seed strategy — Cafe full menu.
    /// IndustryCode: "F&B" — maps to QuickSetup "Quán Cafe" template (a111).
    /// Data sourced from docs/requirements/Menu_An_Uong.md §1 (Cafe — full menu).
    ///
    /// Seeds 1 shop + 32 products (7 cà phê + 5 trà + 5 trà sữa + 5 topping
    ///   + 6 đồ uống khác + 4 bánh) + 15 ingredients + 30+ recipes + inventory.
    /// </summary>
    public sealed class FnbSeedStrategy : IIndustrySeedStrategy
    {
        public string IndustryCode => "F&B";
        public string IndustryName => "Food & Beverage";

        public async Task<IndustrySeedResult> SeedAsync(
            TenantId tenantId,
            IVanAnDbContext dbContext,
            CancellationToken ct = default)
        {
            // ── 1. Default Shop ───────────────────────────────────────────────────
            var shop = new Shop(tenantId, "Vạn An F&B", "123 Nguyễn Huệ, Q1, TP.HCM", "1900-1234", "fnb@vanan.vn");
            await dbContext.Shops.AddAsync(shop, ct);

            // ── 2. Products — Cà phê (7) ──────────────────────────────────────────
            var cafeDenDa = new Product(tenantId, "Cà phê đen đá", "Cà phê pha phin truyền thống", 25_000m, "Cà phê");
            var cafeSuaDa = new Product(tenantId, "Cà phê sữa đá", "Cà phê phin cùng sữa đặc", 30_000m, "Cà phê");
            var bacXiu = new Product(tenantId, "Bạc xỉu", "Sữa nhiều, cà phê nhẹ", 35_000m, "Cà phê");
            var americano = new Product(tenantId, "Americano", "Espresso pha loãng", 40_000m, "Cà phê");
            var cappuccino = new Product(tenantId, "Cappuccino", "Espresso cùng sữa đánh bọt", 50_000m, "Cà phê");
            var latte = new Product(tenantId, "Latte", "Espresso và sữa tươi", 50_000m, "Cà phê");
            var mocha = new Product(tenantId, "Mocha", "Espresso kết hợp chocolate", 55_000m, "Cà phê");

            // ── Products — Trà (5) ───────────────────────────────────────────────
            var traDaoCamSa = new Product(tenantId, "Trà đào cam sả", "Trà đào cùng cam và sả", 45_000m, "Trà");
            var traVai = new Product(tenantId, "Trà vải", "Trà đen kết hợp trái vải", 40_000m, "Trà");
            var traChanh = new Product(tenantId, "Trà chanh", "Trà cùng chanh tươi", 25_000m, "Trà");
            var traTac = new Product(tenantId, "Trà tắc", "Trà cùng quả tắc", 30_000m, "Trà");
            var traSenVang = new Product(tenantId, "Trà sen vàng", "Trà hoa sen thanh mát", 45_000m, "Trà");

            // ── Products — Trà sữa (5) ───────────────────────────────────────────
            var traSuaTruyenThong = new Product(tenantId, "Trà sữa truyền thống", "Hương vị cổ điển", 40_000m, "Trà sữa");
            var traSuaOlong = new Product(tenantId, "Trà sữa ô long", "Trà ô long thơm", 45_000m, "Trà sữa");
            var traSuaMatcha = new Product(tenantId, "Trà sữa matcha", "Matcha Nhật", 48_000m, "Trà sữa");
            var traSuaKhoaiMon = new Product(tenantId, "Trà sữa khoai môn", "Vị khoai môn béo", 45_000m, "Trà sữa");
            var traSuaSocola = new Product(tenantId, "Trà sữa socola", "Socola đậm vị", 45_000m, "Trà sữa");

            // ── Products — Topping (5) ───────────────────────────────────────────
            var tranChauDen = new Product(tenantId, "Trân châu đen", "Topping trân châu đen", 8_000m, "Topping");
            var tranChauTrang = new Product(tenantId, "Trân châu trắng", "Topping trân châu trắng", 8_000m, "Topping");
            var thachRauCau = new Product(tenantId, "Thạch rau câu", "Topping thạch", 8_000m, "Topping");
            var puddingTrung = new Product(tenantId, "Pudding trứng", "Topping pudding", 10_000m, "Topping");
            var cheeseFoam = new Product(tenantId, "Cheese Foam", "Topping cheese foam", 15_000m, "Topping");

            // ── Products — Đồ uống khác (6) ──────────────────────────────────────
            var nuocCamEp = new Product(tenantId, "Nước cam ép", "Nước cam ép tươi", 40_000m, "Đồ uống khác");
            var chanhDay = new Product(tenantId, "Chanh dây", "Nước chanh dây", 35_000m, "Đồ uống khác");
            var sinhToBo = new Product(tenantId, "Sinh tố bơ", "Sinh tố bơ thơm béo", 50_000m, "Đồ uống khác");
            var sinhToXoai = new Product(tenantId, "Sinh tố xoài", "Sinh tố xoài chín", 50_000m, "Đồ uống khác");
            var sodaVietQuat = new Product(tenantId, "Soda việt quất", "Soda việt quất mát", 45_000m, "Đồ uống khác");
            var sodaChanh = new Product(tenantId, "Soda chanh", "Soda chanh tươi", 40_000m, "Đồ uống khác");

            // ── Products — Bánh (4) ──────────────────────────────────────────────
            var tiramisu = new Product(tenantId, "Tiramisu", "Bánh tiramisu Ý", 45_000m, "Bánh");
            var cheesecake = new Product(tenantId, "Cheesecake", "Bánh cheesecake", 50_000m, "Bánh");
            var banhSuKem = new Product(tenantId, "Bánh su kem", "Bánh su kem bơ", 30_000m, "Bánh");
            var croissantBo = new Product(tenantId, "Croissant bơ", "Croissant bơ Pháp", 35_000m, "Bánh");

            var products = new[]
            {
                cafeDenDa, cafeSuaDa, bacXiu, americano, cappuccino, latte, mocha,
                traDaoCamSa, traVai, traChanh, traTac, traSenVang,
                traSuaTruyenThong, traSuaOlong, traSuaMatcha, traSuaKhoaiMon, traSuaSocola,
                tranChauDen, tranChauTrang, thachRauCau, puddingTrung, cheeseFoam,
                nuocCamEp, chanhDay, sinhToBo, sinhToXoai, sodaVietQuat, sodaChanh,
                tiramisu, cheesecake, banhSuKem, croissantBo
            };
            await dbContext.Products.AddRangeAsync(products, ct);

            // ── 3. Ingredients (15) ───────────────────────────────────────────────
            var cafeBot = I(tenantId, "Cà phê bột", "kg", 100m, 5m, 200_000m);
            var suaDac = I(tenantId, "Sữa đặc", "lon", 100m, 10m, 25_000m);
            var suaTuoi = I(tenantId, "Sữa tươi", "chai 1L", 50m, 10m, 35_000m);
            var duong = I(tenantId, "Đường", "kg", 100m, 5m, 15_000m);
            var traDen = I(tenantId, "Trà đen", "gói", 100m, 10m, 8_000m);
            var traXanh = I(tenantId, "Trà xanh", "gói", 50m, 5m, 12_000m);
            var dao = I(tenantId, "Đào hộp", "lon", 100m, 10m, 18_000m);
            var botTranChau = I(tenantId, "Bột trân châu", "kg", 50m, 5m, 120_000m);
            var botMatcha = I(tenantId, "Bột matcha", "kg", 20m, 3m, 800_000m);
            var khoaiMon = I(tenantId, "Khoai môn", "kg", 50m, 5m, 60_000m);
            var bo = I(tenantId, "Bơ", "trái", 100m, 10m, 15_000m);
            var xoai = I(tenantId, "Xoài", "trái", 100m, 10m, 15_000m);
            var cam = I(tenantId, "Cam tươi", "trái", 200m, 20m, 8_000m);
            var chanh = I(tenantId, "Chanh tươi", "trái", 200m, 20m, 3_000m);
            var banhBanh = I(tenantId, "Bánh patisserie", "cái", 50m, 10m, 25_000m);

            var ingredients = new[] { cafeBot, suaDac, suaTuoi, duong, traDen, traXanh, dao, botTranChau, botMatcha, khoaiMon, bo, xoai, cam, chanh, banhBanh };
            await dbContext.Ingredients.AddRangeAsync(ingredients, ct);

            // ── 4. Recipes (product ↔ ingredient) ─────────────────────────────────
            var recipes = new[]
            {
                // Cà phê
                R(tenantId, cafeDenDa.Id, cafeBot.Id, 0.02m),
                R(tenantId, cafeDenDa.Id, duong.Id, 0.01m),
                R(tenantId, cafeSuaDa.Id, cafeBot.Id, 0.02m),
                R(tenantId, cafeSuaDa.Id, suaDac.Id, 0.03m),
                R(tenantId, bacXiu.Id, cafeBot.Id, 0.01m),
                R(tenantId, bacXiu.Id, suaDac.Id, 0.05m),
                R(tenantId, bacXiu.Id, suaTuoi.Id, 0.1m),
                R(tenantId, americano.Id, cafeBot.Id, 0.03m),
                R(tenantId, cappuccino.Id, cafeBot.Id, 0.02m),
                R(tenantId, cappuccino.Id, suaTuoi.Id, 0.15m),
                R(tenantId, latte.Id, cafeBot.Id, 0.02m),
                R(tenantId, latte.Id, suaTuoi.Id, 0.2m),
                R(tenantId, mocha.Id, cafeBot.Id, 0.02m),
                R(tenantId, mocha.Id, suaTuoi.Id, 0.15m),
                // Trà
                R(tenantId, traDaoCamSa.Id, dao.Id, 0.5m),
                R(tenantId, traDaoCamSa.Id, traDen.Id, 0.05m),
                R(tenantId, traVai.Id, traDen.Id, 0.05m),
                R(tenantId, traChanh.Id, traDen.Id, 0.05m),
                R(tenantId, traChanh.Id, chanh.Id, 1m),
                R(tenantId, traTac.Id, traDen.Id, 0.05m),
                R(tenantId, traSenVang.Id, traXanh.Id, 0.05m),
                // Trà sữa
                R(tenantId, traSuaTruyenThong.Id, traDen.Id, 0.05m),
                R(tenantId, traSuaTruyenThong.Id, suaDac.Id, 0.03m),
                R(tenantId, traSuaOlong.Id, traDen.Id, 0.05m),
                R(tenantId, traSuaOlong.Id, suaTuoi.Id, 0.1m),
                R(tenantId, traSuaMatcha.Id, botMatcha.Id, 0.02m),
                R(tenantId, traSuaMatcha.Id, suaTuoi.Id, 0.1m),
                R(tenantId, traSuaKhoaiMon.Id, khoaiMon.Id, 0.1m),
                R(tenantId, traSuaKhoaiMon.Id, suaTuoi.Id, 0.1m),
                R(tenantId, traSuaSocola.Id, suaTuoi.Id, 0.15m),
                // Topping
                R(tenantId, tranChauDen.Id, botTranChau.Id, 0.03m),
                R(tenantId, tranChauTrang.Id, botTranChau.Id, 0.03m),
                R(tenantId, thachRauCau.Id, duong.Id, 0.02m),
                R(tenantId, puddingTrung.Id, suaDac.Id, 0.05m),
                R(tenantId, cheeseFoam.Id, suaTuoi.Id, 0.05m),
                // Đồ uống khác
                R(tenantId, nuocCamEp.Id, cam.Id, 3m),
                R(tenantId, chanhDay.Id, chanh.Id, 2m),
                R(tenantId, chanhDay.Id, duong.Id, 0.03m),
                R(tenantId, sinhToBo.Id, bo.Id, 1m),
                R(tenantId, sinhToBo.Id, suaTuoi.Id, 0.1m),
                R(tenantId, sinhToXoai.Id, xoai.Id, 1m),
                R(tenantId, sinhToXoai.Id, suaTuoi.Id, 0.1m),
                R(tenantId, sodaVietQuat.Id, duong.Id, 0.02m),
                R(tenantId, sodaChanh.Id, chanh.Id, 1m),
                R(tenantId, sodaChanh.Id, duong.Id, 0.02m),
                // Bánh — 1:1 với ingredient
                R(tenantId, tiramisu.Id, banhBanh.Id, 1m),
                R(tenantId, cheesecake.Id, banhBanh.Id, 1m),
                R(tenantId, banhSuKem.Id, banhBanh.Id, 1m),
                R(tenantId, croissantBo.Id, banhBanh.Id, 1m),
            };
            await dbContext.Recipes.AddRangeAsync(recipes, ct);

            // ── 5. Inventory ──────────────────────────────────────────────────────
            var inventories = ingredients
                .Select(i => new Inventory(tenantId, i.Id, 100m))
                .ToList();
            await dbContext.Inventories.AddRangeAsync(inventories, ct);

            return new IndustrySeedResult(
                ProductsCreated: products.Length,
                IngredientsCreated: ingredients.Length,
                RecipesCreated: recipes.Length,
                ShopsCreated: 1,
                Warnings: []);
        }

        private static Ingredient I(TenantId tenantId, string name, string unit,
            decimal currentStock, decimal minStockThreshold, decimal pricePerUnit)
            => new(tenantId, name, unit, currentStock, minStockThreshold, pricePerUnit);

        private static Recipe R(TenantId tenantId, Guid productId, Guid ingredientId, decimal qty)
            => new(tenantId, productId, ingredientId, qty);
    }
}
