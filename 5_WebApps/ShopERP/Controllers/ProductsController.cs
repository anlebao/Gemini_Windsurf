using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VanAn.CoreHub.Infrastructure;
using VanAn.CoreHub.Services;
using VanAn.ShopERP.Services;
using VanAn.Shared.Domain;
using VanAn.Shared.Domain.Common;

namespace VanAn.ShopERP.Controllers
{
    /// <summary>
    /// Public product catalog API for KhachLink customer-facing display.
    /// Read-only endpoints are anonymous; management endpoints require authorization.
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class ProductsController(IVanAnDbContext dbContext, ILogger<ProductsController> logger, CustomerRecommendationService recommendationService, IShopQrCodeService qrCodeService, IShopFeatureSettingsService? shopFeatureSettingsService = null) : ControllerBase
    {
        private readonly IVanAnDbContext _dbContext = dbContext;
        private readonly ILogger<ProductsController> _logger = logger;
        private readonly CustomerRecommendationService _recommendationService = recommendationService;
        private readonly IShopQrCodeService _qrCodeService = qrCodeService;
        // W3-T8: Shop feature settings — for QR_TableNumber_Enabled toggle
        private readonly IShopFeatureSettingsService? _shopFeatureSettingsService = shopFeatureSettingsService;

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
                    TenantId = p.TenantId.Value,
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
                    TenantId = product.TenantId.Value,
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

        /// <summary>
        /// Get personalized product recommendations for a customer based on order history.
        /// </summary>
        [HttpGet("recommended")]
        [AllowAnonymous]
        public async Task<ActionResult<List<RecommendedProductItem>>> GetRecommendedProducts(
            [FromQuery] Guid customerId,
            [FromQuery] Guid tenantId,
            [FromQuery] int topN = 10)
        {
            try
            {
                if (customerId == Guid.Empty || tenantId == Guid.Empty)
                {
                    return BadRequest("CustomerId and TenantId are required");
                }

                var recommendations = await _recommendationService.GetRecommendedProductsAsync(customerId, tenantId, topN);

                // If no recommendations (new customer), return empty list
                if (!recommendations.Any())
                {
                    return Ok(new List<RecommendedProductItem>());
                }

                var result = recommendations.Select(r => new RecommendedProductItem
                {
                    ProductId = r.ProductId,
                    Name = r.Name,
                    Description = r.Description,
                    Price = r.Price,
                    Category = r.Category,
                    ImageUrl = r.ImageUrl,
                    VatRate = r.VatRate,
                    FrequencyScore = r.FrequencyScore,
                    TotalSpent = r.TotalSpent,
                    RecommendationReason = r.RecommendationReason
                }).ToList();

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting recommended products for customer {CustomerId}", customerId);
                return StatusCode(500, "Internal server error");
            }
        }

        /// <summary>
        /// FIX-BATCH-2: Generate QR code PNG for a product. KhachLink scanner scans this QR
        /// to add the product to cart. Payload is JSON: {ProductId, ShopId, Timestamp}.
        /// </summary>
        [HttpGet("{id:guid}/qr")]
        [AllowAnonymous]
        public async Task<ActionResult> GetProductQrCode(Guid id, [FromQuery] Guid? tenantId, [FromQuery] string? tableNumber)
        {
            try
            {
                Product? product = await _dbContext.Products
                    .FirstOrDefaultAsync(p => p.ProductId == new ProductId(id) && p.IsActive && !p.IsDeleted);

                if (product == null)
                {
                    return NotFound($"Product {id} not found or inactive");
                }

                Guid shopId = tenantId ?? product.TenantId.Value;

                // W3-T8: Include table number in QR payload only when QR_TableNumber_Enabled = ON
                string? effectiveTableNumber = null;
                if (!string.IsNullOrEmpty(tableNumber) && _shopFeatureSettingsService != null)
                {
                    try
                    {
                        bool qrTableEnabled = await _shopFeatureSettingsService.IsEnabledAsync(
                            shopId,
                            nameof(ShopFeatureSettingsDto.QR_TableNumber_Enabled));
                        effectiveTableNumber = qrTableEnabled ? tableNumber : null;
                    }
                    catch
                    {
                        // Default to OFF if toggle fetch fails (secure default)
                    }
                }

                byte[] png = _qrCodeService.GenerateProductQRCode(id, shopId, effectiveTableNumber);

                return File(png, "image/png", $"qr-{id}.png");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating QR code for product {ProductId}", id);
                return StatusCode(500, "Internal server error");
            }
        }
    }

    public class ProductCatalogItem
    {
        public Guid ProductId { get; set; }
        public Guid TenantId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public decimal Price { get; set; }
        public string? Category { get; set; }
        public string? ImageUrl { get; set; }
        public decimal VatRate { get; set; }
    }

    /// <summary>
    /// Product catalog item with recommendation metadata
    /// </summary>
    public class RecommendedProductItem : ProductCatalogItem
    {
        public int FrequencyScore { get; set; }
        public decimal TotalSpent { get; set; }
        public string RecommendationReason { get; set; } = string.Empty;
    }
}
