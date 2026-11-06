using VanAn.CoreHub.Services;
using VanAn.Shared.DTOs;
using VanAn.Shared.Services;

namespace VanAn.ShopERP.Services;

/// <summary>Push ShopERP product knowledge → vanan-ai curated mirror (SRS [G9]).</summary>
public interface ITenantAiKnowledgeSync
{
    /// <summary>Push one product after CUD — fire-safe: flag OFF or client down = no-op.</summary>
    Task NotifyProductChangedAsync(Guid tenantId, Guid productId, CancellationToken ct = default);
    Task NotifyProductDeletedAsync(Guid tenantId, Guid productId, CancellationToken ct = default);
    /// <summary>Full replace-by-source sync (manual "Đồng bộ AI" + nightly reconcile).</summary>
    Task<int> SyncNowAsync(Guid tenantId, CancellationToken ct = default);
}

/// <summary>
/// task_ai_p2 — product knowledge push. Flag `TenantAiEnabled` (ops toggle) OFF =
/// zero HTTP calls. All errors swallowed — sync never blocks bán hàng.
/// </summary>
public sealed class TenantAiKnowledgeSyncService(
    ITenantAiClient client,
    IProductService products,
    IShopFeatureSettingsService flags,
    ILogger<TenantAiKnowledgeSyncService> logger) : ITenantAiKnowledgeSync
{
    public const string FlagName = "TenantAiEnabled";

    private async Task<bool> EnabledAsync(Guid tenantId, CancellationToken ct)
    {
        try { return await flags.IsEnabledAsync(tenantId, FlagName, ct); }
        catch { return false; }
    }

    public static TenantAiProductItem Map(ProductDetailDto p)
        => new(p.ProductId, p.Name, p.Description, p.Price,
            Calories: null, Ingredients: null, Allergens: null,
            Available: p.IsActive, CategoryName: p.Category);

    public async Task NotifyProductChangedAsync(Guid tenantId, Guid productId, CancellationToken ct = default)
    {
        try
        {
            if (!await EnabledAsync(tenantId, ct)) return;
            var product = await products.GetProductForManagementAsync(productId, tenantId, ct);
            if (product == null)
            {
                await client.DeleteProductAsync(tenantId, productId, ct);
                return;
            }
            await client.PushProductsBatchAsync(tenantId, [Map(product)], fullSync: false,
                sourceVersion: $"product:{productId}:{DateTime.UtcNow:yyyyMMddHHmmss}",
                idempotencyKey: $"product:{tenantId}:{productId}:{Guid.NewGuid():N}", ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "TenantAi product sync failed (non-blocking) {Product}", productId);
        }
    }

    public async Task NotifyProductDeletedAsync(Guid tenantId, Guid productId, CancellationToken ct = default)
    {
        try
        {
            if (!await EnabledAsync(tenantId, ct)) return;
            await client.DeleteProductAsync(tenantId, productId, ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "TenantAi product delete sync failed (non-blocking) {Product}", productId);
        }
    }

    public async Task<int> SyncNowAsync(Guid tenantId, CancellationToken ct = default)
    {
        try
        {
            if (!await EnabledAsync(tenantId, ct)) return 0;
            var list = await products.GetAllForManagementAsync(tenantId, ct);
            var items = list.Select(Map).ToList();
            var result = await client.PushProductsBatchAsync(tenantId, items, fullSync: true,
                sourceVersion: $"fullsync:{DateTime.UtcNow:yyyyMMddHHmm}",
                idempotencyKey: $"fullsync:{tenantId}:{DateTime.UtcNow:yyyyMMdd}", ct);
            return result?.Upserted ?? 0;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "TenantAi full sync failed (non-blocking) {Tenant}", tenantId);
            return 0;
        }
    }
}
