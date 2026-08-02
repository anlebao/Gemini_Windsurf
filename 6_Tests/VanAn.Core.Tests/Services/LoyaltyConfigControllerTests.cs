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
}
