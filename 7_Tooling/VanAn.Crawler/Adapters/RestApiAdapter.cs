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
    }

    public string Name => _sourceName;

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
                _logger.LogWarning("[{Source}] Search page {Page} failed: {Status}", _sourceName, page, searchResp.StatusCode);
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
