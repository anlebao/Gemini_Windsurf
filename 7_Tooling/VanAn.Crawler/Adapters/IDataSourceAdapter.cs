using VanAn.Crawler.Dtos;

namespace VanAn.Crawler.Adapters;

/// <summary>
/// Abstraction for data source adapters (REST API, HTML scraping, etc.).
/// Each adapter fetches business listings from one source.
/// </summary>
public interface IDataSourceAdapter
{
    /// <summary>Source name (e.g., "doanhnghiep.vn", "trangvangvietnam").</summary>
    string Name { get; }

    /// <summary>Fetch business listings matching the query.</summary>
    Task<List<CrawlListingDto>> FetchAsync(CrawlQuery query, CancellationToken ct = default);
}
