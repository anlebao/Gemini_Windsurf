using Microsoft.AspNetCore.Http;
using VanAn.Shared.DTOs;

namespace VanAn.CoreHub.Services
{

    /// <summary>
    /// Product management service — CRUD + activate/deactivate + image upload.
    /// G3: Clean Architecture service layer (controller does NOT write via IVanAnDbContext directly).
    /// Multi-tenancy: every method verifies product belongs to tenantId before mutating.
    /// </summary>
    public interface IProductService
    {
        /// <summary>Get a single product for management (by ProductId + tenant). Returns null if not found.</summary>
        Task<ProductDetailDto?> GetProductForManagementAsync(Guid productId, Guid tenantId, CancellationToken ct = default);

        /// <summary>Get all products for management (include inactive, exclude deleted).
        /// #114: includePosOnly=false (default) hides POS-only service products from non-POS views.</summary>
        Task<List<ProductDetailDto>> GetAllForManagementAsync(Guid tenantId, CancellationToken ct = default, bool includePosOnly = false);

        /// <summary>Create a new product. Returns the created ProductDetailDto.</summary>
        Task<ProductDetailDto> CreateProductAsync(CreateProductRequest request, Guid tenantId, CancellationToken ct = default);

        /// <summary>Update an existing product. Returns true on success, false if not found.</summary>
        Task<bool> UpdateProductAsync(Guid productId, UpdateProductRequest request, Guid tenantId, CancellationToken ct = default);

        /// <summary>Soft-delete a product (MarkAsDeleted). Returns true on success, false if not found.</summary>
        Task<bool> DeleteProductAsync(Guid productId, Guid tenantId, CancellationToken ct = default);

        /// <summary>Deactivate a product (IsActive = false). Returns true on success, false if not found.</summary>
        Task<bool> DeactivateProductAsync(Guid productId, Guid tenantId, CancellationToken ct = default);

        /// <summary>Activate a product (IsActive = true). Returns true on success, false if not found.</summary>
        Task<bool> ActivateProductAsync(Guid productId, Guid tenantId, CancellationToken ct = default);

        /// <summary>Upload an image for a product (IFormFile — from HTTP endpoint). Returns the image URL, or null on failure.</summary>
        Task<string?> UploadImageAsync(Guid productId, IFormFile file, Guid tenantId, CancellationToken ct = default);

        /// <summary>Upload an image for a product (Stream — from Blazor Server IBrowserFile). Returns the image URL, or null on failure.</summary>
        Task<string?> UploadImageAsync(Guid productId, Stream stream, string fileName, Guid tenantId, CancellationToken ct = default);
    }
}
