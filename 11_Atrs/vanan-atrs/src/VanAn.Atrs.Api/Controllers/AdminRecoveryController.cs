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
/// Permission: recovery.view / recovery.review / recovery.approve.
/// </summary>
[ApiController]
[Route("api/v1/account-trust/admin/recovery-requests")]
[Authorize]
public class AdminRecoveryController(
    IRecoveryCaseManager recoveryCaseManager,
    IAtrsAuthorizationService authz,
    ILogger<AdminRecoveryController> logger) : ControllerBase
{
    /// <summary>A15 — recovery queue.</summary>
    [HttpGet]
    public async Task<IActionResult> Queue([FromQuery] string? status, [FromQuery] int skip = 0, [FromQuery] int take = 50, CancellationToken ct = default)
    {
        if (!await StaffPrincipal.HasPermissionAsync(authz, User, AtrsPermissions.RecoveryView, ct: ct))
            return Forbid();
        var items = await recoveryCaseManager.ListQueueAsync(status, skip, take, ct);
        return Ok(items);
    }

    /// <summary>A16 — review detail.</summary>
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Detail(Guid id, CancellationToken ct)
    {
        if (!await StaffPrincipal.HasPermissionAsync(authz, User, AtrsPermissions.RecoveryView, ct: ct))
            return Forbid();
        var dto = await recoveryCaseManager.GetAsync(id, ct);
        return dto is null ? NotFound() : Ok(dto);
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

    public sealed record RejectRecoveryRequest(string ReasonCode);
}
