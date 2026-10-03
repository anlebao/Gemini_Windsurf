using Bunit;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using VanAn.CoreHub.Services.InventoryIntelligence;
using VanAn.ShopERP.Infrastructure;
using VanAn.ShopERP.Services;

namespace VanAn.ShopERP.Tests.Components.Inventory;

/// <summary>
/// VA-IIE Sprint B (P3, Gate 5): bUnit smoke tests — 4 pages render không crash.
/// ShiftReport + RecipeManagement: mock service (trạng thái rỗng).
/// InventoryDashboard + AlertCenter: ShopERPDbContext SQLite in-memory thật (EnsureCreated — schema mới).
/// </summary>
public class VaIiePagesTests : ComponentTestBase
{
    [Fact]
    public void ShiftReport_Renders_WithEmptyData()
    {
        var shiftService = new Mock<IShiftReportService>();
        shiftService.Setup(s => s.ListShiftsAsync(It.IsAny<DateTime?>(), It.IsAny<DateTime?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        shiftService.Setup(s => s.GetIngredientsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        Services.AddSingleton(shiftService.Object);

        var cut = RenderComponent<ShopERP.Components.Pages.Inventory.ShiftReport>();

        cut.WaitForAssertion(() => cut.Find("h1").TextContent.Should().Contain("Báo cáo ca"));
        // Card "Danh sách ca" render (empty message "Chưa có ca nào")
        cut.WaitForAssertion(() => cut.FindAll(".vanan-card__title").Should().NotBeEmpty());
        cut.Markup.Should().Contain("Chưa có ca nào");
    }

    [Fact]
    public void RecipeManagement_Renders_WithEmptyData()
    {
        var recipeService = new Mock<IRecipeService>();
        recipeService.Setup(r => r.GetProductsAsync(It.IsAny<CancellationToken>())).ReturnsAsync([]);
        recipeService.Setup(r => r.GetIngredientsAsync(It.IsAny<CancellationToken>())).ReturnsAsync([]);
        Services.AddSingleton(recipeService.Object);

        var cut = RenderComponent<ShopERP.Components.Pages.Inventory.RecipeManagement>();

        cut.WaitForAssertion(() => cut.Find("h1").TextContent.Should().Contain("Công thức pha chế"));
        cut.WaitForAssertion(() => cut.FindAll(".vanan-card__title").Should().NotBeEmpty());
    }

    [Fact]
    public void InventoryDashboard_Renders_OnEmptyDatabase()
    {
        RegisterSqliteContext();

        var foodCost = new Mock<IFoodCostService>();
        Services.AddSingleton(foodCost.Object);

        var cut = RenderComponent<ShopERP.Components.Pages.Inventory.InventoryDashboard>();

        cut.WaitForAssertion(() => cut.Find("h1").TextContent.Should().Contain("Tồn kho"));
        cut.WaitForAssertion(() => cut.FindAll(".stat-card").Should().NotBeEmpty());
    }

    [Fact]
    public void AlertCenter_Renders_OnEmptyDatabase()
    {
        RegisterSqliteContext();

        var cut = RenderComponent<ShopERP.Components.Pages.Inventory.AlertCenter>();

        cut.WaitForAssertion(() => cut.Find("h1").TextContent.Should().Contain("Cảnh báo"));
        // Bộ lọc mức độ render
        cut.WaitForAssertion(() => cut.FindAll("select[aria-label=\"Mức độ\"]").Should().NotBeEmpty());
        cut.Markup.Should().Contain("Không có cảnh báo phù hợp");
    }

    private void RegisterSqliteContext()
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
    }
}
