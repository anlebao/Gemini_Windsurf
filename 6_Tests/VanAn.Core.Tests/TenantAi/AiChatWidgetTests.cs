using Bunit;
using Bunit.JSInterop;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using VanAn.UI.Platform.Adapters;
using VanAn.UI.Platform.Components.Ai;
using VanAn.UI.Platform.Core.Interfaces;
using Xunit;

namespace VanAn.Core.Tests.TenantAi;

/// <summary>
/// Task AI-P4 — AiChatWidget bUnit tests: render gate (aiEnabled), product card →
/// cart callback, fallback khi AI down.
/// </summary>
public class AiChatWidgetTests : TestContext
{
    private static readonly Guid TenantId = Guid.NewGuid();

    public AiChatWidgetTests()
    {
        Services.AddSingleton<ICssAdapter, BootstrapAdapter>();
        JSInterop.Mode = JSRuntimeMode.Loose; // localStorage + scrollToBottom → default
    }

    private sealed class FakeAiChatClient : IAiChatClient
    {
        public AiWidgetConfig? Config { get; set; } =
            new(true, "Bé AI", "Chào bạn!", ["Menu hôm nay?"]);
        public AiChatResult? ChatResult { get; set; }
        public int SendCalls { get; private set; }
        public string? LastSessionId { get; private set; }
        public string? LastMessageId { get; private set; }
        public string? LastText { get; private set; }

        public Task<AiWidgetConfig?> GetWidgetConfigAsync(Guid tenantId, CancellationToken ct = default)
            => Task.FromResult(Config);

        public Task<IReadOnlyList<AiSessionMessage>?> GetSessionMessagesAsync(Guid tenantId,
            string sessionId, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<AiSessionMessage>?>([]);

        public Task<AiChatResult?> SendChatAsync(Guid tenantId, string sessionId, string messageId,
            string text, string? locale = null, CancellationToken ct = default)
        {
            SendCalls++;
            LastSessionId = sessionId;
            LastMessageId = messageId;
            LastText = text;
            return Task.FromResult(ChatResult);
        }
    }

    private IRenderedComponent<AiChatWidget> Render(FakeAiChatClient client, Action<AiProductCardItem>? onAddToCart = null)
    {
        Services.AddSingleton<IAiChatClient>(client);
        return RenderComponent<AiChatWidget>(p => p
            .Add(w => w.TenantId, TenantId)
            .Add(w => w.OnAddToCart, onAddToCart ?? (_ => { })));
    }

    [Fact]
    public void Widget_RendersBubble_WhenAiEnabled()
    {
        var cut = Render(new FakeAiChatClient());
        cut.WaitForAssertion(() =>
            cut.Find("button.vanan-ai-bubble-btn").Should().NotBeNull());
    }

    [Fact]
    public void Widget_RendersNothing_WhenAiDisabled()
    {
        var cut = Render(new FakeAiChatClient { Config = new AiWidgetConfig(false, null, null, null) });
        cut.WaitForAssertion(() =>
            cut.Markup.Should().NotContain("vanan-ai-bubble-btn"));
    }

    [Fact]
    public void Widget_RendersNothing_WhenServiceDown_NullConfig()
    {
        var cut = Render(new FakeAiChatClient { Config = null });
        cut.WaitForAssertion(() =>
            cut.Markup.Should().NotContain("vanan-ai-bubble-btn"));
    }

    [Fact]
    public void Open_ShowsPanel_GreetingAndSuggestions()
    {
        var cut = Render(new FakeAiChatClient());
        cut.WaitForAssertion(() => cut.Find("button.vanan-ai-bubble-btn").Click());
        cut.WaitForAssertion(() =>
        {
            cut.Markup.Should().Contain("Bé AI");
            cut.Markup.Should().Contain("Chào bạn!");
            cut.Markup.Should().Contain("Menu hôm nay?");
        });
    }

    [Fact]
    public void Send_AppendsUserAndAssistantMessages()
    {
        var client = new FakeAiChatClient
        {
            ChatResult = new AiChatResult("Quán có phở chay 80k.", [], [], 0.9, false, null, false, false)
        };
        var cut = Render(client);
        cut.WaitForAssertion(() => cut.Find("button.vanan-ai-bubble-btn").Click());
        cut.WaitForAssertion(() => cut.Find(".vanan-ai-panel"));

        cut.Find(".vanan-ai-panel input").Input("Quán có phở chay không?");
        cut.Find("button.vanan-ai-send").Click();

        cut.WaitForAssertion(() =>
        {
            cut.Markup.Should().Contain("Quán có phở chay không?");
            cut.Markup.Should().Contain("Quán có phở chay 80k.");
        });
        client.SendCalls.Should().Be(1);
        client.LastText.Should().Be("Quán có phở chay không?");
        client.LastSessionId.Should().NotBeNullOrEmpty();
        client.LastMessageId.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void ProductCard_AddToCart_InvokesCallbackWithSnapshot()
    {
        var product = new AiProductCardItem(Guid.NewGuid(), "Phở chay", 80_000m, null);
        var client = new FakeAiChatClient
        {
            ChatResult = new AiChatResult("Gợi ý:", [product], ["product:1"], 0.9, false, null, false, false)
        };
        AiProductCardItem? added = null;
        var cut = Render(client, p => added = p);
        cut.WaitForAssertion(() => cut.Find("button.vanan-ai-bubble-btn").Click());
        cut.WaitForAssertion(() => cut.Find(".vanan-ai-panel"));

        cut.Find(".vanan-ai-panel input").Input("Có món chay không?");
        cut.Find("button.vanan-ai-send").Click();

        cut.WaitForAssertion(() => cut.Find(".vanan-ai-product-card"));
        cut.Markup.Should().Contain("Phở chay");
        cut.Markup.Should().Contain("80.000");

        cut.Find(".vanan-ai-product-card button").Click();
        cut.WaitForAssertion(() => added.Should().NotBeNull());
        added!.ProductId.Should().Be(product.ProductId);
        added.Name.Should().Be("Phở chay");
        added.Price.Should().Be(80_000m);
        cut.Markup.Should().Contain("Đã thêm");
    }

    [Fact]
    public void Send_ClientError_ShowsFallbackMessage()
    {
        var client = new FakeAiChatClient { ChatResult = null }; // transport error → null
        var cut = Render(client);
        cut.WaitForAssertion(() => cut.Find("button.vanan-ai-bubble-btn").Click());
        cut.WaitForAssertion(() => cut.Find(".vanan-ai-panel"));

        cut.Find(".vanan-ai-panel input").Input("test");
        cut.Find("button.vanan-ai-send").Click();

        cut.WaitForAssertion(() =>
            cut.Markup.Should().Contain("Trợ lý AI tạm nghỉ"));
    }
}
