using VanAn.Shared.Domain;

namespace VanAn.CoreHub.Services.InventoryIntelligence
{
    /// <summary>1 dòng định mức khi tạo/publish recipe (SRS §3.2).</summary>
    public sealed record RecipeLineInput(Guid IngredientId, decimal Quantity, string Unit);

    /// <summary>
    /// VA-IIE (Sprint B, P2.2): quản lý Recipe — CRUD + versioning + cache active theo ProductId (SRS §3.2, §7.3).
    /// </summary>
    public interface IRecipeService
    {
        /// <summary>Recipe active (IsActive) mới nhất cho product — có cache in-memory (invalidate khi ghi).</summary>
        Task<Recipe?> GetActiveByProductIdAsync(Guid productId, CancellationToken ct = default);

        /// <summary>Recipe theo Id (kèm lines).</summary>
        Task<Recipe?> GetByIdAsync(Guid recipeId, bool includeLines = true, CancellationToken ct = default);

        /// <summary>Tất cả version của product (mới nhất trước).</summary>
        Task<IReadOnlyList<Recipe>> GetVersionsAsync(Guid productId, CancellationToken ct = default);

        /// <summary>Tạo Recipe v1 mới — throw nếu product đã có recipe active (dùng PublishNewVersionAsync).</summary>
        Task<Recipe> CreateAsync(TenantId tenantId, Guid productId, IReadOnlyList<RecipeLineInput> lines, decimal yield = 1m, decimal wasteFactor = 0m, CancellationToken ct = default);

        /// <summary>Publish version mới (deactivate cũ + tạo v+1 với lines mới). Không có version cũ → tạo v1.</summary>
        Task<Recipe> PublishNewVersionAsync(TenantId tenantId, Guid productId, IReadOnlyList<RecipeLineInput> lines, decimal yield = 1m, decimal wasteFactor = 0m, CancellationToken ct = default);

        /// <summary>Bật/tắt active cho 1 version.</summary>
        Task<Recipe> SetActiveAsync(Guid recipeId, bool isActive, CancellationToken ct = default);

        /// <summary>Xoá cache active của product (gọi sau khi ghi).</summary>
        void InvalidateCache(Guid productId);

        /// <summary>Danh sách product của tenant cho RecipeManagement UI (P3) — multi-tenancy filter.</summary>
        Task<IReadOnlyList<Product>> GetProductsAsync(TenantId tenantId, CancellationToken ct = default);

        /// <summary>Danh sách ingredient của tenant (đơn vị cơ sở) cho RecipeManagement UI (P3) — multi-tenancy filter.</summary>
        Task<IReadOnlyList<Ingredient>> GetIngredientsAsync(TenantId tenantId, CancellationToken ct = default);
    }
}
