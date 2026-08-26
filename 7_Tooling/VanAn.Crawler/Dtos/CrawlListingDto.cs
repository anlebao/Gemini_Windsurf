namespace VanAn.Crawler.Dtos;

/// <summary>
/// Mirror of Gateway's CrawlListingDto — kept separate (no shared project reference).
/// Posted to Gateway POST /api/v1/crawl/batch in batches.
/// </summary>
public sealed class CrawlListingDto
{
    public string Name { get; set; } = "";
    public string? TaxCode { get; set; }
    public string? Address { get; set; }
    /// <summary>Raw crawled phone — M3 legal: internal SysAdmin use only, NOT displayed on Pending profile.</summary>
    public string? CrawledPhone { get; set; }
    public string? ContactName { get; set; }
    public string? IndustryCode { get; set; }
    public string SourceSite { get; set; } = "";
    public string SourceUrl { get; set; } = "";
    public DateTime CrawledAt { get; set; } = DateTime.UtcNow;
    public double? Lat { get; set; }
    public double? Lng { get; set; }
}

/// <summary>Query parameters for adapter FetchAsync.</summary>
public sealed class CrawlQuery
{
    public string? SearchTerm { get; set; }
    public string? IndustryCode { get; set; }
    public string? Province { get; set; }
    public int MaxResults { get; set; } = 100;
}

/// <summary>Result of a batch POST to Gateway.</summary>
public sealed record BatchCrawlResult(int Imported, int Skipped, List<BatchCrawlError> Errors);

public sealed record BatchCrawlError(string Identifier, string Error);

/// <summary>Trigger request body for POST /trigger.</summary>
public sealed record CrawlTriggerRequest(
    string? Source,
    string? Industry,
    string? Province,
    int MaxResults = 100);
