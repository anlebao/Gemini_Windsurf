using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
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

        /// <summary>
        /// #110 fix: Create a new tenant in Gateway PostgreSQL (NOT ShopERP SQLite).
        /// ShopERP's TenantManagementService writes to SQLite via IVanAnDbContext,
        /// but the tenant list is loaded from Gateway PG — causing create→list mismatch.
        /// This endpoint ensures tenant creation goes to PG (source of truth).
        /// </summary>
        [HttpPost]
        public async Task<ActionResult<TenantDto>> Create([FromBody] CreateTenantApiRequest request, CancellationToken ct)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(request.Name))
                    return BadRequest(new { error = "Name is required." });

                var createRequest = new CreateTenantRequest(
                    request.Name,
                    request.BusinessType,
                    request.HkdGroup,
                    request.ContactEmail,
                    request.ContactPhone,
                    request.Address,
                    request.TaxCode);

                var tenant = await _tenantService.CreateTenantAsync(createRequest, ct);
                return CreatedAtAction(nameof(GetById), new { tenantId = tenant.Id }, MapToDto(tenant));
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { error = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating tenant {TenantName}", request.Name);
                return StatusCode(500, new { error = ex.Message });
            }
        }

        /// <summary>
        /// Get a single tenant by ID. Used by ShopERP AdminController.Impersonate
        /// to validate tenant existence + status against the PG source of truth (Option C)
        /// instead of querying ShopERP SQLite (which has no Tenants table).
        /// </summary>
        [HttpGet("{tenantId:guid}")]
        public async Task<ActionResult<TenantDto>> GetById(Guid tenantId)
        {
            try
            {
                var tenant = await _tenantService.GetTenantByIdAsync(new TenantId(tenantId));
                if (tenant == null)
                {
                    return NotFound(new { error = $"Tenant {tenantId} not found." });
                }
                return Ok(MapToDto(tenant));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting tenant {TenantId}", tenantId);
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
                    request.Address, request.TaxCode,
                    Slug: null,
                    Latitude: request.Latitude,
                    Longitude: request.Longitude,
                    SocialLinksFb: request.SocialLinksFb,
                    SocialLinksTiktok: request.SocialLinksTiktok,
                    BrandStory: request.BrandStory,
                    Theme: request.Theme,
                    LogoUrl: request.LogoUrl,
                    NavColor: request.NavColor,
                    HeaderColor: request.HeaderColor,
                    FooterColor: request.FooterColor);
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

        /// <summary>
        /// Tenant Profile Page (2026-07-21): Update URL slug for /store/{slug} route.
        /// Slug must be lowercase, alphanumeric + hyphens, max 100 chars. Null clears the slug.
        /// Returns 409 if slug already taken by another tenant.
        /// </summary>
        [HttpPut("{tenantId:guid}/slug")]
        public async Task<ActionResult> UpdateSlug(Guid tenantId, [FromBody] UpdateSlugRequest request)
        {
            try
            {
                await _tenantService.UpdateSlugAsync(new TenantId(tenantId), request.Slug);
                return Ok(new { success = true });
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { error = ex.Message });
            }
            catch (DbUpdateException ex) when (ex.InnerException?.Message.Contains("IX_Tenants_Settings_Slug") == true)
            {
                return Conflict(new { error = "Slug đã được sử dụng bởi tenant khác. Vui lòng chọn slug khác." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating slug for tenant {TenantId}", tenantId);
                return StatusCode(500, new { error = ex.Message });
            }
        }

        /// <summary>
        /// Bug 1 fix: Change tenant BusinessType (SystemAdmin correction).
        /// Guard: returns 409 Conflict if tenant has accounting data.
        /// </summary>
        [HttpPut("{tenantId:guid}/business-type")]
        public async Task<ActionResult> ChangeBusinessType(Guid tenantId, [FromBody] ChangeBusinessTypeApiRequest request, CancellationToken ct)
        {
            try
            {
                await _tenantService.ChangeBusinessTypeAsync(
                    new TenantId(tenantId),
                    request.BusinessType,
                    request.HkdGroup,
                    request.Reason,
                    ct);
                return Ok(new { success = true });
            }
            catch (InvalidOperationException ex)
            {
                return Conflict(new { error = ex.Message });
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { error = ex.Message });
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new { error = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error changing BusinessType for tenant {TenantId}", tenantId);
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
            Slug = t.Settings?.Slug,
            Latitude = t.Settings?.Latitude,
            Longitude = t.Settings?.Longitude,
            SocialLinksFb = t.Settings?.SocialLinksFb,
            SocialLinksTiktok = t.Settings?.SocialLinksTiktok,
            BrandStory = t.Settings?.BrandStory,
            Theme = t.Settings?.Theme ?? ThemeType.Classic,
            LogoUrl = t.Settings?.LogoUrl,
            NavColor = t.Settings?.NavColor,
            HeaderColor = t.Settings?.HeaderColor,
            FooterColor = t.Settings?.FooterColor,
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
        /// <summary>Tenant Profile Page (2026-07-21): URL slug for /store/{slug}. Null if not set.</summary>
        public string? Slug { get; init; }
        /// <summary>Store Finder: latitude in decimal degrees. Null if not set.</summary>
        public double? Latitude { get; init; }
        /// <summary>Store Finder: longitude in decimal degrees. Null if not set.</summary>
        public double? Longitude { get; init; }
        public string? SocialLinksFb { get; init; }
        public string? SocialLinksTiktok { get; init; }
        public string? BrandStory { get; init; }
        public ThemeType Theme { get; init; } = ThemeType.Classic;
        /// <summary>#93 — KhachLink style customization.</summary>
        public string? LogoUrl { get; init; }
        public string? NavColor { get; init; }
        public string? HeaderColor { get; init; }
        public string? FooterColor { get; init; }
        public DateTime CreatedAt { get; init; }
    }

    /// <summary>Tenant Profile Page (2026-07-21): Request body for PUT /api/v1/tenants/{id}/slug.</summary>
    public record UpdateSlugRequest
    {
        /// <summary>Lowercase, alphanumeric + hyphens, max 100 chars. Null clears the slug.</summary>
        public string? Slug { get; init; }
    }

    public record UpdateTenantProfileApiRequest
    {
        public string Name { get; init; } = "";
        public string? ContactEmail { get; init; }
        public string? ContactPhone { get; init; }
        public string? Address { get; init; }
        public string? TaxCode { get; init; }
        /// <summary>Store Finder: latitude in decimal degrees. Null = preserve existing.</summary>
        public double? Latitude { get; init; }
        /// <summary>Store Finder: longitude in decimal degrees. Null = preserve existing.</summary>
        public double? Longitude { get; init; }
        public string? SocialLinksFb { get; init; }
        public string? SocialLinksTiktok { get; init; }
        public string? BrandStory { get; init; }
        public ThemeType Theme { get; init; } = ThemeType.Classic;
        /// <summary>#93 — KhachLink style customization. Null = preserve existing.</summary>
        public string? LogoUrl { get; init; }
        public string? NavColor { get; init; }
        public string? HeaderColor { get; init; }
        public string? FooterColor { get; init; }
    }

    public record AssignShopInstanceRequest
    {
        public Guid ShopInstanceId { get; init; }
    }

    /// <summary>Bug 1 fix: Request body for PUT /api/v1/tenants/{id}/business-type.</summary>
    public record ChangeBusinessTypeApiRequest
    {
        public BusinessType BusinessType { get; init; }
        /// <summary>Required when BusinessType=HouseholdBusiness. Must be null when BusinessType=Company.</summary>
        public HKDGroup? HkdGroup { get; init; }
        /// <summary>Admin reason for the change (audit trail).</summary>
        public string Reason { get; init; } = "";
    }

    /// <summary>#110 fix: Request body for POST /api/v1/tenants (create tenant in Gateway PG).</summary>
    public record CreateTenantApiRequest
    {
        public string Name { get; init; } = "";
        public BusinessType BusinessType { get; init; } = BusinessType.Company;
        /// <summary>Required when BusinessType=HouseholdBusiness. Null when BusinessType=Company.</summary>
        public HKDGroup? HkdGroup { get; init; }
        public string? ContactEmail { get; init; }
        public string? ContactPhone { get; init; }
        public string? Address { get; init; }
        public string? TaxCode { get; init; }
    }
}
