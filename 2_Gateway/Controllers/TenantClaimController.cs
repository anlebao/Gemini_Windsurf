using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using VanAn.CoreHub.Services.Claims;
using VanAn.CoreHub.Services.Onboarding;

namespace VanAn.Gateway.Controllers
{
    /// <summary>
    /// Crawl-to-Onboard Pipeline (2026-08-25): Claim request endpoints.
    /// Split auth:
    /// - POST /api/v1/tenants/{tenantId}/claims — [AllowAnonymous] + rate-limited (owner submits claim)
    /// - GET /api/v1/claims + GET /{id} + POST /approve + POST /reject — [Authorize(Policy="SystemAdmin")]
    /// </summary>
    [ApiController]
    [Authorize(Policy = "SystemAdmin")]
    public class TenantClaimController(
        ITenantClaimService claimService,
        ILogger<TenantClaimController> logger) : ControllerBase
    {
        // ── Owner submits claim (AllowAnonymous + rate-limited) ───────────────

        /// <summary>
        /// Owner submits a claim for a Pending tenant (GPKD upload + claimant info).
        /// Rate-limited: 3 requests per IP per day (policy "claim-submit" — configured in Program.cs).
        /// Returns 409 if tenant is Active ("already verified") or not Pending.
        /// </summary>
        [HttpPost("api/v1/tenants/{tenantId:guid}/claims")]
        [AllowAnonymous]
        [EnableRateLimiting("claim-submit")]
        public async Task<ActionResult<ClaimSubmitResult>> SubmitClaim(
            Guid tenantId,
            [FromBody] SubmitClaimRequest request,
            CancellationToken ct = default)
        {
            try
            {
                var claimId = await claimService.SubmitClaimAsync(tenantId, request, ct);
                logger.LogInformation("Claim submitted for tenant {TenantId} — claim {ClaimId}", tenantId, claimId);
                return Ok(new ClaimSubmitResult(claimId, "Cảm ơn! Yêu cầu xác nhận đã gửi. Chúng tôi sẽ liên hệ trong 3-5 ngày làm việc."));
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

        // ── SysAdmin endpoints (SystemAdmin policy) ──────────────────────────

        /// <summary>List all Submitted claims (SysAdmin queue).</summary>
        [HttpGet("api/v1/claims")]
        [Authorize(Policy = "SystemAdmin")]
        public async Task<ActionResult<List<ClaimDto>>> ListPendingClaims(CancellationToken ct = default)
            => Ok(await claimService.ListPendingClaimsAsync(ct));

        /// <summary>Get a single claim by ID (SysAdmin detail view).</summary>
        [HttpGet("api/v1/claims/{claimId:guid}")]
        [Authorize(Policy = "SystemAdmin")]
        public async Task<ActionResult<ClaimDto>> GetClaim(Guid claimId, CancellationToken ct = default)
        {
            var claim = await claimService.GetClaimAsync(claimId, ct);
            return claim is null ? NotFound() : Ok(claim);
        }

        /// <summary>
        /// SysAdmin approves a claim → calls VerifyAsync (creates owner user + groups + Activate)
        /// + marks claim as Approved. Returns VerifyResult with credentials (SHOWN ONCE — copy immediately).
        /// </summary>
        [HttpPost("api/v1/claims/{claimId:guid}/approve")]
        [Authorize(Policy = "SystemAdmin")]
        public async Task<ActionResult<ClaimApprovalResult>> ApproveClaim(
            Guid claimId,
            [FromBody] ApproveClaimRequest request,
            CancellationToken ct = default)
        {
            var sysAdminUserId = GetSysAdminUserId();
            if (sysAdminUserId == Guid.Empty)
                return Unauthorized(new { error = "SysAdmin user ID not found in JWT claims." });

            try
            {
                var result = await claimService.ApproveClaimAsync(claimId, request.VerifyConfig, sysAdminUserId, ct);
                logger.LogInformation(
                    "Claim {ClaimId} approved — tenant {TenantId} verified, owner user {OwnerUserId}",
                    claimId, result.TenantId, result.OwnerUserId);

                // Credentials shown ONCE — SysAdmin must copy immediately
                return Ok(new ClaimApprovalResult(
                    result.TenantId,
                    result.OwnerUserId,
                    result.PermissionGroupsCreated,
                    result.PublishedSlug,
                    request.VerifyConfig.OwnerUsername,
                    request.VerifyConfig.OwnerPassword,
                    "⚠️ Sao chép credentials ngay — sẽ không hiển thị lại."));
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

        /// <summary>SysAdmin rejects a claim with reason.</summary>
        [HttpPost("api/v1/claims/{claimId:guid}/reject")]
        [Authorize(Policy = "SystemAdmin")]
        public async Task<IActionResult> RejectClaim(
            Guid claimId,
            [FromBody] RejectClaimRequest request,
            CancellationToken ct = default)
        {
            var sysAdminUserId = GetSysAdminUserId();
            if (sysAdminUserId == Guid.Empty)
                return Unauthorized(new { error = "SysAdmin user ID not found in JWT claims." });

            try
            {
                await claimService.RejectClaimAsync(claimId, request.Reason, sysAdminUserId, ct);
                return NoContent();
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

        // ── Helpers ──────────────────────────────────────────────────────────

        private Guid GetSysAdminUserId()
        {
            var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            return Guid.TryParse(userIdClaim, out var userId) ? userId : Guid.Empty;
        }
    }

    // ── DTOs ────────────────────────────────────────────────────────────────

    public record ClaimSubmitResult(Guid ClaimId, string Message);

    public record ApproveClaimRequest(VerifyTenantRequest VerifyConfig);

    public record RejectClaimRequest(string Reason);

    /// <summary>
    /// Claim approval result with credentials (SHOWN ONCE).
    /// SysAdmin must copy OwnerUsername + OwnerPassword immediately — not retrievable after.
    /// </summary>
    public record ClaimApprovalResult(
        Guid TenantId,
        Guid OwnerUserId,
        int PermissionGroupsCreated,
        string PublishedSlug,
        string OwnerUsername,
        string OwnerPassword,
        string Warning);
}
