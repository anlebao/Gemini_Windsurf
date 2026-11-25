using VanAn.ShopERP.Services;
using Xunit;

namespace VanAn.Core.Tests.Services;

/// <summary>
/// W3c — WebContentExtractorService: SSRF guard + HTML→text.
/// Fetch path covered indirectly (HttpClientFactory); guard/strip là pure.
/// </summary>
public class WebContentExtractorServiceTests
{
    // ---------- scheme / shape ----------

    [Theory]
    [InlineData("ftp://example.com")]
    [InlineData("file:///etc/passwd")]
    [InlineData("javascript:alert(1)")]
    [InlineData("not a url")]
    [InlineData("")]
    public void Validate_RejectsNonHttp(string url)
    {
        Assert.False(WebContentExtractorService.TryValidateAndResolve(url, out _, out _));
    }

    [Fact]
    public void Validate_RejectsPrivateIpLiteral()
    {
        Assert.False(WebContentExtractorService.TryValidateAndResolve("http://192.168.1.1/admin", out _, out _));
        Assert.False(WebContentExtractorService.TryValidateAndResolve("http://127.0.0.1:8080", out _, out _));
        Assert.False(WebContentExtractorService.TryValidateAndResolve("http://169.254.169.254/latest/meta-data", out _, out _));
        Assert.False(WebContentExtractorService.TryValidateAndResolve("http://10.0.0.5", out _, out _));
        Assert.False(WebContentExtractorService.TryValidateAndResolve("http://[::1]", out _, out _));
        Assert.False(WebContentExtractorService.TryValidateAndResolve("http://[fd00::1]", out _, out _));
    }

    [Fact]
    public void Validate_RejectsLocalhostHostname()
    {
        // localhost resolves to loopback → SSRF guard must catch DNS-level too.
        Assert.False(WebContentExtractorService.TryValidateAndResolve("http://localhost:5001/api", out _, out _));
    }

    [Fact]
    public void Validate_AcceptsPublicIpLiteral()
    {
        Assert.True(WebContentExtractorService.TryValidateAndResolve("https://1.1.1.1/", out var uri, out _));
        Assert.Equal("1.1.1.1", uri!.Host);
    }

    // ---------- IsPrivateOrReserved ----------

    [Theory]
    [InlineData("0.0.0.0")]
    [InlineData("10.1.2.3")]
    [InlineData("100.64.0.1")]   // CGNAT
    [InlineData("127.0.0.1")]
    [InlineData("169.254.1.1")]
    [InlineData("172.16.0.1")]
    [InlineData("172.31.255.255")]
    [InlineData("192.168.0.1")]
    [InlineData("224.0.0.1")]
    [InlineData("255.255.255.255")]
    public void IsPrivateOrReserved_Blocks(string ip)
        => Assert.True(WebContentExtractorService.IsPrivateOrReserved(System.Net.IPAddress.Parse(ip)));

    [Theory]
    [InlineData("1.1.1.1")]
    [InlineData("8.8.8.8")]
    [InlineData("172.15.0.1")]   // just outside 172.16/12
    [InlineData("172.32.0.1")]
    [InlineData("11.0.0.1")]
    public void IsPrivateOrReserved_Allows(string ip)
        => Assert.False(WebContentExtractorService.IsPrivateOrReserved(System.Net.IPAddress.Parse(ip)));

    // ---------- ExtractLinks (deep crawl) ----------

    [Fact]
    public void ExtractLinks_KeepsSameOrigin_SkipsExternalAndAssets()
    {
        var html = """
            <a href="/menu">Menu</a>
            <a href="https://other.com/page">External</a>
            <a href="/img/logo.png">Img</a>
            <a href="/contact">Lien he</a>
            <a href="mailto:shop@x.com">Mail</a>
            <a href="#section">Anchor</a>
            <a href="/menu">Menu dup</a>
            """;
        var links = WebContentExtractorService.ExtractLinks(html, new Uri("https://shop.com/"));
        var strs = links.Select(l => l.ToString()).ToList();
        Assert.Contains("https://shop.com/menu", strs);
        Assert.Contains("https://shop.com/contact", strs);
        Assert.DoesNotContain(strs, s => s.Contains("other.com"));
        Assert.DoesNotContain(strs, s => s.EndsWith(".png"));
        Assert.DoesNotContain(strs, s => s.StartsWith("mailto"));
        Assert.Single(strs, s => s.EndsWith("/menu")); // dedupe
    }

    [Fact]
    public void ExtractLinks_PrioritizesKeywordPages()
    {
        var html = """
            <a href="/xyz-random-page">Random</a>
            <a href="/bang-gia">Bang gia</a>
            <a href="/gioi-thieu">Gioi thieu</a>
            """;
        var links = WebContentExtractorService.ExtractLinks(html, new Uri("https://shop.com/"));
        Assert.Equal("https://shop.com/bang-gia", links[0].ToString());
        Assert.Equal("https://shop.com/gioi-thieu", links[1].ToString());
    }

    [Fact]
    public void ExtractLinks_ResolvesRelativeUrls()
    {
        var html = "<a href='dich-vu/nail'> dich vu </a>";
        var links = WebContentExtractorService.ExtractLinks(html, new Uri("https://shop.com/"));
        Assert.Equal("https://shop.com/dich-vu/nail", links[0].ToString());
    }

    // ---------- HtmlToText ----------

    [Fact]
    public void HtmlToText_StripsScriptStyleTags()
    {
        var html = "<html><head><style>body{}</style></head><body><script>alert(1)</script>" +
                   "<h1>Menu Quán</h1><p>Phở bò 65.000đ</p><script>track()</script></body></html>";
        var text = WebContentExtractorService.HtmlToText(html);
        Assert.Contains("Menu Quán", text);
        Assert.Contains("Phở bò 65.000đ", text);
        Assert.DoesNotContain("alert", text);
        Assert.DoesNotContain("track", text);
        Assert.DoesNotContain("body{}", text);
    }

    [Fact]
    public void HtmlToText_DecodesEntities_AndCollapsesWhitespace()
    {
        var html = "<p>Gi&aacute;  &amp;  khuyến&nbsp;mãi</p>\n\n   <div>  Cơm   tấm  </div>";
        var text = WebContentExtractorService.HtmlToText(html);
        Assert.Contains("Giá", text);
        Assert.Contains("&", text.Replace("&amp;", "&"));
        Assert.DoesNotContain("  ", text);
    }

    [Fact]
    public void HtmlToText_StripsNavFooterBoilerplate()
    {
        var html = "<nav>Home About Contact</nav><main><p>Nội dung chính của quán</p></main>" +
                   "<footer>© 2026 All rights reserved</footer>";
        var text = WebContentExtractorService.HtmlToText(html);
        Assert.Contains("Nội dung chính", text);
        Assert.DoesNotContain("All rights reserved", text);
    }

    // ── Catalog-Image markers (2026-11-25) ────────────────────────────────

    [Fact]
    public void HtmlToText_WithBaseUri_EmitsImgMarkers_Absolute()
    {
        var baseUri = new Uri("https://shop.com/menu");
        var html = "<div><h3>Trà sữa</h3><img src=\"/img/trasua.jpg\" /><p>25k</p></div>";
        var text = WebContentExtractorService.HtmlToText(html, baseUri);
        Assert.Contains("[ẢNH: https://shop.com/img/trasua.jpg]", text);
        Assert.Contains("Trà sữa", text);
    }

    [Fact]
    public void HtmlToText_ImgMarkers_SkipsDataUriIconsAndNonHttp()
    {
        var baseUri = new Uri("https://shop.com/");
        var html = "<div>" +
                   "<img src=\"data:image/png;base64,AAA\" />" +
                   "<img src=\"/assets/icon-cart.png\" />" +
                   "<img src=\"javascript:void(0)\" />" +
                   "<img src=\"/real/product.webp\" />" +
                   "</div>";
        var text = WebContentExtractorService.HtmlToText(html, baseUri);
        Assert.Contains("[ẢNH: https://shop.com/real/product.webp]", text);
        Assert.DoesNotContain("data:image", text);
        Assert.DoesNotContain("icon-cart", text);
        Assert.DoesNotContain("javascript:", text);
    }

    [Fact]
    public void ExtractJsonLdProducts_ProductWithOffer_EmitsLine()
    {
        var html = """
            <html><head>
            <script type="application/ld+json">
            {"@context":"https://schema.org","@type":"Product","name":"Aqua Peel",
             "offers":{"@type":"Offer","price":590000},
             "image":["https://shop.com/aqua.jpg"]}
            </script>
            </head><body>menu</body></html>
            """;
        var text = WebContentExtractorService.ExtractJsonLdProducts(html);
        Assert.Contains("SẢN PHẨM: Aqua Peel", text);
        Assert.Contains("GIÁ: 590000", text);
        Assert.Contains("ẢNH: https://shop.com/aqua.jpg", text);
    }

    [Fact]
    public void ExtractJsonLdProducts_NoJsonLd_ReturnsEmpty()
    {
        Assert.Equal("", WebContentExtractorService.ExtractJsonLdProducts("<html><body>plain</body></html>"));
    }

    [Fact]
    public void ExtractJsonLdProducts_GraphItemList_CollectsAll()
    {
        var html = """
            <script type="application/ld+json">
            {"@context":"https://schema.org","@graph":[
              {"@type":"Service","name":"Massage chân","price":150000,
               "image":{"url":"https://shop.com/massage.jpg"}},
              {"@type":"ItemList","itemListElement":[
                {"@type":"Product","name":"Kem dưỡng","image":"https://shop.com/kem.jpg"}
              ]}
            ]}
            </script>
            """;
        var text = WebContentExtractorService.ExtractJsonLdProducts(html);
        Assert.Contains("SẢN PHẨM: Massage chân", text);
        Assert.Contains("ẢNH: https://shop.com/massage.jpg", text);
        Assert.Contains("SẢN PHẨM: Kem dưỡng", text);
    }
}
