using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VanAn.CoreHub.Infrastructure;
using VanAn.Shared.Domain;
using VanAn.Shared.Domain.Aggregates.ProductCostPriceAggregate;

namespace VanAn.Gateway.Controllers
{
    /// <summary>
    /// Sprint 7 Q1 — Product cost price admin endpoints (CRUD).
    /// Vạn An's negotiated cost price per product per tenant.
    /// Auth: SystemAdmin Bearer JWT (platform-level, cross-tenant).
    /// </summary>
    [ApiController]
    [Authorize(Policy = "SystemAdmin", AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    [Route("api/admin/product-cost-prices")]
    public class ProductCostPriceController(
        IVanAnDbContext dbContext,
        ILogger<ProductCostPriceController> logger) : ControllerBase
    {
        private readonly IVanAnDbContext _dbContext = dbContext;
        private readonly ILogger<ProductCostPriceController> _logger = logger;

        /// <summary>
        /// GET /api/admin/product-cost-prices?tenantId=...&amp;page=1&amp;pageSize=20
        /// Returns paginated cost prices, optionally filtered by tenant.
        /// </summary>
        [HttpGet]
        [Authorize(Policy = "SystemAdmin", AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        public async Task<IActionResult> GetList([FromQuery] Guid? tenantId, [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
        {
            if (page < 1) page = 1;
            if (pageSize < 1) pageSize = 20;
            if (pageSize > 100) pageSize = 100;

            var query = _dbContext.ProductCostPrices
                .IgnoreQueryFilters()
                .AsNoTracking();

            if (tenantId.HasValue)
            {
                var tid = new TenantId(tenantId.Value);
                query = query.Where(p => p.TenantId == tid);
            }

            var total = await query.CountAsync();

            var items = await query
                .OrderByDescending(p => p.UpdatedAt ?? DateTime.MinValue)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(p => new ProductCostPriceDto
                {
                    Id = p.Id,
                    TenantId = p.TenantId.Value,
                    ProductId = p.ProductId,
                    CostPrice = p.CostPrice,
                    UpdatedAt = p.UpdatedAt,
                    UpdatedBy = p.UpdatedBy
                })
                .ToListAsync();

            return Ok(new { total, items });
        }

        /// <summary>
        /// POST /api/admin/product-cost-prices
        /// Create or update cost price for a product (upsert by TenantId + ProductId).
        /// </summary>
        [HttpPost]
        [Authorize(Policy = "SystemAdmin", AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        public async Task<IActionResult> Upsert([FromBody] UpsertProductCostPriceRequest request)
        {
            if (request.TenantId == Guid.Empty)
                return BadRequest(new { error = "TenantId không hợp lệ." });
            if (request.ProductId == Guid.Empty)
                return BadRequest(new { error = "ProductId không hợp lệ." });
            if (request.CostPrice < 0)
                return BadRequest(new { error = "CostPrice không được âm." });

            var tenantId = new TenantId(request.TenantId);
            var adminId = GetAdminUserId();

            var existing = await _dbContext.ProductCostPrices
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(p => p.TenantId == tenantId && p.ProductId == request.ProductId);

            if (existing == null)
            {
                var entity = new ProductCostPrice(tenantId, request.ProductId, request.CostPrice, adminId);
                _dbContext.ProductCostPrices.Add(entity);
            }
            else
            {
                existing.Update(request.CostPrice, adminId);
            }

            await _dbContext.SaveChangesAsync();

            _logger.LogInformation("Product cost price upserted: Tenant={TenantId} Product={ProductId} CostPrice={CostPrice} by {AdminId}",
                request.TenantId, request.ProductId, request.CostPrice, adminId);

            return Ok(new { tenantId = request.TenantId, productId = request.ProductId, costPrice = request.CostPrice });
        }

        /// <summary>
        /// DELETE /api/admin/product-cost-prices/{id}
        /// Delete a cost price entry.
        /// </summary>
        [HttpDelete("{id}")]
        [Authorize(Policy = "SystemAdmin", AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        public async Task<IActionResult> Delete(Guid id)
        {
            var entity = await _dbContext.ProductCostPrices
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(p => p.Id == id);

            if (entity == null)
                return NotFound(new { error = $"Cost price {id} not found." });

            _dbContext.ProductCostPrices.Remove(entity);
            await _dbContext.SaveChangesAsync();

            _logger.LogInformation("Product cost price deleted: {Id} by admin", id);

            return Ok(new { deleted = id });
        }

        private Guid GetAdminUserId()
        {
            var userIdClaim = User.FindFirst("sub")?.Value
                ?? User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
                ?? User.FindFirst("userId")?.Value;
            return Guid.TryParse(userIdClaim, out var id) ? id : Guid.Empty;
        }
    }

    public class ProductCostPriceDto
    {
        public Guid Id { get; set; }
        public Guid TenantId { get; set; }
        public Guid ProductId { get; set; }
        public decimal CostPrice { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public Guid? UpdatedBy { get; set; }
    }

    public class UpsertProductCostPriceRequest
    {
        public Guid TenantId { get; set; }
        public Guid ProductId { get; set; }
        public decimal CostPrice { get; set; }
    }
}
