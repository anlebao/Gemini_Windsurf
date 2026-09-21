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
using VanAn.CoreHub.Services;
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
        var connection = new SqliteConnection($"DataSource=test_{Guid.NewGuid()};Mode=Memory;Cache=Shared");
        connection.Open();

        var services = new ServiceCollection();
        var efServiceProvider = new ServiceCollection().AddEntityFrameworkSqlite().BuildServiceProvider();
        services.AddDbContext<ShopERPDbContext>(options => options.UseInternalServiceProvider(efServiceProvider).UseSqlite(connection));
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

    // ──────────────────────────────────────────────────────────
    // Loyalty Points Integrity (Batch 1) — extended payload tests
    // ──────────────────────────────────────────────────────────

    /// <summary>
    /// Build an extended payload matching LoyaltyBalanceSyncPublisher shape:
    /// { customerDeviceId?, customerId, tenantId, pointBalance, updatedAt, type, points, reason }.
    /// </summary>
    private static byte[] BuildExtendedPayload(
        Guid customerId, Guid tenantId, int pointBalance, string type, int points, string reason,
        string? updatedAt = null, Guid? deviceId = null)
    {
        return JsonSerializer.SerializeToUtf8Bytes(new
        {
            customerDeviceId = deviceId?.ToString(),
            customerId,
            tenantId,
            pointBalance,
            updatedAt = updatedAt ?? DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ"),
            type,
            points,
            reason
        });
    }

    /// <summary>Seed only a Customer (no LoyaltyRewards row) — for the create-row test.</summary>
    private static async Task SeedCustomerOnlyAsync(ShopERPDbContext db)
    {
        var customer = new Customer(TestTenantId, "Test Customer", "0901234567");
        customer.UpdateCustomerDetails("Test Customer", "0901234567", null, "Bronze", TestDeviceId, true);
        typeof(BaseEntity).GetProperty(nameof(BaseEntity.Id))!.SetValue(customer, TestCustomerId);
        db.Customers.Add(customer);
        await db.SaveChangesAsync();
    }

    [Fact(DisplayName = "LPI-B1-5: extended payload matches by (tenantId, customerId) — no device needed")]
    public async Task SyncLoyaltyBalanceAsync_ExtendedPayload_MatchesByTenantAndCustomer()
    {
        var (subscriber, sp, db) = BuildSubscriber();

        try
        {
            await SeedDataAsync(db, initialBalance: 100);

            // No customerDeviceId in payload — resolution must use (tenantId, customerId)
            byte[] payload = BuildExtendedPayload(
                TestCustomerId, TestTenantGuid, pointBalance: 650, type: "EARN", points: 550,
                reason: "Hoàn tiền từ chiến dịch X - Đơn hàng #123");

            await subscriber.SyncLoyaltyBalanceAsync(payload, CancellationToken.None);

            using IServiceScope verifyScope = sp.CreateScope();
            var verifyDb = verifyScope.ServiceProvider.GetRequiredService<ShopERPDbContext>();
            var rewards = await verifyDb.LoyaltyRewards.FirstOrDefaultAsync(r => r.CustomerId == TestCustomerId);
            Assert.NotNull(rewards);
            Assert.Equal(650, rewards!.PointBalance); // PG authority — mirror overwritten to PG balance

            var history = JsonSerializer.Deserialize<List<LoyaltyHistoryEntry>>(rewards.History);
            Assert.NotNull(history);
            Assert.Single(history!);
            Assert.Equal("EARN", history![0].Type);
            Assert.Equal(550, history[0].Points);
            Assert.Contains("#123", history[0].Reason);
        }
        finally
        {
            await sp.DisposeAsync();
        }
    }

    [Fact(DisplayName = "LPI-B1-6: extended payload creates LoyaltyRewards row when customer exists without one")]
    public async Task SyncLoyaltyBalanceAsync_ExtendedPayload_CreatesMissingRow()
    {
        var (subscriber, sp, db) = BuildSubscriber();

        try
        {
            await SeedCustomerOnlyAsync(db);

            byte[] payload = BuildExtendedPayload(
                TestCustomerId, TestTenantGuid, pointBalance: 250, type: "EARN", points: 250,
                reason: "Hoàn tiền từ chiến dịch Y - Đơn hàng #456");

            await subscriber.SyncLoyaltyBalanceAsync(payload, CancellationToken.None);

            using IServiceScope verifyScope = sp.CreateScope();
            var verifyDb = verifyScope.ServiceProvider.GetRequiredService<ShopERPDbContext>();
            var rewards = await verifyDb.LoyaltyRewards.FirstOrDefaultAsync(r => r.CustomerId == TestCustomerId);
            Assert.NotNull(rewards);
            Assert.Equal(250, rewards!.PointBalance);
        }
        finally
        {
            await sp.DisposeAsync();
        }
    }

    [Fact(DisplayName = "LPI-B1-6b: extended payload creates customer stub + rewards row when customer missing locally")]
    public async Task SyncLoyaltyBalanceAsync_ExtendedPayload_CreatesCustomerStubAndRow()
    {
        var (subscriber, sp, db) = BuildSubscriber();

        try
        {
            // NO seed — customer does not exist locally (guest order completed on Gateway:
            // the stub customer was created in PG, never synced to this ShopERP SQLite).
            byte[] payload = BuildExtendedPayload(
                TestCustomerId, TestTenantGuid, pointBalance: 180, type: "EARN", points: 180,
                reason: "Hoàn tiền từ chiến dịch G - Đơn hàng #999", deviceId: TestDeviceId);

            await subscriber.SyncLoyaltyBalanceAsync(payload, CancellationToken.None);

            using IServiceScope verifyScope = sp.CreateScope();
            var verifyDb = verifyScope.ServiceProvider.GetRequiredService<ShopERPDbContext>();
            var customer = await verifyDb.Customers.IgnoreQueryFilters().FirstOrDefaultAsync(c => c.Id == TestCustomerId);
            Assert.NotNull(customer); // stub created
            Assert.Equal(TestDeviceId, customer!.DeviceId);

            var rewards = await verifyDb.LoyaltyRewards.IgnoreQueryFilters().FirstOrDefaultAsync(r => r.CustomerId == TestCustomerId);
            Assert.NotNull(rewards);
            Assert.Equal(180, rewards!.PointBalance);
        }
        finally
        {
            await sp.DisposeAsync();
        }
    }

    [Fact(DisplayName = "LPI-B1-7 (BUG-1b): PG authority — mirror OVERWRITES local balance even when PG is lower (spend sync)")]
    public async Task SyncLoyaltyBalanceAsync_OverwritesLocalBalance_PgAuthority()
    {
        var (subscriber, sp, db) = BuildSubscriber();

        try
        {
            // Local SQLite has 400 (stale — pre-cutover balance); PG is the authority and says 300
            // (e.g. after a spend at the Gateway ledger). The mirror MUST converge to PG: keeping
            // MAX-merge would freeze the mirror at 400 forever (upward drift on every spend).
            await SeedDataAsync(db, initialBalance: 400);

            byte[] payload = BuildExtendedPayload(
                TestCustomerId, TestTenantGuid, pointBalance: 300, type: "SPEND", points: -100,
                reason: "Đổi điểm - Đơn hàng #789");

            await subscriber.SyncLoyaltyBalanceAsync(payload, CancellationToken.None);

            using IServiceScope verifyScope = sp.CreateScope();
            var verifyDb = verifyScope.ServiceProvider.GetRequiredService<ShopERPDbContext>();
            var rewards = await verifyDb.LoyaltyRewards.FirstOrDefaultAsync(r => r.CustomerId == TestCustomerId);
            Assert.NotNull(rewards);
            Assert.Equal(300, rewards!.PointBalance); // PG wins — mirror overwrites (Batch 2+ authority)
        }
        finally
        {
            await sp.DisposeAsync();
        }
    }

    [Fact(DisplayName = "LPI-B1-9 (ordering guard): stale out-of-order event does NOT clobber the newer balance")]
    public async Task SyncLoyaltyBalanceAsync_StaleOutOfOrderEvent_DoesNotClobberBalance()
    {
        var (subscriber, sp, db) = BuildSubscriber();

        try
        {
            await SeedDataAsync(db, initialBalance: 100);

            string later = "2026-09-21T10:00:05.0000000Z";   // spend processed FIRST
            string earlier = "2026-09-21T10:00:01.0000000Z"; // earn happened EARLIER, arrives late

            // 1) Spend event (newer, balance 70) — delivered first.
            await subscriber.SyncLoyaltyBalanceAsync(
                BuildExtendedPayload(TestCustomerId, TestTenantGuid, pointBalance: 70, type: "REDEEM", points: -30,
                    reason: "Đổi điểm", updatedAt: later, deviceId: TestDeviceId), CancellationToken.None);

            // 2) Stale EARN event (older timestamp, balance 100) — must NOT re-clobber back to 100.
            await subscriber.SyncLoyaltyBalanceAsync(
                BuildExtendedPayload(TestCustomerId, TestTenantGuid, pointBalance: 100, type: "EARN", points: 30,
                    reason: "Đơn hàng cũ", updatedAt: earlier, deviceId: TestDeviceId), CancellationToken.None);

            using IServiceScope verifyScope = sp.CreateScope();
            var verifyDb = verifyScope.ServiceProvider.GetRequiredService<ShopERPDbContext>();
            var rewards = await verifyDb.LoyaltyRewards.FirstOrDefaultAsync(r => r.CustomerId == TestCustomerId);
            Assert.NotNull(rewards);
            Assert.True(rewards!.PointBalance == 70, "stale event must not clobber the newer authoritative balance");

            var history = JsonSerializer.Deserialize<List<LoyaltyHistoryEntry>>(rewards.History);
            Assert.NotNull(history);
            Assert.Equal(2, history!.Count); // both events recorded (audit) — balance guarded separately
        }
        finally
        {
            await sp.DisposeAsync();
        }
    }

    [Fact(DisplayName = "LPI-B1-8: duplicate event is skipped (no duplicate history entry, no balance churn)")]
    public async Task SyncLoyaltyBalanceAsync_DuplicateEvent_Skipped()
    {
        var (subscriber, sp, db) = BuildSubscriber();

        try
        {
            await SeedDataAsync(db, initialBalance: 100);

            string fixedTs = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ");
            byte[] payload = BuildExtendedPayload(
                TestCustomerId, TestTenantGuid, pointBalance: 300, type: "EARN", points: 200,
                reason: "Hoàn tiền từ chiến dịch K - Đơn hàng #111", updatedAt: fixedTs);

            await subscriber.SyncLoyaltyBalanceAsync(payload, CancellationToken.None);
            await subscriber.SyncLoyaltyBalanceAsync(payload, CancellationToken.None); // duplicate replay

            using IServiceScope verifyScope = sp.CreateScope();
            var verifyDb = verifyScope.ServiceProvider.GetRequiredService<ShopERPDbContext>();
            var rewards = await verifyDb.LoyaltyRewards.FirstOrDefaultAsync(r => r.CustomerId == TestCustomerId);
            Assert.NotNull(rewards);
            Assert.Equal(300, rewards!.PointBalance);

            var history = JsonSerializer.Deserialize<List<LoyaltyHistoryEntry>>(rewards.History);
            Assert.NotNull(history);
            Assert.Single(history!); // only one entry despite replay
        }
        finally
        {
            await sp.DisposeAsync();
        }
    }

    // NOTE (Batch 2): double-delivery convergence (extended payload replayed) is already covered by
    // LPI-B1-8 (SyncLoyaltyBalanceAsync_DuplicateEvent_Skipped) — 1 history entry, balance 300.
    // The concurrent UNIQUE-race is handled defensively in the subscriber (retry-once with fresh
    // context) and verified on production by the mirror sync.

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
            : base(serviceProvider, configuration, logger, CreateToggleMock()) { }

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

        /// <summary>REQ-1.2: Creates a toggle mock that returns true (enabled) for all services.</summary>
        private static IBackgroundServiceToggleService CreateToggleMock()
        {
            var mock = new Mock<IBackgroundServiceToggleService>();
            mock.Setup(t => t.IsEnabledAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);
            return mock.Object;
        }
    }
}
