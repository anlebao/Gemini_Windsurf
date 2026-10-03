using Microsoft.EntityFrameworkCore;
using VanAn.CoreHub.Tests.TestInfrastructure;
using VanAn.Shared.Domain;
using Xunit;

namespace VanAn.Core.Tests.InventoryIntelligence
{
    /// <summary>
    /// VA-IIE Sprint B (P1) — Recipe versioning (header + RecipeLine, SRS §7.3).
    /// Pure domain tests + EF round-trip (Single-Identity, cascade save).
    /// </summary>
    public class RecipeVersioningTests
    {
        private static readonly TenantId TestTenantId = new(Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"));
        private static readonly Guid ProductId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        private static readonly Guid IngredientA = Guid.Parse("cccccccc-cccc-cccc-cccc-ccccccccccc1");
        private static readonly Guid IngredientB = Guid.Parse("cccccccc-cccc-cccc-cccc-ccccccccccc2");

        // ── Header ───────────────────────────────────────────────────────────────

        [Fact]
        public void Create_ShouldBeVersion1ActiveWithDefaults()
        {
            var recipe = new Recipe(TestTenantId, ProductId);

            Assert.Equal(ProductId, recipe.ProductId);
            Assert.Equal(1, recipe.Version);
            Assert.True(recipe.IsActive);
            Assert.Equal(1m, recipe.Yield);
            Assert.Equal(0m, recipe.WasteFactor);
            Assert.NotNull(recipe.EffectiveFrom);
            Assert.Empty(recipe.Lines);

            // SINGLE-IDENTITY: Id (PK) == RecipeId.Value (business key)
            Assert.Equal(recipe.Id, recipe.RecipeId.Value);
        }

        // ── RecipeLine ───────────────────────────────────────────────────────────

        [Fact]
        public void AddLine_ShouldAddLineWithUnit()
        {
            var recipe = new Recipe(TestTenantId, ProductId);

            RecipeLine line = recipe.AddLine(IngredientA, 20m, "g");

            Assert.Single(recipe.Lines);
            Assert.Equal(recipe.Id, line.RecipeId);
            Assert.Equal(IngredientA, line.IngredientId);
            Assert.Equal(20m, line.Quantity);
            Assert.Equal("g", line.Unit);
            Assert.Equal(line.Id, line.RecipeLineId.Value); // Single-Identity
            Assert.Equal(TestTenantId, line.TenantId);
        }

        [Fact]
        public void AddLine_EmptyUnit_ShouldThrow()
        {
            var recipe = new Recipe(TestTenantId, ProductId);

            Assert.Throws<ArgumentException>(() => recipe.AddLine(IngredientA, 20m, " "));
        }

        [Fact]
        public void AddLine_MultipleIngredients_ShouldAccumulate()
        {
            var recipe = new Recipe(TestTenantId, ProductId);
            recipe.AddLine(IngredientA, 20m, "g");
            recipe.AddLine(IngredientB, 0.03m, "lon");

            Assert.Equal(2, recipe.Lines.Count);
            Assert.Equal(0.03m, recipe.Lines[1].Quantity);
            Assert.Equal("lon", recipe.Lines[1].Unit);
        }

        // ── Versioning (PublishNewVersion) ───────────────────────────────────────

        [Fact]
        public void PublishNewVersion_ShouldDeactivateCurrent_AndCopyLines()
        {
            var recipe = new Recipe(TestTenantId, ProductId);
            recipe.AddLine(IngredientA, 20m, "g");
            recipe.AddLine(IngredientB, 0.03m, "lon");

            Recipe v2 = recipe.PublishNewVersion();

            // Current version bị deactivate
            Assert.False(recipe.IsActive);

            // Version mới: v2, active, copy toàn bộ lines
            Assert.Equal(2, v2.Version);
            Assert.True(v2.IsActive);
            Assert.Equal(ProductId, v2.ProductId);
            Assert.Equal(2, v2.Lines.Count);
            Assert.Equal(IngredientA, v2.Lines[0].IngredientId);
            Assert.Equal(20m, v2.Lines[0].Quantity);
            Assert.Equal("g", v2.Lines[0].Unit);
            Assert.Equal(IngredientB, v2.Lines[1].IngredientId);
            Assert.Equal(0.03m, v2.Lines[1].Quantity);
            Assert.Equal("lon", v2.Lines[1].Unit);
        }

        [Fact]
        public void PublishNewVersion_ShouldIncrementVersionEachTime()
        {
            var recipe = new Recipe(TestTenantId, ProductId);
            recipe.AddLine(IngredientA, 20m, "g");

            Recipe v2 = recipe.PublishNewVersion();
            Recipe v3 = v2.PublishNewVersion();

            Assert.Equal(3, v3.Version);
            Assert.False(v2.IsActive);
            Assert.True(v3.IsActive);
        }

        // ── EF round-trip (EnsureCreated — PG context trên SQLite in-memory) ─────

        [Fact]
        public async Task Recipe_WithLines_SaveAndLoad_WithInclude()
        {
            using var scope = VanAnDbContextTestFactory.Create();
            scope.TenantProvider!.SetTenant(TestTenantId.Value);
            var ctx = scope.Context;

            var product = new Product(TestTenantId, "Cà phê sữa đá", "Test product", 30_000m, "Cà phê");
            var ingredient = new Ingredient(TestTenantId, "Cà phê bột", "g", 100_000m, 5_000m, 200m);
            ctx.Products.Add(product);
            ctx.Ingredients.Add(ingredient);
            await ctx.SaveChangesAsync();

            var recipe = new Recipe(TestTenantId, product.Id);
            recipe.AddLine(ingredient.Id, 20m, "g");
            ctx.Recipes.Add(recipe);
            await ctx.SaveChangesAsync();

            // Cascade save: RecipeLine được insert cùng Recipe header
            var loaded = await ctx.Recipes
                .Include(r => r.Lines)
                .FirstAsync(r => r.Id == recipe.Id);

            Assert.Equal(1, loaded.Version);
            Assert.True(loaded.IsActive);
            Assert.Single(loaded.Lines);
            Assert.Equal(ingredient.Id, loaded.Lines[0].IngredientId);
            Assert.Equal(20m, loaded.Lines[0].Quantity);
            Assert.Equal("g", loaded.Lines[0].Unit);
            Assert.Equal(loaded.Id, loaded.Lines[0].RecipeId);
            Assert.Equal(TestTenantId, loaded.Lines[0].TenantId);
        }
    }
}
