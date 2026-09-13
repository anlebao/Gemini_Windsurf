using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using VanAn.CoreHub.Infrastructure;
using VanAn.Shared.Domain;

namespace VanAn.CoreHub.Services;

/// <summary>
/// CC-S4 (Sprint 4): Product referral config service — admin CRUD.
/// Validation: CommissionRate 0.02-0.05, AppInstallBonus >= 0, ProductShortCode unique within tenant.
/// Tenant-aware: CreateAsync requires explicit tenantId (the tenant that owns the product).
/// ListAllAsync optionally filters by tenant. ShortCode uniqueness is per-tenant.
/// </summary>
public class ProductReferralConfigService(
    IVanAnDbContext dbContext,
    ILogger<ProductReferralConfigService> logger) : IProductReferralConfigService
{
    private readonly IVanAnDbContext _dbContext = dbContext;
    private readonly ILogger<ProductReferralConfigService> _logger = logger;

    public async Task<ProductReferralConfigDto?> GetByProductIdAsync(Guid productId)
    {
        var config = await _dbContext.ProductReferralConfigs
            .IgnoreQueryFilters()
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.ProductId == productId);

        return config == null ? null : MapToDto(config);
    }

    public async Task<ProductReferralConfigDto> CreateAsync(Guid productId, Guid tenantId, decimal commissionRate, decimal appInstallBonus, string? productShortCode)
    {
        // Check for existing config
        var existing = await _dbContext.ProductReferralConfigs
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(c => c.ProductId == productId);

        if (existing != null)
            throw new InvalidOperationException($"ProductReferralConfig already exists for product {productId}");

        // Validate short code uniqueness WITHIN the same tenant (per-tenant, not global)
        if (!string.IsNullOrEmpty(productShortCode))
        {
            var tid = new TenantId(tenantId);
            var duplicate = await _dbContext.ProductReferralConfigs
                .IgnoreQueryFilters()
                .AsNoTracking()
                .AnyAsync(c => c.ProductShortCode == productShortCode
                    && c.TenantId == tid
                    && c.IsActive);

            if (duplicate)
                throw new InvalidOperationException($"ProductShortCode '{productShortCode}' already in use within this tenant");
        }

        var config = new ProductReferralConfig(new TenantId(tenantId), productId, commissionRate, appInstallBonus, productShortCode);

        _dbContext.ProductReferralConfigs.Add(config);
        await _dbContext.SaveChangesAsync();

        _logger.LogInformation("CreateAsync: ProductReferralConfig {Id} created for product {ProductId} (tenant {TenantId})", config.Id, productId, tenantId);
        return MapToDto(config);
    }

    public async Task<ProductReferralConfigDto> UpdateAsync(Guid productId, decimal commissionRate, decimal appInstallBonus, string? productShortCode, bool isActive)
    {
        var config = await _dbContext.ProductReferralConfigs
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(c => c.ProductId == productId);

        if (config == null)
            throw new InvalidOperationException($"ProductReferralConfig not found for product {productId}");

        // Validate short code uniqueness within the same tenant (exclude self)
        if (!string.IsNullOrEmpty(productShortCode))
        {
            var tid = config.TenantId;
            var duplicate = await _dbContext.ProductReferralConfigs
                .IgnoreQueryFilters()
                .AsNoTracking()
                .AnyAsync(c => c.ProductShortCode == productShortCode
                    && c.TenantId == tid
                    && c.IsActive
                    && c.ProductId != productId);

            if (duplicate)
                throw new InvalidOperationException($"ProductShortCode '{productShortCode}' already in use within this tenant");
        }

        config.Update(commissionRate, appInstallBonus, productShortCode, isActive);
        await _dbContext.SaveChangesAsync();

        _logger.LogInformation("UpdateAsync: ProductReferralConfig {Id} updated for product {ProductId}", config.Id, productId);
        return MapToDto(config);
    }

    public async Task DeactivateAsync(Guid productId)
    {
        var config = await _dbContext.ProductReferralConfigs
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(c => c.ProductId == productId);

        if (config == null)
            throw new InvalidOperationException($"ProductReferralConfig not found for product {productId}");

        config.Deactivate();
        await _dbContext.SaveChangesAsync();

        _logger.LogInformation("DeactivateAsync: ProductReferralConfig {Id} deactivated for product {ProductId}", config.Id, productId);
    }

    public async Task<List<ProductReferralConfigDto>> ListAllAsync(Guid? tenantId = null)
    {
        var query = _dbContext.ProductReferralConfigs
            .IgnoreQueryFilters()
            .AsNoTracking();

        if (tenantId.HasValue && tenantId.Value != Guid.Empty)
        {
            var tid = new TenantId(tenantId.Value);
            query = query.Where(c => c.TenantId == tid);
        }

        var configs = await query.ToListAsync();
        return configs.Select(MapToDto).ToList();
    }

    private static ProductReferralConfigDto MapToDto(ProductReferralConfig config)
    {
        return new ProductReferralConfigDto
        {
            Id = config.Id,
            ProductId = config.ProductId,
            TenantId = config.TenantId.Value,
            ProductShortCode = config.ProductShortCode,
            CommissionRate = config.CommissionRate,
            AppInstallBonus = config.AppInstallBonus,
            IsActive = config.IsActive
        };
    }
}
