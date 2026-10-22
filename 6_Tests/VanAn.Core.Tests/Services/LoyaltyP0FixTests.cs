using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using VanAn.CoreHub.Domain.Repositories;
using VanAn.CoreHub.Infrastructure;
using VanAn.CoreHub.Infrastructure.Messaging;
using VanAn.CoreHub.Infrastructure.Repositories;
using VanAn.CoreHub.Repositories;
using VanAn.CoreHub.Services;
using VanAn.CoreHub.Tests.TestInfrastructure;
using VanAn.Shared.Domain;
using VanAn.Shared.Domain.Common;
using VanAn.Shared.Services;
using Xunit;

namespace VanAn.Tests.Services;

/// <summary>
/// Loyalty P0 fixes — audit defects R1 / R2 / R5 (docs/AI/tasks/loyalty_alliance_p0/).
///
/// R1 (P0.1): AllianceWalletService serializes writes per wallet (PG advisory lock +
/// re-check idempotency under lock + unique filtered index on IdempotencyKey).
/// SQLite tests verify the behavior contract (dedup, balance integrity); the advisory
/// lock itself is a no-op on SQLite (single-writer DB serializes anyway).
///
/// R2 (P0.2): RedemptionService compensates a successful PG deduction when the local
/// voucher persist fails — idempotent "compensate:{voucherCode}" — and persists a
/// LoyaltyReconcileCase when even the compensation fails (double failure).
///
/// R5 (P0.3): OrderWorkflowService awards points only at "completed" (terminal in the
/// built-in flow). ValcnV2_RefundReversal is default-ON as the safety net for custom
/// flows that allow completed→cancelled.
/// </summary>
public class LoyaltyP0FixTests
{
    private static readonly Guid TenantA = Guid.Parse("00000000-0000-0000-0000-00000000000a");

    // ═══════════════════════════════════════════════════════════
    // P0.1 — AllianceWalletService (real service, SQLite in-memory)
    // ═══════════════════════════════════════════════════════════

    // Two DbContexts sharing one in-memory SQLite connection — simulates two
    // concurrent writers. SQLite serializes writes at the DB level (the PG advisory
    // lock is a no-op on SQLite, which is exactly the documented test fallback).
    private sealed class WalletPair : IAsyncDisposable
    {
        public required SqliteConnection Connection { get; init; }
        public required VanAnDbContext Db1 { get; init; }
        public required VanAnDbContext Db2 { get; init; }
        public required AllianceWalletService Svc1 { get; init; }
        public required AllianceWalletService Svc2 { get; init; }

        public async ValueTask DisposeAsync()
        {
            await Db1.DisposeAsync();
            await Db2.DisposeAsync();
            await Connection.DisposeAsync();
        }
    }

    private static AllianceWalletService BuildWalletService(VanAnDbContext db)
    {
        var modeResolverMock = new Mock<ILoyaltyModeResolver>();
        modeResolverMock
            .Setup(m => m.GetEffectiveMaxWalletPointsAsync(It.IsAny<Guid>()))
            .ReturnsAsync(int.MaxValue);
        return new AllianceWalletService(
            db,
            modeResolverMock.Object,
            natsEventPublisher: null,
            logger: NullLogger<AllianceWalletService>.Instance);
    }

    private static async Task<WalletPair> BuildWalletPairAsync()
    {
        var conn = new SqliteConnection($"DataSource=test_{Guid.NewGuid()};Mode=Memory;Cache=Shared");
        await conn.OpenAsync();
        var options = new DbContextOptionsBuilder<VanAnDbContext>().UseSqlite(conn).Options;
        var db1 = new VanAnDbContext(options, new TestTenantProvider());
        var db2 = new VanAnDbContext(options, new TestTenantProvider());
        _ = await db1.Database.EnsureCreatedAsync();
        return new WalletPair
        {
            Connection = conn,
            Db1 = db1,
            Db2 = db2,
            Svc1 = BuildWalletService(db1),
            Svc2 = BuildWalletService(db2),
        };
    }

    [Fact(DisplayName = "P0.1-1: AddPointsAsync same IdempotencyKey twice → single EARN row (dedup)")]
    public async Task AddPoints_SameKey_Twice_SingleEarn()
    {
        await using var pair = await BuildWalletPairAsync();
        var svc = pair.Svc1;
        var deviceId = Guid.NewGuid();

        var r1 = await svc.AddPointsAsync(deviceId, TenantA, 100, "Order #1", idempotencyKey: "earn:o1");
        var r2 = await svc.AddPointsAsync(deviceId, TenantA, 100, "Order #1", idempotencyKey: "earn:o1");

        Assert.True(r1.Success);
        Assert.True(r2.Success, "retry with same key must return the cached result");
        Assert.Equal(100, r2.NewBalance);

        var txs = await pair.Db1.AllianceTransactions.Where(t => t.IdempotencyKey == "earn:o1").ToListAsync();
        Assert.Single(txs);
        var wallet = await svc.GetWalletByDeviceIdAsync(deviceId);
        Assert.Equal(100, wallet!.TotalPointBalance);
    }

    [Fact(DisplayName = "P0.1-2: DeductPointsAsync same IdempotencyKey twice → single REDEEM group")]
    public async Task Deduct_SameKey_Twice_SingleRedeem()
    {
        await using var pair = await BuildWalletPairAsync();
        var svc = pair.Svc1;
        var deviceId = Guid.NewGuid();
        _ = await svc.AddPointsAsync(deviceId, TenantA, 200, "Order #1");

        var r1 = await svc.DeductPointsAsync(deviceId, TenantA, 60, "Redeem", "VAN-AAA", idempotencyKey: "redeem:VAN-AAA");
        var r2 = await svc.DeductPointsAsync(deviceId, TenantA, 60, "Redeem", "VAN-AAA", idempotencyKey: "redeem:VAN-AAA");

        Assert.True(r1.Success);
        Assert.True(r2.Success);
        Assert.Equal(140, r2.NewBalance);

        var redeems = await pair.Db1.AllianceTransactions
            .Where(t => t.IdempotencyKey == "redeem:VAN-AAA")
            .ToListAsync();
        Assert.Single(redeems);
        Assert.Equal(140, (await svc.GetWalletByDeviceIdAsync(deviceId))!.TotalPointBalance);
    }

    [Fact(DisplayName = "P0.1-3: Deduct more than balance → rejected, no ledger row written")]
    public async Task Deduct_Insufficient_Rejected_NoRow()
    {
        await using var pair = await BuildWalletPairAsync();
        var svc = pair.Svc1;
        var deviceId = Guid.NewGuid();
        _ = await svc.AddPointsAsync(deviceId, TenantA, 50, "Order #1");

        var r = await svc.DeductPointsAsync(deviceId, TenantA, 80, "Redeem", "VAN-BBB");

        Assert.False(r.Success);
        Assert.Equal("Insufficient balance", r.Error);
        var redeemCount = await pair.Db1.AllianceTransactions
            .CountAsync(t => t.Type == AllianceTransactionType.REDEEM);
        Assert.Equal(0, redeemCount);
        Assert.Equal(50, (await svc.GetWalletByDeviceIdAsync(deviceId))!.TotalPointBalance);
    }

    [Fact(DisplayName = "P0.1-4: Concurrent deducts of 60 on balance 100 → at most one wins, balance never negative")]
    public async Task Deduct_Concurrent_NoDoubleSpend()
    {
        await using var pair = await BuildWalletPairAsync();
        var deviceId = Guid.NewGuid();
        _ = await pair.Svc1.AddPointsAsync(deviceId, TenantA, 100, "seed");

        var t1 = pair.Svc1.DeductPointsAsync(deviceId, TenantA, 60, "Redeem A", "VAN-C1");
        var t2 = pair.Svc2.DeductPointsAsync(deviceId, TenantA, 60, "Redeem B", "VAN-C2");
        var results = await Task.WhenAll(t1, t2);

        int successes = results.Count(r => r.Success);
        var wallet = await pair.Db1.AllianceWallets
            .AsNoTracking()
            .FirstOrDefaultAsync(w => w.CustomerDeviceId == deviceId);

        Assert.True(successes <= 1, "Two 60-point deducts on balance 100 must not both succeed");
        Assert.True(wallet!.TotalPointBalance >= 0, "Balance must never go negative");
        Assert.Equal(100 - 60 * successes, wallet.TotalPointBalance);

        // Ledger consistent: Σ |REDEEM| == deducted amount, BalanceAfter never negative.
        var redeems = await pair.Db1.AllianceTransactions
            .Where(t => t.Type == AllianceTransactionType.REDEEM).ToListAsync();
        Assert.Equal(60 * successes, redeems.Sum(t => -t.Points));
        Assert.All(redeems, t => Assert.True(t.BalanceAfter >= 0));
    }

    [Fact(DisplayName = "P0.1-5: Concurrent first writes (AddPoints) for same device → single wallet, both points land")]
    public async Task AddPoints_ConcurrentFirstWrite_SingleWallet()
    {
        await using var pair = await BuildWalletPairAsync();
        var deviceId = Guid.NewGuid();

        var results = await Task.WhenAll(
            pair.Svc1.AddPointsAsync(deviceId, TenantA, 40, "a"),
            pair.Svc2.AddPointsAsync(deviceId, TenantA, 60, "b"));

        Assert.All(results, r => Assert.True(r.Success));
        var wallets = await pair.Db1.AllianceWallets
            .AsNoTracking()
            .Where(w => w.CustomerDeviceId == deviceId).ToListAsync();
        Assert.Single(wallets); // unique index on CustomerDeviceId + serialization → no dup wallet
        Assert.Equal(100, wallets[0].TotalPointBalance);
    }

    [Fact(DisplayName = "P0.1-6: RefundAsync same IdempotencyKey twice → single ADJUST row")]
    public async Task Refund_SameKey_Twice_SingleAdjust()
    {
        await using var pair = await BuildWalletPairAsync();
        var svc = pair.Svc1;
        var deviceId = Guid.NewGuid();
        _ = await svc.AddPointsAsync(deviceId, TenantA, 100, "seed");

        var r1 = await svc.RefundAsync(deviceId, TenantA, 60, "Compensation", "VAN-AAA", idempotencyKey: "compensate:VAN-AAA");
        var r2 = await svc.RefundAsync(deviceId, TenantA, 60, "Compensation", "VAN-AAA", idempotencyKey: "compensate:VAN-AAA");

        Assert.True(r1.Success);
        Assert.True(r2.Success);
        var refunds = await pair.Db1.AllianceTransactions
            .Where(t => t.IdempotencyKey == "compensate:VAN-AAA").ToListAsync();
        Assert.Single(refunds);
        Assert.Equal(160, (await svc.GetWalletByDeviceIdAsync(deviceId))!.TotalPointBalance);
    }

    // ═══════════════════════════════════════════════════════════
    // P0.2 — RedemptionService compensation
    // ═══════════════════════════════════════════════════════════

    private static readonly Guid TestTenantGuid = Guid.Parse("00000000-0000-0000-0000-000000000001");
    private static readonly TenantId TestTenantId = new(TestTenantGuid);
    private static readonly Guid TestCustomerId = Guid.NewGuid();
    private static readonly Guid TestCatalogItemId = Guid.NewGuid();
    private static readonly Guid TestDeviceId = Guid.NewGuid();

    private static (RedemptionService sut, Mock<IAllianceWalletService> walletMock,
        ServiceProvider sp, VanAnDbContext db)
        BuildRedemption(bool voucherPersistFails, (bool Success, int NewBalance, string? Error) refundResult)
    {
        var connection = new SqliteConnection($"DataSource=test_{Guid.NewGuid()};Mode=Memory;Cache=Shared");
        connection.Open();

        var tenantProviderMock = new Mock<ITenantProvider>();
        tenantProviderMock.SetupGet(t => t.TenantId).Returns(TestTenantGuid);

        var services = new ServiceCollection();
        services.AddSingleton<ITenantProvider>(tenantProviderMock.Object);
        var efServiceProvider = new ServiceCollection().AddEntityFrameworkSqlite().BuildServiceProvider();
        services.AddDbContext<VanAnDbContext>(options => options.UseInternalServiceProvider(efServiceProvider).UseSqlite(connection));
        services.AddScoped<IVanAnDbContext>(sp => sp.GetRequiredService<VanAnDbContext>());
        services.AddLogging(b => b.AddConsole().SetMinimumLevel(LogLevel.Warning));
        ServiceProvider sp = services.BuildServiceProvider();
        VanAnDbContext db = sp.GetRequiredService<VanAnDbContext>();
        _ = db.Database.EnsureCreated();

        var customer = new Customer(TestTenantId, "Test Customer", "0901234567");
        customer.UpdateCustomerDetails("Test Customer", "0901234567", null, "Bronze", TestDeviceId, true);
        typeof(BaseEntity).GetProperty(nameof(BaseEntity.Id))!.SetValue(customer, TestCustomerId);
        db.Customers.Add(customer);
        db.SaveChanges();

        var repoMock = new Mock<IRedemptionRepository>();
        var catalogItem = new RedemptionCatalogItem(TestTenantId, "Test Product", 100);
        catalogItem.UpdateDetails("Test Product", "Test", null, 100, null, null, 7);
        repoMock.Setup(r => r.GetCatalogItemByIdAsync(TestCatalogItemId)).ReturnsAsync(catalogItem);
        repoMock.Setup(r => r.AddRecordAsync(It.IsAny<RedemptionRecord>()))
            .ReturnsAsync((RedemptionRecord r) => r);
        if (voucherPersistFails)
        {
            repoMock.Setup(r => r.AddVoucherAsync(It.IsAny<Voucher>()))
                .ThrowsAsync(new InvalidOperationException("voucher store down"));
        }
        else
        {
            repoMock.Setup(r => r.AddVoucherAsync(It.IsAny<Voucher>()))
                .ReturnsAsync((Voucher v) => v);
        }
        repoMock.Setup(r => r.UpdateRecordAsync(It.IsAny<RedemptionRecord>()))
            .ReturnsAsync((RedemptionRecord r) => r);
        repoMock.Setup(r => r.UpdateCatalogItemAsync(It.IsAny<RedemptionCatalogItem>()))
            .ReturnsAsync((RedemptionCatalogItem c) => c);

        var loyaltyMock = new Mock<ILoyaltyRewardsService>();
        var modeResolverMock = new Mock<ILoyaltyModeResolver>();
        modeResolverMock.Setup(m => m.GetEffectiveModeAsync(It.IsAny<Guid>())).ReturnsAsync(LoyaltyMode.Alliance);
        modeResolverMock.Setup(m => m.IsAllianceMemberAsync(It.IsAny<Guid>())).ReturnsAsync(true);

        var walletMock = new Mock<IAllianceWalletService>();
        walletMock.Setup(w => w.DeductPointsAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<Guid>()))
            .ReturnsAsync((true, 500, (string?)null));
        walletMock.Setup(w => w.RefundAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<Guid>()))
            .ReturnsAsync(refundResult);

        var sut = new RedemptionService(
            repoMock.Object,
            loyaltyMock.Object,
            tenantProviderMock.Object,
            db,
            null,
            null,
            NullLogger<RedemptionService>.Instance,
            modeResolverMock.Object,
            walletMock.Object);

        return (sut, walletMock, sp, db);
    }

    [Fact(DisplayName = "P0.2-1: Voucher persist fails after PG deduction → compensating refund with compensate:{voucher} key")]
    public async Task Redeem_VoucherFails_CompensatesDeduction()
    {
        var (sut, walletMock, sp, db) = BuildRedemption(voucherPersistFails: true, refundResult: (true, 600, null));
        try
        {
            var result = await sut.RedeemAsync(TestCustomerId, TestCatalogItemId);

            Assert.False(result.Success);
            Assert.Contains("hoàn", result.Error ?? "", StringComparison.OrdinalIgnoreCase);

            // Compensation MUST be attempted with the deterministic idempotency key.
            walletMock.Verify(
                w => w.RefundAsync(
                    TestDeviceId, TestTenantGuid, 100,
                    It.IsAny<string>(), It.Is<string>(v => v.StartsWith("VAN-")),
                    It.Is<string?>(k => k != null && k.StartsWith("compensate:VAN-")),
                    TestCustomerId),
                Times.Once,
                "Voucher-failure path must compensate the PG deduction idempotently");

            // Compensation succeeded → no reconcile case needed.
            Assert.False(await db.LoyaltyReconcileCases.AnyAsync());
        }
        finally
        {
            await sp.DisposeAsync();
        }
    }

    [Fact(DisplayName = "P0.2-2: Voucher fails AND compensation fails → LoyaltyReconcileCase persisted (Open)")]
    public async Task Redeem_CompensationFails_PersistsReconcileCase()
    {
        var (sut, walletMock, sp, db) = BuildRedemption(voucherPersistFails: true, refundResult: (false, 0, "PG down"));
        try
        {
            var result = await sut.RedeemAsync(TestCustomerId, TestCatalogItemId);

            Assert.False(result.Success);
            walletMock.Verify(
                w => w.RefundAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), 100, It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<Guid>()),
                Times.Once);

            var cases = await db.LoyaltyReconcileCases.ToListAsync();
            Assert.Single(cases);
            Assert.Equal(LoyaltyReconcileStatus.Open, cases[0].Status);
            Assert.Equal(100, cases[0].Points);
            Assert.Equal(TestDeviceId, cases[0].CustomerDeviceId);
            Assert.StartsWith("VAN-", cases[0].VoucherCode);
        }
        finally
        {
            await sp.DisposeAsync();
        }
    }

    [Fact(DisplayName = "P0.2-3: Voucher fails AND compensation THROWS → case still persisted (double failure)")]
    public async Task Redeem_CompensationThrows_PersistsReconcileCase()
    {
        var (sut, walletMock, sp, db) = BuildRedemption(voucherPersistFails: true, refundResult: (true, 0, null));
        walletMock.Setup(w => w.RefundAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<Guid>()))
            .ThrowsAsync(new InvalidOperationException("wallet unreachable"));
        try
        {
            var result = await sut.RedeemAsync(TestCustomerId, TestCatalogItemId);

            Assert.False(result.Success);
            Assert.Single(await db.LoyaltyReconcileCases.ToListAsync());
        }
        finally
        {
            await sp.DisposeAsync();
        }
    }

    [Fact(DisplayName = "P0.2-4: LoyaltyReconcileCase.Resolve is set-once (audit trail)")]
    public async Task ReconcileCase_Resolve_SetOnce()
    {
        using var scope = VanAnDbContextTestFactory.Create();
        var c = new LoyaltyReconcileCase(new TenantId(TenantA), "VAN-XYZ", Guid.NewGuid(), null, 100, "test");
        scope.Context.LoyaltyReconcileCases.Add(c);
        await scope.Context.SaveChangesAsync();

        c.Resolve("admin", "refunded via internal API");
        await scope.Context.SaveChangesAsync();
        Assert.Equal(LoyaltyReconcileStatus.Resolved, c.Status);
        Assert.NotNull(c.ResolvedAt);

        Assert.Throws<InvalidOperationException>(() => c.Resolve("admin", "again"));
    }

    // ═══════════════════════════════════════════════════════════
    // P0.3 — OrderWorkflowService: award at completed only
    // ═══════════════════════════════════════════════════════════

    private static (ServiceProvider sp, VanAnDbContext db, IOrderWorkflowService workflow,
        Mock<IAllianceWalletService> walletMock, ILoyaltyRewardsService loyalty,
        ITenantProvider tenantProvider, Mock<IFeatureFlagService> flagMock,
        Mock<IRefundOrchestrationService> reversalMock, Mock<IOrderFlowService>? flowMock)
        BuildWorkflow(bool allowCompletedToCancel = false)
    {
        var connection = new SqliteConnection($"DataSource=test_{Guid.NewGuid()};Mode=Memory;Cache=Shared");
        connection.Open();

        var services = new ServiceCollection();
        var efServiceProvider = new ServiceCollection().AddEntityFrameworkSqlite().BuildServiceProvider();
        services.AddDbContext<VanAnDbContext>(options =>
        {
            options.UseInternalServiceProvider(efServiceProvider);
            options.UseSqlite(connection);
            options.ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning));
        });
        services.AddScoped<IVanAnDbContext>(sp => sp.GetRequiredService<VanAnDbContext>());

        var socialCampaignMock = new Mock<ISocialCampaignService>();
        socialCampaignMock.Setup(s => s.GetCampaignByTrackingCodeAsync(It.IsAny<string>()))
            .ReturnsAsync((SocialCampaign?)null);
        services.AddSingleton(socialCampaignMock.Object);
        services.AddScoped<ILoyaltyRewardsService, LoyaltyRewardsService>();
        services.AddScoped<VanAn.CoreHub.Repositories.ILoyaltyRewardsRepository, VanAn.CoreHub.Infrastructure.Repositories.LoyaltyRewardsRepository>();
        services.AddScoped<ICustomerRepository, CustomerRepository>();
        services.AddScoped<IOrderRepository, OrderRepository>();
        var natsMock = new Mock<INatsEventPublisher>();
        natsMock.SetupGet(n => n.IsConnected).Returns(false);
        services.AddSingleton(natsMock.Object);
        services.AddLogging(b => b.AddConsole().SetMinimumLevel(LogLevel.Warning));
        services.Configure<LoyaltyPointsConfig>(opts =>
        {
            opts.AwardOnAllOrders = true;
            opts.PointsRate = 0.1m;
            opts.MinPointsPerOrder = 10;
        });

        var tenantProvider = new TestTenantProvider();
        tenantProvider.SetTenant(Guid.NewGuid());
        services.AddSingleton<ITenantProvider>(tenantProvider);

        var modeResolverMock = new Mock<ILoyaltyModeResolver>();
        modeResolverMock.Setup(m => m.GetEffectiveModeAsync(It.IsAny<Guid>())).ReturnsAsync(LoyaltyMode.Alliance);
        modeResolverMock.Setup(m => m.IsAllianceMemberAsync(It.IsAny<Guid>())).ReturnsAsync(true);
        services.AddSingleton(modeResolverMock.Object);

        var walletMock = new Mock<IAllianceWalletService>();
        walletMock.Setup(w => w.AddPointsAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<Guid?>(), It.IsAny<string?>(), It.IsAny<Guid>()))
            .ReturnsAsync((true, 500, (string?)null));
        services.AddSingleton(walletMock.Object);

        // P0.3: reversal flag + orchestrator — flag ON for the safety-net tests.
        var flagMock = new Mock<IFeatureFlagService>();
        flagMock.Setup(f => f.IsEnabledAsync(It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        services.AddSingleton(flagMock.Object);
        var reversalMock = new Mock<IRefundOrchestrationService>();
        services.AddSingleton(reversalMock.Object);

        Mock<IOrderFlowService>? flowMock = null;
        if (allowCompletedToCancel)
        {
            // Custom tenant flow that (improperly) allows completed→cancelled — the
            // exact edge case R5's reversal safety net exists for.
            flowMock = new Mock<IOrderFlowService>();
            flowMock.Setup(f => f.IsTransitionAllowedAsync(It.IsAny<Guid>(), It.IsAny<string?>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);
            services.AddSingleton(flowMock.Object);
        }

        services.AddScoped<IOrderWorkflowService, OrderWorkflowService>();

        ServiceProvider sp = services.BuildServiceProvider();
        VanAnDbContext db = sp.GetRequiredService<VanAnDbContext>();
        _ = db.Database.EnsureCreated();

        return (sp, db, sp.GetRequiredService<IOrderWorkflowService>(), walletMock,
            sp.GetRequiredService<ILoyaltyRewardsService>(), tenantProvider, flagMock, reversalMock, flowMock);
    }

    private static async Task<(Customer customer, Order order)> SeedOrderAsync(
        VanAnDbContext db, Guid tenantId, string status)
    {
        var tenantIdValue = new TenantId(tenantId);
        var deviceId = Guid.NewGuid();

        var customer = new Customer(tenantIdValue, "Test Customer", "0901234567");
        customer.UpdateCustomerDetails("Test Customer", "0901234567", null, "Bronze", deviceId, true);
        await db.Customers.AddAsync(customer);

        var product = new Product(tenantIdValue, "Test Product", "Test Description", 100000m, "Test Category");
        await db.Products.AddAsync(product);
        await db.SaveChangesAsync();

        var orderItem = new OrderItem(tenantIdValue, Guid.Empty, product.Id, 1, 100000m, "Test Product", 0.10m);
        var orderId = Guid.NewGuid();
        var order = Order.Create(orderId, tenantIdValue, customer.Id, new List<OrderItem> { orderItem });
        order.SetCustomerDeviceId(deviceId.ToString());
        typeof(Order).GetProperty(nameof(Order.Status))!.SetValue(order, new OrderStatusId(status));

        await db.Orders.AddAsync(order);
        await db.SaveChangesAsync();
        return (customer, order);
    }

    [Fact(DisplayName = "P0.3-1: Transition to 'delivered' does NOT award points (D-Q1: completed only)")]
    public async Task Delivered_DoesNotAward()
    {
        var (sp, db, workflow, walletMock, loyalty, tenantProvider, _, _, _) = BuildWorkflow();
        try
        {
            var (customer, order) = await SeedOrderAsync(db, tenantProvider.TenantId, "ready");

            // ready → delivering → delivered
            Assert.NotNull(await workflow.TransitionStatusAsync(order.Id, new OrderStatusId("delivering")));
            var result = await workflow.TransitionStatusAsync(order.Id, new OrderStatusId("delivered"));

            Assert.NotNull(result);
            Assert.Equal("delivered", result.Status.Value);
            walletMock.Verify(
                w => w.AddPointsAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<Guid?>(), It.IsAny<string?>(), It.IsAny<Guid>()),
                Times.Never,
                "D-Q1: 'delivered' must NOT award points — only 'completed' does");
            Assert.Null(await loyalty.GetCustomerRewardsAsync(customer.Id));
        }
        finally
        {
            await sp.DisposeAsync();
        }
    }

    [Fact(DisplayName = "P0.3-2: delivering → delivered → completed awards EXACTLY once (at completed)")]
    public async Task Completed_AwardsExactlyOnce()
    {
        var (sp, db, workflow, walletMock, _, tenantProvider, _, _, _) = BuildWorkflow();
        try
        {
            var (customer, order) = await SeedOrderAsync(db, tenantProvider.TenantId, "ready");

            Assert.NotNull(await workflow.TransitionStatusAsync(order.Id, new OrderStatusId("delivering")));
            Assert.NotNull(await workflow.TransitionStatusAsync(order.Id, new OrderStatusId("delivered")));
            var result = await workflow.TransitionStatusAsync(order.Id, new OrderStatusId("completed"));

            Assert.NotNull(result);
            Assert.Equal("completed", result.Status.Value);
            walletMock.Verify(
                w => w.AddPointsAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<int>(), It.IsAny<string>(), order.Id, It.IsAny<string?>(), customer.Id),
                Times.Once,
                "Award must happen exactly once — at 'completed', not 'delivered'");
        }
        finally
        {
            await sp.DisposeAsync();
        }
    }

    [Fact(DisplayName = "P0.3-3: completed → cancelled is REJECTED in the built-in flow (terminal state)")]
    public async Task CompletedToCancelled_Builtin_Rejected()
    {
        var (sp, db, workflow, _, _, tenantProvider, _, reversalMock, _) = BuildWorkflow();
        try
        {
            var (_, order) = await SeedOrderAsync(db, tenantProvider.TenantId, "ready");
            Assert.NotNull(await workflow.TransitionStatusAsync(order.Id, new OrderStatusId("delivering")));
            Assert.NotNull(await workflow.TransitionStatusAsync(order.Id, new OrderStatusId("completed")));

            var result = await workflow.TransitionStatusAsync(order.Id, new OrderStatusId("cancelled"));

            Assert.Null(result); // terminal state — transition rejected
            reversalMock.Verify(
                r => r.OrchestrateReversalAsync(It.IsAny<Guid>(), It.IsAny<TenantId>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
                Times.Never,
                "No cancel transition → no reversal needed on the happy path");
        }
        finally
        {
            await sp.DisposeAsync();
        }
    }

    [Fact(DisplayName = "P0.3-4: Custom flow allowing completed→cancelled → reversal safety net fires")]
    public async Task CustomFlow_CompletedCancelled_TriggersReversal()
    {
        var (sp, db, workflow, _, _, tenantProvider, _, reversalMock, _) = BuildWorkflow(allowCompletedToCancel: true);
        try
        {
            var (_, order) = await SeedOrderAsync(db, tenantProvider.TenantId, "ready");
            Assert.NotNull(await workflow.TransitionStatusAsync(order.Id, new OrderStatusId("completed")));
            var result = await workflow.TransitionStatusAsync(order.Id, new OrderStatusId("cancelled"));

            Assert.NotNull(result);
            Assert.Equal("cancelled", result.Status.Value);
            reversalMock.Verify(
                r => r.OrchestrateReversalAsync(order.Id, order.TenantId, It.IsAny<string>(), It.IsAny<CancellationToken>()),
                Times.Once,
                "Custom flow completed→cancelled must trigger the 4-step reversal safety net (flag default ON)");
        }
        finally
        {
            await sp.DisposeAsync();
        }
    }
}
