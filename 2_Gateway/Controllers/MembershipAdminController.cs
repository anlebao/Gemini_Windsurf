using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VanAn.CoreHub.Services.Membership;
using VanAn.Shared.Domain;
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

        /// <summary>Luồng 1 (2026-10-15): chuyển ngược HTX → Công ty/HKD (SystemAdmin).
        /// CHẶN khi còn thành viên (Q3). Xóa HtxProfile + revert Tenant.Type/AccountingStandard.</summary>
        [HttpPost("htx-profile/revert")]
        public async Task<IActionResult> RevertHtxProfile([FromBody] RevertHtxAdminRequest request)
        {
            if (request.HtxTenantId == Guid.Empty)
                return BadRequest(new { error = "htxTenantId is required." });

            if (!TryResolveTargetType(request.TargetType, out var targetType, out var targetStandard))
                return BadRequest(new { error = $"TargetType không hợp lệ: '{request.TargetType}'. Hợp lệ: HKD, Enterprise_SME, Enterprise_Large." });

            try
            {
                await htxProfileService.RevertAsync(request.HtxTenantId, targetType, targetStandard, HttpContext.RequestAborted);
                logger.LogInformation("SystemAdmin reverted tenant {HtxId} HTX → {TargetType} (standard {Standard})",
                    request.HtxTenantId, targetType, targetStandard?.ToString() ?? "null");
                return Ok(new { reverted = true, tenantId = request.HtxTenantId, targetType = targetType.ToString() });
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

            // D4: chỉ cộng tác viên (Salesman/Shipper active) mới được nâng cấp.
            if (!await collaboratorProvisioning.IsCollaboratorAsync(request.CustomerId, HttpContext.RequestAborted))
                return BadRequest(new { error = "Customer is not an active collaborator (Salesman/Shipper)." });

            var existing = await collaboratorProvisioning.GetExistingProfileAsync(request.CustomerId, HttpContext.RequestAborted);
            if (existing is not null)
                return Ok(new { tenantId = existing.Value, created = false });

            var tenantId = await collaboratorProvisioning.GetOrCreateProfileAsync(
                request.CustomerId,
                string.IsNullOrWhiteSpace(request.DisplayName) ? "Hộ kinh doanh cá nhân" : request.DisplayName.Trim(),
                ct: HttpContext.RequestAborted);
            return Ok(new { tenantId = tenantId.Value, created = true });
        }

        /// <summary>Luồng 2: danh sách thành viên của 1 HTX (SystemAdmin — bất kỳ HTX nào).</summary>
        [HttpGet("members")]
        public async Task<IActionResult> ListMembers([FromQuery] Guid htxTenantId, [FromQuery] string? status = null)
        {
            if (htxTenantId == Guid.Empty)
                return BadRequest(new { error = "htxTenantId is required." });

            var members = await memberRegistryService.ListForHtxAsync(htxTenantId, status, HttpContext.RequestAborted);
            return Ok(members);
        }

        private Guid GetAdminUserId()
        {
            var claim = User.FindFirst("sub")?.Value
                ?? User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            return Guid.TryParse(claim, out var userId) ? userId : Guid.Empty;
        }

        /// <summary>Map TargetType (string API) → TenantType + AccountingStandard (HKD → null, TT 152 implied).</summary>
        private static bool TryResolveTargetType(string? targetType, out TenantType type, out AccountingStandard? standard)
        {
            type = default;
            standard = null;
            switch (targetType?.Trim().ToLowerInvariant())
            {
                case "hkd":
                    type = TenantType.HKD;
                    return true;
                case "enterprise_sme" or "company_sme" or "sme":
                    type = TenantType.Enterprise_SME;
                    standard = AccountingStandard.TT133_2016;
                    return true;
                case "enterprise_large" or "company_large" or "large":
                    type = TenantType.Enterprise_Large;
                    standard = AccountingStandard.TT99_2025;
                    return true;
                default:
                    return false;
            }
        }
    }
}
