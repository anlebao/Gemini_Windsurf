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
    ///   + 6 đồ uống khác + 4 bánh) + 15 ingredients + 32 recipes (48 RecipeLines) + inventory.
    ///
    /// VA-IIE refactor (2026-10-03, Q3-C approved): đơn vị cơ sở khối lượng = g (kg→g ×1000).
    /// Đơn vị phi-SI (lon/gói/trái/cái/chai 1L) giữ nguyên — NHẤT QUÁN giữa RecipeLine.Unit & kiểm kê.
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

            // ── 3. Ingredients (15) — VA-IIE Q3-C: khối lượng cơ sở = g ────────────
            var cafeBot = I(tenantId, "Cà phê bột", "g", 100_000m, 5_000m, 200m, varianceThresholdPercent: 15m);
            var suaDac = I(tenantId, "Sữa đặc", "lon", 100m, 10m, 25_000m, varianceThresholdPercent: 10m);
            var suaTuoi = I(tenantId, "Sữa tươi", "chai 1L", 50m, 10m, 35_000m);
            var duong = I(tenantId, "Đường", "g", 100_000m, 5_000m, 15m, varianceThresholdPercent: 20m);
            var traDen = I(tenantId, "Trà đen", "gói", 100m, 10m, 8_000m);
            var traXanh = I(tenantId, "Trà xanh", "gói", 50m, 5m, 12_000m);
            var dao = I(tenantId, "Đào hộp", "lon", 100m, 10m, 18_000m);
            var botTranChau = I(tenantId, "Bột trân châu", "g", 50_000m, 5_000m, 120m, varianceThresholdPercent: 15m);
            var botMatcha = I(tenantId, "Bột matcha", "g", 20_000m, 3_000m, 800m);
            var khoaiMon = I(tenantId, "Khoai môn", "g", 50_000m, 5_000m, 60m);
            var bo = I(tenantId, "Bơ", "trái", 100m, 10m, 15_000m);
            var xoai = I(tenantId, "Xoài", "trái", 100m, 10m, 15_000m);
            var cam = I(tenantId, "Cam tươi", "trái", 200m, 20m, 8_000m);
            var chanh = I(tenantId, "Chanh tươi", "trái", 200m, 20m, 3_000m);
            var banhBanh = I(tenantId, "Bánh patisserie", "cái", 50m, 10m, 25_000m, category: IngredientCategory.Consumable);

            var ingredients = new[] { cafeBot, suaDac, suaTuoi, duong, traDen, traXanh, dao, botTranChau, botMatcha, khoaiMon, bo, xoai, cam, chanh, banhBanh };
            await dbContext.Ingredients.AddRangeAsync(ingredients, ct);

            // ── 4. Recipes (header + RecipeLine — VA-IIE Sprint B) ────────────────
            var recipes = new[]
            {
                // Cà phê
                R(tenantId, cafeDenDa.Id, (cafeBot, 20m), (duong, 10m)),
                R(tenantId, cafeSuaDa.Id, (cafeBot, 20m), (suaDac, 0.03m)),
                R(tenantId, bacXiu.Id, (cafeBot, 10m), (suaDac, 0.05m), (suaTuoi, 0.1m)),
                R(tenantId, americano.Id, (cafeBot, 30m)),
                R(tenantId, cappuccino.Id, (cafeBot, 20m), (suaTuoi, 0.15m)),
                R(tenantId, latte.Id, (cafeBot, 20m), (suaTuoi, 0.2m)),
                R(tenantId, mocha.Id, (cafeBot, 20m), (suaTuoi, 0.15m)),
                // Trà
                R(tenantId, traDaoCamSa.Id, (dao, 0.5m), (traDen, 0.05m)),
                R(tenantId, traVai.Id, (traDen, 0.05m)),
                R(tenantId, traChanh.Id, (traDen, 0.05m), (chanh, 1m)),
                R(tenantId, traTac.Id, (traDen, 0.05m)),
                R(tenantId, traSenVang.Id, (traXanh, 0.05m)),
                // Trà sữa
                R(tenantId, traSuaTruyenThong.Id, (traDen, 0.05m), (suaDac, 0.03m)),
                R(tenantId, traSuaOlong.Id, (traDen, 0.05m), (suaTuoi, 0.1m)),
                R(tenantId, traSuaMatcha.Id, (botMatcha, 20m), (suaTuoi, 0.1m)),
                R(tenantId, traSuaKhoaiMon.Id, (khoaiMon, 100m), (suaTuoi, 0.1m)),
                R(tenantId, traSuaSocola.Id, (suaTuoi, 0.15m)),
                // Topping
                R(tenantId, tranChauDen.Id, (botTranChau, 30m)),
                R(tenantId, tranChauTrang.Id, (botTranChau, 30m)),
                R(tenantId, thachRauCau.Id, (duong, 20m)),
                R(tenantId, puddingTrung.Id, (suaDac, 0.05m)),
                R(tenantId, cheeseFoam.Id, (suaTuoi, 0.05m)),
                // Đồ uống khác
                R(tenantId, nuocCamEp.Id, (cam, 3m)),
                R(tenantId, chanhDay.Id, (chanh, 2m), (duong, 30m)),
                R(tenantId, sinhToBo.Id, (bo, 1m), (suaTuoi, 0.1m)),
                R(tenantId, sinhToXoai.Id, (xoai, 1m), (suaTuoi, 0.1m)),
                R(tenantId, sodaVietQuat.Id, (duong, 20m)),
                R(tenantId, sodaChanh.Id, (chanh, 1m), (duong, 20m)),
                // Bánh — 1:1 với ingredient
                R(tenantId, tiramisu.Id, (banhBanh, 1m)),
                R(tenantId, cheesecake.Id, (banhBanh, 1m)),
                R(tenantId, banhSuKem.Id, (banhBanh, 1m)),
                R(tenantId, croissantBo.Id, (banhBanh, 1m)),
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
            decimal currentStock, decimal minStockThreshold, decimal pricePerUnit,
            IngredientCategory category = IngredientCategory.RawMaterial, decimal? varianceThresholdPercent = null)
            => new(tenantId, name, unit, currentStock, minStockThreshold, pricePerUnit, category, varianceThresholdPercent);

        private static Recipe R(TenantId tenantId, Guid productId, params (Ingredient Ingredient, decimal Quantity)[] lines)
        {
            var recipe = new Recipe(tenantId, productId);
            foreach ((Ingredient ingredient, decimal quantity) in lines)
            {
                recipe.AddLine(ingredient.Id, quantity, ingredient.Unit);
            }
            return recipe;
        }
    }
}
