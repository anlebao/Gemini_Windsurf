using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VanAn.CoreHub.Infrastructure;
using VanAn.Shared.Domain;

namespace VanAn.Gateway.Controllers
{
    /// <summary>
    /// Phase 6 (Admin UI): CRUD API for FeaturedProduct (sysadmin-curated marketing products).
    /// SystemAdmin Bearer JWT only. PG-only entity (NOT in ShopERP SQLite).
    /// </summary>
    [ApiController]
    [Authorize(Policy = "SystemAdmin", AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    [Route("api/v1/featured-products")]
    public class FeaturedProductsController(
        IVanAnDbContext dbContext,
        ILogger<FeaturedProductsController> logger) : ControllerBase
    {
        private readonly IVanAnDbContext _dbContext = dbContext;
        private readonly ILogger<FeaturedProductsController> _logger = logger;

        /// <summary>List all FeaturedProducts (optional tenantId filter).</summary>
        [HttpGet]
        [Authorize(Policy = "SystemAdmin", AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        public async Task<ActionResult<List<FeaturedProductDto>>> List(
            [FromQuery] Guid? tenantId,
            [FromQuery] bool? activeOnly,
            CancellationToken ct = default)
        {
            // IgnoreQueryFilters: SystemAdmin sees ALL featured products across all tenants
            // (global query filter e.TenantId == CurrentTenantId would filter out everything
            // because SystemAdmin JWT has tenant_id=Guid.Empty)
            var query = _dbContext.FeaturedProducts.AsNoTracking().IgnoreQueryFilters();
            if (tenantId.HasValue && tenantId.Value != Guid.Empty)
                query = query.Where(f => f.TenantId == new TenantId(tenantId.Value));
            if (activeOnly == true)
                query = query.Where(f => f.IsActive);

            var items = await query.OrderBy(f => f.SortOrder).ThenBy(f => f.DisplayName).ToListAsync(ct);
            return Ok(items.Select(ToDto).ToList());
        }

        /// <summary>Get a single FeaturedProduct by Id.</summary>
        [HttpGet("{id:guid}")]
        [Authorize(Policy = "SystemAdmin", AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        public async Task<ActionResult<FeaturedProductDto>> GetById(Guid id, CancellationToken ct = default)
        {
            var fp = await _dbContext.FeaturedProducts.AsNoTracking().IgnoreQueryFilters().FirstOrDefaultAsync(f => f.Id == id, ct);
            if (fp == null) return NotFound();
            return Ok(ToDto(fp));
        }

        /// <summary>Create a new FeaturedProduct.</summary>
        [HttpPost]
        [Authorize(Policy = "SystemAdmin", AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        public async Task<ActionResult<FeaturedProductDto>> Create(
            [FromBody] CreateFeaturedProductRequest request, CancellationToken ct = default)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            var fp = new FeaturedProduct(
                new TenantId(request.TenantId),
                request.ProductId,
                request.DisplayName,
                request.DisplayPrice,
                request.DisplayDescription,
                request.ImageUrl,
                request.SortOrder,
                request.VatRate);

            _dbContext.FeaturedProducts.Add(fp);
            await _dbContext.SaveChangesAsync(ct);

            _logger.LogInformation("Created FeaturedProduct {Id} for product {ProductId} (tenant {TenantId})",
                fp.Id, fp.ProductId, fp.TenantId.Value);

            return CreatedAtAction(nameof(GetById), new { id = fp.Id }, ToDto(fp));
        }

        /// <summary>Update display info for an existing FeaturedProduct.</summary>
        [HttpPut("{id:guid}")]
        [Authorize(Policy = "SystemAdmin", AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        public async Task<ActionResult<FeaturedProductDto>> Update(
            Guid id, [FromBody] UpdateFeaturedProductRequest request, CancellationToken ct = default)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            var fp = await _dbContext.FeaturedProducts.IgnoreQueryFilters().FirstOrDefaultAsync(f => f.Id == id, ct);
            if (fp == null) return NotFound();

            fp.UpdateDisplayInfo(request.DisplayName, request.DisplayPrice,
                request.DisplayDescription, request.ImageUrl, request.SortOrder, request.VatRate);
            if (request.IsActive.HasValue)
                fp.SetActive(request.IsActive.Value);

            await _dbContext.SaveChangesAsync(ct);
            return Ok(ToDto(fp));
        }

        /// <summary>Delete a FeaturedProduct (soft delete via IsDeleted flag).</summary>
        [HttpDelete("{id:guid}")]
        [Authorize(Policy = "SystemAdmin", AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        public async Task<IActionResult> Delete(Guid id, CancellationToken ct = default)
        {
            var fp = await _dbContext.FeaturedProducts.IgnoreQueryFilters().FirstOrDefaultAsync(f => f.Id == id, ct);
            if (fp == null) return NotFound();

            _dbContext.FeaturedProducts.Remove(fp);
            await _dbContext.SaveChangesAsync(ct);

            _logger.LogInformation("Deleted FeaturedProduct {Id}", id);
            return NoContent();
        }

        private static FeaturedProductDto ToDto(FeaturedProduct fp) => new()
        {
            Id = fp.Id,
            ProductId = fp.ProductId,
            TenantId = fp.TenantId.Value,
            DisplayName = fp.DisplayName,
            DisplayDescription = fp.DisplayDescription,
            ImageUrl = fp.ImageUrl,
            DisplayPrice = fp.DisplayPrice,
            VatRate = fp.VatRate,
            IsActive = fp.IsActive,
            SortOrder = fp.SortOrder,
            FeaturedAt = fp.FeaturedAt
        };
    }

    public record FeaturedProductDto
    {
        public Guid Id { get; set; }
        public Guid ProductId { get; set; }
        public Guid TenantId { get; set; }
        public string DisplayName { get; set; } = "";
        public string? DisplayDescription { get; set; }
        public string? ImageUrl { get; set; }
        public decimal DisplayPrice { get; set; }
        public decimal VatRate { get; set; }
        public bool IsActive { get; set; }
        public int SortOrder { get; set; }
        public DateTime FeaturedAt { get; set; }
    }

    public record CreateFeaturedProductRequest
    {
        public Guid ProductId { get; set; }
        public Guid TenantId { get; set; }
        public string DisplayName { get; set; } = "";
        public decimal DisplayPrice { get; set; }
        public decimal VatRate { get; set; } = 0.10m;
        public string? DisplayDescription { get; set; }
        public string? ImageUrl { get; set; }
        public int SortOrder { get; set; }
    }

    public record UpdateFeaturedProductRequest
    {
        public string DisplayName { get; set; } = "";
        public decimal DisplayPrice { get; set; }
        public decimal VatRate { get; set; } = 0.10m;
        public string? DisplayDescription { get; set; }
        public string? ImageUrl { get; set; }
        public int SortOrder { get; set; }
        public bool? IsActive { get; set; }
    }
}
