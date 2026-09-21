using System.Net.Http.Json;
using System.Text.Json;
using VanAn.Crawler.Dtos;
using VanAn.Crawler.Options;

namespace VanAn.Crawler.Adapters;

/// <summary>
/// REST API adapter for doanhnghiep.vn — config-driven, no HTML scraping.
/// API endpoints (M2 verified 2026-08-26):
///   GET /api/v1/search?q={name}&limit={N}  → list (mst, name_vi, legal_form, status, ...)
///   GET /api/v1/companies/{mst}            → full details (address, legal_rep, industry, province)
/// Free tier: 100 req/day, no API key needed.
/// No phone field in API → phone comes from TrangVangHtmlAdapter.
/// </summary>
public sealed class RestApiAdapter : IDataSourceAdapter
{
    private readonly HttpClient _httpClient;
    private readonly CrawlerOptions _options;
    private readonly ILogger<RestApiAdapter> _logger;
    private readonly string _sourceName;
    private readonly string _baseUrl;
    private int _requestCount;

    /// <summary>Set when the source API rejects auth (401). Surfaced in crawl status so the
    /// admin UI shows WHY 0 listings were crawled.</summary>
    public string? LastAuthError { get; private set; }

    public RestApiAdapter(
        HttpClient httpClient,
        CrawlerOptions options,
        ILogger<RestApiAdapter> logger,
        string sourceName,
        string baseUrl)
    {
        _httpClient = httpClient;
        _options = options;
        _logger = logger;
        _sourceName = sourceName;
        _baseUrl = baseUrl.TrimEnd('/');

        // 2026-09-21 FIX (crawl 401): doanhnghiep.vn disabled the no-key public tier on
        // 2026-09-14 — every request now requires `x-api-key` (free registration at
        // https://doanhnghiep.vn/api/docs). Previously "no API key needed" (M2, 2026-08-26).
        if (!string.IsNullOrWhiteSpace(options.DoanhNghiepApiKey))
        {
            _httpClient.DefaultRequestHeaders.Remove("x-api-key");
            _httpClient.DefaultRequestHeaders.TryAddWithoutValidation("x-api-key", options.DoanhNghiepApiKey);
        }
    }

    public string Name => _sourceName;

    /// <summary>
    /// 2026-09-21 feature: fetch companies by a list of tax codes (MST). doanhnghiep.vn
    /// search short-circuits to findUnique when q is a 10/13-digit MST; fallback to the
    /// companies/{mst} detail endpoint. Used by "đăng ký tenant bằng mã số thuế".
    /// </summary>
    public async Task<List<CrawlListingDto>> FetchByTaxCodesAsync(List<string> taxCodes, CancellationToken ct = default)
    {
        var results = new List<CrawlListingDto>();
        var seenMst = new HashSet<string>();

        foreach (var raw in taxCodes)
        {
            var mst = raw.Trim();
            if (string.IsNullOrEmpty(mst) || !seenMst.Add(mst)) continue;

            try
            {
                // Try the detail endpoint first (most complete response).
                var detailUrl = $"{_baseUrl}/api/v1/companies/{Uri.EscapeDataString(mst)}";
                var detailResp = await _httpClient.GetAsync(detailUrl, ct);
                _requestCount++;

                if (detailResp.IsSuccessStatusCode)
                {
                    var detailJson = await detailResp.Content.ReadAsStringAsync(ct);
                    using var detailDoc = JsonDocument.Parse(detailJson);
                    var d = detailDoc.RootElement;
                    var listing = MapDetailToListing(d, mst);
                    if (listing is not null)
                    {
                        results.Add(listing);
                        _logger.LogInformation("[{Source}] Tax-code {Mst}: {Name}", _sourceName, mst, listing.Name);
                        continue;
                    }
                }
                else if (detailResp.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                {
                    LastAuthError = string.IsNullOrWhiteSpace(_options.DoanhNghiepApiKey)
                        ? "Thiếu API key doanhnghiep.vn (Crawler__DoanhNghiepApiKey). Đăng ký MIỄN PHÍ: https://doanhnghiep.vn/api/docs"
                        : "API key doanhnghiep.vn bị từ chối (401). Kiểm tra Crawler__DoanhNghiepApiKey.";
                    _logger.LogWarning("[{Source}] Tax-code {Mst} detail failed: 401 — {Detail}", _sourceName, mst, LastAuthError);
                    break;
                }

                // Fallback: search by MST (findUnique short-circuit)
                var searchUrl = $"{_baseUrl}/api/v1/search?q={Uri.EscapeDataString(mst)}&limit=1&page=1";
                var searchResp = await _httpClient.GetAsync(searchUrl, ct);
                _requestCount++;
                if (!searchResp.IsSuccessStatusCode) continue;

                var searchJson = await searchResp.Content.ReadAsStringAsync(ct);
                using var searchDoc = JsonDocument.Parse(searchJson);
                if (!searchDoc.RootElement.TryGetProperty("items", out var itemsEl)) continue;
                var items = itemsEl.EnumerateArray().ToList();
                if (items.Count == 0)
                {
                    _logger.LogInformation("[{Source}] Tax-code {Mst}: not found", _sourceName, mst);
                    continue;
                }

                var itemMst = items[0].TryGetProperty("mst", out var mstEl) ? mstEl.GetString() : mst;
                var listing2 = MapDetailToListing(items[0], itemMst ?? mst);
                if (listing2 is not null)
                {
                    results.Add(listing2);
                    _logger.LogInformation("[{Source}] Tax-code {Mst}: {Name}", _sourceName, mst, listing2.Name);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[{Source}] Tax-code {Mst} fetch failed", _sourceName, mst);
            }

            await Task.Delay(_options.DefaultRateLimitMs, ct);
        }

        _logger.LogInformation("[{Source}] Tax-code fetch complete: {Count} listings", _sourceName, results.Count);
        return results;
    }

    private static CrawlListingDto? MapDetailToListing(JsonElement d, string fallbackMst)
    {
        var mst = d.TryGetProperty("mst", out var mstEl) ? mstEl.GetString() : null;
        if (string.IsNullOrEmpty(mst)) mst = fallbackMst;
        if (string.IsNullOrEmpty(mst)) return null;

        // Skip dissolved/terminated companies — only import operating ones.
        var status = d.TryGetProperty("status", out var statusEl) ? statusEl.GetString() : null;
        if (!string.IsNullOrEmpty(status) && status is "dissolved" or "suspended")
        {
            return null;
        }

        return new CrawlListingDto
        {
            Name = d.TryGetProperty("name_vi", out var nameEl) ? nameEl.GetString() ?? "" : "",
            TaxCode = mst,
            Address = d.TryGetProperty("address_full", out var addrEl) ? addrEl.GetString() : null,
            ContactName = d.TryGetProperty("legal_rep_name", out var repEl) ? repEl.GetString() : null,
            IndustryCode = d.TryGetProperty("industry_main_code", out var indEl) ? indEl.GetString() : null,
            SourceSite = "doanhnghiep.vn",
            SourceUrl = $"https://doanhnghiep.vn/dn/{mst}",
            CrawledAt = DateTime.UtcNow
        };
    }

    public async Task<List<CrawlListingDto>> FetchAsync(CrawlQuery query, CancellationToken ct = default)
    {
        var results = new List<CrawlListingDto>();
        // doanhnghiep.vn API hard-caps at 20 items per page regardless of limit param.
        // Must paginate via page=1,2,3... until we reach MaxResults or empty page.
        const int PageSize = 20;
        var page = 1;
        var seenMst = new HashSet<string>();

        while (results.Count < query.MaxResults
            && _requestCount < _options.DoanhNghiepDailyLimit)
        {
            // Step 1: Search for companies by name/industry — paginate
            var remaining = Math.Min(PageSize, query.MaxResults - results.Count);
            var searchUrl = $"{_baseUrl}/api/v1/search?q={Uri.EscapeDataString(query.SearchTerm ?? "")}" +
                            $"&limit={remaining}&page={page}";
            if (!string.IsNullOrEmpty(query.IndustryCode))
                searchUrl += $"&industry={Uri.EscapeDataString(query.IndustryCode)}";
            if (!string.IsNullOrEmpty(query.Province))
                searchUrl += $"&province={Uri.EscapeDataString(query.Province)}";

            _logger.LogInformation("[{Source}] Search page {Page}: {Url}", _sourceName, page, searchUrl);
            var searchResp = await _httpClient.GetAsync(searchUrl, ct);
            if (!searchResp.IsSuccessStatusCode)
            {
                // 2026-09-21: 401 = API key missing/invalid (public tier disabled 2026-09-14).
                // Surface the reason so the crawl status shows WHY 0 listings instead of silently
                // returning "No listings crawled".
                if (searchResp.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                {
                    var errBody = await searchResp.Content.ReadAsStringAsync(ct);
                    var detail = string.IsNullOrWhiteSpace(_options.DoanhNghiepApiKey)
                        ? "Thiếu API key doanhnghiep.vn (Crawler__DoanhNghiepApiKey). Đăng ký MIỄN PHÍ: https://doanhnghiep.vn/api/docs"
                        : $"API key doanhnghiep.vn bị từ chối (401). Kiểm tra Crawler__DoanhNghiepApiKey. {errBody[..Math.Min(errBody.Length, 200)]}";
                    _logger.LogWarning("[{Source}] Search page {Page} failed: 401 Unauthorized — {Detail}", _sourceName, page, detail);
                    LastAuthError = detail;
                }
                else
                {
                    _logger.LogWarning("[{Source}] Search page {Page} failed: {Status}", _sourceName, page, searchResp.StatusCode);
                }
                break;
            }
            _requestCount++;

            var searchJson = await searchResp.Content.ReadAsStringAsync(ct);
            using var searchDoc = JsonDocument.Parse(searchJson);
            var items = searchDoc.RootElement.GetProperty("items").EnumerateArray().ToList();

            _logger.LogInformation("[{Source}] Page {Page}: Found {Count} companies", _sourceName, page, items.Count);

            if (items.Count == 0)
            {
                _logger.LogInformation("[{Source}] Page {Page} empty — no more results", _sourceName, page);
                break;
            }

            // Step 2: Get full details for each company (rate-limited, skip duplicates)
            foreach (var item in items)
            {
                if (results.Count >= query.MaxResults) break;
                if (_requestCount >= _options.DoanhNghiepDailyLimit)
                {
                    _logger.LogWarning("[{Source}] Daily limit reached ({Limit}), stopping",
                        _sourceName, _options.DoanhNghiepDailyLimit);
                    break;
                }

                var mst = item.GetProperty("mst").GetString();
                if (string.IsNullOrEmpty(mst) || !seenMst.Add(mst)) continue;

                // Rate limit between detail calls
                await Task.Delay(_options.DefaultRateLimitMs, ct);

                var detailUrl = $"{_baseUrl}/api/v1/companies/{mst}";
                var detailResp = await _httpClient.GetAsync(detailUrl, ct);
                _requestCount++;

                if (!detailResp.IsSuccessStatusCode)
                {
                    _logger.LogDebug("[{Source}] Detail fetch failed for {Mst}: {Status}",
                        _sourceName, mst, detailResp.StatusCode);
                    continue;
                }

                var detailJson = await detailResp.Content.ReadAsStringAsync(ct);
                using var detailDoc = JsonDocument.Parse(detailJson);
                var d = detailDoc.RootElement;

                var listing = new CrawlListingDto
                {
                    Name = d.TryGetProperty("name_vi", out var nameEl) ? nameEl.GetString() ?? "" : "",
                    TaxCode = mst,
                    Address = d.TryGetProperty("address_full", out var addrEl) ? addrEl.GetString() : null,
                    ContactName = d.TryGetProperty("legal_rep_name", out var repEl) ? repEl.GetString() : null,
                    IndustryCode = d.TryGetProperty("industry_main_code", out var indEl) ? indEl.GetString() : null,
                    SourceSite = _sourceName,
                    SourceUrl = $"{_baseUrl}/dn/{mst}",
                    CrawledAt = DateTime.UtcNow
                };

                results.Add(listing);
                _logger.LogDebug("[{Source}] Crawled: {Name} ({Mst})", _sourceName, listing.Name, mst);
            }

            // If this page returned fewer than PageSize, no more pages
            if (items.Count < PageSize) break;
            page++;
        }

        _logger.LogInformation("[{Source}] Fetch complete: {Count} listings (pages: {Pages})",
            _sourceName, results.Count, page);
        return results;
    }
}
