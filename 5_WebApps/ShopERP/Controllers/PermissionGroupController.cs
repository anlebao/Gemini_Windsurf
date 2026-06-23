using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VanAn.CoreHub.Services;
using VanAn.Shared.Domain;
using PermissionGroup = VanAn.Shared.Domain.Aggregates.UserAggregate.PermissionGroup;
using UserRole = VanAn.Shared.Domain.Aggregates.UserAggregate.UserRole;

namespace VanAn.ShopERP.Controllers
{
    /// <summary>
    /// Wave 6: Permission group CRUD. Restricted to Owner role.
    /// </summary>
    [ApiController]
    [Route("api/permission-groups")]
    [Authorize(Policy = "OwnerOnly")]
    public class PermissionGroupController(
        IPermissionGroupService groupService,
        ILogger<PermissionGroupController> logger) : ControllerBase
    {
        // ── Create ─────────────────────────────────────────────────────────────

        [HttpPost]
        public async Task<IActionResult> CreateGroup(
            [FromBody] CreatePermissionGroupRequest request,
            CancellationToken ct = default)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            try
            {
                var group = await groupService.CreateGroupAsync(GetCurrentTenantId(), request.Name, request.Description, ct);
                logger.LogInformation("API: Permission group created {GroupId}", group.Id);
                return CreatedAtAction(nameof(GetGroup), new { id = group.Id }, PermissionGroupDto.From(group));
            }
            catch (InvalidOperationException ex)
            {
                return UnprocessableEntity(new { error = ex.Message });
            }
        }

        // ── List ───────────────────────────────────────────────────────────────

        [HttpGet]
        public async Task<IActionResult> ListGroups(CancellationToken ct = default)
        {
            var groups = await groupService.ListGroupsAsync(GetCurrentTenantId(), ct);
            return Ok(groups.Select(PermissionGroupDto.From));
        }

        // ── Get single ────────────────────────────────────────────────────────

        [HttpGet("{id:guid}")]
        public async Task<IActionResult> GetGroup(Guid id, CancellationToken ct = default)
        {
            try
            {
                var group = await groupService.GetGroupAsync(id, GetCurrentTenantId(), ct);
                if (group is null) return NotFound();
                return Ok(PermissionGroupDto.From(group));
            }
            catch (UnauthorizedAccessException)
            {
                return Forbid();
            }
        }

        // ── Update profile ─────────────────────────────────────────────────────

        [HttpPatch("{id:guid}")]
        public async Task<IActionResult> UpdateGroup(
            Guid id,
            [FromBody] UpdatePermissionGroupRequest request,
            CancellationToken ct = default)
        {
            try
            {
                await groupService.UpdateGroupAsync(id, GetCurrentTenantId(), request.Name, request.Description, ct);
                return NoContent();
            }
            catch (KeyNotFoundException) { return NotFound(); }
            catch (InvalidOperationException ex) { return UnprocessableEntity(new { error = ex.Message }); }
            catch (UnauthorizedAccessException) { return Forbid(); }
        }

        // ── Roles ──────────────────────────────────────────────────────────────

        [HttpPost("{id:guid}/roles")]
        public async Task<IActionResult> AddRole(
            Guid id,
            [FromBody] AddRoleToGroupRequest request,
            CancellationToken ct = default)
        {
            try
            {
                await groupService.AddRoleToGroupAsync(id, GetCurrentTenantId(), request.Role, ct);
                return NoContent();
            }
            catch (KeyNotFoundException) { return NotFound(); }
            catch (InvalidOperationException ex) { return UnprocessableEntity(new { error = ex.Message }); }
            catch (UnauthorizedAccessException) { return Forbid(); }
        }

        [HttpDelete("{id:guid}/roles/{role}")]
        public async Task<IActionResult> RemoveRole(Guid id, string role, CancellationToken ct = default)
        {
            if (!Enum.TryParse<UserRole>(role, true, out var userRole))
                return BadRequest(new { error = "Invalid role" });

            try
            {
                await groupService.RemoveRoleFromGroupAsync(id, GetCurrentTenantId(), userRole, ct);
                return NoContent();
            }
            catch (KeyNotFoundException) { return NotFound(); }
            catch (UnauthorizedAccessException) { return Forbid(); }
        }

        // ── Helpers ─────────────────────────────────────────────────────────────

        private TenantId GetCurrentTenantId()
        {
            var tenantClaim = User.FindFirst("tenant_id")?.Value ?? User.FindFirst("TenantId")?.Value;
            if (string.IsNullOrWhiteSpace(tenantClaim) || !Guid.TryParse(tenantClaim, out var tenantId))
                throw new UnauthorizedAccessException("Tenant ID is missing or invalid.");
            return new TenantId(tenantId);
        }
    }

    // ── DTOs ───────────────────────────────────────────────────────────────────

    public record PermissionGroupDto(
        Guid Id,
        string Name,
        string? Description,
        IReadOnlyList<UserRole> Roles)
    {
        public static PermissionGroupDto From(PermissionGroup g) => new(
            g.Id,
            g.Name,
            g.Description,
            g.GetEffectiveRoles());
    }

    public record CreatePermissionGroupRequest(string Name, string? Description);

    public record UpdatePermissionGroupRequest(string Name, string? Description);

    public record AddRoleToGroupRequest(UserRole Role);
}
