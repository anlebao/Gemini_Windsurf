using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using VanAn.CoreHub.Infrastructure;
using VanAn.Shared.Domain;

namespace VanAn.CoreHub.Services;

/// <summary>
/// CC-S4 (Sprint 4): Product referral config service — admin CRUD.
/// Validation: CommissionRate 0.02-0.05, AppInstallBonus >= 0, ProductShortCode unique within tenant.
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

    public async Task<ProductReferralConfigDto> CreateAsync(Guid productId, decimal commissionRate, decimal appInstallBonus, string? productShortCode)
    {
        // Check for existing config
        var existing = await _dbContext.ProductReferralConfigs
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(c => c.ProductId == productId);

        if (existing != null)
            throw new InvalidOperationException($"ProductReferralConfig already exists for product {productId}");

        // Validate short code uniqueness
        if (!string.IsNullOrEmpty(productShortCode))
        {
            var duplicate = await _dbContext.ProductReferralConfigs
                .IgnoreQueryFilters()
                .AsNoTracking()
                .AnyAsync(c => c.ProductShortCode == productShortCode && c.IsActive);

            if (duplicate)
                throw new InvalidOperationException($"ProductShortCode '{productShortCode}' already in use");
        }

        // Use a default tenant ID (community data is cross-tenant on Gateway PG)
        var tenantId = new TenantId(Guid.Parse("00000000-0000-0000-0000-000000000001"));
        var config = new ProductReferralConfig(tenantId, productId, commissionRate, appInstallBonus, productShortCode);

        _dbContext.ProductReferralConfigs.Add(config);
        await _dbContext.SaveChangesAsync();

        _logger.LogInformation("CreateAsync: ProductReferralConfig {Id} created for product {ProductId}", config.Id, productId);
        return MapToDto(config);
    }

    public async Task<ProductReferralConfigDto> UpdateAsync(Guid productId, decimal commissionRate, decimal appInstallBonus, string? productShortCode, bool isActive)
    {
        var config = await _dbContext.ProductReferralConfigs
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(c => c.ProductId == productId);

        if (config == null)
            throw new InvalidOperationException($"ProductReferralConfig not found for product {productId}");

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

    public async Task<List<ProductReferralConfigDto>> ListAllAsync()
    {
        var configs = await _dbContext.ProductReferralConfigs
            .IgnoreQueryFilters()
            .AsNoTracking()
            .ToListAsync();

        return configs.Select(MapToDto).ToList();
    }

    private static ProductReferralConfigDto MapToDto(ProductReferralConfig config)
    {
        return new ProductReferralConfigDto
        {
            Id = config.Id,
            ProductId = config.ProductId,
            ProductShortCode = config.ProductShortCode,
            CommissionRate = config.CommissionRate,
            AppInstallBonus = config.AppInstallBonus,
            IsActive = config.IsActive
        };
    }
}
