using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VanAn.CoreHub.Infrastructure;
using VanAn.Shared.Domain;

namespace VanAn.Gateway.Controllers
{
    /// <summary>
    /// Phase 6 (Admin UI): Public catalog endpoint for KhachLink Home.razor.
    /// Returns union of (a) active FeaturedProducts + (b) customer's previously purchased products.
    /// Queries PG directly — no ShopERP HTTP call. DisplayPrice is marketing price;
    /// actual price validated at checkout (Phase 5 price validation).
    /// Anonymous users (no customerId) → only FeaturedProducts.
    /// </summary>
    [ApiController]
    [Route("api/catalog")]
    public class CatalogController(
        IVanAnDbContext dbContext,
        ILogger<CatalogController> logger) : ControllerBase
    {
        private readonly IVanAnDbContext _dbContext = dbContext;
        private readonly ILogger<CatalogController> _logger = logger;

        /// <summary>
        /// Get recommended products: union of FeaturedProducts + customer purchase history.
        /// Public endpoint — KhachLink Home.razor calls this without auth.
        /// </summary>
        [HttpGet("recommended")]
        [AllowAnonymous]
        public async Task<ActionResult<RecommendedCatalogResponse>> Recommended(
            [FromQuery] Guid? customerId,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20,
            CancellationToken ct = default)
        {
            if (page < 1) page = 1;
            if (pageSize < 1 || pageSize > 100) pageSize = 20;

            // 1. Featured products (active, ordered by SortOrder)
            // IgnoreQueryFilters: public endpoint (anonymous) — no tenant context,
            // global filter would exclude all featured products.
            var featured = await _dbContext.FeaturedProducts
                .AsNoTracking()
                .IgnoreQueryFilters()
                .Where(f => f.IsActive)
                .OrderBy(f => f.SortOrder)
                .ThenBy(f => f.DisplayName)
                .Select(f => new RecommendedProductDto
                {
                    ProductId = f.ProductId,
                    TenantId = f.TenantId.Value,
                    DisplayName = f.DisplayName,
                    DisplayPrice = f.DisplayPrice,
                    ImageUrl = f.ImageUrl,
                    Description = f.DisplayDescription,
                    Source = "Featured",
                    LastOrderedAt = (DateTime?)null
                })
                .ToListAsync(ct);

            var results = new List<RecommendedProductDto>(featured);

            // 2. Customer history (if customerId provided + non-empty)
            if (customerId.HasValue && customerId.Value != Guid.Empty)
            {
                var cid = customerId.Value;
                var history = await _dbContext.OrderItems
                    .AsNoTracking()
                    .Include(oi => oi.Order)
                    .Where(oi => oi.Order.CustomerId == cid && !oi.IsDeleted)
                    .GroupBy(oi => new { oi.ProductId, oi.TenantId })
                    .Select(g => new RecommendedProductDto
                    {
                        ProductId = g.Key.ProductId,
                        TenantId = g.Key.TenantId.Value,
                        DisplayName = g.Max(oi => oi.ProductName) ?? "Sản phẩm đã mua",
                        DisplayPrice = 0m, // History items don't carry marketing price
                        ImageUrl = null,
                        Description = null,
                        Source = "History",
                        LastOrderedAt = g.Max(oi => oi.Order.CreatedAt)
                    })
                    .ToListAsync(ct);

                // Merge: skip history items already in Featured (by ProductId)
                var featuredProductIds = featured.Select(f => f.ProductId).ToHashSet();
                results.AddRange(history.Where(h => !featuredProductIds.Contains(h.ProductId)));
            }

            var totalCount = results.Count;
            var paged = results
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            _logger.LogDebug("Catalog recommended: {Total} total, {Paged} returned (customerId={CustomerId})",
                totalCount, paged.Count, customerId);

            return Ok(new RecommendedCatalogResponse
            {
                Products = paged,
                TotalCount = totalCount,
                Page = page,
                PageSize = pageSize
            });
        }
    }

    public record RecommendedProductDto
    {
        public Guid ProductId { get; set; }
        public Guid TenantId { get; set; }
        public string DisplayName { get; set; } = "";
        public decimal DisplayPrice { get; set; }
        public string? ImageUrl { get; set; }
        public string? Description { get; set; }
        public string Source { get; set; } = "Featured"; // "Featured" | "History"
        public DateTime? LastOrderedAt { get; set; }
    }

    public record RecommendedCatalogResponse
    {
        public List<RecommendedProductDto> Products { get; set; } = new();
        public int TotalCount { get; set; }
        public int Page { get; set; }
        public int PageSize { get; set; }
    }
}
