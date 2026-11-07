using Bunit;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using VanAn.KhachLink.Components;
using VanAn.UI.Platform.Components.Ai;
using Xunit;

namespace VanAn.Core.Tests.KhachLink;

/// <summary>
/// SF-W2 (A2): semantic search bar — render, search → answer + product cards,
/// add-to-cart + "Hỏi AI thêm" callbacks, fail-open hides the result block (NFR-3).
/// </summary>
public class StoreSearchBarTests : TestContext
{
    private static readonly Guid TenantId = Guid.NewGuid();

    private sealed class FakeSearchClient : IAiChatClient
    {
        public AiChatResult? Result { get; set; }
        public string? LastQuery { get; private set; }
        public int SearchCalls { get; private set; }

        public Task<AiWidgetConfig?> GetWidgetConfigAsync(Guid tenantId, CancellationToken ct = default)
            => Task.FromResult<AiWidgetConfig?>(null);

        public Task<IReadOnlyList<AiSessionMessage>?> GetSessionMessagesAsync(Guid tenantId,
            string sessionId, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<AiSessionMessage>?>([]);

        public Task<AiChatResult?> SendChatAsync(Guid tenantId, string sessionId, string messageId,
            string text, string? locale = null, CancellationToken ct = default)
            => Task.FromResult<AiChatResult?>(null);

        public Task<AiChatResult?> SearchAiAsync(Guid tenantId, string query, CancellationToken ct = default)
        {
            SearchCalls++;
            LastQuery = query;
            return Task.FromResult(Result);
        }
    }

    private IRenderedComponent<StoreSearchBar> Render(
        FakeSearchClient client,
        Action<string>? onAskAi = null,
        Action<AiProductCardItem>? onAddToCart = null,
        bool voiceEnabled = false)
    {
        Services.AddSingleton<IAiChatClient>(client);
        return RenderComponent<StoreSearchBar>(p => p
            .Add(c => c.TenantId, TenantId)
            .Add(c => c.OnAskAi, onAskAi ?? (_ => { }))
            .Add(c => c.OnAddToCart, onAddToCart ?? (_ => { }))
            .Add(c => c.VoiceEnabled, voiceEnabled));
    }

    private static void DoSearch(IRenderedComponent<StoreSearchBar> cut, string query)
    {
        cut.Find("input.store-search-input").Input(query);
        cut.Find("button.store-search-btn").Click();
    }

    [Fact]
    public void SearchBar_RendersInputAndButton()
    {
        var cut = Render(new FakeSearchClient());

        cut.Find("input.store-search-input").Should().NotBeNull();
        cut.Find("button.store-search-btn").Should().NotBeNull();
        cut.FindAll(".store-search-result").Should().BeEmpty("nothing searched yet");
    }

    [Fact]
    public void Search_ShowsAnswerAndProductCards()
    {
        var client = new FakeSearchClient
        {
            Result = new AiChatResult("Quán có phở chay 80k ạ.",
                [new AiProductCardItem(Guid.NewGuid(), "Phở chay", 80_000m, null)],
                [], 0.9, false, null, false, false)
        };
        var cut = Render(client);

        DoSearch(cut, "phở chay");

        cut.WaitForAssertion(() =>
        {
            cut.Find(".store-search-answer").TextContent.Should().Contain("phở chay 80k");
            cut.FindAll(".store-search-product").Should().HaveCount(1);
            cut.Find(".store-search-product-name").TextContent.Should().Be("Phở chay");
        });
        client.LastQuery.Should().Be("phở chay");
        client.SearchCalls.Should().Be(1);
    }

    [Fact]
    public void Search_NullResult_HidesResultBlock()
    {
        var client = new FakeSearchClient { Result = null };
        var cut = Render(client);

        DoSearch(cut, "abc");
        cut.WaitForAssertion(() => client.SearchCalls.Should().Be(1));

        cut.FindAll(".store-search-result").Should().BeEmpty("fail-open: upstream error → hidden (NFR-3)");
    }

    [Fact]
    public void Search_EmptyInput_DoesNotCall()
    {
        var client = new FakeSearchClient();
        var cut = Render(client);

        cut.Find("button.store-search-btn").Click();

        client.SearchCalls.Should().Be(0);
    }

    [Fact]
    public void ProductAdd_InvokesOnAddToCart()
    {
        var item = new AiProductCardItem(Guid.NewGuid(), "Phở chay", 80_000m, null);
        var client = new FakeSearchClient
        {
            Result = new AiChatResult("Có ạ.", [item], [], 0.9, false, null, false, false)
        };
        AiProductCardItem? added = null;
        var cut = Render(client, onAddToCart: p => added = p);

        DoSearch(cut, "phở chay");
        cut.WaitForAssertion(() => cut.FindAll(".store-search-add").Should().HaveCount(1));

        cut.Find(".store-search-add").Click();
        added.Should().Be(item);
    }

    [Fact]
    public void AskAi_InvokesWithLastQuery()
    {
        var client = new FakeSearchClient
        {
            Result = new AiChatResult("Trả lời mẫu.", [], [], 0.9, false, null, false, false)
        };
        string? asked = null;
        var cut = Render(client, onAskAi: q => asked = q);

        DoSearch(cut, "phở chay");
        cut.WaitForAssertion(() => cut.FindAll(".store-search-ask").Should().HaveCount(1));

        cut.Find(".store-search-ask").Click();
        asked.Should().Be("phở chay");
    }

    [Fact]
    public void Search_EmptyAnswerAndNoProducts_ShowsFallbackHint()
    {
        var client = new FakeSearchClient
        {
            Result = new AiChatResult("", [], [], 0.1, false, null, false, false)
        };
        var cut = Render(client);

        DoSearch(cut, "xyz");

        cut.WaitForAssertion(() =>
            cut.Find(".store-search-empty").TextContent.Should().Contain("Chưa tìm thấy"));
    }

    // ── SF-W3: voice input ────────────────────────────────────────────────

    [Fact]
    public void Mic_Hidden_WhenVoiceDisabled()
    {
        var cut = Render(new FakeSearchClient(), voiceEnabled: false);

        cut.FindAll(".store-search-mic").Should().BeEmpty(
            "tenant flag off → mic hidden regardless of browser support");
    }

    [Fact]
    public void Mic_Hidden_WhenVoiceEnabled_ButBrowserUnsupported()
    {
        // Strict JSInterop (default) — no isSupported setup → InvokeAsync throws →
        // component catches → mic stays hidden (graceful degradation).
        var cut = Render(new FakeSearchClient(), voiceEnabled: true);

        cut.FindAll(".store-search-mic").Should().BeEmpty();
    }

    [Fact]
    public void Mic_Renders_WhenVoiceEnabledAndSupported()
    {
        JSInterop.Setup<bool>("vananVoiceInput.isSupported").SetResult(true);
        var cut = Render(new FakeSearchClient(), voiceEnabled: true);

        cut.WaitForAssertion(() =>
            cut.Find(".store-search-mic").Should().NotBeNull());
    }

    [Fact]
    public async Task VoiceInterim_FillsInput_WithoutSubmitting()
    {
        var client = new FakeSearchClient();
        var cut = Render(client, voiceEnabled: true);

        await cut.InvokeAsync(() => cut.Instance.OnVoiceInterim("phở"));

        cut.Find("input.store-search-input").GetAttribute("value").Should().Be("phở");
        client.SearchCalls.Should().Be(0, "interim text must not submit");
    }

    [Fact]
    public async Task VoiceFinal_AutoSubmitsSearch()
    {
        var client = new FakeSearchClient
        {
            Result = new AiChatResult("Có ạ.", [], [], 0.9, false, null, false, false)
        };
        var cut = Render(client, voiceEnabled: true);

        await cut.InvokeAsync(() => cut.Instance.OnVoiceFinal("phở chay"));

        client.SearchCalls.Should().Be(1);
        client.LastQuery.Should().Be("phở chay");
    }
}
