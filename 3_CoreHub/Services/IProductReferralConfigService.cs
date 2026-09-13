using VanAn.Shared.Domain;

namespace VanAn.CoreHub.Services;

/// <summary>
/// CC-S4 (Sprint 4): Product referral config service — admin CRUD for per-product commission rate + app-install bonus.
/// Sysadmin only. Validation: CommissionRate 0.02-0.05, AppInstallBonus >= 0, ProductShortCode unique within tenant.
/// Tenant-aware: configs are scoped to the tenant that owns the product.
/// </summary>
public interface IProductReferralConfigService
{
    Task<ProductReferralConfigDto?> GetByProductIdAsync(Guid productId);
    Task<ProductReferralConfigDto> CreateAsync(Guid productId, Guid tenantId, decimal commissionRate, decimal appInstallBonus, string? productShortCode);
    Task<ProductReferralConfigDto> UpdateAsync(Guid productId, decimal commissionRate, decimal appInstallBonus, string? productShortCode, bool isActive);
    Task DeactivateAsync(Guid productId);
    Task<List<ProductReferralConfigDto>> ListAllAsync(Guid? tenantId = null);
}

public class ProductReferralConfigDto
{
    public Guid Id { get; set; }
    public Guid ProductId { get; set; }
    public Guid TenantId { get; set; }
    public string? ProductShortCode { get; set; }
    public decimal CommissionRate { get; set; }
    public decimal AppInstallBonus { get; set; }
    public bool IsActive { get; set; }
}
