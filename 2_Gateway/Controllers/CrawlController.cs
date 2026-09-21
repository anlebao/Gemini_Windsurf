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
        /// 2026-09-21 feature: batch import + OPTIONAL auto-activate (Pending → Active).
        /// Same dedup/skip semantics as POST /batch, but when ActivateImmediately=true each
        /// imported tenant is verified (Pending → Active) with AUTO-GENERATED owner credentials
        /// (returned once in the response for the SysAdmin to hand over). Used by the MST
        /// registration flow ("Kích hoạt ngay").
        /// </summary>
        [HttpPost("batch-import")]
        public async Task<ActionResult<CrawlBatchImportResult>> PostBatchImport(
            [FromBody] CrawlBatchImportRequest request,
            CancellationToken ct = default)
        {
            if (request.Listings is null || request.Listings.Count == 0)
                return BadRequest("Listings list is empty.");

            if (request.Listings.Count > 500)
                return BadRequest("Max 500 listings per batch.");

            var adminId = GetAdminUserId();
            var imported = 0;
            var skipped = 0;
            var errors = new List<BatchCrawlError>();
            var activated = new List<ActivatedTenantCredential>();

            foreach (var listing in request.Listings)
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

                    var tenantId = await onboardingService.OnboardUnverifiedAsync(listing, ct);
                    imported++;

                    if (request.ActivateImmediately)
                    {
                        try
                        {
                            var username = $"owner{listing.TaxCode ?? Guid.NewGuid().ToString("N")[..6]}";
                            var password = GenerateOwnerPassword();
                            var verifyResult = await onboardingService.VerifyAsync(tenantId, new VerifyTenantRequest(
                                OwnerUsername: username,
                                OwnerPassword: password,
                                OwnerDisplayName: listing.Name.Length > 80 ? listing.Name[..80] : listing.Name,
                                ApprovedByUserId: adminId), ct);
                            activated.Add(new ActivatedTenantCredential(
                                listing.TaxCode ?? "",
                                tenantId,
                                username,
                                password,
                                verifyResult.PublishedSlug));
                            logger.LogInformation(
                                "Batch-import activated tenant {TenantId} ({Name}) — owner {Username}",
                                tenantId, listing.Name, username);
                        }
                        catch (Exception verifyEx)
                        {
                            logger.LogWarning(verifyEx,
                                "Batch-import: tenant {TenantId} created as Pending but activation failed",
                                tenantId);
                            errors.Add(new BatchCrawlError(
                                listing.TaxCode ?? listing.Name,
                                $"Đã tạo Pending nhưng kích hoạt thất bại: {verifyEx.Message}"));
                        }
                    }
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
                "Crawl batch-import: imported {Imported}, skipped {Skipped}, activated {Activated}, errors {Errors}",
                imported, skipped, activated.Count, errors.Count);

            return Ok(new CrawlBatchImportResult(imported, skipped, errors, activated));
        }

        private static string GenerateOwnerPassword() =>
            $"VanAn{Convert.ToBase64String(Guid.NewGuid().ToByteArray())[..12].Replace('+', 'x').Replace('/', 'y').Replace('=', 'z')}!2026";

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
            // 2026-09-21 feature: tax-code mode (đăng ký tenant bằng mã số thuế) — validate MST list.
            if (request.TaxCodes is { Count: > 0 })
            {
                if (request.TaxCodes.Count > 500)
                    return BadRequest(new { error = "Tối đa 500 mã số thuế mỗi lần." });

                var cleaned = request.TaxCodes
                    .Select(t => t.Trim())
                    .Where(t => t.Length > 0)
                    .Distinct()
                    .ToList();
                var invalid = cleaned.Where(t => !IsValidTaxCode(t)).ToList();
                if (invalid.Count > 0)
                    return BadRequest(new { error = $"Mã số thuế không hợp lệ (phải 10 hoặc 13 chữ số): {string.Join(", ", invalid.Take(5))}" });

                request = request with { TaxCodes = cleaned };
            }

            logger.LogInformation(
                "Crawl trigger requested: source={Source}, industry={Industry}, province={Province}, maxResults={MaxResults}, taxCodes={TaxCodeCount}",
                request.Source, request.Industry, request.Province, request.MaxResults, request.TaxCodes?.Count ?? 0);

            // Forward to crawler worker via HttpClient (not YARP — YARP is for catch-all routes only)
            try
            {
                var crawlerClient = httpClientFactory.CreateClient("crawler");
                // Use explicit JSON options to ensure crawler receives camelCase matching its
                // minimal API default (JsonSerializerDefaults.Web). Anonymous object props are
                // PascalCase — PostAsJsonAsync default is camelCase, but be explicit to be safe.
                var jsonOpts = new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web);
                var crawlerRequest = System.Text.Json.JsonSerializer.Serialize(new
                {
                    source = request.Source,
                    industry = request.Industry,
                    province = request.Province,
                    maxResults = request.MaxResults,
                    searchTerm = request.SearchTerm,
                    taxCodes = request.TaxCodes,
                    activateImmediately = request.ActivateImmediately
                }, jsonOpts);

                // Fire-and-forget: don't block SysAdmin while crawler runs (can take minutes)
                _ = Task.Run(async () =>
                {
                    try
                    {
                        logger.LogInformation("Forwarding crawl trigger to crawler:5010/trigger — body: {Body}", crawlerRequest);
                        var content = new StringContent(crawlerRequest, System.Text.Encoding.UTF8, "application/json");
                        var resp = await crawlerClient.PostAsync("/trigger", content);
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

        private Guid GetAdminUserId()
        {
            var userIdClaim = User.FindFirst("sub")?.Value
                ?? User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
                ?? User.FindFirst("userId")?.Value;
            return Guid.TryParse(userIdClaim, out var id) ? id : Guid.Empty;
        }

        /// <summary>Vietnamese tax code: 10 digits, optionally 13 digits with -xxx branch suffix (13 digits).</summary>
        private static bool IsValidTaxCode(string taxCode)
        {
            if (string.IsNullOrWhiteSpace(taxCode)) return false;
            var digits = taxCode.Replace("-", "").Trim();
            return (digits.Length == 10 || digits.Length == 13) && digits.All(char.IsDigit);
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
        string? SearchTerm = null, // Search term for business name (e.g., "nhà hàng"). Null = use Industry or default.
        List<string>? TaxCodes = null, // 2026-09-21: MST list — register tenant(s) by tax code (findUnique).
        bool ActivateImmediately = false); // 2026-09-21: true = auto-verify Pending → Active + auto owner credentials.

    /// <summary>2026-09-21: batch-import request (MST flow with optional auto-activation).</summary>
    public record CrawlBatchImportRequest(
        List<CrawlListingDto> Listings,
        bool ActivateImmediately = false);

    /// <summary>2026-09-21: batch-import result with auto-generated owner credentials (shown once).</summary>
    public record CrawlBatchImportResult(
        int Imported,
        int Skipped,
        List<BatchCrawlError> Errors,
        List<ActivatedTenantCredential> Activated);

    /// <summary>Auto-generated owner credentials for an activated tenant (returned once).</summary>
    public record ActivatedTenantCredential(
        string TaxCode,
        Guid TenantId,
        string Username,
        string Password,
        string Slug);
}
