using Bunit;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using VanAn.KhachLink.Components;
using VanAn.Shared.Domain;
using VanAn.UI.Platform.Adapters;
using VanAn.UI.Platform.Core.Interfaces;
using Xunit;

namespace VanAn.Core.Tests.KhachLink;

/// <summary>
/// Map upgrade 2026-11-07: storefront "Tìm chúng tôi" map — Google Embed iframe.
/// embed/v1/place when GoogleMaps:EmbedKey configured, free output=embed fallback.
/// </summary>
public class GoogleMapsTests : TestContext
{
    public GoogleMapsTests()
    {
        Services.AddSingleton<ICssAdapter, BootstrapAdapter>();
    }

    private void UseKey(string? key)
    {
        Services.AddSingleton<IConfiguration>(
            new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?> { ["GoogleMaps:EmbedKey"] = key })
                .Build());
    }

    [Fact]
    public void Renders_EmbedV1_WhenKeyConfigured()
    {
        UseKey("TESTKEY");
        var cut = RenderComponent<GoogleMaps>(p => p
            .Add(c => c.ShopConfig, new ShopConfig { Latitude = 10.5, Longitude = 106.7 }));

        var src = cut.Find("iframe.store-map-iframe").GetAttribute("src")!;
        src.Should().Contain("embed/v1/place");
        src.Should().Contain("key=TESTKEY");
        src.Should().Contain("q=10.5,106.7");
    }

    [Fact]
    public void Renders_OutputEmbed_WhenNoKey()
    {
        UseKey(null);
        var cut = RenderComponent<GoogleMaps>(p => p
            .Add(c => c.ShopConfig, new ShopConfig { Latitude = 10.5, Longitude = 106.7 }));

        var src = cut.Find("iframe.store-map-iframe").GetAttribute("src")!;
        src.Should().Contain("output=embed");
        src.Should().NotContain("key=");
    }

    [Fact]
    public void Shows_NoGps_WhenNoCoords()
    {
        UseKey("K");
        var cut = RenderComponent<GoogleMaps>(p => p
            .Add(c => c.ShopConfig, new ShopConfig()));

        cut.FindAll("iframe").Should().BeEmpty();
        cut.Markup.Should().Contain("Chưa cập nhật tọa độ");
    }
}
