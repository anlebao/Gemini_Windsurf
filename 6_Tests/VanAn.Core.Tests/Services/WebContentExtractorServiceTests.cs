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
}
