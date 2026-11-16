using Microsoft.AspNetCore.Mvc;
using VanAn.Atrs.Api.Auth;
using VanAn.Atrs.Domain;
using VanAn.Atrs.Infra;

namespace VanAn.Atrs.Api.Controllers;

/// <summary>
/// Device registry (U07/U08) — customer endpoints. Auth: X-Customer-Token.
/// Revoke/mark-lost yêu cầu step-up (X-Step-Up-Token, ATRS-3). Device ID = risk signal,
/// KHÔNG phải proof-of-identity badge (wording audit theo UI SRS §12).
/// </summary>
[ApiController]
[Route("api/v1/account-trust/devices")]
public class DevicesController(
    IAtrsCustomerContextService customerContext,
    IDeviceService deviceService,
    IStepUpService stepUpService,
    ILogger<DevicesController> logger) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct)
    {
        var (auth, error) = await AuthenticateAsync(ct);
        if (error is not null) return error;
        if (auth is null) return Unauthorized();
        return Ok(await deviceService.ListAsync(auth.TrustSubjectId.Value, ct));
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct)
    {
        var (auth, error) = await AuthenticateAsync(ct);
        if (error is not null) return error;
        if (auth is null) return Unauthorized();

        var dto = await deviceService.GetAsync(auth.TrustSubjectId.Value, id, ct);
        return dto is null ? NotFound() : Ok(dto);
    }

    [HttpPatch("{id:guid}/rename")]
    public async Task<IActionResult> Rename(Guid id, [FromBody] RenameRequest request, CancellationToken ct)
    {
        var (auth, error) = await AuthenticateAsync(ct);
        if (error is not null) return error;
        if (auth is null) return Unauthorized();
        if (string.IsNullOrWhiteSpace(request.FriendlyName))
            return BadRequest(new { error = "Tên thiết bị không được để trống." });

        var dto = await deviceService.RenameAsync(auth.TrustSubjectId.Value, id, request.FriendlyName, ct);
        return dto is null ? NotFound() : Ok(dto);
    }

    /// <summary>Revoke device + toàn bộ credential của nó — yêu cầu step-up.</summary>
    [HttpPost("{id:guid}/revoke")]
    public async Task<IActionResult> Revoke(Guid id, [FromBody] RevokeRequest? request, CancellationToken ct)
    {
        var (auth, error) = await AuthenticateAsync(ct);
        if (error is not null) return error;
        if (auth is null) return Unauthorized();

        var amr = stepUpService.Validate(Request.Headers["X-Step-Up-Token"].FirstOrDefault(), auth.TrustSubjectId.Value);
        if (amr is null)
            return Unauthorized(new { error = "Cần xác thực gần đây (step-up) để thu hồi thiết bị." });

        var ok = await deviceService.RevokeAsync(auth.TrustSubjectId.Value, id, request?.ReasonCode, ct);
        if (!ok) return NotFound();
        logger.LogInformation("Device {DeviceId} revoked (subject {SubjectId}, amr={Amr})", id, auth.TrustSubjectId.Value, amr);
        return Ok(new { success = true });
    }

    [HttpPost("{id:guid}/report-lost")]
    public async Task<IActionResult> ReportLost(Guid id, CancellationToken ct)
    {
        var (auth, error) = await AuthenticateAsync(ct);
        if (error is not null) return error;
        if (auth is null) return Unauthorized();

        var amr = stepUpService.Validate(Request.Headers["X-Step-Up-Token"].FirstOrDefault(), auth.TrustSubjectId.Value);
        if (amr is null)
            return Unauthorized(new { error = "Cần xác thực gần đây (step-up) để báo mất thiết bị." });

        var ok = await deviceService.MarkLostAsync(auth.TrustSubjectId.Value, id, ct);
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

    public sealed record RenameRequest(string FriendlyName);
    public sealed record RevokeRequest(string? ReasonCode);
}