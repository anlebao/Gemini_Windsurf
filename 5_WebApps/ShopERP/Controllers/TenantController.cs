using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VanAn.CoreHub.Services;
using VanAn.Shared.Domain;
using VanAn.Shared.Domain.Aggregates.TenantAggregate;
using Tenant = VanAn.Shared.Domain.Aggregates.TenantAggregate.Tenant;

namespace VanAn.ShopERP.Controllers
{
    /// <summary>
    /// Wave 5: Tenant lifecycle CRUD. Platform-level SystemAdmin only (cross-tenant operations).
    /// POST   /api/tenants              → create tenant
    /// GET    /api/tenants              → list all tenants
    /// GET    /api/tenants/{id}         → get single tenant
    /// PATCH  /api/tenants/{id}/profile → update name/settings
    /// POST   /api/tenants/{id}/suspend → suspend (reversible)
    /// POST   /api/tenants/{id}/reactivate → reactivate
    /// POST   /api/tenants/{id}/deactivate → permanently deactivate
    /// </summary>
    [ApiController]
    [Route("api/tenants")]
    [Authorize(Policy = "SystemAdmin")]
    public class TenantController(
        ITenantManagementService tenantService,
        ILogger<TenantController> logger) : ControllerBase
    {
        // ── Create ────────────────────────────────────────────────────────────

        [HttpPost]
        public async Task<IActionResult> CreateTenant(
            [FromBody] CreateTenantRequest request,
            CancellationToken ct = default)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            Tenant tenant = await tenantService.CreateTenantAsync(request, ct);
            logger.LogInformation("API: Tenant created {Id}", tenant.Id.Value);
            return CreatedAtAction(nameof(GetTenant), new { id = tenant.Id.Value }, TenantDto.From(tenant));
        }

        // ── List ──────────────────────────────────────────────────────────────

        [HttpGet]
        public async Task<IActionResult> ListTenants(CancellationToken ct = default)
        {
            IReadOnlyList<Tenant> tenants = await tenantService.ListTenantsAsync(ct);
            return Ok(tenants.Select(TenantDto.From));
        }

        // ── Get single ────────────────────────────────────────────────────────

        [HttpGet("{id:guid}")]
        public async Task<IActionResult> GetTenant(Guid id, CancellationToken ct = default)
        {
            Tenant? tenant = await tenantService.GetTenantByIdAsync(new TenantId(id), ct);
            if (tenant is null) return NotFound();
            return Ok(TenantDto.From(tenant));
        }

        // ── Update profile ────────────────────────────────────────────────────

        [HttpPatch("{id:guid}/profile")]
        public async Task<IActionResult> UpdateProfile(
            Guid id,
            [FromBody] UpdateTenantProfileRequest request,
            CancellationToken ct = default)
        {
            try
            {
                await tenantService.UpdateProfileAsync(new TenantId(id), request, ct);
                return NoContent();
            }
            catch (KeyNotFoundException) { return NotFound(); }
            catch (InvalidOperationException ex) { return UnprocessableEntity(new { error = ex.Message }); }
        }

        // ── Lifecycle ─────────────────────────────────────────────────────────

        [HttpPost("{id:guid}/suspend")]
        public async Task<IActionResult> Suspend(
            Guid id,
            [FromBody] ReasonRequest body,
            CancellationToken ct = default)
        {
            try
            {
                await tenantService.SuspendAsync(new TenantId(id), body.Reason, ct);
                return NoContent();
            }
            catch (KeyNotFoundException) { return NotFound(); }
            catch (InvalidOperationException ex) { return UnprocessableEntity(new { error = ex.Message }); }
        }

        [HttpPost("{id:guid}/reactivate")]
        public async Task<IActionResult> Reactivate(Guid id, CancellationToken ct = default)
        {
            try
            {
                await tenantService.ReactivateAsync(new TenantId(id), ct);
                return NoContent();
            }
            catch (KeyNotFoundException) { return NotFound(); }
            catch (InvalidOperationException ex) { return UnprocessableEntity(new { error = ex.Message }); }
        }

        [HttpPost("{id:guid}/deactivate")]
        public async Task<IActionResult> Deactivate(
            Guid id,
            [FromBody] ReasonRequest body,
            CancellationToken ct = default)
        {
            try
            {
                await tenantService.DeactivateAsync(new TenantId(id), body.Reason, ct);
                return NoContent();
            }
            catch (KeyNotFoundException) { return NotFound(); }
            catch (InvalidOperationException ex) { return UnprocessableEntity(new { error = ex.Message }); }
        }
    }

    // ── DTOs ──────────────────────────────────────────────────────────────────

    public record TenantDto(
        Guid Id,
        string Name,
        string BusinessType,
        string? HKDGroup,
        string Status,
        string? ContactEmail,
        string? ContactPhone,
        string? Address,
        string? TaxCode,
        DateTime CreatedAt)
    {
        public static TenantDto From(Tenant t) => new(
            t.Id.Value,
            t.Name,
            t.BusinessType.ToString(),
            t.HKDGroup?.ToString(),
            t.Status.ToString(),
            t.Settings.ContactEmail,
            t.Settings.ContactPhone,
            t.Settings.Address,
            t.Settings.TaxCode,
            t.CreatedAt);
    }

    public record ReasonRequest(string Reason);
}
