using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VanAn.Atrs.Api.Auth;
using VanAn.Atrs.Contracts;
using VanAn.Atrs.Domain;
using VanAn.Atrs.Infra;

namespace VanAn.Atrs.Api.Controllers;

/// <summary>
/// Recovery requests (FR-011) — customer endpoints, tiered proof ① (recovery code).
/// Entry khi user mất hết credential: vẫn phải có token (đã đăng nhập trên thiết bị mới)
/// HOẶC verified-contact path (②) — wave sau. Wave 1: authenticated customer.
/// </summary>
[ApiController]
[Route("api/v1/account-trust/recovery-requests")]
public class RecoveryRequestsController(
    IAtrsCustomerContextService customerContext,
    IRecoveryCaseManager recoveryCaseManager,
    ILogger<RecoveryRequestsController> logger) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Submit([FromBody] SubmitRecoveryRequest request, CancellationToken ct)
    {
        var auth = await customerContext.AuthenticateAsync(Request, ct);
        if (auth.Status == AtrsCustomerTokenStatus.DownstreamUnavailable)
            return StatusCode(503, new { error = "Dịch vụ đang bảo trì, vui lòng thử lại sau." });
        if (auth.TrustSubjectId is null)
            return Unauthorized(new { error = "Token không hợp lệ hoặc đã hết hạn." });

        if (string.IsNullOrWhiteSpace(request.IdempotencyKey))
            return BadRequest(new { error = "IdempotencyKey là bắt buộc (chống gửi trùng)." });

        var result = await recoveryCaseManager.SubmitAsync(
            auth.TrustSubjectId.Value, request.RequestType, request.IdempotencyKey,
            request.NewDeviceId, request.EvidenceSummaryJson, ct);
        logger.LogInformation("Recovery request {RequestId} submitted (existing={Existing})", result.Request.Id, result.Existing);
        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct)
    {
        var auth = await customerContext.AuthenticateAsync(Request, ct);
        if (auth.Status == AtrsCustomerTokenStatus.DownstreamUnavailable)
            return StatusCode(503, new { error = "Dịch vụ đang bảo trì, vui lòng thử lại sau." });
        if (auth.TrustSubjectId is null)
            return Unauthorized(new { error = "Token không hợp lệ hoặc đã hết hạn." });

        var dto = await recoveryCaseManager.GetAsync(id, ct);
        if (dto is null || dto.TrustSubjectId != auth.TrustSubjectId.Value)
            return NotFound(); // không lộ sự tồn tại của request người khác (anti-enumeration)

        return Ok(dto);
    }

    /// <summary>Wave 4 (U10): recovery requests của chính customer — resume pending.</summary>
    [HttpGet("mine")]
    public async Task<IActionResult> Mine(CancellationToken ct)
    {
        var auth = await customerContext.AuthenticateAsync(Request, ct);
        if (auth.Status == AtrsCustomerTokenStatus.DownstreamUnavailable)
            return StatusCode(503, new { error = "Dịch vụ đang bảo trì, vui lòng thử lại sau." });
        if (auth.TrustSubjectId is null)
            return Unauthorized(new { error = "Token không hợp lệ hoặc đã hết hạn." });

        return Ok(await recoveryCaseManager.ListMineAsync(auth.TrustSubjectId.Value, ct));
    }

    /// <summary>Wave 4 (U10): user tự huỷ request đang mở.</summary>
    [HttpPost("{id:guid}/cancel")]
    public async Task<IActionResult> Cancel(Guid id, CancellationToken ct)
    {
        var auth = await customerContext.AuthenticateAsync(Request, ct);
        if (auth.Status == AtrsCustomerTokenStatus.DownstreamUnavailable)
            return StatusCode(503, new { error = "Dịch vụ đang bảo trì, vui lòng thử lại sau." });
        if (auth.TrustSubjectId is null)
            return Unauthorized(new { error = "Token không hợp lệ hoặc đã hết hạn." });

        var dto = await recoveryCaseManager.CancelAsync(id, auth.TrustSubjectId.Value, ct);
        return dto is null ? NotFound() : Ok(dto);
    }

    /// <summary>Tiered proof ①: verify recovery code → risk engine → approve/review/reject.</summary>
    [HttpPost("{id:guid}/verify-code")]
    public async Task<IActionResult> VerifyCode(Guid id, [FromBody] VerifyCodeRequest request, CancellationToken ct)
    {
        var auth = await customerContext.AuthenticateAsync(Request, ct);
        if (auth.Status == AtrsCustomerTokenStatus.DownstreamUnavailable)
            return StatusCode(503, new { error = "Dịch vụ đang bảo trì, vui lòng thử lại sau." });
        if (auth.TrustSubjectId is null)
            return Unauthorized(new { error = "Token không hợp lệ hoặc đã hết hạn." });

        var dto = await recoveryCaseManager.GetAsync(id, ct);
        if (dto is null || dto.TrustSubjectId != auth.TrustSubjectId.Value)
            return NotFound();

        var result = await recoveryCaseManager.VerifyCodePathAsync(id, request.Code, request.DeviceId, request.HighAccountValue, ct);
        return result.Success
            ? Ok(result)
            : BadRequest(new { success = false, reason = result.Reason });
    }

    public sealed record SubmitRecoveryRequest(
        RecoveryRequestType RequestType,
        string IdempotencyKey,
        Guid? NewDeviceId,
        string? EvidenceSummaryJson);

    public sealed record VerifyCodeRequest(string Code, Guid? DeviceId, bool HighAccountValue = false);
}

/// <summary>
/// Internal staff endpoints (legacy — route api/internal không qua YARP):
/// approve/reject sau manual review. ATRS-10 subset: permission recovery.approve
/// (SystemAdmin break-glass / grant / Owner legacy map).
/// </summary>
[ApiController]
[Route("api/internal/recovery-requests")]
[Authorize]
public class InternalRecoveryController(
    IRecoveryCaseManager recoveryCaseManager,
    IAtrsAuthorizationService authz,
    ILogger<InternalRecoveryController> logger) : ControllerBase
{
    [HttpPost("{id:guid}/approve")]
    public async Task<IActionResult> Approve(Guid id, CancellationToken ct)
    {
        if (!await StaffPrincipal.HasPermissionAsync(authz, User, AtrsPermissions.RecoveryApprove, ct: ct))
            return Forbid();
        var reviewerId = StaffPrincipal.UserId(User);
        var dto = await recoveryCaseManager.ManualApproveAsync(id, reviewerId, ct);
        logger.LogInformation("Recovery {RequestId} approved by reviewer {ReviewerId}", id, reviewerId);
        return Ok(dto);
    }

    [HttpPost("{id:guid}/reject")]
    public async Task<IActionResult> Reject(Guid id, [FromBody] RejectRequest request, CancellationToken ct)
    {
        if (!await StaffPrincipal.HasPermissionAsync(authz, User, AtrsPermissions.RecoveryApprove, ct: ct))
            return Forbid();
        var reviewerId = StaffPrincipal.UserId(User);
        if (string.IsNullOrWhiteSpace(request.ReasonCode))
            return BadRequest(new { error = "ReasonCode là bắt buộc (audit)." });
        var dto = await recoveryCaseManager.ManualRejectAsync(id, reviewerId, request.ReasonCode, ct);
        logger.LogInformation("Recovery {RequestId} rejected by reviewer {ReviewerId} ({Reason})", id, reviewerId, request.ReasonCode);
        return Ok(dto);
    }

    public sealed record RejectRequest(string ReasonCode);
}