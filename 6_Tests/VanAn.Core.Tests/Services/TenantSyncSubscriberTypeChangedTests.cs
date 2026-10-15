using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using System.Text;
using System.Text.Json;
using VanAn.Shared.Domain;
using VanAn.ShopERP.Infrastructure;
using VanAn.ShopERP.Services;
using Xunit;
using Tenant = VanAn.Shared.Domain.Aggregates.TenantAggregate.Tenant;

namespace VanAn.Core.Tests.Services;

/// <summary>
/// 2026-10-15 (fix báo cáo HTX): TenantSyncSubscriber xử lý "vanan.cloud.tenant.type.changed"
/// (HtxProfileService MarkAsHtx / RevertAsync chỉ ghi PG) → cập nhật SQLite mirror
/// Type/AccountingStandard cho khớp PG. Không cần NATS thật — gọi thẳng handler internal.
/// </summary>
public class TenantSyncSubscriberTypeChangedTests
{
    private static readonly Guid TestTenantGuid = Guid.Parse("00000000-0000-0000-0000-000000000001");
    private static readonly TenantId TestTenantId = new(TestTenantGuid);

    private static (TenantSyncSubscriber subscriber, ServiceProvider sp, ShopERPDbContext db)
        BuildSubscriber()
    {
        var connection = new SqliteConnection($"DataSource=test_{Guid.NewGuid()};Mode=Memory;Cache=Shared");
        connection.Open();

        var services = new ServiceCollection();
        var efServiceProvider = new ServiceCollection().AddEntityFrameworkSqlite().BuildServiceProvider();
        services.AddDbContext<ShopERPDbContext>(options => options.UseInternalServiceProvider(efServiceProvider).UseSqlite(connection));
        services.AddLogging(b => b.AddConsole().SetMinimumLevel(LogLevel.Warning));

        ServiceProvider sp = services.BuildServiceProvider();
        ShopERPDbContext db = sp.GetRequiredService<ShopERPDbContext>();
        _ = db.Database.EnsureCreated();

        var config = new ConfigurationBuilder().Build();
        var subscriber = new TenantSyncSubscriber(sp, config, NullLogger<TenantSyncSubscriber>.Instance);

        return (subscriber, sp, db);
    }

    /// <summary>Seed mirror tenant row (giống seed ShopERP: TenantId = own Id cho query filter).</summary>
    private static async Task SeedMirrorTenantAsync(ShopERPDbContext db, TenantType type)
    {
        var tenant = type == TenantType.HTX
            ? Tenant.CreateCompany(TestTenantId, "Mirror Tenant")
            : Tenant.CreateHouseholdBusiness(TestTenantId, "Mirror Tenant", HKDGroup.Group1);
        if (type == TenantType.HTX)
        {
            tenant.MarkAsHtx();
        }
        db.Tenants.Add(tenant);
        db.Entry(tenant).Property("TenantId").CurrentValue = TestTenantId;
        await db.SaveChangesAsync();
    }

    private static byte[] BuildPayload(TenantType? type, AccountingStandard? standard)
    {
        var payload = new
        {
            TenantId = TestTenantGuid,
            Type = type.HasValue ? (int)type.Value : (int?)null,
            AccountingStandard = standard.HasValue ? (int)standard.Value : (int?)null,
            UpdatedAt = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffffffZ")
        };
        return Encoding.UTF8.GetBytes(JsonSerializer.Serialize(payload));
    }

    [Fact(DisplayName = "TSC-1: type.changed (HTX) → SQLite mirror Type=HTX + TT71")]
    public async Task TypeChanged_HtxEvent_UpdatesMirrorToHtx()
    {
        (TenantSyncSubscriber subscriber, ServiceProvider sp, ShopERPDbContext db) = BuildSubscriber();
        await SeedMirrorTenantAsync(db, TenantType.HKD); // mirror đang HKD (stale)

        await subscriber.SyncTenantTypeChangedAsync(BuildPayload(TenantType.HTX, AccountingStandard.TT71_2024), CancellationToken.None);

        using IServiceScope verifyScope = sp.CreateScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<ShopERPDbContext>();
        var reloaded = await verifyDb.Tenants.IgnoreQueryFilters().SingleAsync(t => t.Id == TestTenantId);
        Assert.Equal(TenantType.HTX, reloaded.Type);
        Assert.Equal(AccountingStandard.TT71_2024, reloaded.AccountingStandard);
    }

    [Fact(DisplayName = "TSC-2: type.changed (revert HKD) → mirror Type=HKD + standard null")]
    public async Task TypeChanged_RevertEvent_UpdatesMirrorBackToHkd()
    {
        (TenantSyncSubscriber subscriber, ServiceProvider sp, ShopERPDbContext db) = BuildSubscriber();
        await SeedMirrorTenantAsync(db, TenantType.HTX); // mirror đang HTX

        await subscriber.SyncTenantTypeChangedAsync(BuildPayload(TenantType.HKD, null), CancellationToken.None);

        using IServiceScope verifyScope = sp.CreateScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<ShopERPDbContext>();
        var reloaded = await verifyDb.Tenants.IgnoreQueryFilters().SingleAsync(t => t.Id == TestTenantId);
        Assert.Equal(TenantType.HKD, reloaded.Type);
        Assert.Null(reloaded.AccountingStandard);
    }

    [Fact(DisplayName = "TSC-3: type.changed cho tenant không tồn tại trong mirror → skip an toàn")]
    public async Task TypeChanged_UnknownTenant_Skips()
    {
        (TenantSyncSubscriber subscriber, ServiceProvider sp, ShopERPDbContext db) = BuildSubscriber();
        // Không seed tenant nào

        await subscriber.SyncTenantTypeChangedAsync(BuildPayload(TenantType.HTX, AccountingStandard.TT71_2024), CancellationToken.None);

        using IServiceScope verifyScope = sp.CreateScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<ShopERPDbContext>();
        Assert.Equal(0, await verifyDb.Tenants.IgnoreQueryFilters().CountAsync());
    }

    [Fact(DisplayName = "TSC-4: type.changed payload lặp lại (re-delivery) → idempotent")]
    public async Task TypeChanged_DuplicateEvent_Idempotent()
    {
        (TenantSyncSubscriber subscriber, ServiceProvider sp, ShopERPDbContext db) = BuildSubscriber();
        await SeedMirrorTenantAsync(db, TenantType.HKD);

        byte[] payload = BuildPayload(TenantType.HTX, AccountingStandard.TT71_2024);
        await subscriber.SyncTenantTypeChangedAsync(payload, CancellationToken.None);
        await subscriber.SyncTenantTypeChangedAsync(payload, CancellationToken.None); // replay

        using IServiceScope verifyScope = sp.CreateScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<ShopERPDbContext>();
        var reloaded = await verifyDb.Tenants.IgnoreQueryFilters().SingleAsync(t => t.Id == TestTenantId);
        Assert.Equal(TenantType.HTX, reloaded.Type);
        Assert.Equal(AccountingStandard.TT71_2024, reloaded.AccountingStandard);
    }
}
