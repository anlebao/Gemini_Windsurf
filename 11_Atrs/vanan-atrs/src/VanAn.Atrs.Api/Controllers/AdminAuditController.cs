using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VanAn.Atrs.Api.Auth;
using VanAn.Atrs.Contracts;
using VanAn.Atrs.Domain;
using VanAn.Atrs.Infra;

namespace VanAn.Atrs.Api.Controllers;

/// <summary>
/// ATRS-10 A20 — audit explorer. READ-ONLY: chỉ GET, không update/delete.
/// Permission: audit.read. Response sanitized (KHÔNG payload JSON).
/// Mọi access không cần audit riêng (bản thân explorer đã là audit log).
/// </summary>
[ApiController]
[Route("api/v1/account-trust/admin/security-events")]
[Authorize]
public class AdminAuditController(
    IAuditQueryService auditQueryService,
    IAtrsAuthorizationService authz,
    ILogger<AdminAuditController> logger) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Query(
        [FromQuery] string? eventType, [FromQuery] string? outcome, [FromQuery] string? actorType,
        [FromQuery] string? sourceApplication, [FromQuery] Guid? subjectId,
        [FromQuery] DateTime? from, [FromQuery] DateTime? to,
        [FromQuery] int skip = 0, [FromQuery] int take = 50, CancellationToken ct = default)
    {
        if (!await StaffPrincipal.HasPermissionAsync(authz, User, AtrsPermissions.AuditRead, ct: ct))
            return Forbid();
        var page = await auditQueryService.QueryAsync(
            new AuditQuery(eventType, outcome, actorType, sourceApplication, subjectId, from, to, skip, take), ct);
        return Ok(page);
    }
}