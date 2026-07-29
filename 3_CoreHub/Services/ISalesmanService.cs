using VanAn.Shared.Domain;

namespace VanAn.CoreHub.Services;

/// <summary>
/// CC-S4 (Sprint 4): Salesman service — composite QR referral + per-product commission + app-install bonus.
/// v1.1: composite referral code "{salesmanCode}|{productShortCode}".
/// v1.2: risk scoring integration (IRiskScoringService) on commission creation.
/// </summary>
public interface ISalesmanService
{
    /// <summary>
    /// Get nearby products for salesman to refer. Joins FeaturedProducts + TenantSettings (Haversine)
    /// + LEFT JOIN ProductReferralConfig (commission rate + app-install bonus + product short code).
    /// </summary>
    Task<List<NearbyProductDto>> GetNearbyProductsAsync(double lat, double lng, int radiusKm, Guid salesmanId);

    /// <summary>
    /// v1.1: Get composite salesman QR code for a specific product.
    /// Returns "{salesmanCode}|{productShortCode}" + QR URL.
    /// </summary>
    Task<CompositeSalesmanQrDto?> GetCompositeSalesmanQrAsync(Guid salesmanId, Guid productId);

    /// <summary>
    /// v1.1: Get commission summary for salesman (tách biệt commission + app-install bonus).
    /// </summary>
    Task<CommissionSummaryDto> GetCommissionsAsync(Guid salesmanId);

    /// <summary>
    /// v1.1: Resolve composite referral code "{salesmanCode}|{productShortCode}" → (salesmanId, productId).
    /// Returns null if code invalid or config not found.
    /// </summary>
    Task<(Guid salesmanId, Guid productId)?> ResolveCompositeReferralCodeAsync(string referralCode);

    /// <summary>
    /// v1.1: Create commission when Order completes. Per-product commission from ProductReferralConfig.
    /// v1.2: Computes RiskScore + sets CommissionStatus (Pending/Held/Rejected).
    /// </summary>
    Task<SalesReferral?> CreateCommissionAsync(Guid orderId);
}

// === DTOs ===

public class NearbyProductDto
{
    public Guid ProductId { get; set; }
    public Guid TenantId { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public string ShopName { get; set; } = string.Empty;
    public double DistanceKm { get; set; }
    public decimal? CommissionRate { get; set; }
    public decimal? AppInstallBonus { get; set; }
    public string? ProductShortCode { get; set; }
    public bool HasReferralConfig { get; set; }
}

public class CompositeSalesmanQrDto
{
    public string SalesmanCode { get; set; } = string.Empty;
    public string ProductShortCode { get; set; } = string.Empty;
    public string CompositeCode { get; set; } = string.Empty;
    public string QrUrl { get; set; } = string.Empty;
    public Guid ProductId { get; set; }
}

public class CommissionSummaryDto
{
    public decimal TotalSales { get; set; }
    public decimal TotalCommission { get; set; }
    public decimal PendingCommission { get; set; }
    public decimal PaidCommission { get; set; }
    public decimal HeldCommission { get; set; }
    public decimal RejectedCommission { get; set; }
    public decimal TotalAppInstallBonus { get; set; }
    public decimal PendingAppInstallBonus { get; set; }
    public decimal PaidAppInstallBonus { get; set; }
    public List<CommissionRecordDto> CommissionRecords { get; set; } = new();
    public List<AppInstallBonusRecordDto> AppInstallBonusRecords { get; set; } = new();
}

public class CommissionRecordDto
{
    public Guid Id { get; set; }
    public Guid? OrderId { get; set; }
    public Guid ProductId { get; set; }
    public decimal OrderTotal { get; set; }
    public decimal CommissionRate { get; set; }
    public decimal CommissionAmount { get; set; }
    public string Status { get; set; } = string.Empty;
    public int RiskScore { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class AppInstallBonusRecordDto
{
    public Guid Id { get; set; }
    public Guid CustomerId { get; set; }
    public Guid ProductId { get; set; }
    public decimal BonusAmount { get; set; }
    public string Status { get; set; } = string.Empty;
    public int RiskScore { get; set; }
    public DateTime InstalledAt { get; set; }
}
