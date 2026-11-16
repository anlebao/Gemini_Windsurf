using Microsoft.AspNetCore.Mvc;
using VanAn.Atrs.Api.Auth;
using VanAn.Atrs.Contracts;
using VanAn.Atrs.Infra;

namespace VanAn.Atrs.Api.Controllers;

/// <summary>
/// Recovery codes (FR-008) — customer endpoints. Auth: X-Customer-Token (forward ShopERP /me).
/// Generate yêu cầu step-up (ATRS-3 step-up token — wave sau); wave 1: authenticated customer
/// + đã có recovery request hợp lệ hoặc profile ACTIVE (ghi chú card ATRS-4).
/// KHÔNG trả code sau lần hiển thị đầu.
/// </summary>
[ApiController]
[Route("api/v1/account-trust/recovery-codes")]
public class RecoveryCodesController(
    IAtrsCustomerContextService customerContext,
    IRecoveryCodeService recoveryCodeService,
    ILogger<RecoveryCodesController> logger) : ControllerBase
{
    [HttpGet("status")]
    public async Task<IActionResult> Status(CancellationToken ct)
    {
        var auth = await customerContext.AuthenticateAsync(Request, ct);
        if (auth.Status == AtrsCustomerTokenStatus.DownstreamUnavailable)
            return StatusCode(503, new { error = "Dịch vụ đang bảo trì, vui lòng thử lại sau." });
        if (auth.TrustSubjectId is null)
            return Unauthorized(new { error = "Token không hợp lệ hoặc đã hết hạn." });

        return Ok(await recoveryCodeService.GetStatusAsync(auth.TrustSubjectId.Value, ct));
    }

    /// <summary>Generate bộ codes mới — bộ cũ bị REVOKED. Plaintext trả về ĐÚNG 1 lần.</summary>
    [HttpPost("generate")]
    public async Task<IActionResult> Generate(CancellationToken ct)
    {
        var auth = await customerContext.AuthenticateAsync(Request, ct);
        if (auth.Status == AtrsCustomerTokenStatus.DownstreamUnavailable)
            return StatusCode(503, new { error = "Dịch vụ đang bảo trì, vui lòng thử lại sau." });
        if (auth.TrustSubjectId is null)
            return Unauthorized(new { error = "Token không hợp lệ hoặc đã hết hạn." });

        // TODO(ATRS-3): yêu cầu step-up token (X-Step-Up-Token) — wave sau khi step-up primitive hoàn thiện.
        var result = await recoveryCodeService.GenerateSetAsync(auth.TrustSubjectId.Value, ct);
        logger.LogInformation("Recovery codes generated for subject {SubjectId}", auth.TrustSubjectId.Value);
        return Ok(new { result.SetId, result.Codes, result.GeneratedAt });
    }

    /// <summary>Verify + consume 1 code — dùng trong recovery wizard (tiered proof ①).</summary>
    [HttpPost("verify")]
    public async Task<IActionResult> Verify([FromBody] VerifyRecoveryCodeRequest request, CancellationToken ct)
    {
        var auth = await customerContext.AuthenticateAsync(Request, ct);
        if (auth.Status == AtrsCustomerTokenStatus.DownstreamUnavailable)
            return StatusCode(503, new { error = "Dịch vụ đang bảo trì, vui lòng thử lại sau." });
        if (auth.TrustSubjectId is null)
            return Unauthorized(new { error = "Token không hợp lệ hoặc đã hết hạn." });

        var result = await recoveryCodeService.VerifyAndConsumeAsync(
            auth.TrustSubjectId.Value, request.Code, request.DeviceId, ct);
        return result.Success
            ? Ok(result)
            : BadRequest(result); // reason: InvalidCode | Locked — generic, anti-enumeration
    }

    public sealed record VerifyRecoveryCodeRequest(string Code, Guid? DeviceId);
}