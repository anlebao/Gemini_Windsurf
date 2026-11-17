using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VanAn.Atrs.Api.Auth;
using VanAn.Atrs.Contracts;
using VanAn.Atrs.Domain;
using VanAn.Atrs.Infra;

namespace VanAn.Atrs.Api.Controllers;

/// <summary>
/// ATRS-10 A21 — security policy: read/propose (policy.manage) + approve/reject (policy.approve).
/// Maker-checker enforce ở service (approver ≠ proposer). Bảng policy tự là audit trail.
/// KHÔNG lộ policy cho người không có policy.manage (fraud thresholds).
/// </summary>
[ApiController]
[Route("api/v1/account-trust/admin/security-policies")]
[Authorize]
public class AdminPolicyController(
    ISecurityPolicyService policyService,
    IAtrsAuthorizationService authz,
    ILogger<AdminPolicyController> logger) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct)
    {
        if (!await StaffPrincipal.HasPermissionAsync(authz, User, AtrsPermissions.PolicyManage, ct: ct))
            return Forbid();
        return Ok(await policyService.ListAsync(ct));
    }

    [HttpPost]
    public async Task<IActionResult> Propose([FromBody] ProposePolicyRequest request, CancellationToken ct)
    {
        if (!await StaffPrincipal.HasPermissionAsync(authz, User, AtrsPermissions.PolicyManage, ct: ct))
            return Forbid();
        if (string.IsNullOrWhiteSpace(request.PolicyKey) || string.IsNullOrWhiteSpace(request.PolicyGroup) || string.IsNullOrWhiteSpace(request.JsonValue))
            return BadRequest(new { error = "PolicyKey/PolicyGroup/JsonValue là bắt buộc." });
        try
        {
            var dto = await policyService.ProposeAsync(
                request.PolicyKey.Trim(), request.PolicyGroup.Trim(), request.JsonValue,
                StaffPrincipal.UserId(User), request.ChangeNote, ct);
            logger.LogInformation("Policy {Key} v{Version} proposed by {By}", dto.PolicyKey, dto.Version, StaffPrincipal.UserId(User));
            return Ok(dto);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { error = ex.Message });
        }
    }

    [HttpPost("{id:guid}/approve")]
    public async Task<IActionResult> Approve(Guid id, CancellationToken ct)
    {
        if (!await StaffPrincipal.HasPermissionAsync(authz, User, AtrsPermissions.PolicyApprove, ct: ct))
            return Forbid();
        try
        {
            var dto = await policyService.ApproveAsync(id, StaffPrincipal.UserId(User), ct);
            logger.LogInformation("Policy {Key} v{Version} approved by {By}", dto.PolicyKey, dto.Version, StaffPrincipal.UserId(User));
            return Ok(dto);
        }
        catch (InvalidOperationException ex)
        {
            return ex.Message.Contains("cannot be the proposer", StringComparison.Ordinal)
                ? StatusCode(403, new { error = ex.Message })
                : Conflict(new { error = ex.Message });
        }
    }

    [HttpPost("{id:guid}/reject")]
    public async Task<IActionResult> Reject(Guid id, CancellationToken ct)
    {
        if (!await StaffPrincipal.HasPermissionAsync(authz, User, AtrsPermissions.PolicyApprove, ct: ct))
            return Forbid();
        try
        {
            var dto = await policyService.RejectAsync(id, StaffPrincipal.UserId(User), ct);
            logger.LogInformation("Policy {Key} v{Version} rejected by {By}", dto.PolicyKey, dto.Version, StaffPrincipal.UserId(User));
            return Ok(dto);
        }
        catch (InvalidOperationException ex)
        {
            return ex.Message.Contains("cannot be the proposer", StringComparison.Ordinal)
                ? StatusCode(403, new { error = ex.Message })
                : Conflict(new { error = ex.Message });
        }
    }

    public sealed record ProposePolicyRequest(string PolicyKey, string PolicyGroup, string JsonValue, string? ChangeNote);
}