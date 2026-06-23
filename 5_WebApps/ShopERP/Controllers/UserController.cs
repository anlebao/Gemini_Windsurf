using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using VanAn.CoreHub.Services;
using VanAn.Shared.Domain;
using DemoUser = VanAn.Shared.Domain.Aggregates.UserAggregate.DemoUser;
using UserRole = VanAn.Shared.Domain.Aggregates.UserAggregate.UserRole;

namespace VanAn.ShopERP.Controllers
{
    /// <summary>
    /// Wave 6: User lifecycle CRUD. Restricted to Owner role for writes; StoreManagement can list.
    /// </summary>
    [ApiController]
    [Route("api/users")]
    public class UserController(
        IUserManagementService userService,
        IRoleAssignmentService roleService,
        ILogger<UserController> logger) : ControllerBase
    {
        // ── Create ─────────────────────────────────────────────────────────────

        [HttpPost]
        [Authorize(Policy = "OwnerOnly")]
        public async Task<IActionResult> CreateUser(
            [FromBody] CreateUserRequest request,
            CancellationToken ct = default)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            try
            {
                var user = await userService.CreateUserAsync(
                    GetCurrentTenantId(),
                    request.Username,
                    request.Password,
                    request.DisplayName,
                    request.Role,
                    ct);
                logger.LogInformation("API: User created {UserId}", user.Id);
                return CreatedAtAction(nameof(GetUser), new { id = user.Id }, UserDto.From(user));
            }
            catch (InvalidOperationException ex)
            {
                return UnprocessableEntity(new { error = ex.Message });
            }
        }

        // ── List ───────────────────────────────────────────────────────────────

        [HttpGet]
        [Authorize(Policy = "StoreManagement")]
        public async Task<IActionResult> ListUsers(CancellationToken ct = default)
        {
            var users = await userService.ListUsersAsync(GetCurrentTenantId(), ct);
            return Ok(users.Select(UserDto.From));
        }

        // ── Get single ────────────────────────────────────────────────────────

        [HttpGet("{id:guid}")]
        [Authorize(Policy = "StoreManagement")]
        public async Task<IActionResult> GetUser(Guid id, CancellationToken ct = default)
        {
            try
            {
                var user = await userService.GetUserByIdAsync(id, GetCurrentTenantId(), ct);
                if (user is null) return NotFound();
                return Ok(UserDto.From(user));
            }
            catch (UnauthorizedAccessException)
            {
                return Forbid();
            }
        }

        // ── Update profile ───────────────────────────────────────────────────

        [HttpPatch("{id:guid}")]
        [Authorize(Policy = "OwnerOnly")]
        public async Task<IActionResult> UpdateProfile(
            Guid id,
            [FromBody] UpdateUserProfileRequest request,
            CancellationToken ct = default)
        {
            try
            {
                await userService.UpdateProfileAsync(id, GetCurrentTenantId(), request.DisplayName, ct);
                return NoContent();
            }
            catch (KeyNotFoundException) { return NotFound(); }
            catch (InvalidOperationException ex) { return UnprocessableEntity(new { error = ex.Message }); }
            catch (UnauthorizedAccessException) { return Forbid(); }
        }

        // ── Change password ────────────────────────────────────────────────────

        [HttpPost("{id:guid}/change-password")]
        [Authorize(Policy = "OwnerOnly")]
        public async Task<IActionResult> ChangePassword(
            Guid id,
            [FromBody] ChangePasswordRequest request,
            CancellationToken ct = default)
        {
            try
            {
                await userService.ChangePasswordAsync(id, GetCurrentTenantId(), request.Password, ct);
                return NoContent();
            }
            catch (KeyNotFoundException) { return NotFound(); }
            catch (InvalidOperationException ex) { return UnprocessableEntity(new { error = ex.Message }); }
            catch (UnauthorizedAccessException) { return Forbid(); }
        }

        // ── Lifecycle ───────────────────────────────────────────────────────────

        [HttpPost("{id:guid}/deactivate")]
        [Authorize(Policy = "OwnerOnly")]
        public async Task<IActionResult> Deactivate(Guid id, CancellationToken ct = default)
        {
            try
            {
                await userService.DeactivateUserAsync(id, GetCurrentTenantId(), ct);
                return NoContent();
            }
            catch (KeyNotFoundException) { return NotFound(); }
            catch (InvalidOperationException ex) { return UnprocessableEntity(new { error = ex.Message }); }
            catch (UnauthorizedAccessException) { return Forbid(); }
        }

        [HttpPost("{id:guid}/reactivate")]
        [Authorize(Policy = "OwnerOnly")]
        public async Task<IActionResult> Reactivate(Guid id, CancellationToken ct = default)
        {
            try
            {
                await userService.ReactivateUserAsync(id, GetCurrentTenantId(), ct);
                return NoContent();
            }
            catch (KeyNotFoundException) { return NotFound(); }
            catch (InvalidOperationException ex) { return UnprocessableEntity(new { error = ex.Message }); }
            catch (UnauthorizedAccessException) { return Forbid(); }
        }

        // ── Role assignment ───────────────────────────────────────────────────

        [HttpPost("{id:guid}/roles")]
        [Authorize(Policy = "OwnerOnly")]
        public async Task<IActionResult> AssignRole(
            Guid id,
            [FromBody] AssignRoleRequest request,
            CancellationToken ct = default)
        {
            try
            {
                await roleService.AssignRoleToUserAsync(id, GetCurrentTenantId(), request.Role, ct);
                return NoContent();
            }
            catch (KeyNotFoundException) { return NotFound(); }
            catch (UnauthorizedAccessException) { return Forbid(); }
        }

        [HttpDelete("{id:guid}/roles/{role}")]
        [Authorize(Policy = "OwnerOnly")]
        public async Task<IActionResult> RevokeRole(Guid id, string role, CancellationToken ct = default)
        {
            if (!Enum.TryParse<UserRole>(role, true, out var userRole))
                return BadRequest(new { error = "Invalid role" });

            try
            {
                await roleService.RevokeRoleAsync(id, GetCurrentTenantId(), userRole, ct);
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

    public record UserDto(
        Guid Id,
        string Username,
        string DisplayName,
        UserRole Role,
        bool IsActive,
        DateTime CreatedAt)
    {
        public static UserDto From(DemoUser u) => new(
            u.Id,
            u.Username,
            u.DisplayName,
            u.Role,
            u.IsActive,
            u.CreatedAt);
    }

    public record CreateUserRequest(
        string Username,
        string Password,
        string DisplayName,
        UserRole Role);

    public record UpdateUserProfileRequest(string DisplayName);

    public record ChangePasswordRequest(string Password);

    public record AssignRoleRequest(UserRole Role);
}
