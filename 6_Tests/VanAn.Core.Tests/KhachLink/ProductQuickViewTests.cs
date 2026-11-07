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
/// SF-W2 (B3): product quick-view modal — renders product content inside VanAnModal,
/// close/add-to-cart/ask-AI callbacks, "Hỏi AI" gated by the tenant feature flag.
/// </summary>
public class ProductQuickViewTests : TestContext
{
    public ProductQuickViewTests()
    {
        Services.AddSingleton<ICssAdapter, BootstrapAdapter>();
    }

    private static ProductDto Sample() => new()
    {
        ProductId = Guid.NewGuid(),
        Name = "Phở chay đặc biệt",
        Description = "Nước dùng chay thanh ngọt.",
        Price = 80_000m,
        ImageUrl = "https://img.test/pho-chay.png"
    };

    private IRenderedComponent<ProductQuickView> Render(
        ProductDto? product,
        bool aiEnabled = true,
        Action? onClose = null,
        Action<ProductDto>? onAddToCart = null,
        Action<ProductDto>? onAskAi = null)
        => RenderComponent<ProductQuickView>(p => p
            .Add(c => c.Product, product)
            .Add(c => c.AiEnabled, aiEnabled)
            .Add(c => c.OnClose, onClose ?? (() => { }))
            .Add(c => c.OnAddToCart, onAddToCart ?? (_ => { }))
            .Add(c => c.OnAskAi, onAskAi ?? (_ => { })));

    [Fact]
    public void NullProduct_RendersNoModal()
    {
        var cut = Render(null);
        cut.FindAll(".modal").Should().BeEmpty();
    }

    [Fact]
    public void Product_RendersNamePriceAndImage()
    {
        var cut = Render(Sample());

        cut.Find(".modal-title").TextContent.Should().Be("Phở chay đặc biệt");
        cut.Find(".qv-desc").TextContent.Should().Contain("Nước dùng chay");
        cut.Find(".qv-price").TextContent.Should().Contain("80");
        cut.Find(".qv-media img").GetAttribute("src").Should().Contain("pho-chay.png");
    }

    [Fact]
    public void CloseButton_InvokesOnClose()
    {
        var closed = false;
        var cut = Render(Sample(), onClose: () => closed = true);

        cut.Find("button.btn-close").Click();
        closed.Should().BeTrue();
    }

    [Fact]
    public void AddToCart_InvokesWithProduct()
    {
        var product = Sample();
        ProductDto? added = null;
        var cut = Render(product, onAddToCart: p => added = p);

        cut.Find(".qv-actions .btn-cta").Click();
        added.Should().BeSameAs(product);
    }

    [Fact]
    public void AskAi_RendersOnlyWhenEnabled_AndInvokes()
    {
        var product = Sample();

        var offCut = Render(product, aiEnabled: false);
        offCut.FindAll(".qv-actions .store-ai-chip").Should().BeEmpty("AI flag off → chip hidden");

        ProductDto? asked = null;
        var onCut = Render(product, aiEnabled: true, onAskAi: p => asked = p);
        onCut.Find(".qv-actions .store-ai-chip").Click();
        asked.Should().BeSameAs(product);
    }
}
