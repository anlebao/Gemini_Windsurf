using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using VanAn.CoreHub.Repositories;
using VanAn.Shared.Domain;
using VanAn.Shared.DTOs;

namespace VanAn.CoreHub.Services
{
    /// <summary>
    /// Product management service implementation — G3 Clean Architecture.
    /// Injects IProductRepository + IImageStorageService. Verifies tenant ownership before every mutation.
    /// </summary>
    public class ProductService(
        IProductRepository productRepository,
        IImageStorageService imageStorageService,
        ILogger<ProductService> logger) : IProductService
    {
        private readonly IProductRepository _productRepository = productRepository;
        private readonly IImageStorageService _imageStorageService = imageStorageService;
        private readonly ILogger<ProductService> _logger = logger;

        public async Task<ProductDetailDto?> GetProductForManagementAsync(Guid productId, Guid tenantId, CancellationToken ct = default)
        {
            Product? product = await _productRepository.GetByIdAsync(new ProductId(productId), new TenantId(tenantId), ct);
            return product == null ? null : MapToDto(product);
        }

        public async Task<List<ProductDetailDto>> GetAllForManagementAsync(Guid tenantId, CancellationToken ct = default)
        {
            List<Product> products = await _productRepository.GetAllForManagementAsync(new TenantId(tenantId), ct);
            return products.Select(MapToDto).ToList();
        }

        public async Task<ProductDetailDto> CreateProductAsync(CreateProductRequest request, Guid tenantId, CancellationToken ct = default)
        {
            var product = new Product(
                new TenantId(tenantId),
                request.Name,
                request.Description ?? string.Empty,
                request.Price,
                request.Category,
                isActive: true,
                imageUrl: request.ImageUrl,
                vatRate: request.VatRate,
                costPrice: request.CostPrice);

            _ = await _productRepository.AddAsync(product, ct);
            await _productRepository.SaveChangesAsync(ct);

            _logger.LogInformation("Created product {ProductId} for tenant {TenantId}", product.ProductId.Value, tenantId);
            return MapToDto(product);
        }

        public async Task<bool> UpdateProductAsync(Guid productId, UpdateProductRequest request, Guid tenantId, CancellationToken ct = default)
        {
            Product? product = await _productRepository.GetByIdAsync(new ProductId(productId), new TenantId(tenantId), ct);
            if (product == null)
            {
                return false;
            }

            product.Update(
                request.Name,
                request.Description ?? string.Empty,
                request.Price,
                request.Category,
                request.IsActive,
                request.ImageUrl,
                request.VatRate);

            _ = await _productRepository.UpdateAsync(product, ct);
            await _productRepository.SaveChangesAsync(ct);

            _logger.LogInformation("Updated product {ProductId} for tenant {TenantId}", productId, tenantId);
            return true;
        }

        public async Task<bool> DeleteProductAsync(Guid productId, Guid tenantId, CancellationToken ct = default)
        {
            Product? product = await _productRepository.GetByIdAsync(new ProductId(productId), new TenantId(tenantId), ct);
            if (product == null)
            {
                return false;
            }

            product.MarkAsDeleted();
            _ = await _productRepository.UpdateAsync(product, ct);
            await _productRepository.SaveChangesAsync(ct);

            _logger.LogInformation("Soft-deleted product {ProductId} for tenant {TenantId}", productId, tenantId);
            return true;
        }

        public async Task<bool> DeactivateProductAsync(Guid productId, Guid tenantId, CancellationToken ct = default)
        {
            Product? product = await _productRepository.GetByIdAsync(new ProductId(productId), new TenantId(tenantId), ct);
            if (product == null)
            {
                return false;
            }

            product.Deactivate();
            _ = await _productRepository.UpdateAsync(product, ct);
            await _productRepository.SaveChangesAsync(ct);

            return true;
        }

        public async Task<bool> ActivateProductAsync(Guid productId, Guid tenantId, CancellationToken ct = default)
        {
            Product? product = await _productRepository.GetByIdAsync(new ProductId(productId), new TenantId(tenantId), ct);
            if (product == null)
            {
                return false;
            }

            product.Activate();
            _ = await _productRepository.UpdateAsync(product, ct);
            await _productRepository.SaveChangesAsync(ct);

            return true;
        }

        public async Task<string?> UploadImageAsync(Guid productId, IFormFile file, Guid tenantId, CancellationToken ct = default)
        {
            Product? product = await _productRepository.GetByIdAsync(new ProductId(productId), new TenantId(tenantId), ct);
            if (product == null)
            {
                return null;
            }

            string folder = $"products/{tenantId}/{productId}";
            string? url = await _imageStorageService.UploadAsync(file, folder, ct);
            if (url == null)
            {
                return null;
            }

            // Update the product's ImageUrl via domain Update() method (preserves audit trail).
            product.Update(product.Name, product.Description, product.Price, product.Category, product.IsActive, url, product.VatRate);
            _ = await _productRepository.UpdateAsync(product, ct);
            await _productRepository.SaveChangesAsync(ct);

            return url;
        }

        public async Task<string?> UploadImageAsync(Guid productId, Stream stream, string fileName, Guid tenantId, CancellationToken ct = default)
        {
            Product? product = await _productRepository.GetByIdAsync(new ProductId(productId), new TenantId(tenantId), ct);
            if (product == null)
            {
                return null;
            }

            string folder = $"products/{tenantId}/{productId}";
            string? url = await _imageStorageService.UploadAsync(stream, fileName, folder, ct);
            if (url == null)
            {
                return null;
            }

            product.Update(product.Name, product.Description, product.Price, product.Category, product.IsActive, url, product.VatRate);
            _ = await _productRepository.UpdateAsync(product, ct);
            await _productRepository.SaveChangesAsync(ct);

            return url;
        }

        private static ProductDetailDto MapToDto(Product p) => new()
        {
            ProductId = p.ProductId.Value,
            TenantId = p.TenantId.Value,
            Name = p.Name,
            Description = p.Description,
            Price = p.Price,
            CostPrice = p.CostPrice,
            Category = p.Category,
            IsActive = p.IsActive,
            ImageUrl = p.ImageUrl,
            VatRate = p.VatRate,
            CreatedAt = p.CreatedAt,
            UpdatedAt = p.UpdatedAt
        };
    }
}
