using System.Net;
using System.Text;
using System.Text.RegularExpressions;

namespace VanAn.ShopERP.Services;

/// <summary>
/// W3c — URL website → plain text cho knowledge extraction.
/// Fetch server-side (owner-auth context), SSRF-guarded: chỉ http/https,
/// resolve DNS và chặn private/loopback/link-local/reserved IP,
/// manual redirect (re-validate từng hop), cap bytes + timeout.
/// Trả text thô → UI đổ vào ô extract (KnowledgeExtractor xử lý tiếp).
/// </summary>
public interface IWebContentExtractor
{
    Task<WebExtractResult?> ExtractAsync(string url, CancellationToken ct = default);
}

public sealed record WebExtractResult(string Text, string? Warning);

public sealed partial class WebContentExtractorService : IWebContentExtractor
{
    private const int MaxBytes = 512 * 1024;
    private const int MaxRedirects = 3;
    private const int MaxTextChars = 20_000; // mirror extract endpoint limit
    private const int MaxPages = 6;          // start page + tối đa 5 link cùng site
    private const int MaxExtraPages = MaxPages - 1;
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan PageTimeout = TimeSpan.FromSeconds(8);

    private readonly IHttpClientFactory _httpFactory;
    private readonly ILogger<WebContentExtractorService> _logger;

    public WebContentExtractorService(IHttpClientFactory httpFactory, ILogger<WebContentExtractorService> logger)
    {
        _httpFactory = httpFactory;
        _logger = logger;
    }

    public async Task<WebExtractResult?> ExtractAsync(string url, CancellationToken ct = default)
    {
        if (!TryValidateAndResolve(url, out var uri, out var error))
        {
            _logger.LogDebug("WebExtract rejected url {Url}: {Error}", url, error);
            return new WebExtractResult(string.Empty, error);
        }

        try
        {
            var (body, contentType, finalUrl) = await FetchFollowingRedirectsAsync(uri!, ct);
            if (body is null) return null;

            var isHtml = contentType?.Contains("html", StringComparison.OrdinalIgnoreCase) == true;
            var pages = new List<string>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            if (isHtml)
            {
                pages.Add(HtmlToText(body));
                // Deep crawl: cùng origin, ưu tiên link keyword, tối đa MaxExtraPages.
                var links = ExtractLinks(body, finalUrl ?? uri!)
                    .Where(l => seen.Add(l.ToString()))
                    .Take(MaxExtraPages).ToList();
                foreach (var link in links)
                {
                    var (sub, subType, _) = await FetchFollowingRedirectsAsync(link, ct, PageTimeout);
                    if (sub is null) continue;
                    var subText = subType?.Contains("html", StringComparison.OrdinalIgnoreCase) == true
                        ? HtmlToText(sub) : sub;
                    if (!string.IsNullOrWhiteSpace(subText))
                        pages.Add($"--- {link} ---\n{subText}");
                }
            }
            else
            {
                pages.Add(body);
            }

            var text = string.Join("\n\n", pages.Where(p => !string.IsNullOrWhiteSpace(p)));
            if (string.IsNullOrWhiteSpace(text))
                return new WebExtractResult(string.Empty, "Không trích được nội dung text từ trang.");

            string? warning = null;
            if (pages.Count > 1)
                warning = $"Đã gộp nội dung {pages.Count} trang cùng website.";
            if (text.Length < 200)
                warning = "Trang có rất ít nội dung text — site có thể render bằng JavaScript, hãy copy text thủ công.";
            if (text.Length > MaxTextChars) text = text[..MaxTextChars];

            _logger.LogInformation("WebExtract ok {Url} → {Chars} chars ({Pages} pages, final {Final})",
                uri!.Host, text.Length, pages.Count, finalUrl?.Host);
            return new WebExtractResult(text, warning);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogDebug(ex, "WebExtract failed {Url}", uri!.Host);
            return null;
        }
    }

    // ---------- fetch ----------

    private async Task<(string? Body, string? ContentType, Uri? FinalUrl)> FetchFollowingRedirectsAsync(
        Uri start, CancellationToken ct, TimeSpan? timeout = null)
    {
        var limit = timeout ?? Timeout;
        var current = start;
        for (var hop = 0; hop <= MaxRedirects; hop++)
        {
            if (!TryValidateAndResolve(current.ToString(), out var checkedUri, out _))
                return (null, null, null);

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(limit);
            var client = _httpFactory.CreateClient();
            client.Timeout = limit;

            using var req = new HttpRequestMessage(HttpMethod.Get, checkedUri);
            req.Headers.UserAgent.ParseAdd("VanAnKnowledgeBot/1.0 (+https://vanan.vn)");
            req.Headers.Accept.ParseAdd("text/html,text/plain;q=0.9,*/*;q=0.1");

            using var resp = await client.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, cts.Token);

            if (resp.StatusCode is >= HttpStatusCode.MultipleChoices and < HttpStatusCode.BadRequest
                && resp.Headers.Location is { } location)
            {
                if (!location.IsAbsoluteUri) location = new Uri(checkedUri!, location);
                current = location;
                continue;
            }

            if (!resp.IsSuccessStatusCode) return (null, null, null);

            var contentType = resp.Content.Headers.ContentType?.MediaType;
            // Chỉ nhận text — chặn download binary/ảnh/pdf.
            if (contentType is not null
                && !contentType.StartsWith("text/", StringComparison.OrdinalIgnoreCase)
                && !contentType.Contains("html", StringComparison.OrdinalIgnoreCase)
                && !contentType.Contains("json", StringComparison.OrdinalIgnoreCase))
                return (null, contentType, checkedUri);

            await using var stream = await resp.Content.ReadAsStreamAsync(cts.Token);
            var buffer = new byte[MaxBytes];
            var read = 0;
            int n;
            while (read < MaxBytes && (n = await stream.ReadAsync(buffer.AsMemory(read, MaxBytes - read), cts.Token)) > 0)
                read += n;
            var body = Encoding.UTF8.GetString(buffer, 0, read);
            return (body, contentType, checkedUri);
        }
        return (null, null, null);
    }

    // ---------- SSRF guard ----------

    /// <summary>Validate scheme + resolve host → mọi IP phải public. Trả Uri đã check.</summary>
    internal static bool TryValidateAndResolve(string url, out Uri? uri, out string? error)
    {
        uri = null; error = null;
        if (string.IsNullOrWhiteSpace(url) || url.Length > 2048)
        { error = "URL không hợp lệ."; return false; }
        if (!Uri.TryCreate(url.Trim(), UriKind.Absolute, out var u)
            || (u.Scheme != Uri.UriSchemeHttp && u.Scheme != Uri.UriSchemeHttps)
            || string.IsNullOrWhiteSpace(u.Host))
        { error = "Chỉ hỗ trợ URL http/https."; return false; }

        // IP literal trong URL → check trực tiếp
        if (IPAddress.TryParse(u.Host.Trim('[', ']'), out var literal))
        {
            if (IsPrivateOrReserved(literal)) { error = "Địa chỉ không được phép."; return false; }
            uri = u; return true;
        }

        try
        {
            var addresses = Dns.GetHostAddresses(u.Host);
            if (addresses.Length == 0) { error = "Không phân giải được hostname."; return false; }
            if (addresses.Any(IsPrivateOrReserved)) { error = "Địa chỉ không được phép."; return false; }
        }
        catch (Exception)
        {
            error = "Không phân giải được hostname."; return false;
        }
        uri = u; return true;
    }

    /// <summary>Loopback/private/link-local/multicast/reserved/unspecified → chặn.</summary>
    internal static bool IsPrivateOrReserved(IPAddress ip)
    {
        if (IPAddress.IsLoopback(ip)) return true;
        var bytes = ip.GetAddressBytes();
        return ip.AddressFamily switch
        {
            System.Net.Sockets.AddressFamily.InterNetwork =>
                bytes[0] == 0 || bytes[0] == 10 || bytes[0] == 127
                || (bytes[0] == 169 && bytes[1] == 254)   // link-local + cloud metadata
                || (bytes[0] == 172 && bytes[1] >= 16 && bytes[1] <= 31)
                || (bytes[0] == 192 && bytes[1] == 168)
                || (bytes[0] == 100 && bytes[1] >= 64 && bytes[1] <= 127) // CGNAT
                || bytes[0] >= 224,                        // multicast + reserved
            System.Net.Sockets.AddressFamily.InterNetworkV6 =>
                ip.IsIPv6LinkLocal || ip.IsIPv6Multicast || ip.IsIPv6SiteLocal
                || (bytes[0] == 0xFC || bytes[0] == 0xFD)   // unique local fc00::/7
                || (bytes[0] == 0xFE && (bytes[1] & 0xC0) == 0x80), // fe80::/10 link-local
            _ => true,
        };
    }

    // ---------- link discovery (same-origin deep crawl) ----------

    private static readonly string[] SkipExtensions =
        [".jpg", ".jpeg", ".png", ".gif", ".webp", ".svg", ".pdf", ".zip", ".mp4", ".mp3", ".css", ".js", ".ico", ".woff", ".woff2"];

    // Keyword trong path/anchor → trang nhiều khả năng chứa tri thức shop.
    private static readonly string[] LinkKeywords =
        ["menu", "dich-vu", "service", "about", "gioi-thieu", "lien-he", "contact",
         "bang-gia", "price", "chinh-sach", "policy", "faq", "gio-mo-cua", "khuyen-mai"];

    [GeneratedRegex("<a\\s[^>]*href=[\"']([^\"'#]+)[\"']", RegexOptions.IgnoreCase)]
    private static partial Regex AnchorRegex();

    /// <summary>Trích link same-origin từ HTML, ưu tiên keyword, loại asset/login.</summary>
    internal static IReadOnlyList<Uri> ExtractLinks(string html, Uri baseUri)
    {
        var scored = new List<(Uri Uri, int Score)>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (Match m in AnchorRegex().Matches(html))
        {
            var href = m.Groups[1].Value;
            if (href.StartsWith("mailto:", StringComparison.OrdinalIgnoreCase)
                || href.StartsWith("tel:", StringComparison.OrdinalIgnoreCase)
                || href.StartsWith("javascript:", StringComparison.OrdinalIgnoreCase))
                continue;
            if (!Uri.TryCreate(baseUri, href, out var link)) continue;
            if (!string.Equals(link.Scheme, baseUri.Scheme, StringComparison.OrdinalIgnoreCase)
                || !string.Equals(link.Host, baseUri.Host, StringComparison.OrdinalIgnoreCase))
                continue;
            var path = link.AbsolutePath;
            if (SkipExtensions.Any(ext => path.EndsWith(ext, StringComparison.OrdinalIgnoreCase))) continue;
            if (path.Equals("/", StringComparison.OrdinalIgnoreCase)
                || string.Equals(link.ToString(), baseUri.ToString(), StringComparison.OrdinalIgnoreCase))
                continue;
            var key = link.GetLeftPart(UriPartial.Path).ToLowerInvariant();
            if (!seen.Add(key)) continue;

            var haystack = (path + " " + link.Query).ToLowerInvariant();
            var score = LinkKeywords.Count(k => haystack.Contains(k, StringComparison.Ordinal));
            scored.Add((link, score));
        }
        return scored.OrderByDescending(s => s.Score).ThenBy(s => s.Uri.ToString()).Select(s => s.Uri).ToList();
    }

    // ---------- HTML → text ----------

    [GeneratedRegex("<(script|style|noscript|svg|head|nav|footer|header|form|iframe|button|select)[^>]*>.*?</\\1>", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex BlockRegex();

    [GeneratedRegex("<[^>]+>")]
    private static partial Regex TagRegex();

    internal static string HtmlToText(string html)
    {
        var s = BlockRegex().Replace(html, " ");
        s = TagRegex().Replace(s, " ");
        s = WebUtility.HtmlDecode(s);
        s = Regex.Replace(s, @"\s+", " ").Trim();
        return s;
    }
}
