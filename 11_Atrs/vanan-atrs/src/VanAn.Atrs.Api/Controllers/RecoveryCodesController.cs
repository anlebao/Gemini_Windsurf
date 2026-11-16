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
    IStepUpService stepUpService,
    ILogger<RecoveryCodesController> logger) : ControllerBase
{
    [HttpGet("status")]
    public async Task<IActionResult> Status(CancellationToken ct)
    {
        var (auth, error) = await AuthenticateAsync(ct);
        if (error is not null) return error;
        if (auth is null) return Unauthorized();

        return Ok(await recoveryCodeService.GetStatusAsync(auth.TrustSubjectId.Value, ct));
    }

    /// <summary>Generate bộ codes mới — bộ cũ bị REVOKED. Plaintext trả về ĐÚNG 1 lần.
    /// Yêu cầu step-up (X-Step-Up-Token, ATRS-3): passkey assert hoặc recovery-code vừa verify.</summary>
    [HttpPost("generate")]
    public async Task<IActionResult> Generate(CancellationToken ct)
    {
        var (auth, error) = await AuthenticateAsync(ct);
        if (error is not null) return error;
        if (auth is null) return Unauthorized();

        var amr = stepUpService.Validate(Request.Headers["X-Step-Up-Token"].FirstOrDefault(), auth.TrustSubjectId.Value);
        if (amr is null)
            return Unauthorized(new { error = "Cần xác thực gần đây (step-up) để tạo recovery codes." });

        var result = await recoveryCodeService.GenerateSetAsync(auth.TrustSubjectId.Value, ct);
        logger.LogInformation("Recovery codes generated for subject {SubjectId} (amr={Amr})", auth.TrustSubjectId.Value, amr);
        return Ok(new { result.SetId, result.Codes, result.GeneratedAt });
    }

    /// <summary>Verify + consume 1 code — dùng trong recovery wizard (tiered proof ①).
    /// Thành công → trả step-up token (amr=recovery-code) để user tạo codes mới / quản lý thiết bị.</summary>
    [HttpPost("verify")]
    public async Task<IActionResult> Verify([FromBody] VerifyRecoveryCodeRequest request, CancellationToken ct)
    {
        var (auth, error) = await AuthenticateAsync(ct);
        if (error is not null) return error;
        if (auth is null) return Unauthorized();

        var result = await recoveryCodeService.VerifyAndConsumeAsync(
            auth.TrustSubjectId.Value, request.Code, request.DeviceId, ct);
        if (!result.Success)
            return BadRequest(result); // reason: InvalidCode | Locked — generic, anti-enumeration

        var stepUpToken = stepUpService.Issue(auth.TrustSubjectId.Value, "recovery-code");
        return Ok(result with { StepUpToken = stepUpToken });
    }

    private async Task<(AtrsCustomerAuthResult? auth, IActionResult? error)> AuthenticateAsync(CancellationToken ct)
    {
        var auth = await customerContext.AuthenticateAsync(Request, ct);
        if (auth.Status == AtrsCustomerTokenStatus.DownstreamUnavailable)
            return (null, StatusCode(503, new { error = "Dịch vụ đang bảo trì, vui lòng thử lại sau." }));
        if (auth.TrustSubjectId is null)
            return (null, Unauthorized(new { error = "Token không hợp lệ hoặc đã hết hạn." }));
        return (auth, null);
    }

    public sealed record VerifyRecoveryCodeRequest(string Code, Guid? DeviceId);
}