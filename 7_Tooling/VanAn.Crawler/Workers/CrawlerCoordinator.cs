using System.Net.Http.Json;
using System.Text.Json.Serialization;
using VanAn.Crawler.Adapters;
using VanAn.Crawler.Dtos;
using VanAn.Crawler.Options;

namespace VanAn.Crawler.Workers;

/// <summary>
/// Background service that listens for HTTP trigger requests on port 5010,
/// runs crawl adapters, and posts results to Gateway POST /api/v1/crawl/batch.
/// Exposes <see cref="GetStatus"/> for polling crawl progress from UI.
/// </summary>
public sealed class CrawlerCoordinator : BackgroundService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly CrawlerOptions _options;
    private readonly ILogger<CrawlerCoordinator> _logger;
    private readonly List<IDataSourceAdapter> _adapters;

    // ── Status tracking (thread-safe, polled by GET /status) ────────────────
    private static readonly object _statusLock = new();
    private static volatile bool _isRunning;
    private static string? _currentPhase;
    private static string? _currentSource;
    private static DateTime? _lastRunStartedAt;
    private static DateTime? _lastRunFinishedAt;
    private static BatchCrawlResult? _lastResult;
    private static string? _lastError;

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

    /// <summary>Snapshot of crawl status for GET /status endpoint (UI polling).</summary>
    public static CrawlStatusDto GetStatus()
    {
        lock (_statusLock)
        {
            return new CrawlStatusDto(
                IsRunning: _isRunning,
                CurrentPhase: _currentPhase,
                CurrentSource: _currentSource,
                LastRunStartedAt: _lastRunStartedAt,
                LastRunFinishedAt: _lastRunFinishedAt,
                LastResult: _lastResult,
                LastError: _lastError);
        }
    }

    private static void SetStatus(string phase, string? source = null)
    {
        lock (_statusLock)
        {
            _currentPhase = phase;
            if (source is not null) _currentSource = source;
        }
    }

    private static void StartRun(string? source)
    {
        lock (_statusLock)
        {
            _isRunning = true;
            _currentSource = source;
            _currentPhase = "Starting";
            _lastRunStartedAt = DateTime.UtcNow;
            _lastRunFinishedAt = null;
            _lastResult = null;
            _lastError = null;
        }
    }

    private static void FinishRun(BatchCrawlResult? result, string? error = null)
    {
        lock (_statusLock)
        {
            _isRunning = false;
            _currentPhase = error is not null ? "Failed" : "Completed";
            _lastRunFinishedAt = DateTime.UtcNow;
            _lastResult = result;
            _lastError = error;
        }
    }

    /// <summary>
    /// Run a crawl job triggered by POST /trigger.
    /// Called by the minimal API endpoint in Program.cs.
    /// </summary>
    public async Task<BatchCrawlResult> RunCrawlAsync(CrawlTriggerRequest request, CancellationToken ct = default)
    {
        _logger.LogInformation("Crawl triggered: source={Source}, industry={Industry}, province={Province}, maxResults={MaxResults}",
            request.Source, request.Industry, request.Province, request.MaxResults);

        StartRun(request.Source);

        // Select adapters — if Source specified, use only that one; otherwise use all
        var selectedAdapters = string.IsNullOrEmpty(request.Source)
            ? _adapters
            : _adapters.Where(a => a.Name.Equals(request.Source, StringComparison.OrdinalIgnoreCase)).ToList();

        if (selectedAdapters.Count == 0)
        {
            _logger.LogWarning("No adapters matched source '{Source}'", request.Source);
            var errResult = new BatchCrawlResult(0, 0, [new BatchCrawlError(request.Source ?? "all", "No matching adapter")]);
            FinishRun(errResult, $"No adapter matched source '{request.Source}'");
            return errResult;
        }

        // Fetch from all selected adapters
        var allListings = new List<CrawlListingDto>();
        foreach (var adapter in selectedAdapters)
        {
            try
            {
                SetStatus($"Crawling from {adapter.Name}", adapter.Name);
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
            var emptyResult = new BatchCrawlResult(0, 0, []);
            FinishRun(emptyResult, "No listings crawled (source returned 0 results or was blocked)");
            return emptyResult;
        }

        // Post to Gateway in batches
        SetStatus($"Posting {allListings.Count} listings to Gateway");
        var gatewayClient = _httpClientFactory.CreateClient("gateway");
        var imported = 0;
        var skipped = 0;
        var errors = new List<BatchCrawlError>();

        foreach (var batch in Chunk(allListings, _options.MaxBatchSize))
        {
            try
            {
                SetStatus($"Posting batch {imported + 1}-{imported + batch.Count} of {allListings.Count} to Gateway");
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
                        if (result.Errors is not null)
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
        var finalResult = new BatchCrawlResult(imported, skipped, errors);
        FinishRun(finalResult);
        return finalResult;
    }

    private static List<List<T>> Chunk<T>(List<T> source, int chunkSize)
    {
        var chunks = new List<List<T>>();
        for (int i = 0; i < source.Count; i += chunkSize)
            chunks.Add(source.GetRange(i, Math.Min(chunkSize, source.Count - i)));
        return chunks;
    }
}

/// <summary>Status DTO for GET /status endpoint. Polled by UI for progress.</summary>
public sealed record CrawlStatusDto(
    [property: JsonPropertyName("isRunning")] bool IsRunning,
    [property: JsonPropertyName("currentPhase")] string? CurrentPhase,
    [property: JsonPropertyName("currentSource")] string? CurrentSource,
    [property: JsonPropertyName("lastRunStartedAt")] DateTime? LastRunStartedAt,
    [property: JsonPropertyName("lastRunFinishedAt")] DateTime? LastRunFinishedAt,
    [property: JsonPropertyName("lastResult")] BatchCrawlResult? LastResult,
    [property: JsonPropertyName("lastError")] string? LastError);
