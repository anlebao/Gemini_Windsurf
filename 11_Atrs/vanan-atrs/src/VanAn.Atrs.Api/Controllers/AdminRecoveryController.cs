using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VanAn.Atrs.Api.Auth;
using VanAn.Atrs.Contracts;
using VanAn.Atrs.Domain;
using VanAn.Atrs.Infra;

namespace VanAn.Atrs.Api.Controllers;

/// <summary>
/// Staff recovery endpoints (A15/A16 backend, FR-011/FR-014) — thay thế
/// InternalRecoveryController (route api/internal không qua được YARP).
/// Permission: recovery.view / recovery.review / recovery.approve / case.assign.
/// Mọi staff access/action → AccountSecurityEvent (ghi trong RecoveryCaseManager).
/// </summary>
[ApiController]
[Route("api/v1/account-trust/admin/recovery-requests")]
[Authorize]
public class AdminRecoveryController(
    IRecoveryCaseManager recoveryCaseManager,
    IAtrsAuthorizationService authz,
    ILogger<AdminRecoveryController> logger) : ControllerBase
{
    /// <summary>A15 — recovery queue, server-side filter + paging + total.</summary>
    [HttpGet]
    public async Task<IActionResult> Queue(
        [FromQuery] string? status, [FromQuery] string? requestType,
        [FromQuery] Guid? assignedTo, [FromQuery] bool unassigned, [FromQuery] Guid? subjectId,
        [FromQuery] int skip = 0, [FromQuery] int take = 50, CancellationToken ct = default)
    {
        if (!await StaffPrincipal.HasPermissionAsync(authz, User, AtrsPermissions.RecoveryView, ct: ct))
            return Forbid();
        var page = await recoveryCaseManager.ListQueuePageAsync(
            new RecoveryQueueQuery(status, requestType, assignedTo, unassigned, subjectId, skip, take), ct);
        return Ok(page);
    }

    /// <summary>A16 — review detail: request + risk + notes + devices + links + timeline.</summary>
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Detail(Guid id, CancellationToken ct)
    {
        if (!await StaffPrincipal.HasPermissionAsync(authz, User, AtrsPermissions.RecoveryView, ct: ct))
            return Forbid();
        var dto = await recoveryCaseManager.GetStaffDetailAsync(id, StaffPrincipal.UserId(User), ct);
        return dto is null ? NotFound() : Ok(dto);
    }

    /// <summary>A15 — assign case cho staff (case.assign).</summary>
    [HttpPost("{id:guid}/assign")]
    public async Task<IActionResult> Assign(Guid id, [FromBody] AssignRecoveryRequest request, CancellationToken ct)
    {
        if (!await StaffPrincipal.HasPermissionAsync(authz, User, AtrsPermissions.CaseAssign, ct: ct))
            return Forbid();
        var item = await recoveryCaseManager.AssignAsync(id, request.StaffUserId, ct);
        return item is null ? NotFound() : Ok(item);
    }

    /// <summary>A16 — request more info (recovery.review).</summary>
    [HttpPost("{id:guid}/request-info")]
    public async Task<IActionResult> RequestInfo(Guid id, CancellationToken ct)
    {
        if (!await StaffPrincipal.HasPermissionAsync(authz, User, AtrsPermissions.RecoveryReview, ct: ct))
            return Forbid();
        try
        {
            var dto = await recoveryCaseManager.RequestMoreInfoAsync(id, StaffPrincipal.UserId(User), ct);
            return Ok(dto);
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { error = ex.Message });
        }
    }

    /// <summary>A16 — add internal note / recommendation (recovery.review).</summary>
    [HttpPost("{id:guid}/notes")]
    public async Task<IActionResult> AddNote(Guid id, [FromBody] AddRecoveryNoteRequest request, CancellationToken ct)
    {
        if (!await StaffPrincipal.HasPermissionAsync(authz, User, AtrsPermissions.RecoveryReview, ct: ct))
            return Forbid();
        if (string.IsNullOrWhiteSpace(request.Body))
            return BadRequest(new { error = "Body là bắt buộc." });
        if (!Enum.TryParse<RecoveryNoteType>(request.NoteType, true, out var noteType))
            return BadRequest(new { error = $"NoteType không hợp lệ: {request.NoteType}" });
        try
        {
            var dto = await recoveryCaseManager.AddNoteAsync(id, StaffPrincipal.UserId(User), request.Body, noteType, ct);
            return Ok(dto);
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new { error = ex.Message });
        }
    }

    /// <summary>A16 — escalate lên cấp xử lý cao hơn (recovery.review).</summary>
    [HttpPost("{id:guid}/escalate")]
    public async Task<IActionResult> Escalate(Guid id, CancellationToken ct)
    {
        if (!await StaffPrincipal.HasPermissionAsync(authz, User, AtrsPermissions.RecoveryReview, ct: ct))
            return Forbid();
        try
        {
            var dto = await recoveryCaseManager.EscalateAsync(id, StaffPrincipal.UserId(User), ct);
            return Ok(dto);
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { error = ex.Message });
        }
    }

    /// <summary>A16 — de-escalate: trả case về PENDING_REVIEW (recovery.review).</summary>
    [HttpPost("{id:guid}/return-to-review")]
    public async Task<IActionResult> ReturnToReview(Guid id, CancellationToken ct)
    {
        if (!await StaffPrincipal.HasPermissionAsync(authz, User, AtrsPermissions.RecoveryReview, ct: ct))
            return Forbid();
        try
        {
            var dto = await recoveryCaseManager.ReturnToReviewAsync(id, StaffPrincipal.UserId(User), ct);
            return Ok(dto);
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { error = ex.Message });
        }
    }

    /// <summary>A16 — approve (recovery.approve).</summary>
    [HttpPost("{id:guid}/approve")]
    public async Task<IActionResult> Approve(Guid id, CancellationToken ct)
    {
        if (!await StaffPrincipal.HasPermissionAsync(authz, User, AtrsPermissions.RecoveryApprove, ct: ct))
            return Forbid();
        var dto = await recoveryCaseManager.ManualApproveAsync(id, StaffPrincipal.UserId(User), ct);
        logger.LogInformation("Recovery {RequestId} approved via admin API", id);
        return Ok(dto);
    }

    /// <summary>A16 — reject (recovery.approve).</summary>
    [HttpPost("{id:guid}/reject")]
    public async Task<IActionResult> Reject(Guid id, [FromBody] RejectRecoveryRequest request, CancellationToken ct)
    {
        if (!await StaffPrincipal.HasPermissionAsync(authz, User, AtrsPermissions.RecoveryApprove, ct: ct))
            return Forbid();
        if (string.IsNullOrWhiteSpace(request.ReasonCode))
            return BadRequest(new { error = "ReasonCode là bắt buộc (audit)." });
        var dto = await recoveryCaseManager.ManualRejectAsync(id, StaffPrincipal.UserId(User), request.ReasonCode, ct);
        logger.LogInformation("Recovery {RequestId} rejected via admin API", id);
        return Ok(dto);
    }

    public sealed record AssignRecoveryRequest(Guid StaffUserId);
    public sealed record AddRecoveryNoteRequest(string Body, string NoteType);
    public sealed record RejectRecoveryRequest(string ReasonCode);
}