using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VanAn.CoreHub.Services.Membership;
using VanAn.Gateway.Services;
using VanAn.Shared.Domain.Aggregates.MembershipAggregate;

namespace VanAn.Gateway.Controllers
{
    /// <summary>
    /// Membership Infrastructure (2026-09-29): Hồ sơ HTX (Điều lệ versioning — SRS §14)
    /// + Digital Consent (SRS §14).
    ///
    /// Auth:
    /// - GET htx-profile: AllowAnonymous — applicant phải xem Điều lệ + version TRƯỚC khi consent (không PII).
    /// - POST/PUT htx-profile: HtxMembershipOfficer.
    /// - Consent endpoints: X-Customer-Token (applicant tự ghi nhận consent — evidence).
    /// </summary>
    [ApiController]
    [Route("api/membership")]
    public class MembershipProfileController(
        IHtxProfileService htxProfileService,
        IConsentService consentService,
        IHttpClientFactory httpClientFactory,
        ILogger<MembershipProfileController> logger) : ControllerBase
    {
        // ── HtxProfile ────────────────────────────────────────────────────────

        /// <summary>Xem Điều lệ/version của HTX (public — applicant cần xem trước khi consent).</summary>
        [HttpGet("htx-profile/{htxTenantId:guid}")]
        [AllowAnonymous]
        public async Task<IActionResult> GetProfile(Guid htxTenantId)
        {
            var profile = await htxProfileService.GetAsync(htxTenantId, HttpContext.RequestAborted);
            if (profile is null) return NotFound(new { error = $"HTX {htxTenantId} chưa đăng ký Membership Infrastructure." });
            return Ok(profile);
        }

        /// <summary>Hồ sơ HTX của tenant đang đăng nhập (officer — tenant từ JWT claim, IDOR-safe).</summary>
        [HttpGet("htx-profile")]
        [Authorize(Policy = "HtxMembershipOfficer")]
        public async Task<IActionResult> GetMyProfile()
        {
            var tenantId = GetTenantIdFromClaim();
            if (tenantId == Guid.Empty)
                return Unauthorized(new { error = "Missing or invalid tenant_id claim." });

            var profile = await htxProfileService.GetAsync(tenantId, HttpContext.RequestAborted);
            if (profile is null) return NotFound(new { error = $"HTX {tenantId} chưa đăng ký Membership Infrastructure." });
            return Ok(profile);
        }

        /// <summary>Tạo hồ sơ HTX (đánh dấu tenant là HTX + version Điều lệ ban đầu).</summary>
        [HttpPost("htx-profile")]
        [Authorize(Policy = "HtxMembershipOfficer")]
        public async Task<IActionResult> CreateProfile([FromBody] CreateHtxProfileRequest request)
        {
            var tenantId = GetTenantIdFromClaim();
            if (tenantId == Guid.Empty)
                return Unauthorized(new { error = "Missing or invalid tenant_id claim." });

            try
            {
                var profile = await htxProfileService.GetOrCreateAsync(
                    tenantId, request.CharterVersion, request.TermsVersion, HttpContext.RequestAborted);
                return Ok(profile);
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }

        /// <summary>Cập nhật phiên bản Điều lệ hiện hành.</summary>
        [HttpPut("htx-profile/charter")]
        [Authorize(Policy = "HtxMembershipOfficer")]
        public async Task<IActionResult> UpdateCharter([FromBody] UpdateCharterRequest request)
        {
            var tenantId = GetTenantIdFromClaim();
            if (tenantId == Guid.Empty)
                return Unauthorized(new { error = "Missing or invalid tenant_id claim." });

            try
            {
                await htxProfileService.UpdateCharterAsync(
                    tenantId, request.CharterVersion, request.CharterUrl, HttpContext.RequestAborted);
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

        /// <summary>Cập nhật phiên bản điều kiện gia nhập.</summary>
        [HttpPut("htx-profile/terms")]
        [Authorize(Policy = "HtxMembershipOfficer")]
        public async Task<IActionResult> UpdateTerms([FromBody] UpdateTermsRequest request)
        {
            var tenantId = GetTenantIdFromClaim();
            if (tenantId == Guid.Empty)
                return Unauthorized(new { error = "Missing or invalid tenant_id claim." });

            try
            {
                await htxProfileService.UpdateTermsAsync(tenantId, request.TermsVersion, HttpContext.RequestAborted);
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

        // ── Consent (SRS §14) ─────────────────────────────────────────────────

        /// <summary>Applicant ghi nhận consent cho 1 phiên bản tài liệu (evidence, append-only).</summary>
        [HttpPost("consent")]
        [AllowAnonymous]
        public async Task<IActionResult> RecordConsent([FromBody] RecordConsentRequest request)
        {
            if (!Request.Headers.TryGetValue("X-Customer-Token", out var token) || string.IsNullOrEmpty(token))
                return Unauthorized(new { error = "X-Customer-Token header is required." });

            var (status, customerId) = await CustomerTokenValidationHelper.ValidateAsync(
                httpClientFactory, token.ToString(), HttpContext.RequestAborted);

            if (status == CustomerTokenValidationStatus.InvalidToken)
                return Unauthorized(new { error = "Token không hợp lệ hoặc đã hết hạn." });
            if (status != CustomerTokenValidationStatus.Valid || customerId is null)
                return StatusCode(503, new { error = "Dịch vụ đang bảo trì, vui lòng thử lại sau." });

            if (!Enum.TryParse<ConsentDocumentType>(request.DocumentType, ignoreCase: true, out var documentType))
                return BadRequest(new { error = $"Invalid DocumentType: {request.DocumentType}. Must be Charter/Terms/DataPolicy." });

            try
            {
                var consentId = await consentService.RecordConsentAsync(
                    request.HtxTenantId,
                    customerId.Value,
                    documentType,
                    request.DocumentVersion,
                    request.EvidenceReference,
                    HttpContext.RequestAborted);
                return Ok(new { consentId });
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }

        /// <summary>Lịch sử consent của applicant tại 1 HTX (evidence — SRS §14).</summary>
        [HttpGet("my-consents")]
        [AllowAnonymous]
        public async Task<IActionResult> MyConsents([FromQuery] Guid htxTenantId)
        {
            if (!Request.Headers.TryGetValue("X-Customer-Token", out var token) || string.IsNullOrEmpty(token))
                return Unauthorized(new { error = "X-Customer-Token header is required." });

            var (status, customerId) = await CustomerTokenValidationHelper.ValidateAsync(
                httpClientFactory, token.ToString(), HttpContext.RequestAborted);

            if (status == CustomerTokenValidationStatus.InvalidToken)
                return Unauthorized(new { error = "Token không hợp lệ hoặc đã hết hạn." });
            if (status != CustomerTokenValidationStatus.Valid || customerId is null)
                return StatusCode(503, new { error = "Dịch vụ đang bảo trì, vui lòng thử lại sau." });

            var list = await consentService.ListForApplicantAsync(
                htxTenantId, customerId.Value, HttpContext.RequestAborted);
            return Ok(list);
        }

        // ── Helpers ──────────────────────────────────────────────────────────

        private Guid GetTenantIdFromClaim()
        {
            var claim = User.FindFirst("tenant_id")?.Value ?? User.FindFirst("tenantid")?.Value;
            return Guid.TryParse(claim, out var tenantId) ? tenantId : Guid.Empty;
        }
    }

    public record CreateHtxProfileRequest(string CharterVersion, string TermsVersion);
    public record UpdateCharterRequest(string CharterVersion, string? CharterUrl = null);
    public record UpdateTermsRequest(string TermsVersion);
    public record RecordConsentRequest(
        Guid HtxTenantId,
        string DocumentType,
        string DocumentVersion,
        string? EvidenceReference = null);
}
