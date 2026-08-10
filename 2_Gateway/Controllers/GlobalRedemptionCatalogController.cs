using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VanAn.CoreHub.Infrastructure;
using VanAn.Shared.Domain;
using VanAn.Shared.Domain.Common;

namespace VanAn.Gateway.Controllers
{
    /// <summary>
    /// #121.3: Global Redemption Catalog — stored in Gateway PostgreSQL (TenantId = Empty, IsGlobal = true).
    /// System admin creates global catalog items visible to ALL tenants.
    /// GET is anonymous (KhachLink PWA reads without auth).
    /// POST/PUT/DELETE require SystemAdmin policy (cookie auth from ShopERP admin UI).
    /// </summary>
    [ApiController]
    [Route("api/redemption/catalog/global")]
    [Authorize]
    public class GlobalRedemptionCatalogController(
        IVanAnDbContext dbContext,
        ILogger<GlobalRedemptionCatalogController> logger) : ControllerBase
    {
        private readonly IVanAnDbContext _dbContext = dbContext;
        private readonly ILogger<GlobalRedemptionCatalogController> _logger = logger;

        /// <summary>
        /// GET /api/redemption/catalog/global — returns all active global catalog items.
        /// Anonymous: KhachLink PWA reads this on /rewards page load (merged with tenant-local catalog).
        /// </summary>
        [HttpGet]
        [AllowAnonymous]
        public async Task<IActionResult> GetActiveGlobalCatalog()
        {
            var items = await _dbContext.RedemptionCatalogItems
                .IgnoreQueryFilters()
                .Where(i => i.IsGlobal && i.IsActive)
                .OrderByDescending(i => i.CreatedAt)
                .Select(i => new GlobalCatalogItemDto
                {
                    Id = i.Id,
                    ProductName = i.ProductName,
                    Description = i.Description,
                    ImageUrl = i.ImageUrl,
                    PointsRequired = i.PointsRequired,
                    StockCount = i.StockCount,
                    ValidFrom = i.ValidFrom,
                    ValidTo = i.ValidTo,
                    VoucherExpiryDays = i.VoucherExpiryDays,
                    IsGlobal = true
                })
                .ToListAsync();

            return Ok(items);
        }

        /// <summary>POST — create a global catalog item (SystemAdmin only).</summary>
        [HttpPost]
        [Authorize(Policy = "SystemAdmin")]
        public async Task<IActionResult> Create([FromBody] CreateGlobalCatalogItemRequest body)
        {
            if (string.IsNullOrWhiteSpace(body.ProductName) || body.PointsRequired <= 0)
                return BadRequest(new { error = "ProductName + PointsRequired required." });

            var item = new RedemptionCatalogItem(TenantId.Empty, body.ProductName, body.PointsRequired, isGlobal: true);
            item.UpdateDetails(body.ProductName, body.Description, body.ImageUrl,
                body.PointsRequired, body.StockCount, body.ValidTo, body.VoucherExpiryDays, isGlobal: true);
            _ = _dbContext.RedemptionCatalogItems.Add(item);
            await _dbContext.SaveChangesAsync();
            _logger.LogInformation("Created global catalog item {Id}: {Name} ({Points} pts)", item.Id, item.ProductName, item.PointsRequired);
            return Ok(new { id = item.Id, success = true });
        }

        /// <summary>PUT — update a global catalog item (SystemAdmin only).</summary>
        [HttpPut("{id:guid}")]
        [Authorize(Policy = "SystemAdmin")]
        public async Task<IActionResult> Update(Guid id, [FromBody] CreateGlobalCatalogItemRequest body)
        {
            var item = await _dbContext.RedemptionCatalogItems
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(i => i.Id == id && i.IsGlobal);
            if (item == null) return NotFound(new { error = "Global catalog item not found." });

            item.UpdateDetails(body.ProductName, body.Description, body.ImageUrl,
                body.PointsRequired, body.StockCount, body.ValidTo, body.VoucherExpiryDays, isGlobal: true);
            await _dbContext.SaveChangesAsync();
            return Ok(new { success = true });
        }

        /// <summary>DELETE — deactivate a global catalog item (SystemAdmin only).</summary>
        [HttpDelete("{id:guid}")]
        [Authorize(Policy = "SystemAdmin")]
        public async Task<IActionResult> Deactivate(Guid id)
        {
            var item = await _dbContext.RedemptionCatalogItems
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(i => i.Id == id && i.IsGlobal);
            if (item == null) return NotFound(new { error = "Global catalog item not found." });

            item.Deactivate();
            await _dbContext.SaveChangesAsync();
            return Ok(new { success = true });
        }
    }

    public class GlobalCatalogItemDto
    {
        public Guid Id { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string? ImageUrl { get; set; }
        public int PointsRequired { get; set; }
        public int? StockCount { get; set; }
        public DateTime ValidFrom { get; set; }
        public DateTime? ValidTo { get; set; }
        public int VoucherExpiryDays { get; set; }
        public bool IsGlobal { get; set; } = true;
    }

    public class CreateGlobalCatalogItemRequest
    {
        public string ProductName { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string? ImageUrl { get; set; }
        public int PointsRequired { get; set; }
        public int? StockCount { get; set; }
        public DateTime? ValidTo { get; set; }
        public int VoucherExpiryDays { get; set; } = 30;
    }
}
