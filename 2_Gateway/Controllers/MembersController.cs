using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VanAn.CoreHub.Services.Membership;
using VanAn.Gateway.Services;

namespace VanAn.Gateway.Controllers
{
    /// <summary>
    /// Membership Infrastructure (2026-09-29): Sổ đăng ký thành viên điện tử (SRS §17, §22, §31).
    ///
    /// Auth:
    /// - Officer endpoints (list/get/lifecycle): Policy "HtxMembershipOfficer" + tenant_id claim (IDOR safe).
    /// - Public status verify: AllowAnonymous — tra theo MemberNumber (token từ QR), KHÔNG PII (SRS §22).
    /// - MyMemberships: X-Customer-Token — "HTX của tôi" (multi-HTX, cross-tenant read có chủ đích).
    /// </summary>
    [ApiController]
    [Route("api/membership/members")]
    public class MembersController(
        IMemberRegistryService registryService,
        IHttpClientFactory httpClientFactory,
        ILogger<MembersController> logger) : ControllerBase
    {
        // ── Officer ───────────────────────────────────────────────────────────

        /// <summary>Danh sách thành viên của HTX (SRS §31 — GET /api/htx/{htxId}/members). Tenant từ JWT.</summary>
        [HttpGet]
        [Authorize(Policy = "HtxMembershipOfficer")]
        public async Task<IActionResult> List([FromQuery] string? status = null)
        {
            var tenantId = GetTenantIdFromClaim();
            if (tenantId == Guid.Empty)
                return Unauthorized(new { error = "Missing or invalid tenant_id claim." });

            var list = await registryService.ListForHtxAsync(tenantId, status, HttpContext.RequestAborted);
            return Ok(list);
        }

        /// <summary>Chi tiết 1 member (officer). IDOR guard: member thuộc tenant của officer.</summary>
        [HttpGet("{memberId:guid}")]
        [Authorize(Policy = "HtxMembershipOfficer")]
        public async Task<IActionResult> GetById(Guid memberId)
        {
            var tenantId = GetTenantIdFromClaim();
            if (tenantId == Guid.Empty)
                return Unauthorized(new { error = "Missing or invalid tenant_id claim." });

            var member = await registryService.GetAsync(memberId, HttpContext.RequestAborted);
            if (member is null) return NotFound(new { error = $"Member {memberId} not found." });
            if (member.HtxTenantId != tenantId)
                return Forbid();

            return Ok(member);
        }

        /// <summary>Active → Suspended (tạm đình chỉ).</summary>
        [HttpPost("{memberId:guid}/suspend")]
        [Authorize(Policy = "HtxMembershipOfficer")]
        public async Task<IActionResult> Suspend(Guid memberId, [FromBody] MemberLifecycleRequest request)
        {
            return await ExecuteLifecycleAsync(memberId, request.Reason,
                (id, reason, ct) => registryService.SuspendAsync(id, reason, ct));
        }

        /// <summary>Suspended → Active (khôi phục).</summary>
        [HttpPost("{memberId:guid}/reactivate")]
        [Authorize(Policy = "HtxMembershipOfficer")]
        public async Task<IActionResult> Reactivate(Guid memberId)
        {
            return await ExecuteLifecycleAsync(memberId, "Reactivated by HTX officer",
                (id, _, ct) => registryService.ReactivateAsync(id, ct));
        }

        /// <summary>Active/Suspended → Resigned (tự rút).</summary>
        [HttpPost("{memberId:guid}/resign")]
        [Authorize(Policy = "HtxMembershipOfficer")]
        public async Task<IActionResult> Resign(Guid memberId, [FromBody] MemberLifecycleRequest request)
        {
            return await ExecuteLifecycleAsync(memberId, request.Reason,
                (id, reason, ct) => registryService.ResignAsync(id, reason, ct));
        }

        /// <summary>Active/Suspended → Terminated (chấm dứt tư cách).</summary>
        [HttpPost("{memberId:guid}/terminate")]
        [Authorize(Policy = "HtxMembershipOfficer")]
        public async Task<IActionResult> Terminate(Guid memberId, [FromBody] MemberLifecycleRequest request)
        {
            return await ExecuteLifecycleAsync(memberId, request.Reason,
                (id, reason, ct) => registryService.TerminateAsync(id, reason, ct));
        }

        // ── Public verify (SRS §22) ───────────────────────────────────────────

        /// <summary>
        /// Quét Member QR → status. KHÔNG PII: Active flag + status + member number + HTX name + joined.
        /// Cross-tenant read có chủ đích — bất kỳ ai quét QR đều xem được status, không xem được dữ liệu riêng tư.
        /// </summary>
        [HttpGet("status/{memberNumber}")]
        [AllowAnonymous]
        public async Task<IActionResult> VerifyStatus(string memberNumber)
        {
            var status = await registryService.GetStatusAsync(memberNumber, HttpContext.RequestAborted);
            if (status is null) return NotFound(new { error = "Member không tồn tại." });
            return Ok(status);
        }

        // ── Applicant / member ────────────────────────────────────────────────

        /// <summary>"HTX của tôi" — network identity xem các HTX đang tham gia (multi-HTX — SRS §19).</summary>
        [HttpGet("my")]
        [AllowAnonymous]
        public async Task<IActionResult> MyMemberships()
        {
            if (!Request.Headers.TryGetValue("X-Customer-Token", out var token) || string.IsNullOrEmpty(token))
                return Unauthorized(new { error = "X-Customer-Token header is required." });

            var (status, customerId) = await CustomerTokenValidationHelper.ValidateAsync(
                httpClientFactory, token.ToString(), HttpContext.RequestAborted);

            if (status == CustomerTokenValidationStatus.InvalidToken)
                return Unauthorized(new { error = "Token không hợp lệ hoặc đã hết hạn." });
            if (status != CustomerTokenValidationStatus.Valid || customerId is null)
                return StatusCode(503, new { error = "Dịch vụ đang bảo trì, vui lòng thử lại sau." });

            var list = await registryService.ListForCustomerAsync(customerId.Value, HttpContext.RequestAborted);
            return Ok(list);
        }

        // ── Helpers ──────────────────────────────────────────────────────────

        private async Task<IActionResult> ExecuteLifecycleAsync(
            Guid memberId,
            string reason,
            Func<Guid, string, CancellationToken, Task> action)
        {
            var tenantId = GetTenantIdFromClaim();
            if (tenantId == Guid.Empty)
                return Unauthorized(new { error = "Missing or invalid tenant_id claim." });

            var member = await registryService.GetAsync(memberId, HttpContext.RequestAborted);
            if (member is null) return NotFound(new { error = $"Member {memberId} not found." });
            if (member.HtxTenantId != tenantId)
                return Forbid();

            try
            {
                await action(memberId, reason, HttpContext.RequestAborted);
                return NoContent();
            }
            catch (InvalidOperationException ex)
            {
                return Conflict(new { error = ex.Message });
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }

        private Guid GetTenantIdFromClaim()
        {
            var claim = User.FindFirst("tenant_id")?.Value ?? User.FindFirst("tenantid")?.Value;
            return Guid.TryParse(claim, out var tenantId) ? tenantId : Guid.Empty;
        }
    }

    /// <summary>Reason bắt buộc cho lifecycle transition (audit trail — SRS §28).</summary>
    public record MemberLifecycleRequest(string Reason);
}
