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

    // ---------- 2026-11-26: pagination — 8 sản phẩm/trang + prev/next ----------

    private static List<ProductDto> Samples(int count)
        => Enumerable.Range(1, count).Select(i => Sample($"SP {i:D2}")).ToList();

    [Fact]
    public void Pagination_RendersMax8PerPage()
    {
        var cut = RenderComponent<VibeShowcase>(p => p
            .Add(c => c.Products, Samples(10)));

        cut.FindAll(".vibe-qv").Count.Should().Be(8, "chỉ 8 sản phẩm mỗi trang");
        cut.FindAll(".vibe-pager").Count.Should().Be(1, "pager hiện khi >8 sản phẩm");
    }

    [Fact]
    public void Pagination_NextAndPrev_NavigatePages()
    {
        var cut = RenderComponent<VibeShowcase>(p => p
            .Add(c => c.Products, Samples(10)));

        cut.Find(".vibe-pager-next").Click();
        cut.FindAll(".vibe-qv").Count.Should().Be(2, "trang 2 còn 2 sản phẩm");
        cut.Markup.Should().Contain("SP 09");

        cut.Find(".vibe-pager-prev").Click();
        cut.Markup.Should().Contain("SP 01").And.NotContain("SP 09");
    }

    [Fact]
    public void Pagination_Hidden_WhenUnder8()
    {
        var cut = RenderComponent<VibeShowcase>(p => p
            .Add(c => c.Products, Samples(5)));

        cut.FindAll(".vibe-pager").Count.Should().Be(0, "≤8 sản phẩm không cần pager");
    }

    [Fact]
    public void Pagination_PrevDisabled_OnFirstPage()
    {
        var cut = RenderComponent<VibeShowcase>(p => p
            .Add(c => c.Products, Samples(10)));

        cut.Find(".vibe-pager-prev").HasAttribute("disabled").Should().BeTrue();
        cut.Find(".vibe-pager-next").HasAttribute("disabled").Should().BeFalse();
    }
}
