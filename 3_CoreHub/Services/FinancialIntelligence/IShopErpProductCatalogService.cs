using VanAn.Shared.Domain;

namespace VanAn.CoreHub.Services.FinancialIntelligence
{
    /// <summary>
    /// VA-FI-MVP2 Bug 3 fix (2026-08-22): Fetches product catalog from ShopERP SQLite
    /// via HTTP (routed by Tenant.ShopInstanceId → ShopInstance.BaseUrl).
    ///
    /// Rationale: Per Option C Phase 3, Products live in ShopERP SQLite (per-tenant,
    /// fresh + trusted). Gateway PG Products table is empty (sync disabled). Financial
    /// Intelligence services need Product.Price + CostPrice + Category for unit economics
    /// and multi-product break-even — fetch via HTTP is acceptable (1-2 uses/day, latency
    /// not critical).
    ///
    /// Precedent: ProductsController.ResolveShopErpClientAsync (same routing pattern).
    /// Graceful degradation: returns empty list on any failure (Financial Intelligence
    /// is non-critical — UI shows "Chưa có dữ liệu").
    /// </summary>
    public interface IShopErpProductCatalogService
    {
        /// <summary>
        /// Fetch all active products for a tenant from ShopERP SQLite.
        /// Returns empty list on any failure (HTTP error, timeout, ShopInstance not found).
        /// </summary>
        Task<List<ProductSnapshot>> GetProductsAsync(TenantId tenantId, CancellationToken ct = default);
    }

    /// <summary>Snapshot of product fields needed by Financial Intelligence calculations.</summary>
    public record ProductSnapshot(
        Guid ProductId,
        string Name,
        decimal Price,
        decimal CostPrice,
        string Category);
}
