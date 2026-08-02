using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using VanAn.CoreHub.Infrastructure;
using VanAn.CoreHub.Services;
using VanAn.Shared.Domain;
using Xunit;

using Microsoft.Extensions.DependencyInjection;
namespace VanAn.Core.Tests.Community;

/// <summary>
/// CC-S4 (Sprint 4 v1.2): FraudFlagService + RiskScoring integration tests.
/// 9 test cases (T21-T31 per detailed plan). Uses SQLite in-memory.
/// </summary>
public class FraudFlagServiceTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly VanAnDbContext _context;
    private readonly FraudFlagService _service;
    private readonly RiskScoringService _riskService;
    private static readonly Guid TenantId = Guid.Parse("00000000-0000-0000-0000-000000000001");
    private static readonly Guid SalesmanId = Guid.NewGuid();
    private static readonly Guid ProductId = Guid.NewGuid();

    public FraudFlagServiceTests()
    {
        _connection = new SqliteConnection($"DataSource=test_{Guid.NewGuid()};Mode=Memory;Cache=Shared");
        _connection.Open();

        var efServiceProvider = new ServiceCollection().AddEntityFrameworkSqlite().BuildServiceProvider();

        var options = new DbContextOptionsBuilder<VanAnDbContext>()
            .UseInternalServiceProvider(efServiceProvider).UseSqlite(_connection)
            .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        _context = new VanAnDbContext(options);
        _context.Database.EnsureCreated();
        _service = new FraudFlagService(_context, NullLogger<FraudFlagService>.Instance);
        _riskService = new RiskScoringService();
    }

    public void Dispose()
    {
        _context.Dispose();
        _connection.Dispose();
    }

    private SalesReferral CreateReferral(int riskScore, string riskFactors)
    {
        var referral = new SalesReferral(new TenantId(TenantId), SalesmanId, "ABC123", ProductId, "TR-001");
        referral.AttachToOrder(Guid.NewGuid(), Guid.NewGuid(), 100000, 0.05m);
        referral.SetRiskScore(riskScore, riskFactors);
        return referral;
    }

    private AppInstallAttribution CreateAttribution(int riskScore, string riskFactors)
    {
        var attr = new AppInstallAttribution(new TenantId(TenantId), Guid.NewGuid(), SalesmanId, ProductId, 10000);
        attr.SetRiskScore(riskScore, riskFactors);
        return attr;
    }

    // === T21: SalesReferral_Create_HighRisk_HoldsCommission ===
    [Fact(DisplayName = "T21: SalesReferral_Create_HighRisk_HoldsCommission")]
    public void SalesReferral_Create_HighRisk_HoldsCommission()
    {
        var referral = CreateReferral(70, "{}");

        Assert.Equal(CommissionStatus.Held, referral.CommissionStatus);
        Assert.NotNull(referral.HoldUntil);
    }

    // === T22: SalesReferral_Create_VeryHighRisk_RejectsCommission ===
    [Fact(DisplayName = "T22: SalesReferral_Create_VeryHighRisk_RejectsCommission")]
    public void SalesReferral_Create_VeryHighRisk_RejectsCommission()
    {
        var referral = CreateReferral(85, "{}");

        Assert.Equal(CommissionStatus.Rejected, referral.CommissionStatus);
    }

    // === T23: SalesReferral_Create_LowRisk_PendingWithCooling ===
    [Fact(DisplayName = "T23: SalesReferral_Create_LowRisk_PendingWithCooling")]
    public void SalesReferral_Create_LowRisk_PendingWithCooling()
    {
        var referral = CreateReferral(30, "{}");

        Assert.Equal(CommissionStatus.Pending, referral.CommissionStatus);
        Assert.Null(referral.HoldUntil);
    }

    // === T24: AppInstallAttribution_Create_HighRisk_HoldsBonus ===
    [Fact(DisplayName = "T24: AppInstallAttribution_Create_HighRisk_HoldsBonus")]
    public void AppInstallAttribution_Create_HighRisk_HoldsBonus()
    {
        var attr = CreateAttribution(65, "{}");

        Assert.Equal(AttributionStatus.Held, attr.AttributionStatus);
        Assert.NotNull(attr.HoldUntil);
    }

    // === T25: AppInstallAttribution_Create_VeryHighRisk_RejectsBonus ===
    [Fact(DisplayName = "T25: AppInstallAttribution_Create_VeryHighRisk_RejectsBonus")]
    public void AppInstallAttribution_Create_VeryHighRisk_RejectsBonus()
    {
        var attr = CreateAttribution(90, "{}");

        Assert.Equal(AttributionStatus.Rejected, attr.AttributionStatus);
    }

    // === T26: AppInstallAttribution_Create_LowRisk_PendingWithCooling ===
    [Fact(DisplayName = "T26: AppInstallAttribution_Create_LowRisk_PendingWithCooling")]
    public void AppInstallAttribution_Create_LowRisk_PendingWithCooling()
    {
        var attr = CreateAttribution(20, "{}");

        Assert.Equal(AttributionStatus.Pending, attr.AttributionStatus);
        Assert.Null(attr.HoldUntil);
    }

    // === T27: FraudFlagService_Create_WhenRiskScoreHigh_CreatesFlag ===
    [Fact(DisplayName = "T27: FraudFlagService_Create_WhenRiskScoreHigh_CreatesFlag")]
    public async Task FraudFlagService_Create_WhenRiskScoreHigh_CreatesFlag()
    {
        var flag = await _service.CreateFlagAsync(
            TenantId, FraudEntityType.SalesReferral, Guid.NewGuid(),
            Guid.NewGuid(), FraudFlagType.HighRiskScore, 60, "{}", "High risk");

        Assert.NotNull(flag);
        Assert.Equal(FraudFlagStatus.Pending, flag.Status);
        Assert.Equal(60, flag.RiskScore);
    }

    // === T28: FraudFlagService_GetPendingFlags_ReturnsSortedByRiskScore ===
    [Fact(DisplayName = "T28: FraudFlagService_GetPendingFlags_ReturnsSortedByRiskScore")]
    public async Task FraudFlagService_GetPendingFlags_ReturnsSortedByRiskScore()
    {
        await _service.CreateFlagAsync(TenantId, FraudEntityType.SalesReferral, Guid.NewGuid(),
            null, FraudFlagType.HighRiskScore, 60, "{}", "Flag 1");
        await _service.CreateFlagAsync(TenantId, FraudEntityType.SalesReferral, Guid.NewGuid(),
            null, FraudFlagType.HighRiskScore, 90, "{}", "Flag 2");
        await _service.CreateFlagAsync(TenantId, FraudEntityType.SalesReferral, Guid.NewGuid(),
            null, FraudFlagType.HighRiskScore, 70, "{}", "Flag 3");

        var flags = await _service.GetPendingFlagsAsync();

        Assert.Equal(3, flags.Count);
        Assert.Equal(90, flags[0].RiskScore);
        Assert.Equal(70, flags[1].RiskScore);
        Assert.Equal(60, flags[2].RiskScore);
    }

    // === T29: FraudFlagService_Confirm_UpdatesEntityStatus ===
    [Fact(DisplayName = "T29: FraudFlagService_Confirm_UpdatesEntityStatus")]
    public async Task FraudFlagService_Confirm_UpdatesEntityStatus()
    {
        var referral = CreateReferral(70, "{}");
        _context.SalesReferrals.Add(referral);
        await _context.SaveChangesAsync();

        var flag = await _service.CreateFlagAsync(
            TenantId, FraudEntityType.SalesReferral, referral.Id,
            null, FraudFlagType.HighRiskScore, 70, "{}", "High risk");

        await _service.ConfirmFlagAsync(flag.Id, Guid.NewGuid(), "Confirmed fraud");

        var updatedReferral = await _context.SalesReferrals.IgnoreQueryFilters().FirstAsync(r => r.Id == referral.Id);
        Assert.Equal(CommissionStatus.Rejected, updatedReferral.CommissionStatus);

        var updatedFlag = await _context.FraudFlags.IgnoreQueryFilters().FirstAsync(f => f.Id == flag.Id);
        Assert.Equal(FraudFlagStatus.Confirmed, updatedFlag.Status);
    }

    // === T30: AppInstall_WithFingerprint_MatchesSalesman_HighRisk ===
    [Fact(DisplayName = "T30: AppInstall_WithFingerprint_MatchesSalesman_HighRisk")]
    public void AppInstall_WithFingerprint_MatchesSalesman_HighRisk()
    {
        var result = _riskService.CalculateScore(new RiskScoreInput(
            SameFingerprint: true,
            SameIp24h: false,
            CustomerAgeDaysLessThan7: false,
            DeviceFirstSeenLessThan24h: false,
            OrdersFromDeviceTodayGreaterThan3: false,
            ReferralBonusAmountGreaterThan50K: false,
            AppInstallTimeLessThan30s: false,
            BlacklistedFingerprint: false
        ));

        Assert.True(result.Score >= 50);
        Assert.Contains("SameFingerprint", result.RiskFactors);
    }

    // === T31: AppInstall_WithFingerprint_DifferentFromSalesman_LowRisk ===
    [Fact(DisplayName = "T31: AppInstall_WithFingerprint_DifferentFromSalesman_LowRisk")]
    public void AppInstall_WithFingerprint_DifferentFromSalesman_LowRisk()
    {
        var result = _riskService.CalculateScore(new RiskScoreInput(
            SameFingerprint: false,
            SameIp24h: false,
            CustomerAgeDaysLessThan7: false,
            DeviceFirstSeenLessThan24h: false,
            OrdersFromDeviceTodayGreaterThan3: false,
            ReferralBonusAmountGreaterThan50K: false,
            AppInstallTimeLessThan30s: false,
            BlacklistedFingerprint: false
        ));

        Assert.Equal(0, result.Score);
    }
}
