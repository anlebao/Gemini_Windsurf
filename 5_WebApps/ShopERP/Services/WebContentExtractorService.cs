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
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

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
            var text = isHtml ? HtmlToText(body) : body;
            if (string.IsNullOrWhiteSpace(text))
                return new WebExtractResult(string.Empty, "Không trích được nội dung text từ trang.");

            string? warning = null;
            if (text.Length < 200)
                warning = "Trang có rất ít nội dung text — site có thể render bằng JavaScript, hãy copy text thủ công.";
            if (text.Length > MaxTextChars) text = text[..MaxTextChars];

            _logger.LogInformation("WebExtract ok {Url} → {Chars} chars (final {Final})", uri!.Host, text.Length, finalUrl?.Host);
            return new WebExtractResult(text, warning);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogDebug(ex, "WebExtract failed {Url}", uri!.Host);
            return null;
        }
    }

    // ---------- fetch ----------

    private async Task<(string? Body, string? ContentType, Uri? FinalUrl)> FetchFollowingRedirectsAsync(Uri start, CancellationToken ct)
    {
        var current = start;
        for (var hop = 0; hop <= MaxRedirects; hop++)
        {
            if (!TryValidateAndResolve(current.ToString(), out var checkedUri, out _))
                return (null, null, null);

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(Timeout);
            var client = _httpFactory.CreateClient();
            client.Timeout = Timeout;

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
