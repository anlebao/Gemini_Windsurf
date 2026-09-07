using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using VanAn.CoreHub.Infrastructure;
using VanAn.Shared.Domain;
using VanAn.Shared.Domain.Aggregates.KhachLinkAggregate;
using VanAn.Shared.Domain.Aggregates.TenantAggregate;
using Tenant = VanAn.Shared.Domain.Aggregates.TenantAggregate.Tenant;

namespace VanAn.Gateway.Controllers
{
    /// <summary>
    /// GTM Drill Machine W1 (2026-09-06): public Merchant Audit — "Kiểm tra cửa hàng".
    /// Anonymous endpoint backing the Directory landing /kiem-tra-cua-hang.
    ///
    /// Ground Rule 5 (docs/AI/plans/ecosystem-master-business-model.md): report shows ONLY
    /// measured data — directory presence, storefront, social links, KhachLink domain,
    /// industry peer count from crawl data. NO estimated/fabricated demand numbers.
    ///
    /// Match: MST exact (Settings.TaxCode — DuplicateDetectionService precedent) OR
    /// name ILIKE (exact first, then contains — TenantStoreController.Search pattern).
    /// Input SĐT/FB link deferred to v2 (CrawledPhone internal-only per M3).
    ///
    /// Pending tenants (M3): name + slug only — no address/phone/social/logo
    /// per Luật 91/2025 Điều 16 (same rule as TenantStoreController.MapToStoreDto).
    ///
    /// Rate limit (growth-audit, 10/IP/hour): Directory SSR calls this server-side and
    /// forwards the end-user IP via X-Forwarded-For — UseForwardedHeaders middleware
    /// rewrites RemoteIpAddress to the real client, so the partition key is correct.
    /// </summary>
    [ApiController]
    [Route("api/v1/growth")]
    public class GrowthController(
        IVanAnDbContext dbContext,
        ILogger<GrowthController> logger) : ControllerBase
    {
        private readonly IVanAnDbContext _dbContext = dbContext;
        private readonly ILogger<GrowthController> _logger = logger;

        /// <summary>
        /// Audit a merchant by name or MST. Returns measured presence report.
        /// 400 when both params empty. 200 with Found=false when no match (landing shows not-found state).
        /// </summary>
        [HttpGet("audit")]
        [AllowAnonymous]
        [EnableRateLimiting("growth-audit")]
        public async Task<ActionResult<MerchantAuditDto>> Audit([FromQuery] string? name, [FromQuery] string? mst)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(name) && string.IsNullOrWhiteSpace(mst))
                    return BadRequest(new { message = "Vui lòng nhập tên cửa hàng hoặc mã số thuế." });

                var baseQuery = _dbContext.Tenants
                    .AsNoTracking()
                    .IgnoreQueryFilters()
                    .Where(t => t.Status == TenantStatus.Active || t.Status == TenantStatus.Pending);

                Tenant? tenant = null;

                // MST exact match (public business data — DuplicateDetectionService precedent)
                if (!string.IsNullOrWhiteSpace(mst))
                {
                    var code = mst.Trim();
                    tenant = await baseQuery.FirstOrDefaultAsync(t => t.Settings.TaxCode == code);
                }

                // Name match: case-insensitive exact first, then contains (shortest name = closest)
                if (tenant is null && !string.IsNullOrWhiteSpace(name))
                {
                    var q = name.Trim();
                    tenant = await baseQuery.FirstOrDefaultAsync(t => EF.Functions.ILike(t.Name, q))
                        ?? await baseQuery
                            .Where(t => EF.Functions.ILike(t.Name, $"%{q}%"))
                            .OrderBy(t => t.Name.Length)
                            .FirstOrDefaultAsync();
                }

                if (tenant is null)
                {
                    return Ok(new MerchantAuditDto { Found = false });
                }

                var isPending = tenant.Status == TenantStatus.Pending;
                var industry = tenant.Settings?.BusinessField;

                // Industry peer count — measured from crawl data (Active + Pending, excluding self).
                // Null when industry unknown — never fabricated (Ground Rule 5).
                int? industryPeerCount = null;
                if (!string.IsNullOrWhiteSpace(industry))
                {
                    var ind = industry.Trim();
                    industryPeerCount = await _dbContext.Tenants
                        .AsNoTracking()
                        .IgnoreQueryFilters()
                        .Where(t => (t.Status == TenantStatus.Active || t.Status == TenantStatus.Pending)
                                    && t.Id != tenant.Id
                                    && EF.Functions.ILike(t.Settings.BusinessField, $"%{ind}%"))
                        .CountAsync();
                }

                // KhachLink storefront domain (Active only — Pending has no commerce domain).
                // FullCommerce=0 preferred over Reseller=4 (same rule as BuildKhachLinkDomainMapAsync).
                string? khachLinkDomain = null;
                if (!isPending)
                {
                    khachLinkDomain = await _dbContext.KhachLinkInstances
                        .AsNoTracking()
                        .IgnoreQueryFilters()
                        // tenant.Id.Value is on the CONSTANT side (in-memory value → SQL parameter) —
                        // safe for translation, unlike entity-side value object member access (Pattern #8).
                        .Where(i => i.OwnerTenantId == tenant.Id.Value
                                    && i.IsActive
                                    && i.Profile != KhachLinkProfile.Directory)
                        .OrderBy(i => i.Profile)
                        .Select(i => i.CustomDomain)
                        .FirstOrDefaultAsync();
                }

                return Ok(new MerchantAuditDto
                {
                    Found = true,
                    Tenant = new MerchantAuditTenantDto
                    {
                        Id = tenant.Id.Value,
                        Name = tenant.Name,
                        IsPending = isPending,
                        Slug = tenant.Settings?.Slug,
                        Industry = industry,
                        // Measured checklist (Ground Rule 5)
                        HasStorefront = !string.IsNullOrEmpty(tenant.Settings?.Slug),
                        HasKhachLinkDomain = khachLinkDomain != null,
                        KhachLinkDomain = khachLinkDomain,
                        HasSocialLinks = !isPending
                            && (!string.IsNullOrEmpty(tenant.Settings?.SocialLinksFb)
                                || !string.IsNullOrEmpty(tenant.Settings?.SocialLinksTiktok)),
                        // M3: Pending hides social links (same rule as MapToStoreDto)
                        SocialLinksFb = isPending ? null : tenant.Settings?.SocialLinksFb,
                        SocialLinksTiktok = isPending ? null : tenant.Settings?.SocialLinksTiktok
                    },
                    IndustryPeerCount = industryPeerCount,
                    // Claim flow lives on KhachLink (browser → Gateway: correct client-IP rate limit)
                    ClaimUrl = isPending && !string.IsNullOrEmpty(tenant.Settings?.Slug)
                        ? $"/store/{tenant.Settings.Slug}/claim"
                        : null,
                    // Self-serve signup — reserved for W2 Interactive Demo (null in W1)
                    RegisterUrl = null
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error auditing merchant (name={Name}, mst={Mst})", name, mst);
                return StatusCode(500, "Internal server error");
            }
        }
    }

    /// <summary>
    /// Merchant audit report — measured presence data only (Ground Rule 5).
    /// Mirrored by 5_WebApps/Directory/Services/GrowthAuditService.cs.
    /// </summary>
    public record MerchantAuditDto
    {
        public bool Found { get; init; }

        public MerchantAuditTenantDto? Tenant { get; init; }

        /// <summary>Count of OTHER Active+Pending tenants with matching BusinessField
        /// (measured from crawl data). Null when industry unknown — never fabricated.</summary>
        public int? IndustryPeerCount { get; init; }

        /// <summary>KhachLink claim URL for Pending tenants ("/store/{slug}/claim"). Null otherwise.</summary>
        public string? ClaimUrl { get; init; }

        /// <summary>Self-serve signup URL — reserved for W2 Interactive Demo. Null in W1.</summary>
        public string? RegisterUrl { get; init; }
    }

    public record MerchantAuditTenantDto
    {
        public Guid Id { get; init; }
        public string Name { get; init; } = string.Empty;

        /// <summary>M3: Pending = crawled, not yet verified. Landing shows claim CTA.</summary>
        public bool IsPending { get; init; }

        public string? Slug { get; init; }

        /// <summary>Crawled industry (Settings.BusinessField). Null when unknown.</summary>
        public string? Industry { get; init; }

        public bool HasStorefront { get; init; }
        public bool HasKhachLinkDomain { get; init; }
        public string? KhachLinkDomain { get; init; }
        public bool HasSocialLinks { get; init; }
        public string? SocialLinksFb { get; init; }
        public string? SocialLinksTiktok { get; init; }
    }
}
