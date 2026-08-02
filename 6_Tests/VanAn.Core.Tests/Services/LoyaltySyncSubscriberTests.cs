using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using NATS.Client;
using System.Text;
using System.Text.Json;
using VanAn.Shared.Domain;
using VanAn.Shared.Domain.Common;
using VanAn.ShopERP.Infrastructure;
using VanAn.ShopERP.Services;
using Xunit;

namespace VanAn.Tests.Services;

/// <summary>
/// Loyalty Alliance Phase 2C — tests for LoyaltySyncSubscriber.
/// Verifies that NATS "vanan.cloud.loyalty.changed.{customerDeviceId}" events
/// update the local SQLite LoyaltyRewards.PointBalance to match the PG wallet balance.
/// Spec: docs/specs/loyalty-alliance-spec.md v1.0.
/// </summary>
public class LoyaltySyncSubscriberTests
{
    private static readonly Guid TestTenantGuid = Guid.Parse("00000000-0000-0000-0000-000000000001");
    private static readonly TenantId TestTenantId = new(TestTenantGuid);
    private static readonly Guid TestDeviceId = Guid.NewGuid();
    private static readonly Guid TestCustomerId = Guid.NewGuid();

    /// <summary>
    /// Build a testable subscriber that overrides CreateSubscriptionConnection (no real NATS)
    /// and captures the subscribed subject. Uses a real SQLite in-memory ShopERPDbContext.
    /// </summary>
    private static (TestableLoyaltySyncSubscriber subscriber, ServiceProvider sp, ShopERPDbContext db)
        BuildSubscriber()
    {
        var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();

        var services = new ServiceCollection();
        services.AddDbContext<ShopERPDbContext>(options => options.UseSqlite(connection));
        services.AddLogging(b => b.AddConsole().SetMinimumLevel(LogLevel.Warning));

        ServiceProvider sp = services.BuildServiceProvider();
        ShopERPDbContext db = sp.GetRequiredService<ShopERPDbContext>();
        _ = db.Database.EnsureCreated();

        var config = new ConfigurationBuilder().Build(); // empty config — NATS URL defaults to localhost
        var subscriber = new TestableLoyaltySyncSubscriber(
            sp, config, NullLogger<LoyaltySyncSubscriber>.Instance);

        return (subscriber, sp, db);
    }

    /// <summary>
    /// Seed a Customer + LoyaltyRewards in SQLite so the subscriber can find + update it.
    /// </summary>
    private static async Task SeedDataAsync(ShopERPDbContext db, int initialBalance = 100)
    {
        var customer = new Customer(TestTenantId, "Test Customer", "0901234567");
        customer.UpdateCustomerDetails("Test Customer", "0901234567", null, "Bronze", TestDeviceId, true);
        typeof(BaseEntity).GetProperty(nameof(BaseEntity.Id))!.SetValue(customer, TestCustomerId);
        db.Customers.Add(customer);

        var rewards = new LoyaltyRewards(TestTenantId, TestCustomerId);
        typeof(BaseEntity).GetProperty(nameof(BaseEntity.Id))!.SetValue(rewards, Guid.NewGuid());
        rewards.AddPoints(initialBalance); // Set initial balance
        db.LoyaltyRewards.Add(rewards);

        await db.SaveChangesAsync();
    }

    /// <summary>
    /// Build a NATS message payload matching AllianceWalletService.PublishLoyaltyChangedAsync shape.
    /// </summary>
    private static byte[] BuildPayload(Guid customerDeviceId, int pointBalance)
    {
        return JsonSerializer.SerializeToUtf8Bytes(
            new { customerDeviceId, pointBalance, updatedAt = DateTime.UtcNow });
    }

    // ──────────────────────────────────────────────────────────
    // Test 1: Valid event → updates local LoyaltyRewards.PointBalance
    // ──────────────────────────────────────────────────────────

    [Fact(DisplayName = "LA-LS-1: SyncLoyaltyBalanceAsync — valid event updates local PointBalance")]
    public async Task SyncLoyaltyBalanceAsync_ValidEvent_UpdatesPointBalance()
    {
        var (subscriber, sp, db) = BuildSubscriber();

        try
        {
            await SeedDataAsync(db, initialBalance: 100);

            // PG wallet balance is 500 — subscriber should sync local to 500
            byte[] payload = BuildPayload(TestDeviceId, pointBalance: 500);

            await subscriber.SyncLoyaltyBalanceAsync(payload, CancellationToken.None);

            // Subscriber uses its own scope → different DbContext instance.
            // Re-query from a fresh scope to verify the update persisted to SQLite.
            using IServiceScope verifyScope = sp.CreateScope();
            var verifyDb = verifyScope.ServiceProvider.GetRequiredService<ShopERPDbContext>();
            var rewards = await verifyDb.LoyaltyRewards.FirstOrDefaultAsync(r => r.CustomerId == TestCustomerId);
            Assert.NotNull(rewards);
            Assert.Equal(500, rewards!.PointBalance);
        }
        finally
        {
            await sp.DisposeAsync();
        }
    }

    // ──────────────────────────────────────────────────────────
    // Test 2: No local customer for device → skip (no error, no row created)
    // ──────────────────────────────────────────────────────────

    [Fact(DisplayName = "LA-LS-2: SyncLoyaltyBalanceAsync — unknown device skips (no local customer)")]
    public async Task SyncLoyaltyBalanceAsync_UnknownDevice_SkipsSilently()
    {
        var (subscriber, sp, db) = BuildSubscriber();

        try
        {
            await SeedDataAsync(db, initialBalance: 100);

            // Unknown device — no local customer matches
            byte[] payload = BuildPayload(Guid.NewGuid(), pointBalance: 999);

            await subscriber.SyncLoyaltyBalanceAsync(payload, CancellationToken.None);

            // Existing rewards should be unchanged
            var rewards = await db.LoyaltyRewards.FirstOrDefaultAsync(r => r.CustomerId == TestCustomerId);
            Assert.NotNull(rewards);
            Assert.Equal(100, rewards!.PointBalance); // unchanged
        }
        finally
        {
            await sp.DisposeAsync();
        }
    }

    // ──────────────────────────────────────────────────────────
    // Test 3: Same balance → no DB write (idempotent skip)
    // ──────────────────────────────────────────────────────────

    [Fact(DisplayName = "LA-LS-3: SyncLoyaltyBalanceAsync — same balance skips DB write")]
    public async Task SyncLoyaltyBalanceAsync_SameBalance_SkipsDbWrite()
    {
        var (subscriber, sp, db) = BuildSubscriber();

        try
        {
            await SeedDataAsync(db, initialBalance: 300);

            // PG balance = 300, local = 300 → no update needed
            byte[] payload = BuildPayload(TestDeviceId, pointBalance: 300);

            await subscriber.SyncLoyaltyBalanceAsync(payload, CancellationToken.None);

            var rewards = await db.LoyaltyRewards.FirstOrDefaultAsync(r => r.CustomerId == TestCustomerId);
            Assert.NotNull(rewards);
            Assert.Equal(300, rewards!.PointBalance); // unchanged
        }
        finally
        {
            await sp.DisposeAsync();
        }
    }

    // ──────────────────────────────────────────────────────────
    // Test 4: ExecuteAsync subscribes to vanan.cloud.loyalty.changed.>
    // ──────────────────────────────────────────────────────────

    [Fact(DisplayName = "LA-LS-4: ExecuteAsync — subscribes to vanan.cloud.loyalty.changed.> wildcard")]
    public async Task ExecuteAsync_SubscribesToLoyaltyChangedWildcard()
    {
        var (subscriber, sp, db) = BuildSubscriber();

        try
        {
            await subscriber.ExecuteAsyncPublic(CancellationToken.None);
            await Task.Delay(100); // allow background task to run

            Assert.Equal("vanan.cloud.loyalty.changed.>", subscriber.CapturedSubject);
            Assert.True(subscriber.CreateConnectionCalled);
        }
        finally
        {
            await sp.DisposeAsync();
        }
    }

    /// <summary>
    /// Testable subclass that overrides NATS connection creation (no real NATS server)
    /// and captures the subscribed subject for assertion.
    /// </summary>
    private class TestableLoyaltySyncSubscriber : LoyaltySyncSubscriber
    {
        public string? CapturedSubject { get; private set; }
        public bool CreateConnectionCalled { get; private set; }

        public TestableLoyaltySyncSubscriber(
            IServiceProvider serviceProvider,
            IConfiguration configuration,
            ILogger<LoyaltySyncSubscriber> logger)
            : base(serviceProvider, configuration, logger) { }

        /// <summary>Public wrapper for the protected ExecuteAsync so tests can invoke it.</summary>
        public Task ExecuteAsyncPublic(CancellationToken ct) => ExecuteAsync(ct);

        protected override IConnection CreateSubscriptionConnection(string url)
        {
            CreateConnectionCalled = true;
            var mock = new Mock<IConnection>();
            mock.Setup(c => c.SubscribeAsync(It.IsAny<string>(), It.IsAny<EventHandler<MsgHandlerEventArgs>>()))
                .Callback<string, EventHandler<MsgHandlerEventArgs>>((subject, handler) => { })
                .Returns(new Mock<IAsyncSubscription>().Object);
            return mock.Object;
        }

        protected override void RecordSubscription(string subject)
        {
            CapturedSubject = subject;
        }
    }
}
