using VanAn.Shared.Domain.Common;

namespace VanAn.Shared.Domain.Aggregates.TenantAggregate
{
    /// <summary>
    /// Crawl-to-Onboard Pipeline (2026-08-25): Audit trail record for crawled business listing.
    /// Tracks which source provided the data for each Pending tenant (provenance for legal
    /// compliance + duplicate investigation).
    ///
    /// Single-Identity Pattern (correction C1): FK to Tenants is via BaseEntity.TenantId
    /// (TenantId value object — after refactor, TenantId.Value == Tenants.Id PK).
    /// No separate Guid TenantId property — BaseEntity.TenantId IS the FK.
    ///
    /// Not an aggregate root (no domain events, no lifecycle transitions) —
    /// simple audit entity inheriting BaseEntity for multi-tenancy + audit fields.
    /// </summary>
    public class CrawlSource : BaseEntity
    {
        /// <summary>Source site name (e.g., "doanhnghiep.vn", "trangvangvietnam", "xinvoice.vn").</summary>
        public string SourceSite { get; private set; } = string.Empty;

        /// <summary>Full URL of the crawled listing (for audit + re-verification).</summary>
        public string SourceUrl { get; private set; } = string.Empty;

        /// <summary>Raw JSON response from API or scraped HTML (for audit + data provenance).</summary>
        public string RawJson { get; private set; } = string.Empty;

        /// <summary>Timestamp when the crawl occurred.</summary>
        public DateTime CrawledAt { get; private set; }

        // EF Core requires parameterless constructor
        private CrawlSource() { }

        /// <summary>
        /// Factory: create a new CrawlSource audit record.
        /// </summary>
        /// <param name="tenantId">The Pending tenant created from this crawl (FK via BaseEntity.TenantId).</param>
        /// <param name="sourceSite">Source site name (e.g., "doanhnghiep.vn").</param>
        /// <param name="sourceUrl">Full URL of the crawled listing.</param>
        /// <param name="rawJson">Raw JSON/HTML response from the crawl.</param>
        public static CrawlSource Create(
            TenantId tenantId,
            string sourceSite,
            string sourceUrl,
            string rawJson)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(sourceSite);
            ArgumentException.ThrowIfNullOrWhiteSpace(sourceUrl);
            ArgumentException.ThrowIfNullOrWhiteSpace(rawJson);

            var source = new CrawlSource
            {
                SourceSite = sourceSite,
                SourceUrl = sourceUrl,
                RawJson = rawJson,
                CrawledAt = DateTime.UtcNow
            };
            source.SetTenantId(tenantId);  // BaseEntity.TenantId — FK to Tenants.Id
            return source;
        }
    }
}
