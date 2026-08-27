using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Net.Http.Json;
using VanAn.CoreHub.Infrastructure;
using VanAn.CoreHub.Services.Onboarding;
using VanAn.Shared.Domain.Aggregates.TenantAggregate;

namespace VanAn.Gateway.Controllers
{
    /// <summary>
    /// Crawl-to-Onboard Pipeline (2026-08-25): Crawl batch endpoint + audit trail.
    /// SysAdmin-only — accepts crawled business listings from crawler worker, creates Pending tenants.
    /// Crawler worker (Phase 5) authenticates via JWT (POST /api/platform/login) + posts to this endpoint.
    /// </summary>
    [ApiController]
    [Route("api/v1/crawl")]
    [Authorize(Policy = "SystemAdmin")]
    public class CrawlController(
        IVanAnDbContext dbContext,
        ITenantOnboardingService onboardingService,
        IHttpClientFactory httpClientFactory,
        ILogger<CrawlController> logger) : ControllerBase
    {
        /// <summary>
        /// Batch import crawled business listings → Pending tenants.
        /// Crawler worker posts List<CrawlListingDto> from doanhnghiep.vn + trangvangvietnam.
        /// Returns counts: Imported (new Pending), Skipped (already existed), Errors (failed).
        /// </summary>
        [HttpPost("batch")]
        public async Task<ActionResult<BatchCrawlResult>> PostBatch(
            [FromBody] List<CrawlListingDto> listings,
            CancellationToken ct = default)
        {
            if (listings is null || listings.Count == 0)
                return BadRequest("Listings list is empty.");

            if (listings.Count > 500)
                return BadRequest("Max 500 listings per batch.");

            var imported = 0;
            var skipped = 0;
            var errors = new List<BatchCrawlError>();

            foreach (var listing in listings)
            {
                try
                {
                    // Skip if tenant with same MST already exists (Active OR Pending)
                    if (!string.IsNullOrWhiteSpace(listing.TaxCode))
                    {
                        var existing = await dbContext.Tenants
                            .IgnoreQueryFilters()
                            .AsNoTracking()
                            .AnyAsync(t => t.Settings.TaxCode == listing.TaxCode, ct);
                        if (existing)
                        {
                            skipped++;
                            continue;
                        }
                    }

                    await onboardingService.OnboardUnverifiedAsync(listing, ct);
                    imported++;
                }
                catch (Exception ex)
                {
                    errors.Add(new BatchCrawlError(
                        listing.TaxCode ?? listing.Name,
                        ex.Message));
                    logger.LogWarning(ex, "Failed to import listing {TaxCode}", listing.TaxCode);
                }
            }

            logger.LogInformation(
                "Crawl batch imported {Imported}, skipped {Skipped}, errors {Errors}",
                imported, skipped, errors.Count);

            return Ok(new BatchCrawlResult(imported, skipped, errors));
        }

        /// <summary>
        /// Audit trail: list CrawlSource records for a tenant (provenance).
        /// </summary>
        [HttpGet("sources/{tenantId:guid}")]
        public async Task<ActionResult<List<CrawlSourceDto>>> GetSources(
            Guid tenantId,
            CancellationToken ct = default)
        {
            var sources = await dbContext.CrawlSources
                .IgnoreQueryFilters()
                .AsNoTracking()
                .Where(s => s.TenantId.Value == tenantId)
                .OrderByDescending(s => s.CrawledAt)
                .ToListAsync(ct);

            return Ok(sources.Select(s => new CrawlSourceDto(
                s.Id,
                s.TenantId.Value,
                s.SourceSite,
                s.SourceUrl,
                s.CrawledAt)).ToList());
        }

        /// <summary>
        /// Trigger crawl run — forwards to crawler worker (http://crawler:5010/trigger).
        /// Returns 202 Accepted — crawler processes asynchronously and posts results back
        /// to POST /api/v1/crawl/batch.
        /// </summary>
        [HttpPost("trigger")]
        [ProducesResponseType(StatusCodes.Status202Accepted)]
        public async Task<IActionResult> TriggerCrawl([FromBody] CrawlTriggerRequest request)
        {
            logger.LogInformation(
                "Crawl trigger requested: source={Source}, industry={Industry}, province={Province}, maxResults={MaxResults}",
                request.Source, request.Industry, request.Province, request.MaxResults);

            // Forward to crawler worker via HttpClient (not YARP — YARP is for catch-all routes only)
            try
            {
                var crawlerClient = httpClientFactory.CreateClient("crawler");
                var crawlerRequest = new
                {
                    Source = request.Source,
                    Industry = request.Industry,
                    Province = request.Province,
                    MaxResults = request.MaxResults,
                    SearchTerm = request.SearchTerm
                };

                // Fire-and-forget: don't block SysAdmin while crawler runs (can take minutes)
                _ = Task.Run(async () =>
                {
                    try
                    {
                        logger.LogInformation("Forwarding crawl trigger to crawler:5010/trigger");
                        var resp = await crawlerClient.PostAsJsonAsync("/trigger", crawlerRequest);
                        logger.LogInformation("Crawler responded: {Status}", resp.StatusCode);
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex, "Failed to forward crawl trigger to crawler worker");
                    }
                });

                return Accepted(new { message = "Crawl trigger forwarded to crawler worker.", request });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Crawl trigger failed to start");
                return StatusCode(500, "Crawl trigger failed to start.");
            }
        }

        /// <summary>
        /// Crawl status — forwards to crawler worker (http://crawler:5010/status).
        /// Polled by ShopERP UI every 5s after trigger to show progress.
        /// Returns: { isRunning, currentPhase, currentSource, lastResult, lastError, ... }
        /// </summary>
        [HttpGet("status")]
        public async Task<IActionResult> GetCrawlStatus(CancellationToken ct = default)
        {
            try
            {
                var crawlerClient = httpClientFactory.CreateClient("crawler");
                var resp = await crawlerClient.GetAsync("/status", ct);
                if (!resp.IsSuccessStatusCode)
                    return StatusCode((int)resp.StatusCode, "Crawler status query failed.");
                var body = await resp.Content.ReadAsStringAsync(ct);
                return Content(body, "application/json");
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Crawler status query failed");
                return StatusCode(502, "Crawler worker unreachable.");
            }
        }
    }

    // ── DTOs ────────────────────────────────────────────────────────────────

    public record BatchCrawlResult(int Imported, int Skipped, List<BatchCrawlError> Errors);

    public record BatchCrawlError(string Identifier, string Error);

    public record CrawlSourceDto(Guid Id, Guid TenantId, string SourceSite, string SourceUrl, DateTime CrawledAt);

    public record CrawlTriggerRequest(
        string? Source,        // Source name (e.g., "doanhnghiep.vn", "trangvangvietnam"). Null = all.
        string? Industry,      // Industry code filter
        string? Province,      // Province filter
        int MaxResults = 100,  // Max listings to crawl (default 100, max 500)
        string? SearchTerm = null); // Search term for business name (e.g., "nhà hàng"). Null = use Industry or default.
}
