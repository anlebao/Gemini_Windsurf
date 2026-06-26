using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VanAn.CoreHub.Infrastructure;
using VanAn.Shared.Domain;

namespace VanAn.ShopERP.Controllers
{
    /// <summary>
    /// Public product catalog API for KhachLink customer-facing display.
    /// Read-only endpoints are anonymous; management endpoints require authorization.
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class ProductsController(VanAnDbContext dbContext, ILogger<ProductsController> logger) : ControllerBase
    {
        private readonly VanAnDbContext _dbContext = dbContext;
        private readonly ILogger<ProductsController> _logger = logger;

        /// <summary>
        /// Get active products for a shop's public catalog (KhachLink).
        /// </summary>
        [HttpGet]
        [AllowAnonymous]
        public async Task<ActionResult<List<ProductCatalogItem>>> GetProducts([FromQuery] Guid? shopId)
        {
            try
            {
                IQueryable<Product> query = _dbContext.Products
                    .Where(p => p.IsActive && !p.IsDeleted);

                if (shopId.HasValue)
                {
                    query = query.Where(p => p.TenantId == new TenantId(shopId.Value));
                }

                List<Product> products = await query
                    .OrderBy(p => p.Category)
                    .ThenBy(p => p.Name)
                    .ToListAsync();

                List<ProductCatalogItem> result = products.Select(p => new ProductCatalogItem
                {
                    ProductId = p.ProductId.Value,
                    Name = p.Name,
                    Description = p.Description,
                    Price = p.Price,
                    Category = p.Category,
                    ImageUrl = p.ImageUrl,
                    VatRate = p.VatRate
                }).ToList();

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting products catalog for shopId: {ShopId}", shopId);
                return StatusCode(500, "Internal server error");
            }
        }

        /// <summary>
        /// Get a single product by ID (public).
        /// </summary>
        [HttpGet("{id:guid}")]
        [AllowAnonymous]
        public async Task<ActionResult<ProductCatalogItem>> GetProduct(Guid id)
        {
            try
            {
                Product? product = await _dbContext.Products
                    .FirstOrDefaultAsync(p => p.ProductId == new ProductId(id) && p.IsActive && !p.IsDeleted);

                if (product == null)
                {
                    return NotFound();
                }

                return Ok(new ProductCatalogItem
                {
                    ProductId = product.ProductId.Value,
                    Name = product.Name,
                    Description = product.Description,
                    Price = product.Price,
                    Category = product.Category,
                    ImageUrl = product.ImageUrl,
                    VatRate = product.VatRate
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting product {ProductId}", id);
                return StatusCode(500, "Internal server error");
            }
        }
    }

    public class ProductCatalogItem
    {
        public Guid ProductId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public decimal Price { get; set; }
        public string? Category { get; set; }
        public string? ImageUrl { get; set; }
        public decimal VatRate { get; set; }
    }
}
