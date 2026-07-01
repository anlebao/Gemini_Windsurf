using VanAn.CoreHub.Infrastructure;
using VanAn.Shared.Domain;

namespace VanAn.CoreHub.Services.Onboarding.Strategies
{
    /// <summary>
    /// F&amp;B (Food &amp; Beverage) seed strategy.
    /// Seeds a default shop, 8 products (drinks + food), 12 ingredients,
    /// 14 recipe mappings, and 12 inventory records for a new F&amp;B tenant.
    ///
    /// Wave 2: Full implementation for cafe, tea shop, fast food, and restaurant.
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

            // ── 2. Products — Drinks ──────────────────────────────────────────────
            var cafeDen = new Product(tenantId, "Cà phê đen", "Cà phê đen đậm đà, pha phin truyền thống", 25_000m, "Đồ uống");
            var cafeSua = new Product(tenantId, "Cà phê sữa", "Cà phê sữa đặc ngọt ngào", 30_000m, "Đồ uống");
            var traDao = new Product(tenantId, "Trà đào", "Trà đào cam sả mát lạnh", 35_000m, "Đồ uống");
            var traSuaTranChau = new Product(tenantId, "Trà sữa trân châu", "Trà sữa trân châu đen đặc trưng", 40_000m, "Đồ uống");
            var sinhToBo = new Product(tenantId, "Sinh tố bơ", "Sinh tố bơ đặc sánh, thơm béo", 45_000m, "Đồ uống");

            // ── Products — Food ───────────────────────────────────────────────────
            var banhMiThitNguoi = new Product(tenantId, "Bánh mì thịt nguội", "Bánh mì thịt nguội kiểu Sài Gòn", 35_000m, "Đồ ăn");
            var comGaXoiMo = new Product(tenantId, "Cơm gà xối mỡ", "Cơm gà xối mỡ giòn rụm", 55_000m, "Đồ ăn");
            var miYBoBam = new Product(tenantId, "Mì ý bò bằm", "Mì Ý sốt bò bằm kiểu Ý", 65_000m, "Đồ ăn");

            var products = new[] { cafeDen, cafeSua, traDao, traSuaTranChau, sinhToBo, banhMiThitNguoi, comGaXoiMo, miYBoBam };
            await dbContext.Products.AddRangeAsync(products, ct);

            // ── 3. Ingredients ────────────────────────────────────────────────────
            var cafeBotIngredient = CreateIngredient(tenantId, "Cà phê bột", "kg", 100m, 5m, 200_000m);
            var suaDacIngredient = CreateIngredient(tenantId, "Sữa đặc", "lon", 100m, 10m, 25_000m);
            var duongIngredient = CreateIngredient(tenantId, "Đường", "kg", 100m, 5m, 15_000m);
            var traDaoIngredient = CreateIngredient(tenantId, "Trà đào", "gói", 100m, 10m, 8_000m);
            var botTranChauIngredient = CreateIngredient(tenantId, "Bột trân châu", "kg", 100m, 3m, 120_000m);
            var boIngredient = CreateIngredient(tenantId, "Bơ", "trái", 100m, 10m, 15_000m);
            var banhMiIngredient = CreateIngredient(tenantId, "Bánh mì", "ổ", 100m, 20m, 5_000m);
            var thitNguoiIngredient = CreateIngredient(tenantId, "Thịt nguội", "kg", 100m, 3m, 180_000m);
            var gaIngredient = CreateIngredient(tenantId, "Gà", "con", 100m, 5m, 80_000m);
            var comIngredient = CreateIngredient(tenantId, "Cơm", "kg", 100m, 10m, 20_000m);
            var miYIngredient = CreateIngredient(tenantId, "Mì ý", "gói", 100m, 15m, 12_000m);
            var thitBoBamIngredient = CreateIngredient(tenantId, "Thịt bò bằm", "kg", 100m, 3m, 220_000m);

            var ingredients = new[]
            {
                cafeBotIngredient, suaDacIngredient, duongIngredient, traDaoIngredient,
                botTranChauIngredient, boIngredient, banhMiIngredient, thitNguoiIngredient,
                gaIngredient, comIngredient, miYIngredient, thitBoBamIngredient
            };
            await dbContext.Ingredients.AddRangeAsync(ingredients, ct);

            // ── 4. Recipes (product ↔ ingredient mappings) ────────────────────────
            // NOTE: Recipe.ProductId and Recipe.IngredientId are FK → BaseEntity.Id (PK Guid),
            // not the domain value-object IngredientId/ProductId. Use .Id from BaseEntity.
            var recipes = new[]
            {
                CreateRecipe(tenantId, cafeDen.Id, cafeBotIngredient.Id, 0.02m),
                CreateRecipe(tenantId, cafeSua.Id, cafeBotIngredient.Id, 0.02m),
                CreateRecipe(tenantId, cafeSua.Id, suaDacIngredient.Id, 0.05m),
                CreateRecipe(tenantId, traDao.Id, traDaoIngredient.Id, 1m),
                CreateRecipe(tenantId, traDao.Id, duongIngredient.Id, 0.05m),
                CreateRecipe(tenantId, traSuaTranChau.Id, botTranChauIngredient.Id, 0.05m),
                CreateRecipe(tenantId, traSuaTranChau.Id, suaDacIngredient.Id, 0.05m),
                CreateRecipe(tenantId, sinhToBo.Id, boIngredient.Id, 1m),
                CreateRecipe(tenantId, banhMiThitNguoi.Id, banhMiIngredient.Id, 1m),
                CreateRecipe(tenantId, banhMiThitNguoi.Id, thitNguoiIngredient.Id, 0.05m),
                CreateRecipe(tenantId, comGaXoiMo.Id, gaIngredient.Id, 0.5m),
                CreateRecipe(tenantId, comGaXoiMo.Id, comIngredient.Id, 0.2m),
                CreateRecipe(tenantId, miYBoBam.Id, miYIngredient.Id, 1m),
                CreateRecipe(tenantId, miYBoBam.Id, thitBoBamIngredient.Id, 0.1m),
            };
            await dbContext.Recipes.AddRangeAsync(recipes, ct);

            // ── 5. Inventory (initial stock for each ingredient) ──────────────────
            // NOTE: Inventory.IngredientId is FK → Ingredient.Id (base entity PK)
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

        // ── Private helpers ───────────────────────────────────────────────────────

        private static Ingredient CreateIngredient(
            TenantId tenantId, string name, string unit,
            decimal currentStock, decimal minStockThreshold, decimal pricePerUnit)
        {
            var ingredient = (Ingredient)Activator.CreateInstance(typeof(Ingredient), nonPublic: true)!;
            ingredient.Name = name;
            ingredient.Unit = unit;
            ingredient.CurrentStock = currentStock;
            ingredient.MinStockThreshold = minStockThreshold;
            ingredient.PricePerUnit = pricePerUnit;

            // Set TenantId via reflection (protected setter on BaseEntity)
            System.Reflection.PropertyInfo? tenantProp = typeof(Ingredient).GetProperty("TenantId");
            tenantProp?.SetValue(ingredient, tenantId);

            return ingredient;
        }

        private static Recipe CreateRecipe(TenantId tenantId, Guid productId, Guid ingredientId, decimal quantityNeeded)
        {
            var recipe = (Recipe)Activator.CreateInstance(typeof(Recipe), nonPublic: true)!;
            recipe.ProductId = productId;
            recipe.IngredientId = ingredientId;
            recipe.QuantityNeeded = quantityNeeded;

            // Set TenantId via reflection (protected setter on BaseEntity)
            System.Reflection.PropertyInfo? tenantProp = typeof(Recipe).GetProperty("TenantId");
            tenantProp?.SetValue(recipe, tenantId);

            return recipe;
        }
    }
}
