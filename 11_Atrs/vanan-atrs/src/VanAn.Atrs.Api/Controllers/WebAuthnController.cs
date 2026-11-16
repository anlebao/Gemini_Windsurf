using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using VanAn.Atrs.Api.Auth;
using VanAn.Atrs.Contracts;
using VanAn.Atrs.Infra;

namespace VanAn.Atrs.Api.Controllers;

/// <summary>
/// WebAuthn/passkey ceremonies (FR-004, ATRS-5) — customer endpoints.
/// Auth: X-Customer-Token (forward ShopERP /me). Origin + RP ID client truyền → server verify
/// trong allowlist (Fido2 config). Finish authentication thành công → issue step-up token (amr=passkey).
/// </summary>
[ApiController]
[Route("api/v1/account-trust/webauthn")]
public class WebAuthnController(
    IAtrsCustomerContextService customerContext,
    IWebAuthnService webAuthnService,
    IStepUpService stepUpService,
    ILogger<WebAuthnController> logger) : ControllerBase
{
    [HttpPost("registration/begin")]
    public async Task<IActionResult> BeginRegistration([FromBody] BeginRegistrationRequest request, CancellationToken ct)
    {
        var (auth, error) = await AuthenticateAsync(ct);
        if (error is not null) return error;
        if (auth is null) return Unauthorized();

        try
        {
            var result = await webAuthnService.BeginRegistrationAsync(
                auth.TrustSubjectId.Value, request.DeviceRecordId, request.Origin, request.RpId, ct);
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPost("registration/finish")]
    public async Task<IActionResult> FinishRegistration([FromBody] FinishRegistrationRequest request, CancellationToken ct)
    {
        var (auth, error) = await AuthenticateAsync(ct);
        if (error is not null) return error;
        if (auth is null) return Unauthorized();

        var result = await webAuthnService.FinishRegistrationAsync(
            auth.TrustSubjectId.Value, request.Challenge, request.Origin, request.RpId,
            request.AttestationResponse, request.DeviceRecordId, ct);
        if (!result.Success)
            return BadRequest(new { success = false, reason = result.Reason });
        logger.LogInformation("Passkey registered for subject {SubjectId}", auth.TrustSubjectId.Value);
        return Ok(result);
    }

    [HttpPost("authentication/begin")]
    public async Task<IActionResult> BeginAuthentication([FromBody] BeginAuthenticationRequest request, CancellationToken ct)
    {
        var (auth, error) = await AuthenticateAsync(ct);
        if (error is not null) return error;
        if (auth is null) return Unauthorized();

        try
        {
            var result = await webAuthnService.BeginAuthenticationAsync(
                auth.TrustSubjectId.Value, request.Origin, request.RpId, ct);
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>Finish authentication → success = step-up token (amr=passkey) cho revoke/regenerate.</summary>
    [HttpPost("authentication/finish")]
    public async Task<IActionResult> FinishAuthentication([FromBody] FinishAuthenticationRequest request, CancellationToken ct)
    {
        var (auth, error) = await AuthenticateAsync(ct);
        if (error is not null) return error;
        if (auth is null) return Unauthorized();

        var result = await webAuthnService.FinishAuthenticationAsync(
            auth.TrustSubjectId.Value, request.Challenge, request.Origin, request.RpId,
            request.AssertionResponse, ct);
        if (!result.Success)
            return BadRequest(new { success = false, reason = result.Reason });

        var stepUpToken = stepUpService.Issue(auth.TrustSubjectId.Value, "passkey");
        logger.LogInformation("Passkey step-up success for subject {SubjectId}", auth.TrustSubjectId.Value);
        return Ok(result with { StepUpToken = stepUpToken });
    }

    [HttpGet("credentials")]
    public async Task<IActionResult> ListCredentials(CancellationToken ct)
    {
        var (auth, error) = await AuthenticateAsync(ct);
        if (error is not null) return error;
        if (auth is null) return Unauthorized();

        return Ok(await webAuthnService.ListCredentialsAsync(auth.TrustSubjectId.Value, ct));
    }

    /// <summary>Revoke credential — yêu cầu step-up (X-Step-Up-Token, ATRS-3).</summary>
    [HttpPost("credentials/{id:guid}/revoke")]
    public async Task<IActionResult> RevokeCredential(Guid id, CancellationToken ct)
    {
        var (auth, error) = await AuthenticateAsync(ct);
        if (error is not null) return error;
        if (auth is null) return Unauthorized();

        var amr = stepUpService.Validate(Request.Headers["X-Step-Up-Token"].FirstOrDefault(), auth.TrustSubjectId.Value);
        if (amr is null)
            return Unauthorized(new { error = "Cần xác thực gần đây (step-up) để thu hồi passkey." });

        var ok = await webAuthnService.RevokeCredentialAsync(auth.TrustSubjectId.Value, id, ct);
        return ok ? Ok(new { success = true }) : NotFound();
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

    public sealed record BeginRegistrationRequest(string Origin, string RpId, Guid? DeviceRecordId);
    public sealed record FinishRegistrationRequest(string Challenge, string Origin, string RpId, JsonElement AttestationResponse, Guid? DeviceRecordId);
    public sealed record BeginAuthenticationRequest(string Origin, string RpId);
    public sealed record FinishAuthenticationRequest(string Challenge, string Origin, string RpId, JsonElement AssertionResponse);
}