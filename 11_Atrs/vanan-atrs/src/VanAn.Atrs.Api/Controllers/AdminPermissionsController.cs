using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VanAn.Atrs.Api.Auth;
using VanAn.Atrs.Domain;
using VanAn.Atrs.Infra;

namespace VanAn.Atrs.Api.Controllers;

/// <summary>
/// Staff permission grants (A22 subset Wave 3) — SystemAdmin only (break-glass quản trị).
/// Grant/revoke có audit log; list trả metadata (không PII).
/// </summary>
[ApiController]
[Route("api/v1/account-trust/admin/permissions")]
[Authorize]
public class AdminPermissionsController(
    IAtrsAuthorizationService authz,
    ILogger<AdminPermissionsController> logger) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] Guid? demoUserId, CancellationToken ct)
    {
        if (!StaffPrincipal.IsSystemAdmin(User))
            return Forbid();
        var grants = await authz.ListAllGrantsAsync(demoUserId, ct);
        return Ok(grants.Select(g => new
        {
            g.Id, g.DemoUserId, g.Permission, g.ScopeTenantId,
            g.GrantedBy, g.GrantedAt, g.ExpiresAt, g.IsRevoked
        }));
    }

    [HttpPost]
    public async Task<IActionResult> Grant([FromBody] GrantPermissionRequest request, CancellationToken ct)
    {
        if (!StaffPrincipal.IsSystemAdmin(User))
            return Forbid();
        if (request.DemoUserId == Guid.Empty || string.IsNullOrWhiteSpace(request.Permission))
            return BadRequest(new { error = "demoUserId + permission là bắt buộc." });
        if (!AtrsPermissions.All.Contains(request.Permission))
            return BadRequest(new { error = $"Permission không hợp lệ: {request.Permission}. Hợp lệ: {string.Join(", ", AtrsPermissions.All)}" });

        var grant = await authz.GrantAsync(
            request.DemoUserId, request.Permission,
            grantedBy: StaffPrincipal.UserId(User).ToString(),
            scopeTenantId: request.ScopeTenantId, expiresAt: request.ExpiresAt, ct: ct);
        logger.LogInformation("ATRS permission granted: {Permission} → user {UserId} by {By}",
            request.Permission, request.DemoUserId, StaffPrincipal.UserId(User));
        return Ok(new { grant.Id, grant.DemoUserId, grant.Permission, grant.ScopeTenantId, grant.GrantedAt, grant.ExpiresAt });
    }

    [HttpPost("{id:guid}/revoke")]
    public async Task<IActionResult> Revoke(Guid id, CancellationToken ct)
    {
        if (!StaffPrincipal.IsSystemAdmin(User))
            return Forbid();
        var ok = await authz.RevokeAsync(id, ct);
        if (ok)
            logger.LogInformation("ATRS permission grant {GrantId} revoked by {By}", id, StaffPrincipal.UserId(User));
        return ok ? Ok(new { success = true }) : NotFound();
    }

    public sealed record GrantPermissionRequest(Guid DemoUserId, string Permission, Guid? ScopeTenantId, DateTime? ExpiresAt);
}
