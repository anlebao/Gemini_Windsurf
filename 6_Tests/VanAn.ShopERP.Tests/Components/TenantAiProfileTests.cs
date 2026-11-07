using Bunit;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using VanAn.Shared.Services;
using VanAn.ShopERP.Components.Pages.Admin.TenantAi;
using VanAn.ShopERP.Services;
using Xunit;

namespace VanAn.ShopERP.Tests.Components;

/// <summary>
/// Task AI-P10 — regression RV production: option rollback từng render literal
/// "v@v.Version" vì Razor parse "v@v." như email. Assert label chứa số version thật.
/// </summary>
public sealed class TenantAiProfileTests : ComponentTestBase
{
    private static readonly Guid Tenant = Guid.Parse("00000000-0000-0000-0000-000000000001");

    [Fact]
    public void RollbackDropdown_OptionLabels_ShowVersionNumbers()
    {
        var client = new Mock<ITenantAiClient>(MockBehavior.Strict);
        Services.AddSingleton(client.Object);
        var flags = new Mock<IShopFeatureSettingsService>();
        flags.Setup(f => f.IsEnabledAsync(
                Tenant, TenantAiKnowledgeSyncService.FlagName, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        Services.AddSingleton(flags.Object);

        var published = new TenantAiProfileVersion(3, "published", "Shi", "{}", "{}",
            DateTime.UtcNow, "ops");
        var archived = new TenantAiProfileVersion(1, "archived", "Shi", "{}", "{}",
            DateTime.UtcNow.AddDays(-1), "ops");
        client.Setup(c => c.GetProfileAsync(Tenant, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TenantAiProfileResponse(null, published, [published, archived]));

        var cut = RenderComponent<TenantAiProfile>();

        cut.WaitForAssertion(() =>
            cut.Find("[data-testid='select-rollback-version']").Should().NotBeNull(),
            TimeSpan.FromSeconds(5));
        var labels = cut.FindAll("[data-testid='select-rollback-version'] option")
            .Select(o => o.TextContent).ToList();
        labels.Should().Contain(l => l.Contains("v3"), "bản published phải hiện số version");
        labels.Should().Contain(l => l.Contains("v1"), "bản archived phải hiện số version");
        labels.Should().NotContain(l => l.Contains("v@v"), "không được render literal 'v@v.Version'");
    }
}
