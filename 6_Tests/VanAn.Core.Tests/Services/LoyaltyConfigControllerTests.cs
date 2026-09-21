using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using System.Security.Claims;
using VanAn.CoreHub.Infrastructure;
using VanAn.Gateway.Controllers;
using VanAn.Shared.Domain;
using VanAn.Shared.Domain.Common;
using VanAn.Shared.Services;
using Xunit;

namespace VanAn.Tests.Services;

/// <summary>
/// Loyalty Alliance Phase 3A — tests for LoyaltyConfigController (SystemAdmin API).
/// Verifies GET/PUT endpoints for global + per-tenant config CRUD.
/// Uses real SQLite in-memory VanAnDbContext (LoyaltyGlobalConfigs + LoyaltyTenantConfigs tables)
/// + mocked SystemAdmin claims on the controller context.
/// Spec: docs/specs/loyalty-alliance-spec.md v1.0.
/// </summary>
public class LoyaltyConfigControllerTests
{
    private static readonly Guid TestTenantGuid = Guid.Parse("00000000-0000-0000-0000-000000000001");

    /// <summary>
    /// Build a LoyaltyConfigController with a real SQLite in-memory VanAnDbContext
    /// + SystemAdmin claims on the HttpContext.
    /// </summary>
    private static (LoyaltyConfigController controller, VanAnDbContext db, ServiceProvider sp)
        BuildController()
    {
        var connection = new SqliteConnection($"DataSource=test_{Guid.NewGuid()};Mode=Memory;Cache=Shared");
        connection.Open();

        var services = new ServiceCollection();
        var efServiceProvider = new ServiceCollection().AddEntityFrameworkSqlite().BuildServiceProvider();
        services.AddDbContext<VanAnDbContext>(options => options.UseInternalServiceProvider(efServiceProvider).UseSqlite(connection));
        services.AddScoped<IVanAnDbContext>(sp => sp.GetRequiredService<VanAnDbContext>());
        services.AddLogging(b => b.AddConsole().SetMinimumLevel(LogLevel.Warning));
        ServiceProvider sp = services.BuildServiceProvider();
        VanAnDbContext db = sp.GetRequiredService<VanAnDbContext>();
        _ = db.Database.EnsureCreated();

        var controller = new LoyaltyConfigController(db, new Mock<IAllianceWalletService>().Object, NullLogger<LoyaltyConfigController>.Instance);

        // Set up SystemAdmin claims on the controller context
        var claims = new[]
        {
            new Claim(ClaimTypes.Role, "SystemAdmin"),
            new Claim("sub", "test-admin-id")
        };
        var identity = new ClaimsIdentity(claims, "TestAuth");
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) }
        };

        return (controller, db, sp);
    }

    // ──────────────────────────────────────────────────────────
    // Global Config — GET
    // ──────────────────────────────────────────────────────────

    [Fact(DisplayName = "LA-LC-1: GetGlobalConfig — no row returns defaults")]
    public async Task GetGlobalConfig_NoRow_ReturnsDefaults()
    {
        var (controller, _, sp) = BuildController();

        try
        {
            var result = await controller.GetGlobalConfig();

            var ok = Assert.IsType<OkObjectResult>(result);
            var dto = Assert.IsType<GlobalConfigDto>(ok.Value);
            Assert.Equal(LoyaltyMode.Silo, dto.Mode);
            Assert.Equal(100000, dto.MaxWalletPoints);
        }
        finally
        {
            await sp.DisposeAsync();
        }
    }

    [Fact(DisplayName = "LA-LC-2: GetGlobalConfig — existing row returns stored values")]
    public async Task GetGlobalConfig_ExistingRow_ReturnsStoredValues()
    {
        var (controller, db, sp) = BuildController();

        try
        {
            // Seed a global config row
            var config = new LoyaltyGlobalConfig();
            config.UpdateMode(LoyaltyMode.Alliance, "admin");
            config.UpdateLimits(50, 200000, "admin");
            db.LoyaltyGlobalConfigs.Add(config);
            await db.SaveChangesAsync();

            var result = await controller.GetGlobalConfig();

            var ok = Assert.IsType<OkObjectResult>(result);
            var dto = Assert.IsType<GlobalConfigDto>(ok.Value);
            Assert.Equal(LoyaltyMode.Alliance, dto.Mode);
            Assert.Equal(50, dto.MaxPointsPerOrder);
            Assert.Equal(200000, dto.MaxWalletPoints);
        }
        finally
        {
            await sp.DisposeAsync();
        }
    }

    // ──────────────────────────────────────────────────────────
    // Global Config — PUT
    // ──────────────────────────────────────────────────────────

    [Fact(DisplayName = "LA-LC-3: UpdateGlobalConfig — creates row if not exists")]
    public async Task UpdateGlobalConfig_NoRow_CreatesRow()
    {
        var (controller, db, sp) = BuildController();

        try
        {
            var body = new UpdateGlobalConfigRequest
            {
                Mode = LoyaltyMode.Alliance,
                MaxPointsPerOrder = 50,
                MaxWalletPoints = 200000
            };

            var result = await controller.UpdateGlobalConfig(body);

            var ok = Assert.IsType<OkObjectResult>(result);
            var dto = Assert.IsType<GlobalConfigDto>(ok.Value);
            Assert.Equal(LoyaltyMode.Alliance, dto.Mode);
            Assert.Equal(50, dto.MaxPointsPerOrder);
            Assert.Equal(200000, dto.MaxWalletPoints);
            Assert.Equal("test-admin-id", dto.LastChangedBy);

            // Verify row was persisted
            var config = await db.LoyaltyGlobalConfigs.FirstOrDefaultAsync();
            Assert.NotNull(config);
            Assert.Equal(LoyaltyMode.Alliance, config!.Mode);
        }
        finally
        {
            await sp.DisposeAsync();
        }
    }

    [Fact(DisplayName = "LA-LC-4: UpdateGlobalConfig — updates existing row")]
    public async Task UpdateGlobalConfig_ExistingRow_UpdatesValues()
    {
        var (controller, db, sp) = BuildController();

        try
        {
            // Seed existing row
            var config = new LoyaltyGlobalConfig();
            config.UpdateMode(LoyaltyMode.Silo, "initial");
            db.LoyaltyGlobalConfigs.Add(config);
            await db.SaveChangesAsync();

            var body = new UpdateGlobalConfigRequest
            {
                Mode = LoyaltyMode.Alliance,
                MaxPointsPerOrder = 100,
                MaxWalletPoints = 500000
            };

            var result = await controller.UpdateGlobalConfig(body);

            var ok = Assert.IsType<OkObjectResult>(result);
            var dto = Assert.IsType<GlobalConfigDto>(ok.Value);
            Assert.Equal(LoyaltyMode.Alliance, dto.Mode);
            Assert.Equal(100, dto.MaxPointsPerOrder);
            Assert.Equal(500000, dto.MaxWalletPoints);

            // Verify only 1 row exists (updated, not duplicated)
            var count = await db.LoyaltyGlobalConfigs.CountAsync();
            Assert.Equal(1, count);
        }
        finally
        {
            await sp.DisposeAsync();
        }
    }

    [Fact(DisplayName = "LA-LC-5: UpdateGlobalConfig — negative maxWalletPoints returns 400")]
    public async Task UpdateGlobalConfig_NegativeValue_Returns400()
    {
        var (controller, _, sp) = BuildController();

        try
        {
            var body = new UpdateGlobalConfigRequest
            {
                Mode = LoyaltyMode.Alliance,
                MaxPointsPerOrder = 50,
                MaxWalletPoints = -1
            };

            var result = await controller.UpdateGlobalConfig(body);

            var bad = Assert.IsType<BadRequestObjectResult>(result);
            Assert.NotNull(bad.Value);
        }
        finally
        {
            await sp.DisposeAsync();
        }
    }

    // ──────────────────────────────────────────────────────────
    // Tenant Config — GET
    // ──────────────────────────────────────────────────────────

    [Fact(DisplayName = "LA-LC-6: GetTenantConfig — no row returns inherit defaults")]
    public async Task GetTenantConfig_NoRow_ReturnsInheritDefaults()
    {
        var (controller, _, sp) = BuildController();

        try
        {
            var result = await controller.GetTenantConfig(TestTenantGuid);

            var ok = Assert.IsType<OkObjectResult>(result);
            var dto = Assert.IsType<TenantConfigDto>(ok.Value);
            Assert.Equal(TestTenantGuid, dto.TenantId);
            Assert.Null(dto.Mode); // inherit global
            Assert.False(dto.IsAllianceMember);
            Assert.Null(dto.MaxWalletPoints); // inherit global
        }
        finally
        {
            await sp.DisposeAsync();
        }
    }

    [Fact(DisplayName = "LA-LC-7: GetTenantConfig — existing row returns stored override")]
    public async Task GetTenantConfig_ExistingRow_ReturnsOverride()
    {
        var (controller, db, sp) = BuildController();

        try
        {
            // Seed tenant config
            var config = new LoyaltyTenantConfig(new TenantId(TestTenantGuid));
            config.SetMode(LoyaltyMode.Alliance, "admin");
            config.SetAllianceMembership(true, "admin");
            config.SetMaxWalletPoints(50000, "admin");
            db.LoyaltyTenantConfigs.Add(config);
            await db.SaveChangesAsync();

            var result = await controller.GetTenantConfig(TestTenantGuid);

            var ok = Assert.IsType<OkObjectResult>(result);
            var dto = Assert.IsType<TenantConfigDto>(ok.Value);
            Assert.Equal(LoyaltyMode.Alliance, dto.Mode);
            Assert.True(dto.IsAllianceMember);
            Assert.Equal(50000, dto.MaxWalletPoints);
        }
        finally
        {
            await sp.DisposeAsync();
        }
    }

    // ──────────────────────────────────────────────────────────
    // Tenant Config — PUT
    // ──────────────────────────────────────────────────────────

    [Fact(DisplayName = "LA-LC-8: UpdateTenantConfig — creates row if not exists")]
    public async Task UpdateTenantConfig_NoRow_CreatesRow()
    {
        var (controller, db, sp) = BuildController();

        try
        {
            var body = new UpdateTenantConfigRequest
            {
                Mode = LoyaltyMode.Alliance,
                IsAllianceMember = true,
                MaxWalletPoints = 50000
            };

            var result = await controller.UpdateTenantConfig(TestTenantGuid, body);

            var ok = Assert.IsType<OkObjectResult>(result);
            var dto = Assert.IsType<TenantConfigDto>(ok.Value);
            Assert.Equal(LoyaltyMode.Alliance, dto.Mode);
            Assert.True(dto.IsAllianceMember);
            Assert.Equal(50000, dto.MaxWalletPoints);
            Assert.Equal("test-admin-id", dto.LastChangedBy);

            // Verify row persisted
            var config = await db.LoyaltyTenantConfigs.IgnoreQueryFilters()
                .FirstOrDefaultAsync(c => c.TenantId == new TenantId(TestTenantGuid));
            Assert.NotNull(config);
            Assert.True(config!.IsAllianceMember);
        }
        finally
        {
            await sp.DisposeAsync();
        }
    }

    [Fact(DisplayName = "LA-LC-9: UpdateTenantConfig — null mode inherits global")]
    public async Task UpdateTenantConfig_NullMode_InheritsGlobal()
    {
        var (controller, _, sp) = BuildController();

        try
        {
            var body = new UpdateTenantConfigRequest
            {
                Mode = null, // inherit global
                IsAllianceMember = true,
                MaxWalletPoints = null // inherit global
            };

            var result = await controller.UpdateTenantConfig(TestTenantGuid, body);

            var ok = Assert.IsType<OkObjectResult>(result);
            var dto = Assert.IsType<TenantConfigDto>(ok.Value);
            Assert.Null(dto.Mode);
            Assert.True(dto.IsAllianceMember);
            Assert.Null(dto.MaxWalletPoints);
        }
        finally
        {
            await sp.DisposeAsync();
        }
    }

    [Fact(DisplayName = "LA-LC-10: UpdateTenantConfig — empty tenantId returns 400")]
    public async Task UpdateTenantConfig_EmptyTenantId_Returns400()
    {
        var (controller, _, sp) = BuildController();

        try
        {
            var body = new UpdateTenantConfigRequest
            {
                Mode = LoyaltyMode.Alliance,
                IsAllianceMember = true,
                MaxWalletPoints = 50000
            };

            var result = await controller.UpdateTenantConfig(Guid.Empty, body);

            var bad = Assert.IsType<BadRequestObjectResult>(result);
            Assert.NotNull(bad.Value);
        }
        finally
        {
            await sp.DisposeAsync();
        }
    }

    // ──────────────────────────────────────────────────────────
    // Migration — POST /migrate (Phase 5A wiring of Phase 4 Consolidate/Split)
    // ──────────────────────────────────────────────────────────

    /// <summary>
    /// Build a controller with a settable IAllianceWalletService mock (for migrate tests).
    /// </summary>
    private static (LoyaltyConfigController controller, ServiceProvider sp, Mock<IAllianceWalletService> walletMock)
        BuildControllerWithWalletMock()
    {
        var connection = new SqliteConnection($"DataSource=test_{Guid.NewGuid()};Mode=Memory;Cache=Shared");
        connection.Open();

        var services = new ServiceCollection();
        var efServiceProvider = new ServiceCollection().AddEntityFrameworkSqlite().BuildServiceProvider();
        services.AddDbContext<VanAnDbContext>(options => options.UseInternalServiceProvider(efServiceProvider).UseSqlite(connection));
        services.AddScoped<IVanAnDbContext>(sp => sp.GetRequiredService<VanAnDbContext>());
        services.AddLogging(b => b.AddConsole().SetMinimumLevel(LogLevel.Warning));
        ServiceProvider sp = services.BuildServiceProvider();
        var db = sp.GetRequiredService<VanAnDbContext>();
        _ = db.Database.EnsureCreated();

        var walletMock = new Mock<IAllianceWalletService>();
        var controller = new LoyaltyConfigController(db, walletMock.Object, NullLogger<LoyaltyConfigController>.Instance);

        var claims = new[]
        {
            new Claim(ClaimTypes.Role, "SystemAdmin"),
            new Claim("sub", "test-admin-id")
        };
        var identity = new ClaimsIdentity(claims, "TestAuth");
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) }
        };

        return (controller, sp, walletMock);
    }

    [Fact(DisplayName = "LA-LC-11: Migrate — consolidate calls service and returns result")]
    public async Task Migrate_Consolidate_CallsServiceAndReturnsResult()
    {
        var (controller, sp, walletMock) = BuildControllerWithWalletMock();

        try
        {
            walletMock.Setup(w => w.ConsolidateWalletsAsync(
                    TestTenantGuid,
                    It.Is<IReadOnlyList<CustomerBalanceInput>>(l => l.Count == 1 && l.First().PointBalance == 500),
                    "test-admin-id"))
                .ReturnsAsync(new MigrationResult { CustomersProcessed = 1, TotalPointsTransferred = 500 })
                .Verifiable();

            var body = new MigrateRequest
            {
                Direction = "consolidate",
                TenantId = TestTenantGuid,
                CustomerBalances = new List<CustomerBalanceInputDto>
                {
                    new() { CustomerDeviceId = Guid.NewGuid(), PointBalance = 500, PhoneNumber = "0900" }
                }
            };

            var result = await controller.Migrate(body);

            var ok = Assert.IsType<OkObjectResult>(result);
            var dto = Assert.IsType<MigrationResultDto>(ok.Value);
            Assert.True(dto.Success);
            Assert.Equal(1, dto.CustomersProcessed);
            Assert.Equal(500, dto.TotalPointsTransferred);
            walletMock.Verify();
        }
        finally
        {
            await sp.DisposeAsync();
        }
    }

    [Fact(DisplayName = "LA-LC-12: Migrate — split calls service and returns allocations")]
    public async Task Migrate_Split_CallsServiceAndReturnsAllocations()
    {
        var (controller, sp, walletMock) = BuildControllerWithWalletMock();

        try
        {
            var device = Guid.NewGuid();
            walletMock.Setup(w => w.SplitWalletsAsync(TestTenantGuid, "test-admin-id"))
                .ReturnsAsync(new MigrationResult
                {
                    CustomersProcessed = 1,
                    TotalPointsTransferred = 300,
                    Allocations = new List<WalletAllocation>
                    {
                        new(device, TestTenantGuid, 300)
                    }
                })
                .Verifiable();

            var body = new MigrateRequest { Direction = "split", TenantId = TestTenantGuid };

            var result = await controller.Migrate(body);

            var ok = Assert.IsType<OkObjectResult>(result);
            var dto = Assert.IsType<MigrationResultDto>(ok.Value);
            Assert.True(dto.Success);
            Assert.Single(dto.Allocations);
            Assert.Equal(300, dto.Allocations[0].Points);
            walletMock.Verify();
        }
        finally
        {
            await sp.DisposeAsync();
        }
    }

    [Fact(DisplayName = "LA-LC-13: Migrate — consolidate without balances returns 400")]
    public async Task Migrate_ConsolidateWithoutBalances_Returns400()
    {
        var (controller, sp, _) = BuildControllerWithWalletMock();

        try
        {
            var body = new MigrateRequest { Direction = "consolidate", TenantId = TestTenantGuid };

            var result = await controller.Migrate(body);

            var bad = Assert.IsType<BadRequestObjectResult>(result);
            Assert.NotNull(bad.Value);
        }
        finally
        {
            await sp.DisposeAsync();
        }
    }

    [Fact(DisplayName = "LA-LC-14: Migrate — invalid direction returns 400")]
    public async Task Migrate_InvalidDirection_Returns400()
    {
        var (controller, sp, _) = BuildControllerWithWalletMock();

        try
        {
            var body = new MigrateRequest { Direction = "sideways", TenantId = TestTenantGuid };

            var result = await controller.Migrate(body);

            var bad = Assert.IsType<BadRequestObjectResult>(result);
            Assert.NotNull(bad.Value);
        }
        finally
        {
            await sp.DisposeAsync();
        }
    }

    // ──────────────────────────────────────────────────────────
    // Batch 3 — Budget caps (PUT + Reset counters)
    // ──────────────────────────────────────────────────────────

    // ──────────────────────────────────────────────────────────
    // Batch 5 — Settlement report (GET /api/platform/loyalty/settlement)
    // ──────────────────────────────────────────────────────────

    [Fact(DisplayName = "LA-LC-16: GetSettlement — per-tenant earn/consume/redeem numbers (cross-tenant attribution)")]
    public async Task GetSettlement_CrossTenantRedemption_ReturnsCorrectNumbers()
    {
        var (controller, db, sp) = BuildController();

        try
        {
            var tenantA = Guid.Parse("00000000-0000-0000-0000-000000000001");
            var tenantB = Guid.Parse("00000000-0000-0000-0000-000000000002");
            var wallet = new AllianceWallet(Guid.NewGuid(), null);
            db.AllianceWallets.Add(wallet);
            await db.SaveChangesAsync();

            // A earned 100, B earned 50; customer redeemed 80 at B → B's 50 (B→B) + A's 30 (A→B).
            db.AllianceTransactions.Add(new AllianceTransaction(wallet.Id, tenantA, AllianceTransactionType.EARN, 100, 100, "order A"));
            db.AllianceTransactions.Add(new AllianceTransaction(wallet.Id, tenantB, AllianceTransactionType.EARN, 50, 150, "order B"));

            var redeemB = new AllianceTransaction(wallet.Id, tenantB, AllianceTransactionType.REDEEM, -50, 100, "redeem at B", voucherCode: "VC-1");
            redeemB.SetSourceTenant(tenantB);
            db.AllianceTransactions.Add(redeemB);
            var redeemA = new AllianceTransaction(wallet.Id, tenantB, AllianceTransactionType.REDEEM, -30, 70, "redeem at B", voucherCode: "VC-1");
            redeemA.SetSourceTenant(tenantA);
            db.AllianceTransactions.Add(redeemA);
            await db.SaveChangesAsync();

            // Tenant A: earned 100, own points consumed 30 (all "chi hộ" at B), nothing redeemed at A.
            var resultA = await controller.GetSettlement(tenantA);
            var okA = Assert.IsType<OkObjectResult>(resultA);
            var reportA = Assert.IsType<SettlementReportDto>(okA.Value);
            Assert.Equal(tenantA, reportA.TenantId);
            Assert.Equal(100, reportA.PointsEarnedAtTenant);
            Assert.Equal(30, reportA.PointsConsumedAtTenant);
            Assert.Equal(30, reportA.PointsConsumedAtOtherTenants);
            Assert.Equal(0, reportA.PointsRedeemedByCustomersAtTenant);
            Assert.Equal(70, reportA.OutstandingPoints);

            // Tenant B: earned 50, own points consumed 50 (at itself), 80 redeemed at B.
            var resultB = await controller.GetSettlement(tenantB);
            var okB = Assert.IsType<OkObjectResult>(resultB);
            var reportB = Assert.IsType<SettlementReportDto>(okB.Value);
            Assert.Equal(50, reportB.PointsEarnedAtTenant);
            Assert.Equal(50, reportB.PointsConsumedAtTenant);
            Assert.Equal(0, reportB.PointsConsumedAtOtherTenants);
            Assert.Equal(80, reportB.PointsRedeemedByCustomersAtTenant);
            Assert.Equal(0, reportB.OutstandingPoints);
        }
        finally
        {
            await sp.DisposeAsync();
        }
    }

    [Fact(DisplayName = "LA-LC-17: GetSettlement — empty tenantId returns 400")]
    public async Task GetSettlement_EmptyTenantId_Returns400()
    {
        var (controller, _, sp) = BuildController();

        try
        {
            var result = await controller.GetSettlement(Guid.Empty);

            var bad = Assert.IsType<BadRequestObjectResult>(result);
            Assert.NotNull(bad.Value);
        }
        finally
        {
            await sp.DisposeAsync();
        }
    }

    [Fact(DisplayName = "LA-LC-18: GetSettlement — no transactions returns zero report")]
    public async Task GetSettlement_NoTransactions_ReturnsZeroReport()
    {
        var (controller, _, sp) = BuildController();

        try
        {
            var result = await controller.GetSettlement(TestTenantGuid);

            var ok = Assert.IsType<OkObjectResult>(result);
            var report = Assert.IsType<SettlementReportDto>(ok.Value);
            Assert.Equal(TestTenantGuid, report.TenantId);
            Assert.Equal(0, report.PointsEarnedAtTenant);
            Assert.Equal(0, report.PointsConsumedAtTenant);
            Assert.Equal(0, report.PointsConsumedAtOtherTenants);
            Assert.Equal(0, report.PointsRedeemedByCustomersAtTenant);
            Assert.Equal(0, report.OutstandingPoints);
        }
        finally
        {
            await sp.DisposeAsync();
        }
    }

    [Fact(DisplayName = "LA-LC-15: UpdateTenantConfig — budget caps persist + response returns caps")]
    public async Task UpdateTenantConfig_BudgetCaps_PersistAndReturn()
    {
        var (controller, db, sp) = BuildController();

        try
        {
            var body = new UpdateTenantConfigRequest
            {
                Mode = LoyaltyMode.Silo,
                IsAllianceMember = false,
                MonthlyPointsBudget = 10000,
                DailyPointsBudget = 500,
                PerCustomerDailyLimit = 100,
                PerOrderRateCap = 0.03m
            };

            var result = await controller.UpdateTenantConfig(TestTenantGuid, body);

            var ok = Assert.IsType<OkObjectResult>(result);
            var dto = Assert.IsType<TenantConfigDto>(ok.Value);
            Assert.Equal(10000, dto.MonthlyPointsBudget);
            Assert.Equal(500, dto.DailyPointsBudget);
            Assert.Equal(100, dto.PerCustomerDailyLimit);
            Assert.Equal(0.03m, dto.PerOrderRateCap);
            Assert.Equal(0, dto.PointsIssuedThisMonth);
            Assert.Equal(0, dto.PointsIssuedToday);

            // Verify row persisted
            var config = await db.LoyaltyTenantConfigs.IgnoreQueryFilters()
                .FirstOrDefaultAsync(c => c.TenantId == new TenantId(TestTenantGuid));
            Assert.NotNull(config);
            Assert.Equal(10000, config!.MonthlyPointsBudget);
            Assert.Equal(0.03m, config.PerOrderRateCap);
        }
        finally
        {
            await sp.DisposeAsync();
        }
    }

    [Fact(DisplayName = "LA-LC-16: UpdateTenantConfig — budget caps update existing row + counters returned")]
    public async Task UpdateTenantConfig_BudgetCaps_UpdateExistingRow()
    {
        var (controller, db, sp) = BuildController();

        try
        {
            // Seed existing config with runtime counters
            var config = new LoyaltyTenantConfig(new TenantId(TestTenantGuid));
            config.SetBudgetCaps(1000, null, null, null, "admin");
            config.IncrementIssuedCounters(300, 50);
            db.LoyaltyTenantConfigs.Add(config);
            await db.SaveChangesAsync();

            var body = new UpdateTenantConfigRequest
            {
                Mode = LoyaltyMode.Alliance,
                IsAllianceMember = true,
                MonthlyPointsBudget = 2000,
                DailyPointsBudget = 600,
                PerCustomerDailyLimit = null, // clear → unlimited
                PerOrderRateCap = null        // clear → no cap
            };

            var result = await controller.UpdateTenantConfig(TestTenantGuid, body);

            var ok = Assert.IsType<OkObjectResult>(result);
            var dto = Assert.IsType<TenantConfigDto>(ok.Value);
            Assert.Equal(2000, dto.MonthlyPointsBudget);
            Assert.Equal(600, dto.DailyPointsBudget);
            Assert.Null(dto.PerCustomerDailyLimit);
            Assert.Null(dto.PerOrderRateCap);
            // Counters survive config update
            Assert.Equal(300, dto.PointsIssuedThisMonth);
            Assert.Equal(50, dto.PointsIssuedToday);

            // Only 1 row (updated, not duplicated)
            var count = await db.LoyaltyTenantConfigs.IgnoreQueryFilters().CountAsync();
            Assert.Equal(1, count);
        }
        finally
        {
            await sp.DisposeAsync();
        }
    }

    [Fact(DisplayName = "LA-LC-17: UpdateTenantConfig — negative budget returns 400")]
    public async Task UpdateTenantConfig_NegativeBudget_Returns400()
    {
        var (controller, _, sp) = BuildController();

        try
        {
            var body = new UpdateTenantConfigRequest
            {
                Mode = LoyaltyMode.Silo,
                MonthlyPointsBudget = -1
            };

            var result = await controller.UpdateTenantConfig(TestTenantGuid, body);

            var bad = Assert.IsType<BadRequestObjectResult>(result);
            Assert.NotNull(bad.Value);
        }
        finally
        {
            await sp.DisposeAsync();
        }
    }

    [Fact(DisplayName = "LA-LC-18: UpdateTenantConfig — PerOrderRateCap > 1 returns 400")]
    public async Task UpdateTenantConfig_RateCapOver100Percent_Returns400()
    {
        var (controller, _, sp) = BuildController();

        try
        {
            var body = new UpdateTenantConfigRequest
            {
                Mode = LoyaltyMode.Silo,
                PerOrderRateCap = 1.5m
            };

            var result = await controller.UpdateTenantConfig(TestTenantGuid, body);

            var bad = Assert.IsType<BadRequestObjectResult>(result);
            Assert.NotNull(bad.Value);
        }
        finally
        {
            await sp.DisposeAsync();
        }
    }

    [Fact(DisplayName = "LA-LC-19: ResetTenantCounters — daily resets PointsIssuedToday, keeps month")]
    public async Task ResetTenantCounters_Daily_ResetsDailyCounter()
    {
        var (controller, db, sp) = BuildController();

        try
        {
            var config = new LoyaltyTenantConfig(new TenantId(TestTenantGuid));
            config.IncrementIssuedCounters(300, 50);
            db.LoyaltyTenantConfigs.Add(config);
            await db.SaveChangesAsync();

            var result = await controller.ResetTenantCounters(TestTenantGuid, new ResetTenantCountersRequest { Scope = "daily" });

            var ok = Assert.IsType<OkObjectResult>(result);
            var dto = Assert.IsType<TenantConfigDto>(ok.Value);
            Assert.Equal(0, dto.PointsIssuedToday);
            Assert.Equal(300, dto.PointsIssuedThisMonth); // monthly untouched
        }
        finally
        {
            await sp.DisposeAsync();
        }
    }

    [Fact(DisplayName = "LA-LC-20: ResetTenantCounters — monthly resets PointsIssuedThisMonth")]
    public async Task ResetTenantCounters_Monthly_ResetsMonthlyCounter()
    {
        var (controller, db, sp) = BuildController();

        try
        {
            var config = new LoyaltyTenantConfig(new TenantId(TestTenantGuid));
            config.IncrementIssuedCounters(300, 50);
            db.LoyaltyTenantConfigs.Add(config);
            await db.SaveChangesAsync();

            var result = await controller.ResetTenantCounters(TestTenantGuid, new ResetTenantCountersRequest { Scope = "monthly" });

            var ok = Assert.IsType<OkObjectResult>(result);
            var dto = Assert.IsType<TenantConfigDto>(ok.Value);
            Assert.Equal(0, dto.PointsIssuedThisMonth);
            Assert.Equal(50, dto.PointsIssuedToday); // daily untouched
        }
        finally
        {
            await sp.DisposeAsync();
        }
    }

    [Fact(DisplayName = "LA-LC-21: ResetTenantCounters — invalid scope returns 400")]
    public async Task ResetTenantCounters_InvalidScope_Returns400()
    {
        var (controller, _, sp) = BuildController();

        try
        {
            var result = await controller.ResetTenantCounters(TestTenantGuid, new ResetTenantCountersRequest { Scope = "yearly" });

            var bad = Assert.IsType<BadRequestObjectResult>(result);
            Assert.NotNull(bad.Value);
        }
        finally
        {
            await sp.DisposeAsync();
        }
    }
}
