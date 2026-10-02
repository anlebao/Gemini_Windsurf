using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VanAn.CoreHub.Services.Membership;
using VanAn.Shared.Domain.Aggregates.MembershipAggregate;

namespace VanAn.Gateway.Controllers
{
    /// <summary>
    /// Membership Infrastructure (2026-10-02, user directive): 2 luồng admin — SystemAdmin thực hiện.
    ///   Luồng 1: tenant → Hợp tác xã (tạo HtxProfile hộ — S1 hook Type=HTX + TT 71).
    ///   Luồng 2: tenant làm thành viên HTX (add trực tiếp) + nâng cấp cộng tác viên thành tenant.
    /// SystemAdmin-only — KHÔNG dùng cho Owner (Owner dùng /api/membership/htx-profile của chính mình).
    /// Dữ liệu ghi PG (CoreHub in-process — Gateway source of truth).
    /// </summary>
    [ApiController]
    [Route("api/admin/membership")]
    [Authorize(Roles = "SystemAdmin")]
    public class MembershipAdminController(
        IHtxProfileService htxProfileService,
        IMemberRegistryService memberRegistryService,
        ICollaboratorTenantProvisioningService collaboratorProvisioning,
        ILogger<MembershipAdminController> logger) : ControllerBase
    {
        /// <summary>Luồng 1: tenant → HTX. Tài liệu upload tùy chọn (CharterUrl = URL file đã upload).</summary>
        [HttpPost("htx-profile")]
        public async Task<IActionResult> CreateHtxProfile([FromBody] CreateHtxProfileAdminRequest request)
        {
            if (request.HtxTenantId == Guid.Empty)
                return BadRequest(new { error = "htxTenantId is required." });

            try
            {
                var charter = string.IsNullOrWhiteSpace(request.CharterVersion) ? "v1.0" : request.CharterVersion.Trim();
                var terms = string.IsNullOrWhiteSpace(request.TermsVersion) ? "v1.0" : request.TermsVersion.Trim();
                var profile = await htxProfileService.GetOrCreateAsync(request.HtxTenantId, charter, terms, HttpContext.RequestAborted);

                // Tài liệu (charter) tùy chọn — gắn CharterUrl nếu có.
                if (!string.IsNullOrWhiteSpace(request.CharterUrl))
                {
                    await htxProfileService.UpdateCharterAsync(request.HtxTenantId, charter, request.CharterUrl.Trim(), HttpContext.RequestAborted);
                }

                logger.LogInformation("SystemAdmin marked tenant {HtxId} as HTX (charter {Charter})", request.HtxTenantId, charter);
                return Ok(profile);
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { error = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                return Conflict(new { error = ex.Message });
            }
        }

        /// <summary>Luồng 2: thêm tenant làm thành viên HTX (3 loại + vốn góp theo loại).</summary>
        [HttpPost("members")]
        public async Task<IActionResult> AddMember([FromBody] AddHtxMemberRequest request)
        {
            if (!Enum.TryParse<MembershipType>(request.MembershipType, ignoreCase: true, out var membershipType))
                return BadRequest(new { error = $"Invalid MembershipType: {request.MembershipType}" });
            var adminId = GetAdminUserId();

            try
            {
                var memberId = await memberRegistryService.AddMemberAsync(
                    request.HtxTenantId,
                    request.MemberTenantId,
                    membershipType,
                    request.CapitalContributionAmount,
                    adminId,
                    HttpContext.RequestAborted);
                var member = await memberRegistryService.GetAsync(memberId, HttpContext.RequestAborted);
                return Ok(new { memberId, memberNumber = member?.MemberNumber });
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { error = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                return Conflict(new { error = ex.Message });
            }
        }

        /// <summary>D4: nâng cấp cộng tác viên (Salesman/Shipper) thành tenant profile — idempotent (reuse nếu đã có).</summary>
        [HttpPost("collaborator-upgrade")]
        public async Task<IActionResult> UpgradeCollaborator([FromBody] CollaboratorUpgradeRequest request)
        {
            if (request.CustomerId == Guid.Empty)
                return BadRequest(new { error = "customerId is required." });

            var existing = await collaboratorProvisioning.GetExistingProfileAsync(request.CustomerId, HttpContext.RequestAborted);
            if (existing is not null)
                return Ok(new { tenantId = existing.Value, created = false });

            var tenantId = await collaboratorProvisioning.GetOrCreateProfileAsync(
                request.CustomerId,
                string.IsNullOrWhiteSpace(request.DisplayName) ? "Hộ kinh doanh cá nhân" : request.DisplayName.Trim(),
                ct: HttpContext.RequestAborted);
            return Ok(new { tenantId = tenantId.Value, created = true });
        }

        private Guid GetAdminUserId()
        {
            var claim = User.FindFirst("sub")?.Value
                ?? User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            return Guid.TryParse(claim, out var userId) ? userId : Guid.Empty;
        }
    }
}
