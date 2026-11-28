using System.Net;
using System.Text;
using Bunit;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;
using VanAn.KhachLink.Pages;
using VanAn.KhachLink.Services;
using VanAn.KhachLink.Services.Http;
using VanAn.KhachLink.Services.PWA;
using VanAn.UI.Platform.Adapters;
using VanAn.UI.Platform.Core.Interfaces;
using VanAn.UI.Platform.Services;
using Xunit;

namespace VanAn.Core.Tests.KhachLink;

/// <summary>
/// Personalized DidYouMean W2 — StoreFinder (KhachLink, tenant scope):
/// load hardening (debounce giữ nguyên + CT abort in-flight + min-length ≥2 +
/// client cache TTL 60s) + learned resolve/record/note/undo.
/// </summary>
public class StoreFinderTests : TestContext
{
    private const string CorrectionsKey = "vanan_search_corrections";
    private static readonly Guid StoreId = Guid.NewGuid();

    private sealed class StubTenantService : ITenantService
    {
        public Guid GetCurrentTenantId() => Guid.NewGuid();
        public void SetCurrentTenant(Guid tenantId) { }
        public TenantConfig GetTenantConfig() => new();
    }

    private static string TenantsJson(string? decodedQuery)
    {
        var dym = decodedQuery is not null
            && decodedQuery.Contains("cafe tan qui", StringComparison.OrdinalIgnoreCase)
            ? "\"Cafe Tân Quy\""
            : "null";
        return $$"""{"results":[{"id":"{{StoreId}}","name":"Cafe Tân Quy","address":"120 Tỉnh lộ 8","phone":"0984496730","latitude":10.9,"longitude":106.5,"distanceKm":null,"slug":"cafe-tan-quy","khachLinkDomain":null,"aiChatEnabled":true,"voiceCallEnabled":true}],"suggestedKeywords":[],"matchStrategy":"contains","didYouMean":{{dym}}}""";
    }

    private sealed class StubHandler : HttpMessageHandler
    {
        public List<(string Uri, CancellationToken Ct)> Requests { get; } = [];
        public int ResponseDelayMs { get; set; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var uri = request.RequestUri!.ToString();
            Requests.Add((uri, cancellationToken));
            var body = "{}";
            if (request.RequestUri.AbsolutePath.EndsWith("/api/tenants/search", StringComparison.Ordinal))
            {
                var q = Uri.UnescapeDataString(request.RequestUri.Query);
                body = TenantsJson(q.Contains("name=") ? q : null);
            }
            if (ResponseDelayMs > 0)
                await Task.Delay(ResponseDelayMs, cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json")
            };
        }

        public List<string> NameSearches =>
            Requests.Where(r => r.Uri.Contains("tenants/search?name=", StringComparison.Ordinal))
                .Select(r => r.Uri).ToList();
    }

    private sealed class FakeHttpFactory(StubHandler handler) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name)
            => new(handler, disposeHandler: false) { BaseAddress = new Uri("https://gw.test/") };
    }

    public StoreFinderTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    private (IRenderedComponent<StoreFinder> Cut, StubHandler Http) RenderPage(
        string? correctionsJson = null)
    {
        Services.AddSingleton<ICssAdapter, BootstrapAdapter>();
        Services.AddLogging();
        Services.AddSingleton<ITenantService, StubTenantService>();
        Services.AddSingleton<IThemeProvider, ThemeProvider>();
        var handler = new StubHandler();
        Services.AddSingleton<IHttpClientFactory>(new FakeHttpFactory(handler));
        Services.AddScoped<CatalogHttpService>();
        Services.AddScoped<ShopFeatureSettingsHttpService>();
        Services.AddScoped<ShopConfigHttpService>();
        Services.AddScoped<KhachLinkInstanceHttpService>();
        Services.AddScoped<CommunityHttpService>();
        Services.AddScoped<CartService>();
        Services.AddScoped<PWAService>();
        Services.AddScoped<SearchCorrectionStore>();
        Services.AddScoped<SearchResultCache>();
        JSInterop.Setup<string?>("localStorage.getItem", CorrectionsKey)
            .SetResult(correctionsJson);

        return (RenderComponent<StoreFinder>(), handler);
    }

    private static string? CorrectionsSentToJs(TestContext ctx)
        => ctx.JSInterop.Invocations
            .Where(i => i.Identifier == "localStorage.setItem")
            .Select(i => i.Arguments.LastOrDefault()?.ToString())
            .LastOrDefault();

    private static Task FireValueChanged(StoreFinder page, string value)
        => page.OnSearchValueChanged(value);

    [Fact]
    public async Task Typing_RapidKeys_CoalescesToSingleRequest()
    {
        var (cut, http) = RenderPage();

        // Gõ nhanh "ca" rồi "caf" — không await lần đầu (handler chạy debounce 300ms).
        var t1 = cut.InvokeAsync(() => FireValueChanged(cut.Instance, "ca"));
        var t2 = cut.InvokeAsync(() => FireValueChanged(cut.Instance, "caf"));
        await Task.WhenAll(t1, t2).ContinueWith(_ => { }); // nuốt cancel của debounce cũ

        cut.WaitForAssertion(() =>
            http.NameSearches.Count.Should().Be(1, "2 keystroke gộp về 1 request (debounce 300ms)"),
            timeout: TimeSpan.FromSeconds(5));
        Uri.UnescapeDataString(http.NameSearches[0]).Should().Contain("caf");
    }

    [Fact]
    public async Task Typing_SingleChar_NoRequest()
    {
        var (cut, http) = RenderPage();

        await cut.InvokeAsync(() => FireValueChanged(cut.Instance, "c"));

        await Task.Delay(600); // qua debounce window
        http.NameSearches.Count.Should().Be(0, "1 ký tự → min-length guard, không call");
    }

    [Fact]
    public async Task Typing_NextKey_AbortsInflightRequest()
    {
        var (cut, http) = RenderPage();
        http.ResponseDelayMs = 1500;

        var t1 = cut.InvokeAsync(() => FireValueChanged(cut.Instance, "ca"));
        await Task.Delay(600); // qua debounce → request 1 đang in-flight (handler delay 1.5s)
        var t2 = cut.InvokeAsync(() => FireValueChanged(cut.Instance, "caf"));
        await Task.WhenAll(t1, t2).ContinueWith(_ => { });

        cut.WaitForAssertion(() =>
        {
            var search = http.Requests.Where(r => r.Uri.Contains("name=", StringComparison.Ordinal)).ToList();
            search.Should().HaveCountGreaterThanOrEqualTo(1);
            search[0].Ct.IsCancellationRequested.Should().BeTrue(
                "request cũ phải bị abort khi keystroke mới đến (A1)");
        }, timeout: TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task SameQuery_Twice_SecondServedFromClientCache()
    {
        var (cut, http) = RenderPage();

        await cut.InvokeAsync(() => FireValueChanged(cut.Instance, "ca"));
        cut.WaitForAssertion(() => http.NameSearches.Count.Should().Be(1),
            timeout: TimeSpan.FromSeconds(5));

        // Search lại đúng query — cache TTL 60s phải phục vụ, không HTTP mới.
        await cut.InvokeAsync(() => FireValueChanged(cut.Instance, ""));
        await cut.InvokeAsync(() => FireValueChanged(cut.Instance, "ca"));

        cut.WaitForAssertion(() =>
            http.NameSearches.Count.Should().Be(1, "query trùng → client cache hit (A3)"),
            timeout: TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task LearnedEntry_RewritesNameParam_AndShowsNote()
    {
        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var json = "{\"v\":1,\"scopes\":{\"tenant\":{\"cafe tan qui\":{\"c\":\"cafe tan quy\",\"d\":\"Cafe Tân Quy\",\"n\":1,\"t\":" + now + ",\"s\":\"typed\"}}}}";
        var (cut, http) = RenderPage(json);

        await cut.InvokeAsync(() => FireValueChanged(cut.Instance, "cafe tan qui"));

        cut.WaitForAssertion(() =>
        {
            var last = http.NameSearches.LastOrDefault();
            last.Should().NotBeNull();
            Uri.UnescapeDataString(last!).Should().Contain("Cafe Tân Quy",
                "Level 0 resolve tenant scope rewrites trước khi gọi API");
        }, timeout: TimeSpan.FromSeconds(5));
        cut.WaitForAssertion(() =>
        {
            var note = cut.Find(".search-learned-note");
            note.TextContent.Should().Contain("cafe tan qui").And.Contain("Cafe Tân Quy");
        });
    }

    [Fact]
    public async Task ApplyDidYouMean_RecordsTenantScope_AndResearches()
    {
        var (cut, http) = RenderPage();

        await cut.InvokeAsync(() => FireValueChanged(cut.Instance, "cafe tan qui"));
        cut.WaitForAssertion(() =>
            cut.FindAll("button").Should().Contain(b => b.TextContent.Contains("Cafe Tân Quy"),
                "DidYouMean chip"), timeout: TimeSpan.FromSeconds(5));

        cut.FindAll("button").First(b => b.TextContent.Contains("Cafe Tân Quy")).Click();

        cut.WaitForAssertion(() =>
        {
            var sent = CorrectionsSentToJs(this);
            sent.Should().NotBeNull("accept chip → record tenant-scope correction");
            sent.Should().Contain("\"tenant\"").And.Contain("cafe tan qui");
        });
        cut.WaitForAssertion(() =>
            Uri.UnescapeDataString(http.NameSearches.Last()).Should().Contain("Cafe Tân Quy"),
            timeout: TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task UndoResolved_ForgetsEntry_AndResearchesRaw()
    {
        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var json = "{\"v\":1,\"scopes\":{\"tenant\":{\"cafe tan qui\":{\"c\":\"cafe tan quy\",\"d\":\"Cafe Tân Quy\",\"n\":1,\"t\":" + now + ",\"s\":\"typed\"}}}}";
        var (cut, http) = RenderPage(json);

        await cut.InvokeAsync(() => FireValueChanged(cut.Instance, "cafe tan qui"));
        cut.WaitForAssertion(() => cut.Find(".search-learned-note").Should().NotBeNull(),
            timeout: TimeSpan.FromSeconds(5));

        cut.Find(".search-learned-undo").Click();

        cut.WaitForAssertion(() =>
        {
            var sent = CorrectionsSentToJs(this);
            sent.Should().NotContain("cafe tan qui", "undo → entry xóa khỏi store");
            Uri.UnescapeDataString(http.NameSearches.Last()).Should().Contain("cafe tan qui",
                "undo → re-search câu gốc");
        }, timeout: TimeSpan.FromSeconds(5));
    }

    // ── Voice input hardening V1a: migrate voice-note.js → vananVoiceInput ────

    [Fact]
    public async Task VoiceToggle_StartsVananVoiceInput_NotVoiceNoteJs()
    {
        var (cut, _) = RenderPage();
        JSInterop.Setup<bool>("vananVoiceInput.start", _ => true).SetResult(true);

        await cut.InvokeAsync(() => cut.Instance.ToggleVoiceSearch());

        JSInterop.Invocations.Should().Contain(i => i.Identifier == "vananVoiceInput.start",
            "V1a: mic phải dùng vananVoiceInput (contract OnVoiceInterim/Final/Error/End)");
        JSInterop.Invocations.Should().NotContain(i =>
            i.Identifier == "startRecording" || i.Identifier == "initializeSpeechRecognition",
            "không còn dùng voice-note.js — contract cũ gây isRecording kẹt (A1)");
    }

    [Fact]
    public async Task VoiceFinal_SearchesTranscript_AndResetsListening()
    {
        var (cut, http) = RenderPage();
        JSInterop.Setup<bool>("vananVoiceInput.start", _ => true).SetResult(true);
        await cut.InvokeAsync(() => cut.Instance.ToggleVoiceSearch());

        await cut.InvokeAsync(() => cut.Instance.OnVoiceFinal("cafe"));

        cut.WaitForAssertion(() =>
            http.NameSearches.Should().Contain(u =>
                Uri.UnescapeDataString(u).Contains("name=cafe"),
                "OnVoiceFinal → search transcript ngay (không qua debounce)"),
            timeout: TimeSpan.FromSeconds(5));
        // _listening reset → mic không còn trạng thái "listening" trên markup
        cut.WaitForAssertion(() =>
            cut.FindAll("button.listening").Should().BeEmpty("OnVoiceFinal reset _listening"));
    }

    [Fact]
    public async Task VoiceInterim_FillsInput_WithoutTriggeringSearch()
    {
        var (cut, http) = RenderPage();

        await cut.InvokeAsync(() => cut.Instance.OnVoiceInterim("ca"));
        await Task.Delay(500); // qua debounce window — interim KHÔNG được trigger search

        http.NameSearches.Count.Should().Be(0,
            "interim chỉ fill input — search chỉ chạy trên OnVoiceFinal (tránh request mỗi interim)");
        cut.Find("#search-input").GetAttribute("value").Should().Be("ca");
    }

    [Fact]
    public async Task VoiceError_ResetsListening_AndShowsMessage()
    {
        var (cut, _) = RenderPage();
        JSInterop.Setup<bool>("vananVoiceInput.start", _ => true).SetResult(true);
        await cut.InvokeAsync(() => cut.Instance.ToggleVoiceSearch());

        await cut.InvokeAsync(() => cut.Instance.OnVoiceError("Quyền micro bị từ chối"));

        cut.WaitForAssertion(() =>
        {
            cut.FindAll("button.listening").Should().BeEmpty("error → _listening=false");
            cut.Markup.Should().Contain("Quyền micro bị từ chối", "error message hiển thị");
        });
    }

    [Fact]
    public async Task VoiceEnd_ResetsListening()
    {
        var (cut, _) = RenderPage();
        JSInterop.Setup<bool>("vananVoiceInput.start", _ => true).SetResult(true);
        await cut.InvokeAsync(() => cut.Instance.ToggleVoiceSearch());

        await cut.InvokeAsync(() => cut.Instance.OnVoiceEnd());

        cut.WaitForAssertion(() =>
            cut.FindAll("button.listening").Should().BeEmpty("OnVoiceEnd → _listening=false"));
    }
}
