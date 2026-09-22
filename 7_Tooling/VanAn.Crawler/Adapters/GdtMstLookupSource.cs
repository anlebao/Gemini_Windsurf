using System.Diagnostics;
using System.Text.RegularExpressions;
using VanAn.Crawler.Dtos;
using VanAn.Crawler.Options;

namespace VanAn.Crawler.Adapters;

/// <summary>
/// 2026-09-22 (user-approved fallback source): tracuunnt.gdt.gov.vn — Tổng cục Thuế.
/// Official tax registry — the most authoritative source for MST validity + status.
///
/// Flow (captcha-gated, verified 2026-09-22 from GCP cloud IP — HTTP 200):
///   1. GET /tcnnt/mstdn.jsp            → session cookies (must be reused!)
///   2. GET /tcnnt/captcha.png?uid=...  → 130x50 PNG captcha (same cookies)
///   3. OCR captcha via tesseract (installed in the crawler image)
///   4. POST /tcnnt/mstdn.jsp           → wrong captcha = "Vui lòng nhập đúng mã xác nhận!"
///                                         (retry with a fresh captcha); success = result table
///
/// Usage policy (polite — the captcha IS the site's anti-spam gate):
///   - SINGLE-MST lookups only (feature "đăng ký tenant bằng mã số thuế") — NEVER batch crawl.
///   - Rate-limited (GdtRateLimitMs, default 5s between requests) + daily cap (GdtMaxPerDay).
///   - Opt-in via Crawler__GdtLookupEnabled (default false).
/// </summary>
public sealed class GdtMstLookupSource
{
    private readonly HttpClient _http;
    private readonly CrawlerOptions _options;
    private readonly ILogger<GdtMstLookupSource> _logger;
    private readonly string _ocrPath;

    // Daily cap tracking (UTC day reset)
    private DateTime _dayStart = DateTime.UtcNow.Date;
    private int _lookupCount;

    // Markers (server responses are HTML-entity encoded)
    private const string CaptchaErrorMarker = "x&#225;c nh&#7853;n";       // "xác nhận" encoded
    private const string CaptchaErrorPlain = "xác nhận";                   // plain variant
    private const string WafMarker = "Request Rejected";
    private const string NotFoundMarker = "kh&#244;ng t&#236;m th&#7845;y"; // "không tìm thấy" encoded
    private const string NotFoundPlain = "không tìm thấy";

    public GdtMstLookupSource(
        HttpClient httpClient,
        CrawlerOptions options,
        ILogger<GdtMstLookupSource> logger)
    {
        _http = httpClient;
        _options = options;
        _logger = logger;
        _ocrPath = string.IsNullOrWhiteSpace(options.GdtOcrPath) ? "tesseract" : options.GdtOcrPath;
    }

    public string Name => "tracuunnt.gdt.gov.vn";

    /// <summary>
    /// Lookup ONE MST. Returns a listing on success, null when: disabled, daily cap hit,
    /// captcha exhausted (all retries), not found, or blocked. Never throws.
    /// </summary>
    public async Task<CrawlListingDto?> LookupByMstAsync(string mst, CancellationToken ct = default)
    {
        if (!_options.GdtLookupEnabled)
        {
            _logger.LogDebug("[{Source}] GdtLookupEnabled=false — skipping MST {Mst}", Name, mst);
            return null;
        }

        // Daily cap (UTC-day window)
        if (DateTime.UtcNow.Date != _dayStart)
        {
            _dayStart = DateTime.UtcNow.Date;
            _lookupCount = 0;
        }
        if (_lookupCount >= _options.GdtMaxPerDay)
        {
            _logger.LogWarning("[{Source}] Daily cap reached ({Cap}) — skipping MST {Mst}",
                Name, _options.GdtMaxPerDay, mst);
            return null;
        }

        for (int attempt = 1; attempt <= Math.Max(1, _options.GdtCaptchaRetries); attempt++)
        {
            try
            {
                _lookupCount++;
                var result = await TryLookupOnceAsync(mst, attempt, ct);
                if (result is not null)
                {
                    _logger.LogInformation("[{Source}] MST {Mst} → {Company}", Name, mst, result.Name);
                    return result;
                }
                // null = captcha wrong / not found / blocked — retry (fresh captcha) with delay
                await Task.Delay(_options.GdtRateLimitMs, ct);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[{Source}] Lookup attempt {Attempt} failed for MST {Mst}", Name, attempt, mst);
                break; // network/parse errors: don't burn retries
            }
        }

        _logger.LogWarning("[{Source}] MST {Mst} lookup failed after {Retries} attempts", Name, mst, _options.GdtCaptchaRetries);
        return null;
    }

    private async Task<CrawlListingDto?> TryLookupOnceAsync(string mst, int attempt, CancellationToken ct)
    {
        // 1. GET the form page → establishes the session cookie the captcha is bound to
        using var pageResp = await _http.GetAsync("/tcnnt/mstdn.jsp", ct);
        if (!pageResp.IsSuccessStatusCode)
        {
            _logger.LogWarning("[{Source}] Page GET failed: {Status}", Name, pageResp.StatusCode);
            return null;
        }

        // 2. GET the captcha image (same cookie jar)
        var uid = Random.Shared.Next(100000, 999999);
        using var capResp = await _http.GetAsync($"/tcnnt/captcha.png?uid={uid}", ct);
        if (!capResp.IsSuccessStatusCode)
        {
            _logger.LogWarning("[{Source}] Captcha GET failed: {Status}", Name, capResp.StatusCode);
            return null;
        }
        var capBytes = await capResp.Content.ReadAsByteArrayAsync(ct);

        var captchaText = await OcrCaptchaAsync(capBytes, ct);
        if (string.IsNullOrWhiteSpace(captchaText))
        {
            _logger.LogWarning("[{Source}] Captcha OCR failed (attempt {Attempt})", Name, attempt);
            return null;
        }

        // 3. POST the search (same cookie jar)
        using var postContent = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["cm"] = "cm",
            ["mst"] = mst,
            ["fullname"] = "",
            ["address"] = "",
            ["cmt"] = "",
            ["captcha"] = captchaText
        });
        using var postResp = await _http.PostAsync("/tcnnt/mstdn.jsp", postContent, ct);
        var html = await postResp.Content.ReadAsStringAsync(ct);

        // WAF / gateway rejection
        if (html.Contains(WafMarker, StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogWarning("[{Source}] WAF rejected request (attempt {Attempt})", Name, attempt);
            return null;
        }

        // Wrong captcha → retry with a fresh one
        if (html.Contains(CaptchaErrorMarker, StringComparison.OrdinalIgnoreCase)
            || html.Contains(CaptchaErrorPlain, StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogDebug("[{Source}] Captcha wrong (attempt {Attempt}) — retrying", Name, attempt);
            return null;
        }

        // Not found
        if (html.Contains(NotFoundMarker, StringComparison.OrdinalIgnoreCase)
            || html.Contains(NotFoundPlain, StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogInformation("[{Source}] MST {Mst} not found", Name, mst);
            return null;
        }

        return ParseResultTable(html, mst);
    }

    /// <summary>OCR the captcha PNG via the tesseract binary. Returns the trimmed text (null on failure).</summary>
    private async Task<string?> OcrCaptchaAsync(byte[] png, CancellationToken ct)
    {
        var tmpDir = Path.Combine(Path.GetTempPath(), $"gdt_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tmpDir);
        var imgPath = Path.Combine(tmpDir, "captcha.png");
        try
        {
            await File.WriteAllBytesAsync(imgPath, png, ct);
            var psi = new ProcessStartInfo(_ocrPath, $"\"{imgPath}\" stdout --psm 7 -l eng")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            using var proc = Process.Start(psi);
            if (proc is null)
            {
                _logger.LogWarning("[{Source}] tesseract not found at '{Path}' — install tesseract-ocr in the image", Name, _ocrPath);
                return null;
            }
            var stdout = await proc.StandardOutput.ReadToEndAsync(ct);
            var stderr = await proc.StandardError.ReadToEndAsync(ct);
            await proc.WaitForExitAsync(ct);

            // GDT captcha = 4 alphanumeric chars; strip everything else
            var text = new string(stdout.Where(char.IsLetterOrDigit).ToArray());
            if (text.Length < 4)
            {
                _logger.LogDebug("[{Source}] OCR output too short ({Text}) stderr={Err}", Name, text.Length > 0 ? text : "''", stderr.Trim());
                return null;
            }
            return text[..4];
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[{Source}] tesseract invocation failed", Name);
            return null;
        }
        finally
        {
            try { Directory.Delete(tmpDir, true); } catch { /* best-effort */ }
        }
    }

    /// <summary>
    /// Parse the GDT result table. Labels are table cells followed by data cells, e.g.:
    ///   Mã số thuế | 0100107518 | Tên tổ chức cá nhân nộp thuế | CÔNG TY ... | ...
    /// Defensive: match by label text; return null when the MST/name is missing.
    /// </summary>
    private static CrawlListingDto? ParseResultTable(string html, string mst)
    {
        var rows = Regex.Matches(html, @"<tr[^>]*>(.*?)</tr>", RegexOptions.Singleline | RegexOptions.IgnoreCase);
        var cells = new List<string>();
        foreach (Match row in rows)
        {
            var rowCells = Regex.Matches(row.Groups[1].Value, @"<t[dh][^>]*>(.*?)</t[dh]>", RegexOptions.Singleline | RegexOptions.IgnoreCase)
                .Select(m => Regex.Replace(m.Groups[1].Value, "<[^>]+>", " "))
                .Select(c => System.Net.WebUtility.HtmlDecode(c).Trim())
                .Where(c => c.Length > 0)
                .ToList();
            cells.AddRange(rowCells);
        }

        if (cells.Count == 0) return null;

        string GetAfter(string label)
        {
            for (int i = 0; i < cells.Count - 1; i++)
            {
                if (cells[i].Contains(label, StringComparison.OrdinalIgnoreCase)
                    && !cells[i + 1].Contains(label, StringComparison.OrdinalIgnoreCase))
                    return cells[i + 1];
            }
            return "";
        }

        var name = GetAfter("Tên tổ chức");
        var address = GetAfter("Địa chỉ trụ sở");
        var rep = GetAfter("Người đại diện");
        var status = GetAfter("Trạng thái");

        if (string.IsNullOrWhiteSpace(name))
        {
            // Fallback: first data-ish cell after the MST label
            name = GetAfter("Mã số thuế");
        }
        if (string.IsNullOrWhiteSpace(name) || name.Equals(mst, StringComparison.OrdinalIgnoreCase))
            return null;

        // Skip suspended/dissolved companies — only import operating ones (mirror doanhnghiep logic)
        if (!string.IsNullOrWhiteSpace(status)
            && (status.Contains("tạm ngừng", StringComparison.OrdinalIgnoreCase)
                || status.Contains("đã chấm dứt", StringComparison.OrdinalIgnoreCase)
                || status.Contains("ngừng hoạt động", StringComparison.OrdinalIgnoreCase)
                || status.Contains("chấm dứt", StringComparison.OrdinalIgnoreCase)))
        {
            return null;
        }

        return new CrawlListingDto
        {
            Name = name.Length > 200 ? name[..200] : name,
            TaxCode = mst,
            Address = string.IsNullOrWhiteSpace(address) ? null : address,
            ContactName = string.IsNullOrWhiteSpace(rep) ? null : rep,
            IndustryCode = null,
            SourceSite = "tracuunnt.gdt.gov.vn",
            SourceUrl = $"https://tracuunnt.gdt.gov.vn/tcnnt/mstdn.jsp",
            CrawledAt = DateTime.UtcNow
        };
    }
}
