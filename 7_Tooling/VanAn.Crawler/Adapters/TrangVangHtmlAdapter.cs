using AngleSharp;
using AngleSharp.Dom;
using VanAn.Crawler.Dtos;
using VanAn.Crawler.Options;

namespace VanAn.Crawler.Adapters;

/// <summary>
/// HTML scraping adapter for trangvangvietnam.com — uses AngleSharp to parse listings.
/// Primary purpose: extract phone numbers (doanhnghiep.vn API has no phone field).
///
/// Legal compliance:
/// - Rate limit: 3-5 seconds between requests
/// - Batch max: 50-100 per run (ToS compliance)
/// - Identifiable User-Agent: VanAnCrawler/1.0 (+contact@vanan.vn)
/// - Only scrapes publicly available business directory pages
/// </summary>
public sealed class TrangVangHtmlAdapter : IDataSourceAdapter
{
    private readonly HttpClient _httpClient;
    private readonly CrawlerOptions _options;
    private readonly ILogger<TrangVangHtmlAdapter> _logger;

    public TrangVangHtmlAdapter(
        HttpClient httpClient,
        CrawlerOptions options,
        ILogger<TrangVangHtmlAdapter> logger)
    {
        _httpClient = httpClient;
        _options = options;
        _logger = logger;
    }

    public string Name => "trangvangvietnam";

    public async Task<List<CrawlListingDto>> FetchAsync(CrawlQuery query, CancellationToken ct = default)
    {
        var results = new List<CrawlListingDto>();
        var maxResults = Math.Min(query.MaxResults, 50); // ToS: max 50 per run

        // Build search URL: trangvangvietnam.com/tim-kiem?q={term}&nganh={industry}&tinh={province}
        var searchTerm = query.SearchTerm ?? query.IndustryCode ?? "";
        var searchUrl = $"https://trangvangvietnam.com/tim-kiem.html?q={Uri.EscapeDataString(searchTerm)}";
        if (!string.IsNullOrEmpty(query.Province))
            searchUrl += $"&tinh={Uri.EscapeDataString(query.Province)}";
        if (!string.IsNullOrEmpty(query.IndustryCode))
            searchUrl += $"&nganh={Uri.EscapeDataString(query.IndustryCode)}";

        _logger.LogInformation("[{Source}] Scraping: {Url}", Name, searchUrl);

        // Fetch HTML with identifiable User-Agent
        _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd(_options.UserAgent);
        var htmlResp = await _httpClient.GetAsync(searchUrl, ct);
        if (!htmlResp.IsSuccessStatusCode)
        {
            _logger.LogWarning("[{Source}] HTML fetch failed: {Status}", Name, htmlResp.StatusCode);
            return results;
        }

        var html = await htmlResp.Content.ReadAsStringAsync(ct);

        // Parse with AngleSharp
        var config = Configuration.Default;
        var context = BrowsingContext.New(config);
        var document = await context.OpenAsync(req => req.Content(html), ct);

        // Parse listing cards — trangvangvietnam uses .listing-item or .company-item class
        var listings = document.QuerySelectorAll(".listing-item, .company-item, .item-company, .dn-item");
        _logger.LogInformation("[{Source}] Found {Count} listing elements", Name, listings.Length);

        foreach (var el in listings.Take(maxResults))
        {
            await Task.Delay(_options.DefaultRateLimitMs, ct); // Rate limit between parses

            var listing = ParseListing(el, searchUrl);
            if (listing is not null)
                results.Add(listing);
        }

        _logger.LogInformation("[{Source}] Fetch complete: {Count} listings", Name, results.Count);
        return results;
    }

    private CrawlListingDto? ParseListing(IElement el, string sourceUrl)
    {
        try
        {
            // Company name
            var nameEl = el.QuerySelector("h3 a, h2 a, .company-name a, .title a");
            var name = nameEl?.TextContent?.Trim();
            if (string.IsNullOrEmpty(name)) return null;

            // Tax code (MST) — may or may not be present
            var taxCodeEl = el.QuerySelector(".mst, .tax-code, .masothue");
            var taxCode = taxCodeEl?.TextContent?.Trim()
                ?.Replace("MST:", "", StringComparison.OrdinalIgnoreCase)
                .Replace("Mã số thuế:", "", StringComparison.OrdinalIgnoreCase)
                .Trim();

            // Address
            var addrEl = el.QuerySelector(".address, .dia-chi, .company-address");
            var address = addrEl?.TextContent?.Trim();

            // Phone — primary purpose of this adapter
            var phoneEl = el.QuerySelector(".phone, .dien-thoai, .tel, .company-phone");
            var phone = phoneEl?.TextContent?.Trim()
                ?.Replace("ĐT:", "", StringComparison.OrdinalIgnoreCase)
                .Replace("Điện thoại:", "", StringComparison.OrdinalIgnoreCase)
                .Replace("Tel:", "", StringComparison.OrdinalIgnoreCase)
                .Trim();

            // Industry
            var industryEl = el.QuerySelector(".nganh, .industry, .category");
            var industry = industryEl?.TextContent?.Trim();

            // Detail page URL
            var linkEl = el.QuerySelector("h3 a, h2 a, .company-name a, .title a");
            var detailUrl = linkEl?.GetAttribute("href");
            if (!string.IsNullOrEmpty(detailUrl) && !detailUrl.StartsWith("http"))
                detailUrl = $"https://trangvangvietnam.com{detailUrl}";

            return new CrawlListingDto
            {
                Name = name,
                TaxCode = string.IsNullOrEmpty(taxCode) ? null : taxCode,
                Address = string.IsNullOrEmpty(address) ? null : address,
                CrawledPhone = string.IsNullOrEmpty(phone) ? null : phone,
                IndustryCode = string.IsNullOrEmpty(industry) ? null : industry,
                SourceSite = Name,
                SourceUrl = detailUrl ?? sourceUrl,
                CrawledAt = DateTime.UtcNow
            };
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "[{Source}] Failed to parse listing element", Name);
            return null;
        }
    }
}
