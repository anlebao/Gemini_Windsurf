using Bunit;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using VanAn.CoreHub.Services.InventoryIntelligence;
using VanAn.Shared.Domain;

namespace VanAn.ShopERP.Tests.Components.Inventory;

/// <summary>
/// VA-IIE Phase 3 (2026-10-05, Gate 5): bUnit smoke tests — Forecast page render không crash
/// + config modal save gọi IForecastService.UpdateConfigAsync.
/// </summary>
public class ForecastPageTests : ComponentTestBase
{
    private static readonly ForecastReport EmptyReport = new(
        DateTime.UtcNow, 14, 2, 1, [], [], new Dictionary<Guid, IReadOnlyList<DailyConsumption>>());

    [Fact]
    public void Forecast_Renders_WithEmptyReport()
    {
        var forecastService = new Mock<IForecastService>();
        forecastService.Setup(f => f.GetForecastAsync(It.IsAny<TenantId>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(EmptyReport);
        Services.AddSingleton(forecastService.Object);

        var cut = RenderComponent<ShopERP.Components.Pages.Inventory.Forecast>();

        cut.WaitForAssertion(() => cut.Find("h1").TextContent.Should().Contain("Dự báo tồn kho"));
        // 4 stat cards render
        cut.WaitForAssertion(() => cut.FindAll(".stat-card").Should().HaveCount(4));
        // Empty states: Restock + Stockout + Trend
        cut.Markup.Should().Contain("Không có nguyên liệu cần nhập");
        cut.Markup.Should().Contain("Chưa có nguyên liệu với dữ liệu tiêu hao");
        cut.Markup.Should().Contain("Chưa có dữ liệu tiêu hao trong window");
    }

    [Fact]
    public void Forecast_ConfigModal_SavesViaService()
    {
        var forecastService = new Mock<IForecastService>();
        forecastService.Setup(f => f.GetForecastAsync(It.IsAny<TenantId>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(EmptyReport);
        forecastService.Setup(f => f.UpdateConfigAsync(It.IsAny<TenantId>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new VaIIeTenantConfig(new TenantId(Guid.Empty)));
        Services.AddSingleton(forecastService.Object);

        var cut = RenderComponent<ShopERP.Components.Pages.Inventory.Forecast>();

        // Mở modal cấu hình
        cut.WaitForAssertion(() => cut.FindAll("button").Should().Contain(b => b.TextContent.Contains("Cấu hình dự báo")));
        cut.FindAll("button").First(b => b.TextContent.Contains("Cấu hình dự báo")).Click();
        cut.WaitForAssertion(() => cut.FindAll(".modal").Should().NotBeEmpty());

        // Confirm modal → UpdateConfigAsync được gọi
        cut.FindAll(".modal button").First(b => b.TextContent.Contains("Confirm")).Click();
        forecastService.Verify(f => f.UpdateConfigAsync(
            It.IsAny<TenantId>(), 14, 2, 1, It.IsAny<CancellationToken>()), Times.Once);
    }
}
