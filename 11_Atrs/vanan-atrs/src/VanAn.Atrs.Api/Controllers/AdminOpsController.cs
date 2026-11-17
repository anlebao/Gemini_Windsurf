using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VanAn.Atrs.Api.Auth;
using VanAn.Atrs.Domain;
using VanAn.Atrs.Infra;

namespace VanAn.Atrs.Api.Controllers;

/// <summary>
/// A14 — operations dashboard aggregate (data thật từ DB, không hardcode).
/// Permission: recovery.view HOẶC dispute.view (staff ops role) — SystemAdmin break-glass.
/// </summary>
[ApiController]
[Route("api/v1/account-trust/admin/dashboard")]
[Authorize]
public class AdminOpsController(
    IRecoveryCaseManager recoveryCaseManager,
    IAtrsAuthorizationService authz,
    ILogger<AdminOpsController> logger) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Dashboard(CancellationToken ct)
    {
        bool allowed = await StaffPrincipal.HasPermissionAsync(authz, User, AtrsPermissions.RecoveryView, ct: ct)
            || await StaffPrincipal.HasPermissionAsync(authz, User, AtrsPermissions.DisputeView, ct: ct);
        if (!allowed)
            return Forbid();
        var dto = await recoveryCaseManager.GetDashboardAsync(ct);
        logger.LogDebug("ATRS dashboard fetched by {UserId}", StaffPrincipal.UserId(User));
        return Ok(dto);
    }
}