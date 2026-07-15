using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VanAn.CoreHub.Infrastructure;
using VanAn.CoreHub.Services;
using VanAn.ShopERP.Services;
using VanAn.Shared.Domain;
using VanAn.Shared.Domain.Common;
using VanAn.Shared.DTOs;
using Tenant = VanAn.Shared.Domain.Aggregates.TenantAggregate.Tenant;

namespace VanAn.ShopERP.Controllers
{
    /// <summary>
    /// Public product catalog API for KhachLink customer-facing display.
    /// Read-only endpoints are anonymous; management endpoints require authorization.
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class ProductsController(
        IVanAnDbContext dbContext,
        ILogger<ProductsController> logger,
        CustomerRecommendationService recommendationService,
        IShopQrCodeService qrCodeService,
        IProductService productService,
        ITenantProvider tenantProvider,
        IShopFeatureSettingsService? shopFeatureSettingsService = null) : ControllerBase
    {
        private readonly IVanAnDbContext _dbContext = dbContext;
        private readonly ILogger<ProductsController> _logger = logger;
        private readonly CustomerRecommendationService _recommendationService = recommendationService;
        private readonly IShopQrCodeService _qrCodeService = qrCodeService;
        private readonly IProductService _productService = productService;
        private readonly ITenantProvider _tenantProvider = tenantProvider;
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
        /// Bug 3: Get products grouped by tenant — top 5 tenants, 1-2 products each.
        /// Returns flat list with TenantName for client-side grouping.
        /// </summary>
        [HttpGet("grouped-by-tenant")]
        [AllowAnonymous]
        public async Task<ActionResult<List<ProductCatalogItem>>> GetProductsGroupedByTenant(
            [FromQuery] int tenantsCount = 5,
            [FromQuery] int productsPerTenant = 2)
        {
            try
            {
                List<Product> allProducts = await _dbContext.Products
                    .Where(p => p.IsActive && !p.IsDeleted)
                    .OrderBy(p => p.Category)
                    .ThenBy(p => p.Name)
                    .ToListAsync();

                if (allProducts.Count == 0)
                    return Ok(new List<ProductCatalogItem>());

                var grouped = allProducts
                    .GroupBy(p => p.TenantId.Value)
                    .Take(tenantsCount)
                    .ToList();

                // Load tenant names in-memory from Tenants table
                // (Cannot use tenantIds.Contains(t.Id.Value) — EF Core cannot translate ValueObject .Value in Contains)
                List<Tenant> allTenants = await _dbContext.Tenants.ToListAsync();
                var tenantNames = allTenants.ToDictionary(t => t.Id.Value, t => t.Name);

                var result = new List<ProductCatalogItem>();
                foreach (var group in grouped)
                {
                    var tid = group.Key;
                    foreach (var p in group.Take(productsPerTenant))
                    {
                        result.Add(new ProductCatalogItem
                        {
                            ProductId = p.ProductId.Value,
                            TenantId = p.TenantId.Value,
                            TenantName = tenantNames.GetValueOrDefault(tid, $"Cửa hàng {tid.ToString()[..8]}"),
                            Name = p.Name,
                            Description = p.Description,
                            Price = p.Price,
                            Category = p.Category,
                            ImageUrl = p.ImageUrl,
                            VatRate = p.VatRate
                        });
                    }
                }

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting grouped products");
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

        // ── Product Management endpoints (Phase 3 — G3 Clean Architecture) ──

        /// <summary>
        /// Get all products for management (include inactive, exclude soft-deleted).
        /// </summary>
        [HttpGet("manage")]
        [Authorize(Policy = "OwnerOnly")]
        public async Task<ActionResult<List<ProductDetailDto>>> GetProductsForManagement(CancellationToken ct)
        {
            if (!_tenantProvider.HasTenant)
            {
                return Unauthorized("No tenant context");
            }

            List<ProductDetailDto> products = await _productService.GetAllForManagementAsync(_tenantProvider.TenantId, ct);
            return Ok(products);
        }

        /// <summary>
        /// Create a new product.
        /// </summary>
        [HttpPost]
        [Authorize(Policy = "OwnerOnly")]
        public async Task<ActionResult<ProductDetailDto>> CreateProduct([FromBody] CreateProductRequest request, CancellationToken ct)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            if (!_tenantProvider.HasTenant)
            {
                return Unauthorized("No tenant context");
            }

            try
            {
                ProductDetailDto created = await _productService.CreateProductAsync(request, _tenantProvider.TenantId, ct);
                return CreatedAtAction(nameof(GetProduct), new { id = created.ProductId }, created);
            }
            catch (ArgumentException ex)
            {
                return BadRequest(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating product for tenant {TenantId}", _tenantProvider.TenantId);
                return StatusCode(500, "Internal server error");
            }
        }

        /// <summary>
        /// Update an existing product.
        /// </summary>
        [HttpPut("{id:guid}")]
        [Authorize(Policy = "OwnerOnly")]
        public async Task<ActionResult> UpdateProduct(Guid id, [FromBody] UpdateProductRequest request, CancellationToken ct)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            if (!_tenantProvider.HasTenant)
            {
                return Unauthorized("No tenant context");
            }

            try
            {
                bool ok = await _productService.UpdateProductAsync(id, request, _tenantProvider.TenantId, ct);
                return ok ? Ok() : NotFound();
            }
            catch (ArgumentException ex)
            {
                return BadRequest(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating product {ProductId}", id);
                return StatusCode(500, "Internal server error");
            }
        }

        /// <summary>
        /// Soft-delete a product (MarkAsDeleted — IsDeleted = true).
        /// </summary>
        [HttpDelete("{id:guid}")]
        [Authorize(Policy = "OwnerOnly")]
        public async Task<ActionResult> DeleteProduct(Guid id, CancellationToken ct)
        {
            if (!_tenantProvider.HasTenant)
            {
                return Unauthorized("No tenant context");
            }

            bool ok = await _productService.DeleteProductAsync(id, _tenantProvider.TenantId, ct);
            return ok ? NoContent() : NotFound();
        }

        /// <summary>
        /// Activate a product (IsActive = true).
        /// </summary>
        [HttpPut("{id:guid}/activate")]
        [Authorize(Policy = "OwnerOnly")]
        public async Task<ActionResult> ActivateProduct(Guid id, CancellationToken ct)
        {
            if (!_tenantProvider.HasTenant)
            {
                return Unauthorized("No tenant context");
            }

            bool ok = await _productService.ActivateProductAsync(id, _tenantProvider.TenantId, ct);
            return ok ? Ok() : NotFound();
        }

        /// <summary>
        /// Deactivate a product (IsActive = false — hide from catalog, still visible in management).
        /// </summary>
        [HttpPut("{id:guid}/deactivate")]
        [Authorize(Policy = "OwnerOnly")]
        public async Task<ActionResult> DeactivateProduct(Guid id, CancellationToken ct)
        {
            if (!_tenantProvider.HasTenant)
            {
                return Unauthorized("No tenant context");
            }

            bool ok = await _productService.DeactivateProductAsync(id, _tenantProvider.TenantId, ct);
            return ok ? Ok() : NotFound();
        }

        /// <summary>
        /// Upload an image for a product (multipart/form-data). Returns the image URL.
        /// G8: Cloudinary — separate endpoint, no binary in JSON DTO.
        /// </summary>
        [HttpPost("{id:guid}/image")]
        [Authorize(Policy = "OwnerOnly")]
        [RequestSizeLimit(10 * 1024 * 1024)] // 10MB max request (file max 5MB enforced in service)
        public async Task<ActionResult> UploadProductImage(Guid id, IFormFile file, CancellationToken ct)
        {
            if (!_tenantProvider.HasTenant)
            {
                return Unauthorized("No tenant context");
            }

            if (file == null || file.Length == 0)
            {
                return BadRequest("No file uploaded");
            }

            string? url = await _productService.UploadImageAsync(id, file, _tenantProvider.TenantId, ct);
            return url == null ? NotFound() : Ok(new { imageUrl = url });
        }
    }

    public class ProductCatalogItem
    {
        public Guid ProductId { get; set; }
        public Guid TenantId { get; set; }
        public string TenantName { get; set; } = string.Empty;
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
