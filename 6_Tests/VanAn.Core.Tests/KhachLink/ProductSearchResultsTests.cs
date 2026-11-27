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
/// Personalized DidYouMean W1 — ProductSearchResults wiring (SRS v1.1):
/// Level 0 client resolve rewrite query trước khi gọi API + note "đã học" + undo;
/// accept chip ghi correction vào localStorage kèm source voice/typed.
/// </summary>
public class ProductSearchResultsTests : TestContext
{
    private const string CorrectionsKey = "vanan_search_corrections";
    private static readonly Guid ProductId = Guid.NewGuid();
    private static readonly Guid TenantId = Guid.NewGuid();

    private sealed class StubTenantService : ITenantService
    {
        public Guid GetCurrentTenantId() => Guid.NewGuid();
        public void SetCurrentTenant(Guid tenantId) { }
        public TenantConfig GetTenantConfig() => new();
    }

    private static string CatalogJson(string decodedQuery) =>
        decodedQuery.Contains("tà sua", StringComparison.OrdinalIgnoreCase)
            ? $$"""{"products":[{"productId":"{{ProductId}}","tenantId":"{{TenantId}}","displayName":"Trà Sữa 79","displayPrice":25000,"vatRate":0.1,"productType":"Paid","source":"Featured","tenantName":"Quán A","aiChatEnabled":false,"voiceCallEnabled":false}],"totalCount":1,"page":1,"pageSize":20,"matchStrategy":"fuzzy","didYouMean":"Trà Sữa 79"}"""
            : $$"""{"products":[{"productId":"{{ProductId}}","tenantId":"{{TenantId}}","displayName":"Trà Sữa 79","displayPrice":25000,"vatRate":0.1,"productType":"Paid","source":"Featured","tenantName":"Quán A","aiChatEnabled":false,"voiceCallEnabled":false}],"totalCount":1,"page":1,"pageSize":20,"matchStrategy":"contains","didYouMean":null}""";

    private sealed class StubHandler : HttpMessageHandler
    {
        public List<string> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var uri = request.RequestUri!.ToString();
            Requests.Add(uri);
            var body = "{}";
            if (request.RequestUri.AbsolutePath.Contains("catalog/search"))
                body = CatalogJson(Uri.UnescapeDataString(request.RequestUri.Query));
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json")
            });
        }

        public string? LastCatalogQuery =>
            Requests.LastOrDefault(r => r.Contains("catalog/search", StringComparison.Ordinal));
    }

    private sealed class FakeHttpFactory(StubHandler handler) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name)
            => new(handler, disposeHandler: false) { BaseAddress = new Uri("https://gw.test/") };
    }

    public ProductSearchResultsTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose; // layout/page gọi JS linh tinh → default
    }

    private (IRenderedComponent<ProductSearchResults> Cut, StubHandler Http) RenderPage(
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
        JSInterop.Setup<string?>("localStorage.getItem", CorrectionsKey)
            .SetResult(correctionsJson);

        return (RenderComponent<ProductSearchResults>(), handler);
    }

    private static void TypeAndSearch(IRenderedComponent<ProductSearchResults> cut, string query)
    {
        cut.Find("#product-search-input").Input(query);
        cut.Find("#product-search-input").KeyUp("Enter");
    }

    private static string? CorrectionsSentToJs(TestContext ctx)
        => ctx.JSInterop.Invocations
            .Where(i => i.Identifier == "localStorage.setItem")
            .Select(i => i.Arguments.LastOrDefault()?.ToString())
            .LastOrDefault();

    [Fact]
    public void Search_PersonalHit_RewritesQuery_AndShowsLearnedNote()
    {
        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var json = "{\"v\":1,\"scopes\":{\"product\":{\"ta sua\":{\"c\":\"tra sua\",\"d\":\"Trà Sữa 79\",\"n\":1,\"t\":" + now + ",\"s\":\"voice\"}}}}";
        var (cut, http) = RenderPage(json);

        TypeAndSearch(cut, "tà sua");

        cut.WaitForAssertion(() =>
        {
            var last = http.LastCatalogQuery;
            last.Should().NotBeNull();
            Uri.UnescapeDataString(last!).Should().Contain("Trà Sữa 79",
                "Level 0 resolve rewrites 'tà sua' → 'Trà Sữa 79' before calling API");
        });
        cut.WaitForAssertion(() =>
        {
            var note = cut.Find(".search-learned-note");
            note.TextContent.Should().Contain("tà sua");
            note.TextContent.Should().Contain("Trà Sữa 79");
        });
    }

    [Fact]
    public void Search_NoCorrection_CallsApiWithRawQuery()
    {
        var (cut, http) = RenderPage(); // store rỗng

        TypeAndSearch(cut, "tà sua");

        cut.WaitForAssertion(() =>
        {
            Uri.UnescapeDataString(http.LastCatalogQuery!).Should().Contain("tà sua");
        });
        cut.FindAll(".search-learned-note").Should().BeEmpty("không học gì → không note");
    }

    [Fact]
    public void ApplyDidYouMean_RecordsCorrection_AndResearches()
    {
        var (cut, http) = RenderPage();

        TypeAndSearch(cut, "tà sua");
        cut.WaitForAssertion(() =>
            cut.FindAll("button").Should().Contain(b => b.TextContent.Contains("Trà Sữa 79"),
                "DidYouMean chip 'Ý bạn là: Trà Sữa 79?'"));

        cut.FindAll("button").First(b => b.TextContent.Contains("Trà Sữa 79")).Click();

        cut.WaitForAssertion(() =>
        {
            var sent = CorrectionsSentToJs(this);
            sent.Should().NotBeNull("accept chip → record correction vào localStorage");
            sent.Should().Contain("ta sua").And.Contain("tra sua");
            sent.Should().Contain("\"s\":\"typed\"");
        });
        cut.WaitForAssertion(() =>
            Uri.UnescapeDataString(http.LastCatalogQuery!).Should().Contain("Trà Sữa 79",
                "sau accept → re-search với câu đã chọn"));
    }

    [Fact]
    public async Task VoiceFinal_Accept_RecordsSourceVoice()
    {
        var (cut, _) = RenderPage();

        await cut.InvokeAsync(() => cut.Instance.OnVoiceFinal("tà sua"));
        cut.WaitForAssertion(() =>
            cut.FindAll("button").Should().Contain(b => b.TextContent.Contains("Trà Sữa 79")));

        cut.FindAll("button").First(b => b.TextContent.Contains("Trà Sữa 79")).Click();

        cut.WaitForAssertion(() =>
            CorrectionsSentToJs(this).Should().Contain("\"s\":\"voice\"",
                "query đến từ OnVoiceFinal → source=voice"));
    }

    [Fact]
    public void UndoResolved_ForgetsCorrection_AndResearchesRaw()
    {
        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var json = "{\"v\":1,\"scopes\":{\"product\":{\"ta sua\":{\"c\":\"tra sua\",\"d\":\"Trà Sữa 79\",\"n\":1,\"t\":" + now + ",\"s\":\"voice\"}}}}";
        var (cut, http) = RenderPage(json);

        TypeAndSearch(cut, "tà sua");
        cut.WaitForAssertion(() => cut.Find(".search-learned-note").Should().NotBeNull());

        cut.Find(".search-learned-undo").Click();

        cut.WaitForAssertion(() =>
        {
            var sent = CorrectionsSentToJs(this);
            sent.Should().NotBeNull("undo → persist corrected map");
            sent.Should().NotContain("ta sua", "entry đã học bị xóa khỏi store");
        });
        cut.WaitForAssertion(() =>
        {
            Uri.UnescapeDataString(http.LastCatalogQuery!).Should().Contain("tà sua",
                "undo → re-search câu gốc");
            cut.FindAll(".search-learned-note").Should().BeEmpty();
        });
    }
}
