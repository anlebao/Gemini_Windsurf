using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VanAn.Atrs.Api.Auth;
using VanAn.Atrs.Contracts;
using VanAn.Atrs.Domain;
using VanAn.Atrs.Infra;

namespace VanAn.Atrs.Api.Controllers;

/// <summary>
/// Staff dispute endpoints (A17/A18/A19 backend, FR-014) — backend-authoritative RBAC.
/// Route dưới /api/v1/account-trust/admin/* → YARP atrs-account-trust-route → vanan-atrs.
/// Mọi access sensitive → AccountSecurityEvent (ghi trong DisputeCaseManager).
/// </summary>
[ApiController]
[Route("api/v1/account-trust/admin/disputes")]
[Authorize]
public class AdminDisputesController(
    IDisputeCaseManager disputeManager,
    IAtrsAuthorizationService authz,
    ILogger<AdminDisputesController> logger) : ControllerBase
{
    /// <summary>A17 — dispute queue, server-side filter + paging.</summary>
    [HttpGet]
    public async Task<IActionResult> Queue([FromQuery] string? status, [FromQuery] Guid? assignedTo, [FromQuery] int skip = 0, [FromQuery] int take = 50, CancellationToken ct = default)
    {
        if (!await StaffPrincipal.HasPermissionAsync(authz, User, AtrsPermissions.DisputeView, ct: ct))
            return Forbid();
        var items = await disputeManager.ListQueueAsync(new DisputeQueueQuery(status, assignedTo, skip, take), ct);
        return Ok(items);
    }

    /// <summary>A18 — investigation detail: case + evidence + decisions + ledger transactions.</summary>
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Detail(Guid id, CancellationToken ct)
    {
        if (!await StaffPrincipal.HasPermissionAsync(authz, User, AtrsPermissions.DisputeView, ct: ct))
            return Forbid();
        var dto = await disputeManager.GetStaffDetailAsync(id, StaffPrincipal.UserId(User), ct);
        return dto is null ? NotFound() : Ok(dto);
    }

    /// <summary>A17 — assign case cho staff (case.assign).</summary>
    [HttpPost("{id:guid}/assign")]
    public async Task<IActionResult> Assign(Guid id, [FromBody] AssignDisputeRequest request, CancellationToken ct)
    {
        if (!await StaffPrincipal.HasPermissionAsync(authz, User, AtrsPermissions.CaseAssign, ct: ct))
            return Forbid();
        var item = await disputeManager.AssignAsync(id, request.StaffUserId, ct);
        return item is null ? NotFound() : Ok(item);
    }

    /// <summary>A18 — chuyển trạng thái case (dispute.investigate).</summary>
    [HttpPost("{id:guid}/transition")]
    public async Task<IActionResult> Transition(Guid id, [FromBody] TransitionDisputeRequest request, CancellationToken ct)
    {
        if (!await StaffPrincipal.HasPermissionAsync(authz, User, AtrsPermissions.DisputeInvestigate, ct: ct))
            return Forbid();
        if (!Enum.TryParse<DisputeCaseStatus>(request.Status, true, out var target))
            return BadRequest(new { error = $"Invalid status: {request.Status}" });
        try
        {
            var dto = await disputeManager.TransitionAsync(id, StaffPrincipal.UserId(User), target, request.Note, ct);
            return dto is null ? NotFound() : Ok(dto);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>A19 — investigator propose decision (maker-checker step 1).</summary>
    [HttpPost("{id:guid}/decisions")]
    public async Task<IActionResult> ProposeDecision(Guid id, [FromBody] ProposeDisputeDecisionRequest request, CancellationToken ct)
    {
        if (!await StaffPrincipal.HasPermissionAsync(authz, User, AtrsPermissions.DisputeProposeAdjustment, ct: ct))
            return Forbid();
        try
        {
            var dto = await disputeManager.ProposeDecisionAsync(id, StaffPrincipal.UserId(User), request, ct);
            return Ok(dto);
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

    /// <summary>A19 — approver duyệt decision (maker-checker step 2) → execute ledger adjust.</summary>
    [HttpPost("decisions/{decisionId:guid}/approve")]
    public async Task<IActionResult> ApproveDecision(Guid decisionId, CancellationToken ct)
    {
        if (!await StaffPrincipal.HasPermissionAsync(authz, User, AtrsPermissions.DisputeApproveAdjustment, ct: ct))
            return Forbid();
        try
        {
            var dto = await disputeManager.ApproveDecisionAsync(decisionId, StaffPrincipal.UserId(User), ct);
            return Ok(dto);
        }
        catch (InvalidOperationException ex)
        {
            // Maker-checker self-approval hoặc sai state → 403/409.
            return ex.Message.Contains("cannot be the proposer", StringComparison.Ordinal)
                ? StatusCode(403, new { error = ex.Message })
                : Conflict(new { error = ex.Message });
        }
    }

    /// <summary>A19 — approver từ chối decision proposal → case quay lại UNDER_REVIEW.</summary>
    [HttpPost("decisions/{decisionId:guid}/reject")]
    public async Task<IActionResult> RejectDecision(Guid decisionId, [FromBody] DecideDisputeRequest request, CancellationToken ct)
    {
        if (!await StaffPrincipal.HasPermissionAsync(authz, User, AtrsPermissions.DisputeApproveAdjustment, ct: ct))
            return Forbid();
        if (string.IsNullOrWhiteSpace(request.RejectionReason))
            return BadRequest(new { error = "RejectionReason là bắt buộc (audit)." });
        try
        {
            var dto = await disputeManager.RejectDecisionAsync(decisionId, StaffPrincipal.UserId(User), request.RejectionReason, ct);
            return Ok(dto);
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { error = ex.Message });
        }
    }

    public sealed record AssignDisputeRequest(Guid StaffUserId);
    public sealed record TransitionDisputeRequest(string Status, string? Note);
}
