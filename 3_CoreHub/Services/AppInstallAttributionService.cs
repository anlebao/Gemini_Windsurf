using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using VanAn.CoreHub.Infrastructure;
using VanAn.Shared.Domain;

namespace VanAn.CoreHub.Services;

/// <summary>
/// CC-S4 (Sprint 4): App-install attribution service implementation.
/// v1.2: risk scoring + FraudFlag integration.
/// v1.4: KHÔNG tạo WalletTransaction (create sau 24h bởi CoolingPeriodJob hoặc admin approve Sprint 6).
/// </summary>
public class AppInstallAttributionService(
    IVanAnDbContext dbContext,
    IRiskScoringService riskScoringService,
    IFraudFlagService fraudFlagService,
    ILogger<AppInstallAttributionService> logger) : IAppInstallAttributionService
{
    private readonly IVanAnDbContext _dbContext = dbContext;
    private readonly IRiskScoringService _riskScoringService = riskScoringService;
    private readonly IFraudFlagService _fraudFlagService = fraudFlagService;
    private readonly ILogger<AppInstallAttributionService> _logger = logger;

    public async Task<AppInstallAttributionDto?> AttributeInstallAsync(
        Guid customerId, string referralCode,
        string? fingerprintHash = null, string? fingerprintSignals = null, string? deviceToken = null)
    {
        if (string.IsNullOrWhiteSpace(referralCode) || !referralCode.Contains('|'))
        {
            _logger.LogWarning("AttributeInstall: Invalid referral code {Code}", referralCode);
            return null;
        }

        // Check customer hasn't already attributed (unique constraint — 1 customer 1 attribution)
        var existing = await _dbContext.AppInstallAttributions
            .IgnoreQueryFilters()
            .AsNoTracking()
            .AnyAsync(a => a.CustomerId == customerId);

        if (existing)
        {
            _logger.LogWarning("AttributeInstall: Customer {CustomerId} already has attribution", customerId);
            throw new InvalidOperationException("Customer already has an app-install attribution.");
        }

        // Resolve composite referral code
        var parts = referralCode.Split('|', 2);
        var salesmanCode = parts[0].Trim();
        var productShortCode = parts[1].Trim();

        // Find salesman by code
        var role = await _dbContext.CommunityRoles
            .IgnoreQueryFilters()
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.SalesmanCode == salesmanCode
                && r.RoleType == CommunityRoleType.Salesman
                && r.IsActive);

        if (role == null)
        {
            _logger.LogWarning("AttributeInstall: Salesman code {Code} not found", salesmanCode);
            return null;
        }

        // Find product config
        var config = await _dbContext.ProductReferralConfigs
            .IgnoreQueryFilters()
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.ProductShortCode == productShortCode && c.IsActive);

        if (config == null)
        {
            _logger.LogWarning("AttributeInstall: Product short code {Code} not found", productShortCode);
            return null;
        }

        // v1.2: Lookup or create DeviceRegistration
        Guid? deviceRegistrationId = null;
        if (!string.IsNullOrEmpty(fingerprintHash) && !string.IsNullOrEmpty(deviceToken))
        {
            var device = await _dbContext.DeviceRegistrations
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(d => d.CustomerId == customerId && d.FingerprintHash == fingerprintHash);

            if (device == null)
            {
                device = new DeviceRegistration(
                    config.TenantId, customerId, deviceToken, fingerprintHash,
                    fingerprintSignals ?? "{}", "", "", "");
                _dbContext.DeviceRegistrations.Add(device);
                await _dbContext.SaveChangesAsync();
            }
            deviceRegistrationId = device.Id;
        }

        // Create AppInstallAttribution
        var attribution = new AppInstallAttribution(
            config.TenantId, customerId, role.CustomerId, config.ProductId,
            config.AppInstallBonus, deviceRegistrationId);

        // v1.2: Compute risk score
        // Check if salesman + customer have same fingerprint (self-deal)
        var salesmanDevice = await _dbContext.DeviceRegistrations
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(d => d.CustomerId == role.CustomerId && d.FingerprintHash == fingerprintHash && d.FingerprintHash != "")
            .FirstOrDefaultAsync();

        var sameFingerprint = salesmanDevice != null && !string.IsNullOrEmpty(fingerprintHash);

        var riskResult = _riskScoringService.CalculateScore(new RiskScoreInput(
            SameFingerprint: sameFingerprint,
            SameIp24h: false,
            CustomerAgeDaysLessThan7: false,
            DeviceFirstSeenLessThan24h: false,
            OrdersFromDeviceTodayGreaterThan3: false,
            ReferralBonusAmountGreaterThan50K: config.AppInstallBonus > 50000,
            AppInstallTimeLessThan30s: false,
            BlacklistedFingerprint: false
        ));

        attribution.SetRiskScore(riskResult.Score, riskResult.RiskFactors);

        _dbContext.AppInstallAttributions.Add(attribution);

        // Create FraudFlag if high risk
        if (riskResult.Score >= 60)
        {
            var fraudFlag = await _fraudFlagService.CreateFlagAsync(
                config.TenantId.Value,
                FraudEntityType.AppInstallAttribution,
                attribution.Id,
                customerId,
                sameFingerprint ? FraudFlagType.SelfDeal : FraudFlagType.HighRiskScore,
                riskResult.Score,
                riskResult.RiskFactors,
                $"App-install auto-flagged: RiskScore={riskResult.Score}, SameFingerprint={sameFingerprint}");
        }

        await _dbContext.SaveChangesAsync();

        _logger.LogInformation("AttributeInstall: Attribution {AttributionId} created for customer {CustomerId}, riskScore={Score}",
            attribution.Id, customerId, attribution.RiskScore);

        return new AppInstallAttributionDto
        {
            Id = attribution.Id,
            CustomerId = attribution.CustomerId,
            SalesmanId = attribution.SalesmanId,
            ProductId = attribution.ProductId,
            BonusAmount = attribution.BonusAmount,
            Status = attribution.AttributionStatus.ToString(),
            RiskScore = attribution.RiskScore,
            RiskFactors = attribution.RiskFactors,
            HoldUntil = attribution.HoldUntil,
            InstalledAt = attribution.InstalledAt
        };
    }

    public async Task<List<AppInstallAttributionDto>> GetBySalesmanAsync(Guid salesmanId)
    {
        var attributions = await _dbContext.AppInstallAttributions
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(a => a.SalesmanId == salesmanId)
            .ToListAsync();

        return attributions.Select(a => new AppInstallAttributionDto
        {
            Id = a.Id,
            CustomerId = a.CustomerId,
            SalesmanId = a.SalesmanId,
            ProductId = a.ProductId,
            BonusAmount = a.BonusAmount,
            Status = a.AttributionStatus.ToString(),
            RiskScore = a.RiskScore,
            RiskFactors = a.RiskFactors,
            HoldUntil = a.HoldUntil,
            InstalledAt = a.InstalledAt
        }).ToList();
    }
}
