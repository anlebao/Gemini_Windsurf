using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VanAn.CoreHub.Services.Membership;
using VanAn.Gateway.Services;
using VanAn.Shared.Domain.Aggregates.MembershipAggregate;

namespace VanAn.Gateway.Controllers
{
    /// <summary>
    /// Membership Infrastructure (2026-09-29): Hồ sơ xin gia nhập HTX (SRS §7, §31).
    ///
    /// Auth:
    /// - Customer endpoints (create/submit/resubmit): X-Customer-Token header → applicant = Vạn An Network ID.
    /// - HTX review endpoints (list/get/approve/reject/request-info): Policy "HtxMembershipOfficer"
    ///   (Owner của tenant HTX — HTX quyết định, KHÔNG phải SystemAdmin — SRS §3.1, §6.5).
    ///   IDOR guard: application.TenantId phải == JWT tenant_id claim.
    /// </summary>
    [ApiController]
    [Route("api/membership/applications")]
    public class MembershipApplicationsController(
        IMembershipApplicationService applicationService,
        IMembershipDocumentService documentService,
        IHttpClientFactory httpClientFactory,
        ILogger<MembershipApplicationsController> logger) : ControllerBase
    {
        // ── Applicant (KhachLink customer) ────────────────────────────────────

        /// <summary>Tạo hồ sơ Draft (SRS §31 — POST /api/membership/applications). Applicant lấy từ token.</summary>
        [HttpPost]
        [AllowAnonymous]
        public async Task<IActionResult> Create([FromBody] CreateMembershipApplicationRequest request)
        {
            var (customerId, error) = await GetCustomerIdFromTokenAsync();
            if (error is not null) return error;

            try
            {
                // IDOR-safe: applicant = từ token, không nhận từ body
                var id = await applicationService.CreateApplicationAsync(
                    request with { ApplicantCustomerId = customerId.Value }, HttpContext.RequestAborted);
                return CreatedAtAction(nameof(GetById), new { id }, new { applicationId = id });
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

        /// <summary>Applicant nộp hồ sơ (SRS §31 — POST .../{id}/submit). Chỉ chủ sở hữu hồ sơ.</summary>
        [HttpPost("{id:guid}/submit")]
        [AllowAnonymous]
        public async Task<IActionResult> Submit(Guid id)
        {
            var (customerId, error) = await GetCustomerIdFromTokenAsync();
            if (error is not null) return error;

            var application = await applicationService.GetAsync(id, HttpContext.RequestAborted);
            if (application is null) return NotFound(new { error = $"Application {id} not found." });
            if (application.ApplicantCustomerId != customerId.Value)
                return Forbid();

            try
            {
                await applicationService.SubmitAsync(id, HttpContext.RequestAborted);
                return NoContent();
            }
            catch (InvalidOperationException ex)
            {
                return Conflict(new { error = ex.Message });
            }
        }

        /// <summary>Applicant bổ sung thông tin (NeedInfo → Submitted).</summary>
        [HttpPost("{id:guid}/resubmit")]
        [AllowAnonymous]
        public async Task<IActionResult> Resubmit(Guid id)
        {
            var (customerId, error) = await GetCustomerIdFromTokenAsync();
            if (error is not null) return error;

            var application = await applicationService.GetAsync(id, HttpContext.RequestAborted);
            if (application is null) return NotFound(new { error = $"Application {id} not found." });
            if (application.ApplicantCustomerId != customerId.Value)
                return Forbid();

            try
            {
                await applicationService.ResubmitAsync(id, HttpContext.RequestAborted);
                return NoContent();
            }
            catch (InvalidOperationException ex)
            {
                return Conflict(new { error = ex.Message });
            }
        }

        /// <summary>"Hồ sơ của tôi" — applicant xem hồ sơ trên MỌI HTX (cross-tenant read có chủ đích).</summary>
        [HttpGet("my")]
        [AllowAnonymous]
        public async Task<IActionResult> MyApplications()
        {
            var (customerId, error) = await GetCustomerIdFromTokenAsync();
            if (error is not null) return error;

            var list = await applicationService.ListForApplicantAsync(customerId.Value, HttpContext.RequestAborted);
            return Ok(list);
        }

        // ── Documents (SRS §17.2 — chữ ký online, form giấy scan) ────────────

        /// <summary>
        /// Applicant đính kèm tài liệu xác nhận (chữ ký online / scan form giấy đã ký tay).
        /// Upload ảnh qua POST /api/v1/images/upload (Cloudinary) trước, rồi attach URL vào đây.
        /// </summary>
        [HttpPost("{id:guid}/documents")]
        [AllowAnonymous]
        public async Task<IActionResult> AttachDocument(Guid id, [FromBody] AttachMembershipDocumentRequest request)
        {
            var (customerId, error) = await GetCustomerIdFromTokenAsync();
            if (error is not null) return error;

            if (!Enum.TryParse<MembershipDocumentType>(request.DocumentType, ignoreCase: true, out var documentType))
                return BadRequest(new { error = $"Invalid DocumentType: {request.DocumentType}. Must be Charter/PaperApplication/Signature/IdProof/Other." });

            try
            {
                var documentId = await documentService.AttachAsync(
                    id, customerId.Value, documentType, request.DocumentVersion, request.StorageReference, request.Hash,
                    HttpContext.RequestAborted);
                return Ok(new { documentId });
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new { error = ex.Message });
            }
            catch (UnauthorizedAccessException)
            {
                return Forbid();
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }

        /// <summary>Danh sách tài liệu của hồ sơ (officer — tenant match; applicant tự xem qua my-applications).</summary>
        [HttpGet("{id:guid}/documents")]
        [Authorize(Policy = "HtxMembershipOfficer")]
        public async Task<IActionResult> ListDocuments(Guid id)
        {
            var tenantId = GetTenantIdFromClaim();
            if (tenantId == Guid.Empty)
                return Unauthorized(new { error = "Missing or invalid tenant_id claim." });

            var application = await applicationService.GetAsync(id, HttpContext.RequestAborted);
            if (application is null) return NotFound(new { error = $"Application {id} not found." });
            if (application.HtxTenantId != tenantId)
                return Forbid();

            var documents = await documentService.ListForApplicationAsync(id, HttpContext.RequestAborted);
            return Ok(documents);
        }

        // ── HTX review (Membership Officer) ───────────────────────────────────

        /// <summary>Queue xét duyệt của HTX (SRS §15 dashboard). Tenant từ JWT claim — IDOR safe.</summary>
        [HttpGet]
        [Authorize(Policy = "HtxMembershipOfficer")]
        public async Task<IActionResult> List([FromQuery] string? status = null)
        {
            var tenantId = GetTenantIdFromClaim();
            if (tenantId == Guid.Empty)
                return Unauthorized(new { error = "Missing or invalid tenant_id claim." });

            var list = await applicationService.ListForHtxAsync(tenantId, status, HttpContext.RequestAborted);
            return Ok(list);
        }

        /// <summary>Chi tiết 1 hồ sơ (officer). IDOR guard: hồ sơ phải thuộc tenant của officer.</summary>
        [HttpGet("{id:guid}")]
        [Authorize(Policy = "HtxMembershipOfficer")]
        public async Task<IActionResult> GetById(Guid id)
        {
            var tenantId = GetTenantIdFromClaim();
            if (tenantId == Guid.Empty)
                return Unauthorized(new { error = "Missing or invalid tenant_id claim." });

            var application = await applicationService.GetAsync(id, HttpContext.RequestAborted);
            if (application is null) return NotFound(new { error = $"Application {id} not found." });
            if (application.HtxTenantId != tenantId)
                return Forbid();

            return Ok(application);
        }

        /// <summary>HTX yêu cầu bổ sung thông tin (Submitted → NeedInfo).</summary>
        [HttpPost("{id:guid}/request-info")]
        [Authorize(Policy = "HtxMembershipOfficer")]
        public async Task<IActionResult> RequestMoreInfo(Guid id, [FromBody] RequestMoreInfoRequest request)
        {
            var tenantId = GetTenantIdFromClaim();
            var officerUserId = GetOfficerUserId();
            if (tenantId == Guid.Empty || officerUserId == Guid.Empty)
                return Unauthorized(new { error = "Missing tenant_id or user id claim." });

            var application = await applicationService.GetAsync(id, HttpContext.RequestAborted);
            if (application is null) return NotFound(new { error = $"Application {id} not found." });
            if (application.HtxTenantId != tenantId)
                return Forbid();

            try
            {
                await applicationService.RequestMoreInfoAsync(
                    id, request with { ReviewedByUserId = officerUserId }, HttpContext.RequestAborted);
                return NoContent();
            }
            catch (InvalidOperationException ex)
            {
                return Conflict(new { error = ex.Message });
            }
        }

        /// <summary>HTX duyệt: tạo Member Active (SRS §31 — POST .../{id}/approve).</summary>
        [HttpPost("{id:guid}/approve")]
        [Authorize(Policy = "HtxMembershipOfficer")]
        public async Task<IActionResult> Approve(Guid id)
        {
            var tenantId = GetTenantIdFromClaim();
            var officerUserId = GetOfficerUserId();
            if (tenantId == Guid.Empty || officerUserId == Guid.Empty)
                return Unauthorized(new { error = "Missing tenant_id or user id claim." });

            var application = await applicationService.GetAsync(id, HttpContext.RequestAborted);
            if (application is null) return NotFound(new { error = $"Application {id} not found." });
            if (application.HtxTenantId != tenantId)
                return Forbid();

            try
            {
                var memberId = await applicationService.ApproveAsync(id, officerUserId, HttpContext.RequestAborted);
                logger.LogInformation(
                    "Membership application {ApplicationId} approved by officer {OfficerId} (HTX {HtxId}) — member {MemberId}",
                    id, officerUserId, tenantId, memberId);
                return Ok(new { memberId });
            }
            catch (InvalidOperationException ex)
            {
                return Conflict(new { error = ex.Message });
            }
        }

        /// <summary>HTX từ chối (kèm lý do — audit trail, SRS §28).</summary>
        [HttpPost("{id:guid}/reject")]
        [Authorize(Policy = "HtxMembershipOfficer")]
        public async Task<IActionResult> Reject(Guid id, [FromBody] RejectApplicationRequest request)
        {
            var tenantId = GetTenantIdFromClaim();
            var officerUserId = GetOfficerUserId();
            if (tenantId == Guid.Empty || officerUserId == Guid.Empty)
                return Unauthorized(new { error = "Missing tenant_id or user id claim." });

            var application = await applicationService.GetAsync(id, HttpContext.RequestAborted);
            if (application is null) return NotFound(new { error = $"Application {id} not found." });
            if (application.HtxTenantId != tenantId)
                return Forbid();

            try
            {
                await applicationService.RejectAsync(
                    id, request with { ReviewedByUserId = officerUserId }, HttpContext.RequestAborted);
                return NoContent();
            }
            catch (InvalidOperationException ex)
            {
                return Conflict(new { error = ex.Message });
            }
        }

        // ── Helpers ──────────────────────────────────────────────────────────

        private Guid GetTenantIdFromClaim()
        {
            var claim = User.FindFirst("tenant_id")?.Value ?? User.FindFirst("tenantid")?.Value;
            return Guid.TryParse(claim, out var tenantId) ? tenantId : Guid.Empty;
        }

        private Guid GetOfficerUserId()
        {
            var claim = User.FindFirst("sub")?.Value
                ?? User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            return Guid.TryParse(claim, out var userId) ? userId : Guid.Empty;
        }

        private async Task<(Guid? CustomerId, IActionResult? Error)> GetCustomerIdFromTokenAsync()
        {
            if (!Request.Headers.TryGetValue("X-Customer-Token", out var token) || string.IsNullOrEmpty(token))
                return (null, Unauthorized(new { error = "X-Customer-Token header is required." }));

            var (status, customerId) = await CustomerTokenValidationHelper.ValidateAsync(
                httpClientFactory, token.ToString(), HttpContext.RequestAborted);

            if (status == CustomerTokenValidationStatus.InvalidToken)
                return (null, Unauthorized(new { error = "Token không hợp lệ hoặc đã hết hạn." }));
            if (status != CustomerTokenValidationStatus.Valid || customerId is null)
                return (null, StatusCode(503, new { error = "Dịch vụ đang bảo trì, vui lòng thử lại sau." }));

            return (customerId, null);
        }
    }
}
