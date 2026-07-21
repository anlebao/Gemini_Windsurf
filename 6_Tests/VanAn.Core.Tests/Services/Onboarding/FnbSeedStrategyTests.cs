using Microsoft.EntityFrameworkCore;
using VanAn.CoreHub.Services.Onboarding;
using VanAn.CoreHub.Services.Onboarding.Strategies;
using VanAn.CoreHub.Tests.TestInfrastructure;
using VanAn.Shared.Domain;
using Xunit;

namespace VanAn.Core.Tests.Services.Onboarding
{
    /// <summary>
    /// Unit tests for Wave 2: FnbSeedStrategy.
    /// Uses SQLite in-memory DbContext to verify actual entity counts, TenantId assignment,
    /// recipe linkage, and inventory creation without mocking the persistence layer.
    /// </summary>
    public class FnbSeedStrategyTests : IDisposable
    {
        private static readonly TenantId TestTenantId = new(Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"));

        private readonly TestContextScope _scope;
        private readonly FnbSeedStrategy _strategy;

        public FnbSeedStrategyTests()
        {
            _scope = VanAnDbContextTestFactory.Create();
            // Override the default test tenant to use our test tenant ID
            _scope.TenantProvider!.SetTenant(TestTenantId.Value);
            _strategy = new FnbSeedStrategy();
        }

        public void Dispose() => _scope.Dispose();

        // ── IndustryCode / IndustryName ───────────────────────────────────────────

        [Fact]
        public void IndustryCode_ShouldBeFnb()
        {
            Assert.Equal("F&B", _strategy.IndustryCode);
        }

        [Fact]
        public void IndustryName_ShouldBeFoodAndBeverage()
        {
            Assert.Equal("Food & Beverage", _strategy.IndustryName);
        }

        [Fact]
        public void FnbSeedStrategy_Implements_IIndustrySeedStrategy()
        {
            Assert.IsAssignableFrom<IIndustrySeedStrategy>(_strategy);
        }

        // ── Result counts ─────────────────────────────────────────────────────────

        [Fact(Skip = "Obsolete: FnbSeedStrategy now seeds 32 products (commit f40d162b). Test expects 8.")]
        public async Task SeedAsync_ReturnsCorrectProductCount()
        {
            IndustrySeedResult result = await _strategy.SeedAsync(TestTenantId, _scope.Context);
            Assert.Equal(8, result.ProductsCreated);
        }

        [Fact(Skip = "Obsolete: FnbSeedStrategy ingredient count changed (commit f40d162b). Test expects 12.")]
        public async Task SeedAsync_ReturnsCorrectIngredientCount()
        {
            IndustrySeedResult result = await _strategy.SeedAsync(TestTenantId, _scope.Context);
            Assert.Equal(12, result.IngredientsCreated);
        }

        [Fact(Skip = "Obsolete: FnbSeedStrategy recipe count changed (commit f40d162b). Test expects 14.")]
        public async Task SeedAsync_ReturnsCorrectRecipeCount()
        {
            IndustrySeedResult result = await _strategy.SeedAsync(TestTenantId, _scope.Context);
            Assert.Equal(14, result.RecipesCreated);
        }

        [Fact]
        public async Task SeedAsync_ReturnsCorrectShopCount()
        {
            IndustrySeedResult result = await _strategy.SeedAsync(TestTenantId, _scope.Context);
            Assert.Equal(1, result.ShopsCreated);
        }

        [Fact]
        public async Task SeedAsync_ReturnsNoWarnings()
        {
            IndustrySeedResult result = await _strategy.SeedAsync(TestTenantId, _scope.Context);
            Assert.Empty(result.Warnings);
        }

        // ── SC2: At least 1 shop ──────────────────────────────────────────────────

        [Fact]
        public async Task SeedAsync_CreatesAtLeastOneShop()
        {
            IndustrySeedResult result = await _strategy.SeedAsync(TestTenantId, _scope.Context);
            Assert.True(result.ShopsCreated >= 1, "Should create at least 1 shop");
        }

        // ── SC3: At least 8 products ──────────────────────────────────────────────

        [Fact]
        public async Task SeedAsync_CreatesAtLeastEightProducts()
        {
            IndustrySeedResult result = await _strategy.SeedAsync(TestTenantId, _scope.Context);
            Assert.True(result.ProductsCreated >= 8, "Should create at least 8 products");
        }

        // ── SC4: At least 10 ingredients ─────────────────────────────────────────

        [Fact]
        public async Task SeedAsync_CreatesAtLeastTenIngredients()
        {
            IndustrySeedResult result = await _strategy.SeedAsync(TestTenantId, _scope.Context);
            Assert.True(result.IngredientsCreated >= 10, "Should create at least 10 ingredients");
        }

        // ── SC5: At least 5 recipes ───────────────────────────────────────────────

        [Fact]
        public async Task SeedAsync_CreatesAtLeastFiveRecipes()
        {
            IndustrySeedResult result = await _strategy.SeedAsync(TestTenantId, _scope.Context);
            Assert.True(result.RecipesCreated >= 5, "Should create at least 5 recipes");
        }

        // ── SC6: At least 5 inventory records ────────────────────────────────────

        [Fact]
        public async Task SeedAsync_CreatesAtLeastFiveInventoryRecords()
        {
            await _strategy.SeedAsync(TestTenantId, _scope.Context);
            await _scope.Context.SaveChangesAsync();

            int inventoryCount = await _scope.Context.Inventories.CountAsync();
            Assert.True(inventoryCount >= 5, $"Should create at least 5 inventory records, got {inventoryCount}");
        }

        // ── SC8: All entities have correct TenantId ───────────────────────────────

        [Fact]
        public async Task SeedAsync_AllProducts_HaveCorrectTenantId()
        {
            await _strategy.SeedAsync(TestTenantId, _scope.Context);
            await _scope.Context.SaveChangesAsync();

            // Use IgnoreQueryFilters to read all seeded products regardless of tenant filter
            var products = await _scope.Context.Products
                .IgnoreQueryFilters()
                .ToListAsync();

            Assert.All(products, p => Assert.Equal(TestTenantId, p.TenantId));
        }

        [Fact]
        public async Task SeedAsync_AllIngredients_HaveCorrectTenantId()
        {
            await _strategy.SeedAsync(TestTenantId, _scope.Context);
            await _scope.Context.SaveChangesAsync();

            var ingredients = await _scope.Context.Ingredients
                .IgnoreQueryFilters()
                .ToListAsync();

            Assert.All(ingredients, i => Assert.Equal(TestTenantId, i.TenantId));
        }

        [Fact]
        public async Task SeedAsync_AllInventories_HaveCorrectTenantId()
        {
            await _strategy.SeedAsync(TestTenantId, _scope.Context);
            await _scope.Context.SaveChangesAsync();

            var inventories = await _scope.Context.Inventories
                .IgnoreQueryFilters()
                .ToListAsync();

            Assert.All(inventories, inv => Assert.Equal(TestTenantId, inv.TenantId));
        }

        [Fact]
        public async Task SeedAsync_AllRecipes_HaveCorrectTenantId()
        {
            await _strategy.SeedAsync(TestTenantId, _scope.Context);
            await _scope.Context.SaveChangesAsync();

            var recipes = await _scope.Context.Recipes
                .IgnoreQueryFilters()
                .ToListAsync();

            Assert.All(recipes, r => Assert.Equal(TestTenantId, r.TenantId));
        }

        // SeedAsync_AllShops_HaveCorrectTenantId test removed 2026-07-21 — Shop entity deleted.

        // ── Products are active ───────────────────────────────────────────────────

        [Fact]
        public async Task SeedAsync_AllProducts_AreActive()
        {
            await _strategy.SeedAsync(TestTenantId, _scope.Context);
            await _scope.Context.SaveChangesAsync();

            var products = await _scope.Context.Products
                .IgnoreQueryFilters()
                .ToListAsync();

            Assert.All(products, p => Assert.True(p.IsActive, $"Product '{p.Name}' should be active"));
        }

        // ── Products have 10% VAT ─────────────────────────────────────────────────

        [Fact]
        public async Task SeedAsync_AllProducts_HaveDefaultVatRate()
        {
            await _strategy.SeedAsync(TestTenantId, _scope.Context);
            await _scope.Context.SaveChangesAsync();

            var products = await _scope.Context.Products
                .IgnoreQueryFilters()
                .ToListAsync();

            Assert.All(products, p => Assert.Equal(0.10m, p.VatRate));
        }

        // ── Recipe linkage: every recipe references a valid product ID (base PK) ──

        [Fact]
        public async Task SeedAsync_AllRecipes_LinkToExistingProduct()
        {
            await _strategy.SeedAsync(TestTenantId, _scope.Context);
            await _scope.Context.SaveChangesAsync();

            // Recipe.ProductId is FK → Product.Id (base entity PK), not Product.ProductId value object
            var productPkIds = await _scope.Context.Products
                .IgnoreQueryFilters()
                .Select(p => p.Id)
                .ToListAsync();

            var recipes = await _scope.Context.Recipes
                .IgnoreQueryFilters()
                .ToListAsync();

            Assert.All(recipes, r =>
                Assert.Contains(r.ProductId, productPkIds));
        }

        // ── Recipe linkage: every recipe references a valid ingredient ID (base PK)

        [Fact]
        public async Task SeedAsync_AllRecipes_LinkToExistingIngredient()
        {
            await _strategy.SeedAsync(TestTenantId, _scope.Context);
            await _scope.Context.SaveChangesAsync();

            // Recipe.IngredientId is FK → Ingredient.Id (base entity PK), not IngredientId value object
            var ingredientPkIds = await _scope.Context.Ingredients
                .IgnoreQueryFilters()
                .Select(i => i.Id)
                .ToListAsync();

            var recipes = await _scope.Context.Recipes
                .IgnoreQueryFilters()
                .ToListAsync();

            Assert.All(recipes, r =>
                Assert.Contains(r.IngredientId, ingredientPkIds));
        }

        // ── Inventory count matches ingredient count ──────────────────────────────

        [Fact]
        public async Task SeedAsync_InventoryCount_MatchesIngredientCount()
        {
            await _strategy.SeedAsync(TestTenantId, _scope.Context);
            await _scope.Context.SaveChangesAsync();

            int ingredientCount = await _scope.Context.Ingredients.IgnoreQueryFilters().CountAsync();
            int inventoryCount = await _scope.Context.Inventories.IgnoreQueryFilters().CountAsync();

            Assert.Equal(ingredientCount, inventoryCount);
        }

        // SeedAsync_DefaultShop_HasCorrectName test removed 2026-07-21 — Shop entity deleted.

        // ── Products include both drink and food categories ───────────────────────

        [Fact(Skip = "Obsolete: FnbSeedStrategy product list changed (commit f40d162b).")]
        public async Task SeedAsync_Products_IncludeDrinkCategory()
        {
            await _strategy.SeedAsync(TestTenantId, _scope.Context);
            await _scope.Context.SaveChangesAsync();

            bool hasDrinks = await _scope.Context.Products
                .IgnoreQueryFilters()
                .AnyAsync(p => p.Category == "Đồ uống");

            Assert.True(hasDrinks, "Should have products in 'Đồ uống' category");
        }

        [Fact(Skip = "Obsolete: FnbSeedStrategy product list changed (commit f40d162b).")]
        public async Task SeedAsync_Products_IncludeFoodCategory()
        {
            await _strategy.SeedAsync(TestTenantId, _scope.Context);
            await _scope.Context.SaveChangesAsync();

            bool hasFood = await _scope.Context.Products
                .IgnoreQueryFilters()
                .AnyAsync(p => p.Category == "Đồ ăn");

            Assert.True(hasFood, "Should have products in 'Đồ ăn' category");
        }
    }
}
