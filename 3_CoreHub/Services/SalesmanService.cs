using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using VanAn.CoreHub.Infrastructure;
using VanAn.Shared.Domain;
using VanAn.Shared.Domain.Aggregates.TenantAggregate;
using Tenant = VanAn.Shared.Domain.Aggregates.TenantAggregate.Tenant;

namespace VanAn.CoreHub.Services;

/// <summary>
/// CC-S4 (Sprint 4): Salesman service implementation.
/// Composite QR referral + per-product commission + nearby products.
/// v1.2: risk scoring integration (IRiskScoringService) on commission creation.
/// Cross-tenant via IgnoreQueryFilters (community data is cross-tenant on Gateway PG).
/// </summary>
public class SalesmanService(
    IVanAnDbContext dbContext,
    IRiskScoringService riskScoringService,
    IFraudFlagService fraudFlagService,
    ILogger<SalesmanService> logger) : ISalesmanService
{
    private readonly IVanAnDbContext _dbContext = dbContext;
    private readonly IRiskScoringService _riskScoringService = riskScoringService;
    private readonly IFraudFlagService _fraudFlagService = fraudFlagService;
    private readonly ILogger<SalesmanService> _logger = logger;

    public async Task<List<NearbyProductDto>> GetNearbyProductsAsync(double lat, double lng, int radiusKm, Guid salesmanId)
    {
        // Load active featured products (cross-tenant)
        var products = await _dbContext.FeaturedProducts
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(fp => fp.IsActive)
            .ToListAsync();

        // Load tenants for shop info + coordinates
        var tenantIds = products.Select(p => p.TenantId).Distinct().ToList();
        var tenants = await _dbContext.Tenants
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(t => tenantIds.Contains(t.Id))
            .ToListAsync();
        var tenantMap = tenants.ToDictionary(t => t.Id, t => t);

        // Load ProductReferralConfigs for these products
        var productIds = products.Select(p => p.ProductId).Distinct().ToList();
        var configs = await _dbContext.ProductReferralConfigs
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(c => productIds.Contains(c.ProductId) && c.IsActive)
            .ToListAsync();
        var configMap = configs.ToDictionary(c => c.ProductId, c => c);

        // Calculate Haversine distance + filter by radius
        var result = new List<NearbyProductDto>();
        foreach (var product in products)
        {
            if (!tenantMap.TryGetValue(product.TenantId, out var tenant))
                continue;

            var shopLat = tenant.Settings?.Latitude ?? 0;
            var shopLng = tenant.Settings?.Longitude ?? 0;
            if (shopLat == 0 && shopLng == 0)
                continue;

            var distance = CalculateHaversineKm(lat, lng, shopLat, shopLng);
            if (distance > radiusKm)
                continue;

            configMap.TryGetValue(product.ProductId, out var config);

            result.Add(new NearbyProductDto
            {
                ProductId = product.ProductId,
                TenantId = product.TenantId.Value,
                Name = product.DisplayName,
                Price = product.DisplayPrice,
                ShopName = tenant.Name ?? "Unknown Shop",
                DistanceKm = Math.Round(distance, 2),
                CommissionRate = config?.CommissionRate,
                AppInstallBonus = config?.AppInstallBonus,
                ProductShortCode = config?.ProductShortCode,
                HasReferralConfig = config != null
            });
        }

        return result.OrderBy(r => r.DistanceKm).ToList();
    }

    public async Task<CompositeSalesmanQrDto?> GetCompositeSalesmanQrAsync(Guid salesmanId, Guid productId)
    {
        // Get salesman role
        var role = await _dbContext.CommunityRoles
            .IgnoreQueryFilters()
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.CustomerId == salesmanId
                && r.RoleType == CommunityRoleType.Salesman
                && r.IsActive);

        if (role == null || string.IsNullOrEmpty(role.SalesmanCode))
        {
            _logger.LogWarning("GetCompositeSalesmanQr: No active Salesman role for {SalesmanId}", salesmanId);
            return null;
        }

        // Get product referral config
        var config = await _dbContext.ProductReferralConfigs
            .IgnoreQueryFilters()
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.ProductId == productId && c.IsActive);

        if (config == null)
        {
            _logger.LogWarning("GetCompositeSalesmanQr: No ProductReferralConfig for product {ProductId}", productId);
            return null;
        }

        var productShortCode = config.ProductShortCode ?? productId.ToString()[..8].ToUpper();
        var compositeCode = $"{role.SalesmanCode}|{productShortCode}";

        return new CompositeSalesmanQrDto
        {
            SalesmanCode = role.SalesmanCode,
            ProductShortCode = productShortCode,
            CompositeCode = compositeCode,
            QrUrl = $"https://diemthuong.khachvip.online/r/{compositeCode}",
            ProductId = productId
        };
    }

    public async Task<CommissionSummaryDto> GetCommissionsAsync(Guid salesmanId)
    {
        var referrals = await _dbContext.SalesReferrals
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(r => r.SalesmanId == salesmanId)
            .ToListAsync();

        var attributions = await _dbContext.AppInstallAttributions
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(a => a.SalesmanId == salesmanId)
            .ToListAsync();

        return new CommissionSummaryDto
        {
            TotalSales = referrals.Where(r => r.OrderId.HasValue).Sum(r => r.CommissionAmount / (r.CommissionRate > 0 ? r.CommissionRate : 1)),
            TotalCommission = referrals.Sum(r => r.CommissionAmount),
            PendingCommission = referrals.Where(r => r.CommissionStatus == CommissionStatus.Pending).Sum(r => r.CommissionAmount),
            PaidCommission = referrals.Where(r => r.CommissionStatus == CommissionStatus.Paid).Sum(r => r.CommissionAmount),
            HeldCommission = referrals.Where(r => r.CommissionStatus == CommissionStatus.Held).Sum(r => r.CommissionAmount),
            RejectedCommission = referrals.Where(r => r.CommissionStatus == CommissionStatus.Rejected).Sum(r => r.CommissionAmount),
            TotalAppInstallBonus = attributions.Sum(a => a.BonusAmount),
            PendingAppInstallBonus = attributions.Where(a => a.AttributionStatus == AttributionStatus.Pending).Sum(a => a.BonusAmount),
            PaidAppInstallBonus = attributions.Where(a => a.AttributionStatus == AttributionStatus.Paid).Sum(a => a.BonusAmount),
            CommissionRecords = referrals.Select(r => new CommissionRecordDto
            {
                Id = r.Id,
                OrderId = r.OrderId,
                ProductId = r.ProductId,
                OrderTotal = r.CommissionRate > 0 ? r.CommissionAmount / r.CommissionRate : 0,
                CommissionRate = r.CommissionRate,
                CommissionAmount = r.CommissionAmount,
                Status = r.CommissionStatus.ToString(),
                RiskScore = r.RiskScore,
                CreatedAt = r.CreatedAt
            }).ToList(),
            AppInstallBonusRecords = attributions.Select(a => new AppInstallBonusRecordDto
            {
                Id = a.Id,
                CustomerId = a.CustomerId,
                ProductId = a.ProductId,
                BonusAmount = a.BonusAmount,
                Status = a.AttributionStatus.ToString(),
                RiskScore = a.RiskScore,
                InstalledAt = a.InstalledAt
            }).ToList()
        };
    }

    public async Task<(Guid salesmanId, Guid productId)?> ResolveCompositeReferralCodeAsync(string referralCode)
    {
        if (string.IsNullOrWhiteSpace(referralCode) || !referralCode.Contains('|'))
            return null;

        var parts = referralCode.Split('|', 2);
        if (parts.Length != 2)
            return null;

        var salesmanCode = parts[0].Trim();
        var productShortCode = parts[1].Trim();

        if (string.IsNullOrEmpty(salesmanCode) || string.IsNullOrEmpty(productShortCode))
            return null;

        // Find salesman by code
        var role = await _dbContext.CommunityRoles
            .IgnoreQueryFilters()
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.SalesmanCode == salesmanCode
                && r.RoleType == CommunityRoleType.Salesman
                && r.IsActive);

        if (role == null)
            return null;

        // Find product by short code
        var config = await _dbContext.ProductReferralConfigs
            .IgnoreQueryFilters()
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.ProductShortCode == productShortCode && c.IsActive);

        if (config == null)
            return null;

        return (role.CustomerId, config.ProductId);
    }

    public async Task<SalesReferral?> CreateCommissionAsync(Guid orderId)
    {
        // Load order (cross-tenant)
        var order = await _dbContext.Orders
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(o => o.Id == orderId);

        if (order == null || order.SalesmanId == null || order.ReferralProductId == null)
        {
            _logger.LogWarning("CreateCommission: Order {OrderId} not found or no SalesmanId/ReferralProductId", orderId);
            return null;
        }

        // Get ProductReferralConfig for commission rate
        var config = await _dbContext.ProductReferralConfigs
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(c => c.ProductId == order.ReferralProductId.Value && c.IsActive);

        if (config == null)
        {
            _logger.LogWarning("CreateCommission: No ProductReferralConfig for product {ProductId}", order.ReferralProductId);
            return null;
        }

        // Get salesman code
        var role = await _dbContext.CommunityRoles
            .IgnoreQueryFilters()
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.CustomerId == order.SalesmanId.Value
                && r.RoleType == CommunityRoleType.Salesman
                && r.IsActive);

        var salesmanCode = role?.SalesmanCode ?? "UNKNOWN";

        // Create SalesReferral
        var referral = new SalesReferral(order.TenantId, order.SalesmanId.Value, salesmanCode, order.ReferralProductId.Value, config.ProductShortCode);
        referral.AttachToOrder(orderId, order.CustomerId ?? Guid.Empty, order.TotalAmount, config.CommissionRate);

        // v1.2: Compute risk score (basic — no fingerprint data in commission flow)
        var riskResult = _riskScoringService.CalculateScore(new RiskScoreInput(
            SameFingerprint: false,
            SameIp24h: false,
            CustomerAgeDaysLessThan7: false,
            DeviceFirstSeenLessThan24h: false,
            OrdersFromDeviceTodayGreaterThan3: false,
            ReferralBonusAmountGreaterThan50K: config.AppInstallBonus > 50000,
            AppInstallTimeLessThan30s: false,
            BlacklistedFingerprint: false
        ));

        referral.SetRiskScore(riskResult.Score, riskResult.RiskFactors);

        // Create FraudFlag if high risk
        if (riskResult.Score >= 60)
        {
            await _fraudFlagService.CreateFlagAsync(
                order.TenantId.Value,
                FraudEntityType.SalesReferral,
                referral.Id,
                order.CustomerId,
                FraudFlagType.HighRiskScore,
                riskResult.Score,
                riskResult.RiskFactors,
                $"Commission auto-flagged: RiskScore={riskResult.Score}");
        }

        _dbContext.SalesReferrals.Add(referral);
        await _dbContext.SaveChangesAsync();

        _logger.LogInformation("CreateCommission: SalesReferral {ReferralId} created for order {OrderId}, commission={Amount}, riskScore={Score}",
            referral.Id, orderId, referral.CommissionAmount, referral.RiskScore);

        return referral;
    }

    /// <summary>
    /// Haversine formula — calculate distance between two lat/lng points in km.
    /// </summary>
    private static double CalculateHaversineKm(double lat1, double lng1, double lat2, double lng2)
    {
        const double EarthRadiusKm = 6371.0;
        var dLat = (lat2 - lat1) * Math.PI / 180;
        var dLng = (lng2 - lng1) * Math.PI / 180;
        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                Math.Cos(lat1 * Math.PI / 180) * Math.Cos(lat2 * Math.PI / 180) *
                Math.Sin(dLng / 2) * Math.Sin(dLng / 2);
        var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
        return EarthRadiusKm * c;
    }
}
