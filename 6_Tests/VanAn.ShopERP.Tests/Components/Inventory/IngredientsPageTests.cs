using Bunit;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using VanAn.CoreHub.Services;
using VanAn.ShopERP.Infrastructure;
using VanAn.Shared.Domain;
using Xunit;

namespace VanAn.ShopERP.Tests.Components.Inventory;

/// <summary>
/// Issue #188 bug 3 (2026-10-08): Ingredients CRUD + nhập/xuất kho — ShopERPDbContext SQLite thật
/// (pattern VaIiePagesTests.RegisterSqliteContext) + IAuditTrailService mock.
/// </summary>
public class IngredientsPageTests : ComponentTestBase
{
    private static readonly TenantId TestTenant = new(Guid.Parse("00000000-0000-0000-0000-000000000001"));

    private (ShopERPDbContext Ctx, ShopERPDbContext? Existing) RegisterSqliteContext()
    {
        var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();
        var options = new DbContextOptionsBuilder<ShopERPDbContext>()
            .UseSqlite(connection)
            .Options;
        var context = new ShopERPDbContext(options);
        context.Database.EnsureCreated();

        Services.AddSingleton(context);
        Services.AddSingleton<VanAn.CoreHub.Infrastructure.IVanAnDbContext>(sp => sp.GetRequiredService<ShopERPDbContext>());
        Services.AddSingleton(new Mock<IAuditTrailService>().Object);
        return (context, null);
    }

    [Fact]
    public void Ingredients_Renders_EmptyState()
    {
        RegisterSqliteContext();

        var cut = RenderComponent<ShopERP.Components.Pages.Inventory.Ingredients>();

        cut.WaitForAssertion(() => cut.Find("h1").TextContent.Should().Contain("Nguyên liệu"));
        cut.WaitForAssertion(() => cut.Markup.Should().Contain("Chưa có nguyên liệu"));
        cut.WaitForAssertion(() => cut.Find("[data-testid='ingredient-add']").Should().NotBeNull());
    }

    [Fact]
    public void Ingredients_Create_AddsIngredient()
    {
        var (ctx, _) = RegisterSqliteContext();

        var cut = RenderComponent<ShopERP.Components.Pages.Inventory.Ingredients>();
        cut.WaitForAssertion(() => cut.Find("h1").TextContent.Should().Contain("Nguyên liệu"));

        cut.FindAll("button").First(b => b.TextContent.Contains("Thêm nguyên liệu")).Click();
        cut.WaitForAssertion(() => cut.FindAll(".modal").Should().NotBeEmpty());
        cut.Find("[data-testid='ingredient-name']").Change("Cà phê rang xay");
        cut.Find("[data-testid='ingredient-unit']").Change("kg");
        cut.Find("[data-testid='ingredient-stock']").Change("10");
        cut.Find("[data-testid='ingredient-threshold']").Change("2");
        cut.Find("[data-testid='ingredient-price']").Change("150000");
        cut.FindAll(".modal button").First(b => b.TextContent.Contains("Confirm")).Click();

        cut.WaitForAssertion(() => cut.Markup.Should().Contain("Đã thêm nguyên liệu"));
        cut.WaitForAssertion(() => cut.Markup.Should().Contain("Cà phê rang xay"));
        var saved = ctx.Ingredients.IgnoreQueryFilters().FirstOrDefault(i => i.Name == "Cà phê rang xay");
        saved.Should().NotBeNull();
        saved!.CurrentStock.Should().Be(10);
        saved.MinStockThreshold.Should().Be(2);
        saved.Unit.Should().Be("kg");
    }

    [Fact]
    public void Ingredients_AdjustIn_UpdatesStock()
    {
        var (ctx, _) = RegisterSqliteContext();
        var ingredient = new Ingredient(TestTenant, "Đường", "kg", 5, 2, 20000, IngredientCategory.RawMaterial);
        ctx.Ingredients.Add(ingredient);
        ctx.SaveChanges();

        var cut = RenderComponent<ShopERP.Components.Pages.Inventory.Ingredients>();
        cut.WaitForAssertion(() => cut.Find("h1").TextContent.Should().Contain("Nguyên liệu"));
        cut.WaitForAssertion(() => cut.Find($"[data-testid='adjust-in-{ingredient.Id}']").Should().NotBeNull());

        cut.Find($"[data-testid='adjust-in-{ingredient.Id}']").Click();
        cut.WaitForAssertion(() => cut.FindAll(".modal").Should().NotBeEmpty());
        cut.Find("[data-testid='adjust-qty']").Change("15");
        cut.FindAll(".modal button").First(b => b.TextContent.Contains("Confirm")).Click();

        cut.WaitForAssertion(() => cut.Markup.Should().Contain("Đã nhập"));
        cut.WaitForAssertion(() => cut.Find($"[data-testid='stock-{ingredient.Id}']").TextContent.Should().Contain("20"));
    }

    [Fact]
    public void Ingredients_AdjustOut_BlocksNegativeStock()
    {
        var (ctx, _) = RegisterSqliteContext();
        var ingredient = new Ingredient(TestTenant, "Sữa tươi", "lít", 3, 1, 30000, IngredientCategory.RawMaterial);
        ctx.Ingredients.Add(ingredient);
        ctx.SaveChanges();

        var cut = RenderComponent<ShopERP.Components.Pages.Inventory.Ingredients>();
        cut.WaitForAssertion(() => cut.Find("h1").TextContent.Should().Contain("Nguyên liệu"));

        cut.Find($"[data-testid='adjust-out-{ingredient.Id}']").Click();
        cut.WaitForAssertion(() => cut.FindAll(".modal").Should().NotBeEmpty());
        cut.Find("[data-testid='adjust-qty']").Change("10");
        cut.FindAll(".modal button").First(b => b.TextContent.Contains("Confirm")).Click();

        cut.WaitForAssertion(() => cut.Markup.Should().Contain("Không đủ tồn kho"));
        ctx.Ingredients.IgnoreQueryFilters().First(i => i.Id == ingredient.Id).CurrentStock.Should().Be(3);
    }

    [Fact]
    public void Ingredients_Delete_BlockedWhenUsedInRecipe()
    {
        var (ctx, _) = RegisterSqliteContext();
        var ingredient = new Ingredient(TestTenant, "Bột cà phê", "kg", 0, 1, 120000, IngredientCategory.RawMaterial);
        ctx.Ingredients.Add(ingredient);
        ctx.SaveChanges();
        // Recipe tham chiếu nguyên liệu này (guard FK). Recipe.ProductId FK → Products — seed Product stub.
        var product = new Product(TestTenant, "Cà phê đen", 25000, "Đồ uống");
        ctx.Products.Add(product);
        ctx.SaveChanges();
        var recipe = new Recipe(TestTenant, product.Id);
        ctx.Recipes.Add(recipe);
        ctx.SaveChanges();
        ctx.RecipeLines.Add(new RecipeLine(TestTenant, recipe.Id, ingredient.Id, 0.05m, "kg"));
        ctx.SaveChanges();

        var cut = RenderComponent<ShopERP.Components.Pages.Inventory.Ingredients>();
        cut.WaitForAssertion(() => cut.Find("h1").TextContent.Should().Contain("Nguyên liệu"));

        cut.FindAll("button").First(b => b.TextContent.Contains("Xóa")).Click();
        cut.WaitForAssertion(() => cut.FindAll(".modal").Should().NotBeEmpty());
        cut.FindAll(".modal button").First(b => b.TextContent.Contains("Confirm")).Click();

        cut.WaitForAssertion(() => cut.Markup.Should().Contain("đang được dùng trong công thức"));
        ctx.Ingredients.IgnoreQueryFilters().Count(i => i.Id == ingredient.Id).Should().Be(1);
    }
}
