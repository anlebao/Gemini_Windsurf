using Bunit;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using VanAn.KhachLink.Components;
using VanAn.KhachLink.Models;
using VanAn.UI.Platform.Adapters;
using VanAn.UI.Platform.Core.Interfaces;
using Xunit;

namespace VanAn.Core.Tests.KhachLink;

/// <summary>
/// SF-W2 (B3): VibeShowcase card click → OnProductClick (host opens the quick-view
/// modal). Order buttons keep invoking AddToCart (stopPropagation guard in markup).
/// </summary>
public class VibeShowcaseTests : TestContext
{
    public VibeShowcaseTests()
    {
        Services.AddSingleton<ICssAdapter, BootstrapAdapter>();
    }

    private static ProductDto Sample(string name = "Phở chay") => new()
    {
        ProductId = Guid.NewGuid(),
        Name = name,
        Price = 80_000m
    };

    [Fact]
    public void CardClick_InvokesOnProductClick()
    {
        var product = Sample();
        ProductDto? clicked = null;
        var cut = RenderComponent<VibeShowcase>(p => p
            .Add(c => c.Products, new List<ProductDto> { product })
            .Add(c => c.OnProductClick, (ProductDto x) => clicked = x));

        cut.Find(".vibe-qv").Click();

        clicked.Should().BeSameAs(product);
    }

    [Fact]
    public void OrderButton_StillInvokesAddToCart()
    {
        var product = Sample();
        ProductDto? added = null;
        var cut = RenderComponent<VibeShowcase>(p => p
            .Add(c => c.Products, new List<ProductDto> { product })
            .Add(c => c.AddToCart, (ProductDto x) => added = x));

        cut.Find("button").Click();

        added.Should().BeSameAs(product);
    }

    [Fact]
    public void CardClick_WithoutDelegate_DoesNotThrow()
    {
        var cut = RenderComponent<VibeShowcase>(p => p
            .Add(c => c.Products, new List<ProductDto> { Sample() }));

        // No OnProductClick wired (e.g., ShopFeatures host) → click is a no-op.
        var act = () => cut.Find(".vibe-qv").Click();
        act.Should().NotThrow();
    }
}
