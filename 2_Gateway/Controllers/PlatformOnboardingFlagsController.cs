using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VanAn.CoreHub.Services.Onboarding;

namespace VanAn.Gateway.Controllers;

/// <summary>
/// Platform onboarding flags (2026-11-09 — SRS FR-22): sysadmin toggle on/off
/// các entry-point self-serve (master + directory CTA + QR + deeplink prefill).
/// GET public (Directory SSR + KhachLink đọc) · PUT SystemAdmin-only.
/// </summary>
[ApiController]
[Route("api/v1/platform/onboarding-flags")]
public class PlatformOnboardingFlagsController(
    IPlatformOnboardingFlagsService flagsService) : ControllerBase
{
    /// <summary>Public — trả 4 bool. Default true khi chưa config.</summary>
    [HttpGet]
    [AllowAnonymous]
    [ResponseCache(Duration = 30)]
    public async Task<ActionResult<OnboardingFlagsDto>> Get(CancellationToken ct = default)
        => Ok(await flagsService.GetFlagsAsync(ct));

    /// <summary>SysAdmin update cả 4 flags (full state).</summary>
    [HttpPut]
    [Authorize(Policy = "SystemAdmin")]
    public async Task<ActionResult<OnboardingFlagsDto>> Update(
        [FromBody] UpdateOnboardingFlagsRequest request,
        CancellationToken ct = default)
    {
        if (request is null)
            return BadRequest(new { error = "Body bắt buộc." });

        var sysAdminUserId = GetSysAdminUserId();
        if (sysAdminUserId == Guid.Empty)
            return Unauthorized(new { error = "SysAdmin user ID not found in JWT claims." });

        await flagsService.SetFlagsAsync(request, sysAdminUserId, ct);
        return Ok(await flagsService.GetFlagsAsync(ct));
    }

    private Guid GetSysAdminUserId()
    {
        var userIdClaim = User.FindFirst("sub")?.Value
            ?? User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        return Guid.TryParse(userIdClaim, out var userId) ? userId : Guid.Empty;
    }
}
