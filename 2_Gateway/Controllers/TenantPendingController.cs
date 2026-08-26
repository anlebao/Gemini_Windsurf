using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VanAn.CoreHub.Infrastructure;
using VanAn.CoreHub.Services;
using VanAn.CoreHub.Services.Onboarding;
using VanAn.Shared.Domain;
using VanAn.Shared.Domain.Aggregates.TenantAggregate;

namespace VanAn.Gateway.Controllers
{
    /// <summary>
    /// Crawl-to-Onboard Pipeline (2026-08-25): Pending tenant management + duplicate resolution.
    /// SysAdmin-only — list Pending tenants, direct verify (bypass claim flow), list/resolve duplicates.
    /// </summary>
    [ApiController]
    [Authorize(Policy = "SystemAdmin")]
    public class TenantPendingController(
        IVanAnDbContext dbContext,
        ITenantOnboardingService onboardingService,
        IDuplicateDetectionService duplicateService,
        ILogger<TenantPendingController> logger) : ControllerBase
    {
        // ── Pending tenants ──────────────────────────────────────────────────

        /// <summary>List all Pending tenants (crawled, not yet verified).</summary>
        [HttpGet("api/v1/tenants/pending")]
        public async Task<ActionResult<List<PendingTenantDto>>> ListPending(CancellationToken ct = default)
        {
            var tenants = await dbContext.Tenants
                .IgnoreQueryFilters()
                .AsNoTracking()
                .Where(t => t.Status == TenantStatus.Pending)
                .OrderByDescending(t => t.CreatedAt)
                .ToListAsync(ct);

            return Ok(tenants.Select(t => new PendingTenantDto(
                t.Id.Value,
                t.Name,
                t.Settings?.TaxCode,
                t.Settings?.Address,
                t.PotentialDuplicateOf,
                t.CreatedAt)).ToList());
        }

        /// <summary>
        /// Direct verify (bypass claim flow) — SysAdmin directly creates admin user for Pending tenant.
        /// Used when SysAdmin knows the owner personally + doesn't need GPKD upload verification.
        /// </summary>
        [HttpPost("api/v1/tenants/{tenantId:guid}/verify")]
        public async Task<ActionResult<VanAn.CoreHub.Services.Onboarding.VerifyResult>> DirectVerify(
            Guid tenantId,
            [FromBody] VerifyTenantRequest request,
            CancellationToken ct = default)
        {
            var sysAdminUserId = GetSysAdminUserId();
            if (sysAdminUserId == Guid.Empty)
                return Unauthorized(new { error = "SysAdmin user ID not found in JWT claims." });

            try
            {
                request = request with { ApprovedByUserId = sysAdminUserId };
                var result = await onboardingService.VerifyAsync(tenantId, request, ct);
                logger.LogInformation(
                    "Direct verify tenant {TenantId} by SysAdmin {SysAdminUserId} — owner user {OwnerUserId}",
                    tenantId, sysAdminUserId, result.OwnerUserId);
                return Ok(result);
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new { error = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                return Conflict(new { error = ex.Message });
            }
        }

        // ── Duplicates ───────────────────────────────────────────────────────

        /// <summary>List all tenants with PotentialDuplicateOf != null (for Duplicates tab).</summary>
        [HttpGet("api/v1/tenants/duplicates")]
        public async Task<ActionResult<List<DuplicateTenantDto>>> ListDuplicates(CancellationToken ct = default)
            => Ok(await duplicateService.ListPotentialDuplicatesAsync(ct));

        /// <summary>
        /// Resolve duplicate: verify the "keep" tenant, deactivate the "other" tenant.
        /// NO data merge — just lifecycle transitions.
        /// </summary>
        [HttpPost("api/v1/tenants/duplicates/resolve")]
        public async Task<IActionResult> ResolveDuplicate(
            [FromBody] ResolveDuplicateRequest request,
            CancellationToken ct = default)
        {
            try
            {
                await duplicateService.ResolveDuplicateAsync(
                    request.KeepTenantId, request.DeactivateTenantId, request.Reason, ct);
                logger.LogInformation(
                    "Duplicate resolved: kept {KeepTenantId}, deactivated {DeactivateTenantId}",
                    request.KeepTenantId, request.DeactivateTenantId);
                return NoContent();
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new { error = ex.Message });
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }

        // ── Helpers ──────────────────────────────────────────────────────────

        private Guid GetSysAdminUserId()
        {
            // Gateway JWT config: MapInboundClaims=false → claims stay as short-form names ("sub"),
            // NOT mapped to ClaimTypes.NameIdentifier (long URI). Must read "sub" first.
            var userIdClaim = User.FindFirst("sub")?.Value
                ?? User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            return Guid.TryParse(userIdClaim, out var userId) ? userId : Guid.Empty;
        }
    }

    // ── DTOs ────────────────────────────────────────────────────────────────

    public record PendingTenantDto(
        Guid Id,
        string Name,
        string? TaxCode,
        string? Address,
        Guid? PotentialDuplicateOf,
        DateTime CreatedAt);

    public record ResolveDuplicateRequest(Guid KeepTenantId, Guid DeactivateTenantId, string Reason);
}
