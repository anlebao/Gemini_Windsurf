using System.Reflection;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using VanAn.ShopERP.Infrastructure;
using VanAn.ShopERP.Services;
using DemoUser = VanAn.Shared.Domain.Aggregates.UserAggregate.DemoUser;
using UserTrustLevel = VanAn.Shared.Domain.Aggregates.UserAggregate.UserTrustLevel;

namespace VanAn.ShopERP.Tests.Services;

/// <summary>
/// W1 (2026-11-10): UserSyncSubscriber — payload TrustLevel sync (ID-P1).
/// Invoke private SyncUserUpsertedAsync qua reflection (subscriber là BackgroundService NATS).
/// </summary>
public class UserSyncSubscriberTests
{
    private static (UserSyncSubscriber Sub, ShopERPDbContext Ctx, SqliteConnection Conn) Create()
    {
        var conn = new SqliteConnection("Data Source=:memory:");
        conn.Open();
        var options = new DbContextOptionsBuilder<ShopERPDbContext>().UseSqlite(conn).Options;

        var services = new ServiceCollection();
        services.AddScoped(_ => new ShopERPDbContext(options));
        var sp = services.BuildServiceProvider();

        var config = new ConfigurationBuilder().Build();
        var sub = new UserSyncSubscriber(sp, config, NullLogger<UserSyncSubscriber>.Instance);
        var ctx = new ShopERPDbContext(options);
        ctx.Database.EnsureCreated();
        return (sub, ctx, conn);
    }

    private static byte[] Payload(Guid userId, Guid tenantId, int? trustLevel = null) => Encoding.UTF8.GetBytes(
        JsonSerializer.Serialize(new Dictionary<string, object?>
        {
            ["UserId"] = userId,
            ["TenantId"] = tenantId,
            ["Username"] = "owner@sync.vn",
            ["PasswordHash"] = "bcrypt-hash",
            ["DisplayName"] = "Owner Sync",
            ["Role"] = 1,          // Owner
            ["IsActive"] = true,
            ["TrustLevel"] = trustLevel
        }));

    private static Task InvokeSyncAsync(UserSyncSubscriber sub, byte[] data)
    {
        var method = typeof(UserSyncSubscriber).GetMethod(
            "SyncUserUpsertedAsync",
            BindingFlags.NonPublic | BindingFlags.Instance)!;
        return (Task)method.Invoke(sub, new object[] { data, CancellationToken.None })!;
    }

    [Fact]
    public async Task Sync_WithTrustLevel_SetsBusinessVerified()
    {
        var (sub, ctx, _) = Create();
        var userId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();

        await InvokeSyncAsync(sub, Payload(userId, tenantId, (int)UserTrustLevel.BusinessVerified));

        var user = await ctx.Users.IgnoreQueryFilters().FirstAsync(u => u.Id == userId);
        user.TrustLevel.Should().Be(UserTrustLevel.BusinessVerified);
        user.PhoneNumber.Should().BeNull(); // KHÔNG sync PII
    }

    [Fact]
    public async Task Sync_WithoutTrustLevel_DefaultsNone_NoCrash()
    {
        var (sub, ctx, _) = Create();
        var userId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();

        await InvokeSyncAsync(sub, Payload(userId, tenantId, trustLevel: null));

        var user = await ctx.Users.IgnoreQueryFilters().FirstAsync(u => u.Id == userId);
        user.TrustLevel.Should().Be(UserTrustLevel.None);
    }

    [Fact]
    public async Task Sync_Redelivery_Idempotent()
    {
        var (sub, ctx, _) = Create();
        var userId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var payload = Payload(userId, tenantId, (int)UserTrustLevel.ContactVerified);

        await InvokeSyncAsync(sub, payload);
        await InvokeSyncAsync(sub, payload);

        var count = await ctx.Users.IgnoreQueryFilters().CountAsync(u => u.Id == userId);
        count.Should().Be(1);
        var user = await ctx.Users.IgnoreQueryFilters().FirstAsync(u => u.Id == userId);
        user.TrustLevel.Should().Be(UserTrustLevel.ContactVerified);
    }
}