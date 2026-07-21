using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VanAn.CoreHub.Services;
using VanAn.Shared.Domain;
using VanAn.Shared.Domain.Aggregates.TenantAggregate;
using Tenant = VanAn.Shared.Domain.Aggregates.TenantAggregate.Tenant;

namespace VanAn.Gateway.Controllers
{
    /// <summary>
    /// Gateway API for tenant management (SystemAdmin).
    /// All operations go to PostgreSQL (Gateway DB) — NOT ShopERP SQLite.
    /// </summary>
    [ApiController]
    [Route("api/v1/tenants")]
    [Authorize(Policy = "SystemAdmin")]
    public class TenantsController(
        ITenantManagementService tenantService,
        ILogger<TenantsController> logger) : ControllerBase
    {
        private readonly ITenantManagementService _tenantService = tenantService;
        private readonly ILogger<TenantsController> _logger = logger;

        [HttpGet]
        public async Task<ActionResult<List<TenantDto>>> ListAll()
        {
            try
            {
                var tenants = await _tenantService.ListTenantsAsync();
                return Ok(tenants.Select(MapToDto).ToList());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error listing tenants");
                return StatusCode(500, new { error = "Internal server error" });
            }
        }

        [HttpPut("{tenantId:guid}/profile")]
        public async Task<ActionResult> UpdateProfile(Guid tenantId, [FromBody] UpdateTenantProfileApiRequest request)
        {
            try
            {
                var profileRequest = new UpdateTenantProfileRequest(
                    request.Name, request.ContactEmail, request.ContactPhone,
                    request.Address, request.TaxCode);
                await _tenantService.UpdateProfileAsync(new TenantId(tenantId), profileRequest);
                return Ok(new { success = true });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating tenant {TenantId} profile", tenantId);
                return StatusCode(500, new { error = ex.Message });
            }
        }

        [HttpPut("{tenantId:guid}/shop-instance")]
        public async Task<ActionResult> AssignShopInstance(Guid tenantId, [FromBody] AssignShopInstanceRequest request)
        {
            try
            {
                if (request.ShopInstanceId == Guid.Empty)
                    return BadRequest(new { error = "ShopInstanceId is required" });

                await _tenantService.AssignShopInstanceAsync(new TenantId(tenantId), request.ShopInstanceId);
                return Ok(new { success = true });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error assigning ShopInstance {ShopInstanceId} to tenant {TenantId}",
                    request.ShopInstanceId, tenantId);
                return StatusCode(500, new { error = ex.Message });
            }
        }

        private static TenantDto MapToDto(Tenant t) => new()
        {
            Id = t.Id,
            Name = t.Name,
            BusinessType = t.BusinessType,
            Status = t.Status,
            ShopInstanceId = t.ShopInstanceId,
            ContactEmail = t.Settings?.ContactEmail,
            ContactPhone = t.Settings?.ContactPhone,
            Address = t.Settings?.Address,
            TaxCode = t.Settings?.TaxCode,
            CreatedAt = t.CreatedAt
        };
    }

    public record TenantDto
    {
        public Guid Id { get; init; }
        public string Name { get; init; } = "";
        public BusinessType BusinessType { get; init; }
        public TenantStatus Status { get; init; }
        public Guid? ShopInstanceId { get; init; }
        public string? ContactEmail { get; init; }
        public string? ContactPhone { get; init; }
        public string? Address { get; init; }
        public string? TaxCode { get; init; }
        public DateTime CreatedAt { get; init; }
    }

    public record UpdateTenantProfileApiRequest
    {
        public string Name { get; init; } = "";
        public string? ContactEmail { get; init; }
        public string? ContactPhone { get; init; }
        public string? Address { get; init; }
        public string? TaxCode { get; init; }
    }

    public record AssignShopInstanceRequest
    {
        public Guid ShopInstanceId { get; init; }
    }
}
