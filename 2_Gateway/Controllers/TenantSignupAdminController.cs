using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VanAn.CoreHub.Services.Onboarding;

namespace VanAn.Gateway.Controllers;

/// <summary>
/// Self-Serve Signup admin (2026-11-09 — SRS FR-3..5): sysadmin xem hồ sơ,
/// verify-signup (dùng credentials merchant — password KHÔNG lộ), reject-pending.
/// SystemAdmin-only — cùng policy với TenantPendingController.
/// </summary>
[ApiController]
[Authorize(Policy = "SystemAdmin")]
public class TenantSignupAdminController(
    IMerchantSignupService signupService) : ControllerBase
{
    /// <summary>Hồ sơ self-serve đầy đủ (ảnh, menu, SĐT, flags) — KHÔNG chứa password.</summary>
    [HttpGet("api/v1/tenants/{tenantId:guid}/signup-detail")]
    public async Task<ActionResult<SignupDetailDto>> GetSignupDetail(Guid tenantId, CancellationToken ct = default)
    {
        var detail = await signupService.GetSignupDetailAsync(tenantId, ct);
        return detail is null ? NotFound(new { error = "Tenant not found." }) : Ok(detail);
    }

    /// <summary>
    /// Verify Pending self-serve → Active. Mặc định dùng credentials merchant tự đặt
    /// (password protected trong payload — sysadmin KHÔNG thấy). Override được qua request.
    /// </summary>
    [HttpPost("api/v1/tenants/{tenantId:guid}/verify-signup")]
    public async Task<ActionResult<VerifySignupResult>> VerifySignup(
        Guid tenantId,
        [FromBody] VerifySignupRequest? request,
        CancellationToken ct = default)
    {
        var sysAdminUserId = GetSysAdminUserId();
        if (sysAdminUserId == Guid.Empty)
            return Unauthorized(new { error = "SysAdmin user ID not found in JWT claims." });

        try
        {
            var result = await signupService.VerifySignupAsync(tenantId, request ?? new VerifySignupRequest(), sysAdminUserId, ct);
            return Ok(result);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { error = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { error = ex.Message });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>Từ chối Pending tenant (bắt buộc lý do — audit log).</summary>
    [HttpPost("api/v1/tenants/{tenantId:guid}/reject-pending")]
    public async Task<IActionResult> RejectPending(
        Guid tenantId,
        [FromBody] RejectPendingRequest request,
        CancellationToken ct = default)
    {
        var sysAdminUserId = GetSysAdminUserId();
        if (sysAdminUserId == Guid.Empty)
            return Unauthorized(new { error = "SysAdmin user ID not found in JWT claims." });

        try
        {
            await signupService.RejectPendingAsync(tenantId, request.Reason, sysAdminUserId, ct);
            return NoContent();
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { error = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { error = ex.Message });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    private Guid GetSysAdminUserId()
    {
        // Gateway JWT: MapInboundClaims=false → "sub" short-form (pattern TenantPendingController)
        var userIdClaim = User.FindFirst("sub")?.Value
            ?? User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        return Guid.TryParse(userIdClaim, out var userId) ? userId : Guid.Empty;
    }
}
