using Microsoft.AspNetCore.Mvc;
using VanAn.Atrs.Api.Auth;
using VanAn.Atrs.Infra;

namespace VanAn.Atrs.Api.Controllers;

/// <summary>
/// Security center (U06) — GET /api/v1/account-trust/me. Customer endpoints.
/// Trả summary an toàn (status/assurance/counts) — KHÔNG lộ technical IDs/hash.
/// </summary>
[ApiController]
[Route("api/v1/account-trust")]
public class SecurityCenterController(
    IAtrsCustomerContextService customerContext,
    ISecurityCenterService securityCenterService) : ControllerBase
{
    [HttpGet("me")]
    public async Task<IActionResult> Me(CancellationToken ct)
    {
        var (auth, error) = await AuthenticateAsync(ct);
        if (error is not null) return error;
        if (auth is null) return Unauthorized();

        return Ok(await securityCenterService.GetSummaryAsync(auth.TrustSubjectId.Value, ct));
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
}