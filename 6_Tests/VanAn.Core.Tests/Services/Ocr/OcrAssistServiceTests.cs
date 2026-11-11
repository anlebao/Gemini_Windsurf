using System.Net;
using System.Net.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using VanAn.Core.Tests.Helpers;
using VanAn.CoreHub.Services.Ocr;
using Xunit;

namespace VanAn.Core.Tests.Services.Ocr;

/// <summary>
/// P6 (2026-11-11): OcrAssistService — provider switch, parse gemini structured JSON,
/// vision lines, no-key graceful, host allowlist chống SSRF, fallback gemini→vision.
/// </summary>
public class OcrAssistServiceTests
{
    private static readonly byte[] FakeImage = [0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10];

    private static OcrAssistService BuildService(HttpMessageHandler handler,
        string? provider = "gemini", string? geminiKey = "test-key",
        string? visionKey = "vis-key", string? allowedHost = "r2.example.com")
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["OcrAssist:Provider"] = provider,
            ["OcrAssist:GeminiApiKey"] = geminiKey,
            ["OcrAssist:VisionApiKey"] = visionKey,
            ["OcrAssist:AllowedUrlHost"] = allowedHost,
            ["OcrAssist:MaxImageBytes"] = "8388608"
        }).Build();
        return new OcrAssistService(new HttpClient(handler), config, NullLogger<OcrAssistService>.Instance);
    }

    private static OcrAssistRequest UrlRequest(OcrAssistKind kind = OcrAssistKind.Sign)
        => new(kind, ImageUrl: $"https://r2.example.com/onboarding/sign/{Guid.NewGuid():N}.jpg");

    /// <summary>HttpMessageHandler stateful — factory nhận request, trả response (đếm attempts).</summary>
    private sealed class SequenceHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> factory) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(factory(request));
    }

    // ── Config / no-key ────────────────────────────────────────────────

    [Fact(DisplayName = "ProviderNone_ReturnsProviderNone_WithWarning")]
    public async Task ProviderNone_ReturnsProviderNone()
    {
        var svc = BuildService(new MockHttpMessageHandler(), provider: "none");
        var r = await svc.AssistAsync(UrlRequest(), CancellationToken.None);
        Assert.Equal("none", r.Provider);
        Assert.False(string.IsNullOrWhiteSpace(r.Warning));
    }

    [Fact(DisplayName = "Gemini_NoKey_ReturnsProviderNone_503")]
    public async Task Gemini_NoKey_ReturnsProviderNone()
    {
        var svc = BuildService(new MockHttpMessageHandler(), geminiKey: "");
        var r = await svc.AssistAsync(UrlRequest(), CancellationToken.None);
        Assert.Equal("none", r.Provider);
    }

    [Fact(DisplayName = "ImageUrl_DisallowedHost_ReturnsProviderNone")]
    public async Task ImageUrl_DisallowedHost_ReturnsNone()
    {
        var svc = BuildService(new MockHttpMessageHandler(), allowedHost: "r2.example.com");
        var r = await svc.AssistAsync(new OcrAssistRequest(OcrAssistKind.Sign,
            ImageUrl: "https://evil.example.com/x.jpg"), CancellationToken.None);
        Assert.Equal("none", r.Provider);
    }

    // ── Gemini structured parse ────────────────────────────────────────

    [Fact(DisplayName = "Gemini_Sign_ParsesSignName_Address_Phone")]
    public async Task Gemini_Sign_ParsesStructured()
    {
        var handler = new MockHttpMessageHandler();
        handler.AddByteResponse("r2.example.com", HttpMethod.Get, FakeImage);
        handler.AddResponse("generateContent", HttpMethod.Post, new
        {
            candidates = new[]
            {
                new { content = new { parts = new[] { new { text = "{\"signName\": \"Phở Hòa\", \"address\": \"12 Nguyễn Trãi, Hà Nội\", \"phone\": \"0912345678\"}" } } } }
            }
        });
        var svc = BuildService(handler);

        var r = await svc.AssistAsync(UrlRequest(), CancellationToken.None);

        Assert.Equal("gemini", r.Provider);
        Assert.Equal("Phở Hòa", r.SignName);
        Assert.Equal("12 Nguyễn Trãi, Hà Nội", r.Address);
        Assert.Equal("0912345678", r.Phone);
    }

    [Fact(DisplayName = "Gemini_Gpdk_ParsesMst_Name_Address_LegalForm_Owner")]
    public async Task Gemini_Gpdk_ParsesStructured()
    {
        var handler = new MockHttpMessageHandler();
        handler.AddByteResponse("r2.example.com", HttpMethod.Get, FakeImage);
        handler.AddResponse("generateContent", HttpMethod.Post, new
        {
            candidates = new[]
            {
                new { content = new { parts = new[] { new { text = "{\"mst\": \"0123456789\", \"name\": \"Công ty TNHH Vạn An\", \"address\": \"1 Lê Lợi, Q1\", \"legalForm\": \"TNHH\", \"owner\": \"Nguyễn Văn A\"}" } } } }
            }
        });
        var svc = BuildService(handler);

        var r = await svc.AssistAsync(UrlRequest(OcrAssistKind.Gpdk), CancellationToken.None);

        Assert.Equal("0123456789", r.Mst);
        Assert.Equal("Công ty TNHH Vạn An", r.Name);
        Assert.Equal("TNHH", r.LegalForm);
        Assert.Equal("Nguyễn Văn A", r.Owner);
    }

    [Fact(DisplayName = "Gemini_Menu_ParsesItems_WithPriceNormalization")]
    public async Task Gemini_Menu_ParsesItems()
    {
        var handler = new MockHttpMessageHandler();
        handler.AddByteResponse("r2.example.com", HttpMethod.Get, FakeImage);
        handler.AddResponse("generateContent", HttpMethod.Post, new
        {
            candidates = new[]
            {
                new { content = new { parts = new[] { new { text = "{\"items\": [{\"name\": \"Phở bò\", \"price\": \"35.000đ\"}, {\"name\": \"Trà đá\", \"price\": \"5k\"}, {\"name\": \"Trứng ốp\", \"price\": 15000}]}" } } } }
            }
        });
        var svc = BuildService(handler);

        var r = await svc.AssistAsync(UrlRequest(OcrAssistKind.Menu), CancellationToken.None);

        Assert.NotNull(r.ProductLines);
        Assert.Equal(3, r.ProductLines.Count);
        Assert.Equal("Phở bò", r.ProductLines[0].Name);
        Assert.Equal(35000m, r.ProductLines[0].Price);
        Assert.Equal(5000m, r.ProductLines[1].Price);
        Assert.Equal(15000m, r.ProductLines[2].Price);
    }

    [Fact(DisplayName = "Gemini_StripsMarkdownFences")]
    public async Task Gemini_StripsMarkdownFences()
    {
        var handler = new MockHttpMessageHandler();
        handler.AddByteResponse("r2.example.com", HttpMethod.Get, FakeImage);
        handler.AddRawResponse("generateContent", HttpMethod.Post,
            System.Net.HttpStatusCode.OK,
            "{\"candidates\":[{\"content\":{\"parts\":[{\"text\":\"```json\\n{\\\"signName\\\": \\\"Quán Cô Ba\\\", \\\"phone\\\": \\\"0988888888\\\"}\\n```\"}]}}]}");
        var svc = BuildService(handler);

        var r = await svc.AssistAsync(UrlRequest(), CancellationToken.None);

        Assert.Equal("Quán Cô Ba", r.SignName);
        Assert.Equal("0988888888", r.Phone);
    }

    [Fact(DisplayName = "Gemini_Transient503_RetriesOnce_AndSucceeds")]
    public async Task Gemini_Transient503_RetriesOnce_AndSucceeds()
    {
        // Stateful handler: GET ảnh luôn 200 bytes; POST gemini lần 1 → 503, lần 2 → 200
        // (retry path — RV 2026-11-11 bắt Gemini 503 upstream).
        var attempts = 0;
        var handler = new SequenceHttpMessageHandler((req) =>
        {
            if (req.Method == HttpMethod.Get)
                return new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(FakeImage) };
            attempts++;
            if (attempts == 1)
                return new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)
                { Content = new StringContent("{}") };
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    "{\"candidates\":[{\"content\":{\"parts\":[{\"text\":\"{\\\"signName\\\": \\\"Quán Retry\\\"}\"}]}}]}")
            };
        });
        var svc = BuildService(handler);

        var r = await svc.AssistAsync(UrlRequest(), CancellationToken.None);

        Assert.Equal("gemini", r.Provider);
        Assert.Equal("Quán Retry", r.SignName);
        Assert.Equal(2, attempts);
    }

    // ── Vision ─────────────────────────────────────────────────────────

    [Fact(DisplayName = "Vision_ReturnsRawLines")]
    public async Task Vision_ReturnsRawLines()
    {
        var handler = new MockHttpMessageHandler();
        handler.AddByteResponse("r2.example.com", HttpMethod.Get, FakeImage);
        handler.AddResponse("images:annotate", HttpMethod.Post, new
        {
            responses = new[]
            {
                new { fullTextAnnotation = new { text = "Phở Hòa\n0912345678\nĐịa chỉ: 12 Nguyễn Trãi" } }
            }
        });
        var svc = BuildService(handler, provider: "vision");

        var r = await svc.AssistAsync(UrlRequest(), CancellationToken.None);

        Assert.Equal("vision", r.Provider);
        Assert.NotNull(r.Lines);
        Assert.Equal(3, r.Lines.Count);
        Assert.Equal("Phở Hòa", r.Lines[0].Text);
    }

    [Fact(DisplayName = "Vision_NoKey_ReturnsProviderNone")]
    public async Task Vision_NoKey_ReturnsNone()
    {
        var svc = BuildService(new MockHttpMessageHandler(), provider: "vision", visionKey: "");
        var r = await svc.AssistAsync(UrlRequest(), CancellationToken.None);
        Assert.Equal("none", r.Provider);
    }

    // ── Fallback + errors ──────────────────────────────────────────────

    [Fact(DisplayName = "Gemini_Failure_FallsBackToVision_WhenConfigured")]
    public async Task Gemini_Failure_FallsBackToVision()
    {
        var handler = new MockHttpMessageHandler();
        handler.AddByteResponse("r2.example.com", HttpMethod.Get, FakeImage);
        handler.AddRawResponse("generateContent", HttpMethod.Post,
            System.Net.HttpStatusCode.InternalServerError, "{}");
        handler.AddResponse("images:annotate", HttpMethod.Post, new
        {
            responses = new[] { new { fullTextAnnotation = new { text = "Phở Hòa\n0912345678" } } }
        });
        var svc = BuildService(handler);

        var r = await svc.AssistAsync(UrlRequest(), CancellationToken.None);

        Assert.Equal("vision", r.Provider);
        Assert.NotNull(r.Lines);
        Assert.False(string.IsNullOrWhiteSpace(r.Warning));
    }

    [Fact(DisplayName = "AllProvidersFail_ReturnsProviderNone_WithWarning")]
    public async Task AllProvidersFail_ReturnsNone()
    {
        var handler = new MockHttpMessageHandler();
        handler.AddByteResponse("r2.example.com", HttpMethod.Get, FakeImage);
        handler.AddRawResponse("generateContent", HttpMethod.Post,
            System.Net.HttpStatusCode.InternalServerError, "{}");
        handler.AddRawResponse("images:annotate", HttpMethod.Post,
            System.Net.HttpStatusCode.InternalServerError, "{}");
        var svc = BuildService(handler);

        var r = await svc.AssistAsync(UrlRequest(), CancellationToken.None);

        Assert.Equal("none", r.Provider);
        Assert.False(string.IsNullOrWhiteSpace(r.Warning));
    }

    // ── Inline base64 ──────────────────────────────────────────────────

    [Fact(DisplayName = "ImageBase64_Inline_Works_WithoutImageUrl")]
    public async Task ImageBase64_Inline_Works()
    {
        var handler = new MockHttpMessageHandler();
        handler.AddResponse("generateContent", HttpMethod.Post, new
        {
            candidates = new[] { new { content = new { parts = new[] { new { text = "{\"signName\": \"Quán Nhỏ\"}" } } } } }
        });
        var svc = BuildService(handler);

        var b64 = Convert.ToBase64String(FakeImage);
        var r = await svc.AssistAsync(new OcrAssistRequest(OcrAssistKind.Sign, ImageBase64: b64), CancellationToken.None);

        Assert.Equal("Quán Nhỏ", r.SignName);
    }
}