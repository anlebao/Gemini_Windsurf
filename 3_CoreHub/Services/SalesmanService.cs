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
    ILogger<SalesmanService> logger,
    Microsoft.Extensions.Configuration.IConfiguration? configuration = null) : ISalesmanService
{
    private readonly IVanAnDbContext _dbContext = dbContext;
    private readonly IRiskScoringService _riskScoringService = riskScoringService;
    private readonly IFraudFlagService _fraudFlagService = fraudFlagService;
    private readonly ILogger<SalesmanService> _logger = logger;
    private readonly Microsoft.Extensions.Configuration.IConfiguration? _configuration = configuration;

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

    public async Task<CompositeSalesmanQrDto?> GetCompositeSalesmanQrAsync(Guid salesmanId, Guid productId, string? khachLinkBaseUrl = null)
    {
        // Get salesman role — load TRACKED so we can backfill a missing SalesmanCode (Issue #175).
        // Legacy roles (created before the constructor assigned a code, or inserted via raw SQL)
        // have a NULL SalesmanCode and would otherwise return null → "Không thể tạo mã QR".
        var role = await _dbContext.CommunityRoles
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(r => r.CustomerId == salesmanId
                && r.RoleType == CommunityRoleType.Salesman
                && r.IsActive);

        if (role == null)
        {
            _logger.LogWarning("GetCompositeSalesmanQr: No active Salesman role for {SalesmanId}", salesmanId);
            return null;
        }

        if (string.IsNullOrEmpty(role.SalesmanCode))
        {
            // Backfill with retry on unique-index collision (6-char random, collision risk is tiny).
            for (int attempt = 0; attempt < 3; attempt++)
            {
                if (attempt == 0)
                    role.EnsureSalesmanCode();
                else
                    role.RegenerateSalesmanCode();
                try
                {
                    await _dbContext.SaveChangesAsync();
                    _logger.LogInformation("GetCompositeSalesmanQr: Backfilled SalesmanCode for role {RoleId} (customer {SalesmanId})", role.Id, salesmanId);
                    break;
                }
                catch (DbUpdateException ex) when (attempt < 2)
                {
                    _logger.LogWarning(ex, "GetCompositeSalesmanQr: SalesmanCode collision on attempt {Attempt}, regenerating", attempt);
                }
            }
            if (string.IsNullOrEmpty(role.SalesmanCode))
            {
                _logger.LogError("GetCompositeSalesmanQr: Failed to persist SalesmanCode for role {RoleId} after 3 attempts", role.Id);
                return null;
            }
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

        // The QR must open the KhachLink instance the customer will actually use. It used to be
        // hardcoded to https://diemthuong.khachvip.online (Oracle VPS), so QR codes scanned from any
        // other instance (e.g. diemthuong2.khachvip.online) landed on the wrong site.
        // The composite code contains '|', which is not safe in a URL path — use the /scan page with
        // an escaped query param (Scan.razor resolves it and adds the product to the cart).
        var baseUrl = ResolveKhachLinkBaseUrl(khachLinkBaseUrl);
        var qrUrl = $"{baseUrl}/scan?ref={Uri.EscapeDataString(compositeCode)}";

        return new CompositeSalesmanQrDto
        {
            SalesmanCode = role.SalesmanCode!,
            ProductShortCode = productShortCode,
            CompositeCode = compositeCode,
            QrUrl = qrUrl,
            ProductId = productId
        };
    }

    /// <summary>
    /// Resolve the KhachLink origin for the referral QR URL.
    /// Priority: explicit caller-supplied origin → config "ExternalUrls:KhachLink" → last-resort default.
    /// </summary>
    private string ResolveKhachLinkBaseUrl(string? khachLinkBaseUrl)
    {
        if (!string.IsNullOrWhiteSpace(khachLinkBaseUrl))
        {
            var trimmed = khachLinkBaseUrl.Trim().TrimEnd('/');
            // Accept a bare host ("diemthuong2.khachvip.online") or a full origin.
            if (!trimmed.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
                && !trimmed.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                trimmed = "https://" + trimmed;
            return trimmed;
        }

        var configured = _configuration?["ExternalUrls:KhachLink"];
        if (!string.IsNullOrWhiteSpace(configured))
            return configured.Trim().TrimEnd('/');

        _logger.LogWarning("GetCompositeSalesmanQr: no KhachLink origin supplied or configured — falling back to default host");
        return "https://diemthuong.khachvip.online";
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
        // Load order + line items (cross-tenant). Items are required for a PER-PRODUCT commission base.
        var order = await _dbContext.Orders
            .IgnoreQueryFilters()
            .Include(o => o.Items)
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

        // CC-S4 fix: commission is per referred product, not per order. If the customer scanned a
        // referral QR but did not actually buy the referred product, there is nothing to commission.
        var commissionBase = ReferralCommissionCalculator.ComputeBase(order, config);
        if (commissionBase <= 0m)
        {
            _logger.LogWarning(
                "CreateCommission: referred product {ProductId} not present in order {OrderId} — no commission created",
                order.ReferralProductId, orderId);
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

        // Create SalesReferral. Base = referred line SubTotal (pre-VAT, shipping excluded), or the
        // pro-rated margin for Reseller OnMargin — see ReferralCommissionCalculator.
        var referral = new SalesReferral(order.TenantId, order.SalesmanId.Value, salesmanCode, order.ReferralProductId.Value, config.ProductShortCode);
        referral.AttachToOrder(orderId, order.CustomerId ?? Guid.Empty, commissionBase, config.CommissionRate, config.CommissionBase);

        // CC-S4 fix: self-referral detection. Previously SameFingerprint was hardcoded false and there
        // was no salesman==customer check, so a salesman buying through their own referral code was
        // paid a Pending commission with RiskScore 0.
        var (sameFingerprint, sameDevice) = await DetectSelfReferralAsync(order);
        bool selfReferral = sameFingerprint
            || sameDevice
            || (order.CustomerId.HasValue && order.CustomerId.Value == order.SalesmanId.Value);

        var riskResult = _riskScoringService.CalculateScore(new RiskScoreInput(
            SameFingerprint: sameFingerprint,
            SameIp24h: false,
            CustomerAgeDaysLessThan7: false,
            DeviceFirstSeenLessThan24h: false,
            OrdersFromDeviceTodayGreaterThan3: false,
            ReferralBonusAmountGreaterThan50K: config.AppInstallBonus > 50000,
            AppInstallTimeLessThan30s: false,
            BlacklistedFingerprint: false,
            SelfReferral: selfReferral
        ));

        referral.SetRiskScore(riskResult.Score, riskResult.RiskFactors);

        // Create FraudFlag if high risk (self-referral always lands here — its weight exceeds the threshold)
        if (riskResult.Score >= 60)
        {
            await _fraudFlagService.CreateFlagAsync(
                order.TenantId.Value,
                FraudEntityType.SalesReferral,
                referral.Id,
                order.CustomerId,
                selfReferral ? FraudFlagType.SelfDeal : FraudFlagType.HighRiskScore,
                riskResult.Score,
                riskResult.RiskFactors,
                $"Commission auto-flagged: RiskScore={riskResult.Score}, SelfReferral={selfReferral}, " +
                $"SameFingerprint={sameFingerprint}, SameDevice={sameDevice}");
        }

        _dbContext.SalesReferrals.Add(referral);
        await _dbContext.SaveChangesAsync();

        _logger.LogInformation(
            "CreateCommission: SalesReferral {ReferralId} created for order {OrderId}, base={Base}, rate={Rate}, commission={Amount}, status={Status}, riskScore={Score}, selfReferral={SelfReferral}",
            referral.Id, orderId, referral.CommissionBaseAmount, referral.CommissionRate,
            referral.CommissionAmount, referral.CommissionStatus, referral.RiskScore, selfReferral);

        return referral;
    }

    /// <summary>
    /// CC-S4 fix: detect a salesman buying through their own referral — matches the buyer's device
    /// registrations (fingerprint / device token) against the salesman's own devices, and falls back to
    /// the order's CustomerDeviceId for guest checkout. Mirrors AppInstallAttributionService's self-deal check.
    /// </summary>
    private async Task<(bool SameFingerprint, bool SameDevice)> DetectSelfReferralAsync(Order order)
    {
        if (order.SalesmanId == null || order.SalesmanId == Guid.Empty)
            return (false, false);

        var salesmanDevices = await _dbContext.DeviceRegistrations
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(d => d.CustomerId == order.SalesmanId.Value)
            .Select(d => new { d.FingerprintHash, d.DeviceToken })
            .ToListAsync();

        if (salesmanDevices.Count == 0)
            return (false, false);

        var salesmanFingerprints = salesmanDevices
            .Select(d => d.FingerprintHash)
            .Where(h => !string.IsNullOrEmpty(h))
            .ToHashSet(StringComparer.Ordinal);
        var salesmanTokens = salesmanDevices
            .Select(d => d.DeviceToken)
            .Where(t => !string.IsNullOrEmpty(t))
            .ToHashSet(StringComparer.Ordinal);

        // Buyer devices: by CustomerId when known, else by the order's device id (guest checkout).
        var buyerCustomerId = order.CustomerId ?? Guid.Empty;
        var buyerDeviceToken = order.CustomerDeviceId ?? string.Empty;

        var buyerDevices = await _dbContext.DeviceRegistrations
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(d => (buyerCustomerId != Guid.Empty && d.CustomerId == buyerCustomerId)
                || (buyerDeviceToken != "" && d.DeviceToken == buyerDeviceToken))
            .Select(d => new { d.FingerprintHash, d.DeviceToken })
            .ToListAsync();

        bool sameFingerprint = buyerDevices.Any(d =>
            !string.IsNullOrEmpty(d.FingerprintHash) && salesmanFingerprints.Contains(d.FingerprintHash));
        bool sameDevice = buyerDevices.Any(d =>
            !string.IsNullOrEmpty(d.DeviceToken) && salesmanTokens.Contains(d.DeviceToken));

        return (sameFingerprint, sameDevice);
    }

    public async Task<ReferralScanResult?> ResolveReferralForScanAsync(string referralCode)
    {
        if (string.IsNullOrWhiteSpace(referralCode))
            return null;

        var resolved = await ResolveCompositeReferralCodeAsync(referralCode);
        if (resolved == null)
        {
            _logger.LogWarning("ResolveReferralForScan: code {Code} did not resolve", referralCode);
            return null;
        }

        var (salesmanId, productId) = resolved.Value;

        // Product display info lives in Gateway PG FeaturedProducts (the operational Product
        // lives in ShopERP SQLite — same source the product-QR fast path uses).
        var fp = await _dbContext.FeaturedProducts
            .IgnoreQueryFilters()
            .AsNoTracking()
            .FirstOrDefaultAsync(f => f.ProductId == productId && f.IsActive);

        if (fp == null)
        {
            _logger.LogWarning("ResolveReferralForScan: no active FeaturedProduct for product {ProductId} (code {Code})", productId, referralCode);
            return null;
        }

        // Composite code format: "{salesmanCode}|{productShortCode}"
        var parts = referralCode.Split('|', 2);

        return new ReferralScanResult
        {
            SalesmanId = salesmanId,
            SalesmanCode = parts.Length > 0 ? parts[0] : string.Empty,
            ProductShortCode = parts.Length > 1 ? parts[1] : string.Empty,
            ReferralCode = referralCode,
            ProductId = fp.ProductId,
            TenantId = fp.TenantId.Value,
            Name = fp.DisplayName,
            Price = fp.DisplayPrice,
            VatRate = fp.VatRate,
            ImageUrl = fp.ImageUrl,
            Description = fp.DisplayDescription,
            IsFree = fp.ProductType != FeaturedProductType.Paid
        };
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
