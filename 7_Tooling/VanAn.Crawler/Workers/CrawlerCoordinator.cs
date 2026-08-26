using System.Net.Http.Json;
using VanAn.Crawler.Adapters;
using VanAn.Crawler.Dtos;
using VanAn.Crawler.Options;

namespace VanAn.Crawler.Workers;

/// <summary>
/// Background service that listens for HTTP trigger requests on port 5010,
/// runs crawl adapters, and posts results to Gateway POST /api/v1/crawl/batch.
/// </summary>
public sealed class CrawlerCoordinator : BackgroundService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly CrawlerOptions _options;
    private readonly ILogger<CrawlerCoordinator> _logger;
    private readonly List<IDataSourceAdapter> _adapters;

    public CrawlerCoordinator(
        IHttpClientFactory httpClientFactory,
        CrawlerOptions options,
        IEnumerable<IDataSourceAdapter> adapters,
        ILogger<CrawlerCoordinator> logger)
    {
        _httpClientFactory = httpClientFactory;
        _options = options;
        _logger = logger;
        _adapters = adapters.ToList();
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("CrawlerCoordinator started. Listening on port {Port}. {AdapterCount} adapters registered: [{Adapters}]",
            _options.ListenPort, _adapters.Count, string.Join(", ", _adapters.Select(a => a.Name)));

        // Keep running until stopped — trigger handled via HTTP endpoint in Program.cs
        await Task.Delay(Timeout.Infinite, stoppingToken);
    }

    /// <summary>
    /// Run a crawl job triggered by POST /trigger.
    /// Called by the minimal API endpoint in Program.cs.
    /// </summary>
    public async Task<BatchCrawlResult> RunCrawlAsync(CrawlTriggerRequest request, CancellationToken ct = default)
    {
        _logger.LogInformation("Crawl triggered: source={Source}, industry={Industry}, province={Province}, maxResults={MaxResults}",
            request.Source, request.Industry, request.Province, request.MaxResults);

        // Select adapters — if Source specified, use only that one; otherwise use all
        var selectedAdapters = string.IsNullOrEmpty(request.Source)
            ? _adapters
            : _adapters.Where(a => a.Name.Equals(request.Source, StringComparison.OrdinalIgnoreCase)).ToList();

        if (selectedAdapters.Count == 0)
        {
            _logger.LogWarning("No adapters matched source '{Source}'", request.Source);
            return new BatchCrawlResult(0, 0, [new BatchCrawlError(request.Source ?? "all", "No matching adapter")]);
        }

        // Fetch from all selected adapters
        var allListings = new List<CrawlListingDto>();
        foreach (var adapter in selectedAdapters)
        {
            try
            {
                var query = new CrawlQuery
                {
                    SearchTerm = request.SearchTerm ?? request.Industry ?? "công ty",
                    IndustryCode = request.Industry,
                    Province = request.Province,
                    MaxResults = request.MaxResults
                };
                var listings = await adapter.FetchAsync(query, ct);
                allListings.AddRange(listings);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Adapter {Adapter} failed", adapter.Name);
            }
        }

        if (allListings.Count == 0)
        {
            _logger.LogInformation("No listings crawled — nothing to post to Gateway");
            return new BatchCrawlResult(0, 0, []);
        }

        // Post to Gateway in batches
        var gatewayClient = _httpClientFactory.CreateClient("gateway");
        var imported = 0;
        var skipped = 0;
        var errors = new List<BatchCrawlError>();

        foreach (var batch in Chunk(allListings, _options.MaxBatchSize))
        {
            try
            {
                _logger.LogInformation("Posting batch of {Count} listings to Gateway", batch.Count);
                var resp = await gatewayClient.PostAsJsonAsync(
                    "/api/v1/crawl/batch", batch, ct);

                // Defensive: check Content-Type is JSON before deserializing.
                // If auth failed, Gateway returns HTML (login redirect) with 200 → ReadFromJsonAsync crashes.
                var contentType = resp.Content.Headers.ContentType?.MediaType ?? "";
                var body = await resp.Content.ReadAsStringAsync(ct);

                if (!resp.IsSuccessStatusCode)
                {
                    _logger.LogError("Gateway batch POST failed: {Status} {Body}", resp.StatusCode, body[..Math.Min(body.Length, 500)]);
                    errors.Add(new BatchCrawlError($"batch-{imported}", $"Gateway {resp.StatusCode}"));
                }
                else if (!contentType.Contains("json", StringComparison.OrdinalIgnoreCase))
                {
                    _logger.LogError("Gateway batch POST returned non-JSON response (Content-Type: {ContentType}). Likely auth redirect. Body: {Body}",
                        contentType, body[..Math.Min(body.Length, 200)]);
                    errors.Add(new BatchCrawlError($"batch-{imported}", $"Gateway returned {contentType}, not JSON (auth failed?)"));
                }
                else
                {
                    var result = System.Text.Json.JsonSerializer.Deserialize<BatchCrawlResult>(body);
                    if (result is not null)
                    {
                        imported += result.Imported;
                        skipped += result.Skipped;
                        errors.AddRange(result.Errors);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Gateway batch POST exception");
                errors.Add(new BatchCrawlError($"batch-{imported}", ex.Message));
            }
        }

        _logger.LogInformation("Crawl complete: imported={Imported}, skipped={Skipped}, errors={Errors}",
            imported, skipped, errors.Count);
        return new BatchCrawlResult(imported, skipped, errors);
    }

    private static List<List<T>> Chunk<T>(List<T> source, int chunkSize)
    {
        var chunks = new List<List<T>>();
        for (int i = 0; i < source.Count; i += chunkSize)
            chunks.Add(source.GetRange(i, Math.Min(chunkSize, source.Count - i)));
        return chunks;
    }
}
