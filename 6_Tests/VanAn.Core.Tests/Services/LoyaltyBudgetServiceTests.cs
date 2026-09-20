using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using VanAn.CoreHub.Infrastructure;
using VanAn.CoreHub.Services;
using VanAn.Shared.Domain;
using VanAn.Shared.Domain.Common;
using Xunit;

namespace VanAn.Tests.Services;

/// <summary>
/// Loyalty Points Integrity — Batch 3 (Phase 3): LoyaltyBudgetService enforcement tests.
/// Verifies all 4 budget caps (per-order rate, monthly, daily, per-customer daily via PG
/// LoyaltyIssuanceRecord) + atomic counter increment (ExecuteUpdate) + reversal decrement.
/// Uses a real SQLite in-memory VanAnDbContext (same pattern as LoyaltyConfigControllerTests).
/// </summary>
public class LoyaltyBudgetServiceTests
{
    private static readonly Guid TenantGuid = Guid.Parse("00000000-0000-0000-0000-00000000000a");
    private static readonly Guid CustomerA = Guid.Parse("00000000-0000-0000-0000-0000000000a1");
    private static readonly Guid CustomerB = Guid.Parse("00000000-0000-0000-0000-0000000000b1");

    private static (LoyaltyBudgetService service, VanAnDbContext db, ServiceProvider sp) BuildService()
    {
        var connection = new SqliteConnection($"DataSource=test_{Guid.NewGuid()};Mode=Memory;Cache=Shared");
        connection.Open();

        var services = new ServiceCollection();
        var efServiceProvider = new ServiceCollection().AddEntityFrameworkSqlite().BuildServiceProvider();
        services.AddDbContext<VanAnDbContext>(options => options.UseInternalServiceProvider(efServiceProvider).UseSqlite(connection));
        services.AddScoped<IVanAnDbContext>(sp => sp.GetRequiredService<VanAnDbContext>());
        ServiceProvider sp = services.BuildServiceProvider();
        VanAnDbContext db = sp.GetRequiredService<VanAnDbContext>();
        _ = db.Database.EnsureCreated();

        var service = new LoyaltyBudgetService(db, NullLogger<LoyaltyBudgetService>.Instance);
        return (service, db, sp);
    }

    private static LoyaltyTenantConfig SeedConfig(VanAnDbContext db, int? monthly = null, int? daily = null,
        int? perCustomerDaily = null, decimal? perOrderRate = null)
    {
        var config = new LoyaltyTenantConfig(new TenantId(TenantGuid));
        config.SetBudgetCaps(monthly, daily, perCustomerDaily, perOrderRate, "test");
        db.LoyaltyTenantConfigs.Add(config);
        db.SaveChanges();
        return config;
    }

    // ──────────────────────────────────────────────────────────
    // Monthly budget
    // ──────────────────────────────────────────────────────────

    [Fact(DisplayName = "LB-1: Monthly budget — 1000 cap, 600 awarded → next 600 request adjusted to 400")]
    public async Task MonthlyBudget_CapsSecondAward()
    {
        var (service, db, sp) = BuildService();

        try
        {
            SeedConfig(db, monthly: 1000);

            int first = await service.CheckAndAdjustPointsAsync(TenantGuid, CustomerA, orderAmount: 100_000m, requestedPoints: 600);
            Assert.Equal(600, first);
            await service.RecordIssuanceAsync(TenantGuid, first);

            int second = await service.CheckAndAdjustPointsAsync(TenantGuid, CustomerA, orderAmount: 100_000m, requestedPoints: 600);
            Assert.Equal(400, second); // 1000 - 600 = 400 remaining
        }
        finally
        {
            await sp.DisposeAsync();
        }
    }

    [Fact(DisplayName = "LB-2: Monthly budget — exhausted returns 0")]
    public async Task MonthlyBudget_Exhausted_ReturnsZero()
    {
        var (service, db, sp) = BuildService();

        try
        {
            SeedConfig(db, monthly: 500);

            int first = await service.CheckAndAdjustPointsAsync(TenantGuid, CustomerA, orderAmount: null, requestedPoints: 500);
            Assert.Equal(500, first);
            await service.RecordIssuanceAsync(TenantGuid, first);

            int second = await service.CheckAndAdjustPointsAsync(TenantGuid, CustomerA, orderAmount: null, requestedPoints: 100);
            Assert.Equal(0, second);
        }
        finally
        {
            await sp.DisposeAsync();
        }
    }

    // ──────────────────────────────────────────────────────────
    // Daily budget
    // ──────────────────────────────────────────────────────────

    [Fact(DisplayName = "LB-3: Daily budget — 500 cap, 600 requested → 500")]
    public async Task DailyBudget_CapsRequest()
    {
        var (service, db, sp) = BuildService();

        try
        {
            SeedConfig(db, daily: 500);

            int adjusted = await service.CheckAndAdjustPointsAsync(TenantGuid, CustomerA, orderAmount: null, requestedPoints: 600);
            Assert.Equal(500, adjusted);
        }
        finally
        {
            await sp.DisposeAsync();
        }
    }

    // ──────────────────────────────────────────────────────────
    // Per-order rate cap
    // ──────────────────────────────────────────────────────────

    [Fact(DisplayName = "LB-4: PerOrderRateCap — 3% of 100,000 = 3,000 cap")]
    public async Task PerOrderRateCap_CapsByOrderAmount()
    {
        var (service, db, sp) = BuildService();

        try
        {
            SeedConfig(db, perOrderRate: 0.03m);

            int adjusted = await service.CheckAndAdjustPointsAsync(TenantGuid, CustomerA, orderAmount: 100_000m, requestedPoints: 5000);
            Assert.Equal(3000, adjusted);
        }
        finally
        {
            await sp.DisposeAsync();
        }
    }

    [Fact(DisplayName = "LB-5: PerOrderRateCap — null orderAmount skips the cap (non-order awards)")]
    public async Task PerOrderRateCap_NullOrderAmount_SkipsCap()
    {
        var (service, db, sp) = BuildService();

        try
        {
            SeedConfig(db, perOrderRate: 0.03m);

            int adjusted = await service.CheckAndAdjustPointsAsync(TenantGuid, CustomerA, orderAmount: null, requestedPoints: 5000);
            Assert.Equal(5000, adjusted);
        }
        finally
        {
            await sp.DisposeAsync();
        }
    }

    // ──────────────────────────────────────────────────────────
    // Per-customer daily limit (via PG LoyaltyIssuanceRecord)
    // ──────────────────────────────────────────────────────────

    [Fact(DisplayName = "LB-6: PerCustomerDailyLimit — 150 limit, customer issued 100 today → 50 remaining")]
    public async Task PerCustomerDailyLimit_CountsOnlyCustomersIssuance()
    {
        var (service, db, sp) = BuildService();

        try
        {
            SeedConfig(db, perCustomerDaily: 150);

            // Customer A already earned 100 today (PG issuance record)
            db.LoyaltyIssuanceRecords.Add(new LoyaltyIssuanceRecord(
                new TenantId(TenantGuid), Guid.NewGuid(), CustomerA, 100));
            await db.SaveChangesAsync();

            int forA = await service.CheckAndAdjustPointsAsync(TenantGuid, CustomerA, orderAmount: null, requestedPoints: 100);
            Assert.Equal(50, forA);

            // Customer B has no records → full amount
            int forB = await service.CheckAndAdjustPointsAsync(TenantGuid, CustomerB, orderAmount: null, requestedPoints: 100);
            Assert.Equal(100, forB);
        }
        finally
        {
            await sp.DisposeAsync();
        }
    }

    [Fact(DisplayName = "LB-7: PerCustomerDailyLimit — reversed records are not counted")]
    public async Task PerCustomerDailyLimit_IgnoresReversedRecords()
    {
        var (service, db, sp) = BuildService();

        try
        {
            SeedConfig(db, perCustomerDaily: 100);

            var record = new LoyaltyIssuanceRecord(new TenantId(TenantGuid), Guid.NewGuid(), CustomerA, 80);
            record.MarkReversed();
            db.LoyaltyIssuanceRecords.Add(record);
            await db.SaveChangesAsync();

            int adjusted = await service.CheckAndAdjustPointsAsync(TenantGuid, CustomerA, orderAmount: null, requestedPoints: 100);
            Assert.Equal(100, adjusted);
        }
        finally
        {
            await sp.DisposeAsync();
        }
    }

    // ──────────────────────────────────────────────────────────
    // Counter increment (atomic) + reversal decrement
    // ──────────────────────────────────────────────────────────

    [Fact(DisplayName = "LB-8: RecordIssuance — atomic counter increment persists")]
    public async Task RecordIssuance_IncrementsCounters()
    {
        var (service, db, sp) = BuildService();

        try
        {
            SeedConfig(db, monthly: 1000, daily: 1000);

            await service.RecordIssuanceAsync(TenantGuid, 250);

            var config = await db.LoyaltyTenantConfigs.IgnoreQueryFilters().AsNoTracking()
                .FirstAsync(c => c.TenantId == new TenantId(TenantGuid));
            Assert.Equal(250, config.PointsIssuedThisMonth);
            Assert.Equal(250, config.PointsIssuedToday);
        }
        finally
        {
            await sp.DisposeAsync();
        }
    }

    [Fact(DisplayName = "LB-9: DecrementIssuance — reversal frees budget (clamped at 0) → award lại được")]
    public async Task DecrementIssuance_Reversal_FreesBudgetForNewAward()
    {
        var (service, db, sp) = BuildService();

        try
        {
            SeedConfig(db, monthly: 1000);

            // Award 900 → only 100 left
            int first = await service.CheckAndAdjustPointsAsync(TenantGuid, CustomerA, orderAmount: null, requestedPoints: 900);
            Assert.Equal(900, first);
            await service.RecordIssuanceAsync(TenantGuid, first);

            // Reversal of 500 → budget freed back to 600 remaining
            await service.DecrementIssuanceAsync(TenantGuid, 500);

            int after = await service.CheckAndAdjustPointsAsync(TenantGuid, CustomerA, orderAmount: null, requestedPoints: 600);
            Assert.Equal(600, after);
        }
        finally
        {
            await sp.DisposeAsync();
        }
    }

    [Fact(DisplayName = "LB-10: DecrementIssuance — clamped at 0, never negative")]
    public async Task DecrementIssuance_ClampedAtZero()
    {
        var (service, db, sp) = BuildService();

        try
        {
            SeedConfig(db, monthly: 1000);

            await service.RecordIssuanceAsync(TenantGuid, 50);
            await service.DecrementIssuanceAsync(TenantGuid, 500); // more than issued

            var config = await db.LoyaltyTenantConfigs.IgnoreQueryFilters().AsNoTracking()
                .FirstAsync(c => c.TenantId == new TenantId(TenantGuid));
            Assert.Equal(0, config.PointsIssuedThisMonth);
            Assert.Equal(0, config.PointsIssuedToday);
        }
        finally
        {
            await sp.DisposeAsync();
        }
    }

    // ──────────────────────────────────────────────────────────
    // No config
    // ──────────────────────────────────────────────────────────

    [Fact(DisplayName = "LB-11: No config row → no caps → original points returned")]
    public async Task NoConfig_ReturnsOriginalPoints()
    {
        var (service, _, sp) = BuildService();

        try
        {
            int adjusted = await service.CheckAndAdjustPointsAsync(TenantGuid, CustomerA, orderAmount: 100_000m, requestedPoints: 5000);
            Assert.Equal(5000, adjusted);
        }
        finally
        {
            await sp.DisposeAsync();
        }
    }
}
