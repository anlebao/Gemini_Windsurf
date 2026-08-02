using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using System.Reflection;
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
/// Loyalty Alliance Phase 2B — tests for OrderWorkflowService EARN mode routing.
/// Verifies that ProcessLoyaltyPointsAsync routes to AllianceWalletService when
/// mode=Alliance + tenant is member, and falls through to Silo (LoyaltyRewardsService)
/// when mode=Silo or tenant opted out.
/// Spec: docs/specs/loyalty-alliance-spec.md v1.0 (Q2: full opt-out).
/// </summary>
public class OrderWorkflowAllianceTests
{
    /// <summary>
    /// Build a service provider with mocked ILoyaltyModeResolver + IAllianceWalletService.
    /// Allows precise control over mode routing without needing PG tables.
    /// </summary>
    private static (ServiceProvider sp, VanAnDbContext db, IOrderWorkflowService workflow,
        Mock<ILoyaltyModeResolver> modeResolverMock, Mock<IAllianceWalletService> walletServiceMock,
        ILoyaltyRewardsService loyaltyService, ITenantProvider tenantProvider)
        BuildServices(LoyaltyMode mode, bool isAllianceMember)
    {
        // Use a single shared SQLite in-memory connection so EnsureCreated persists across scopes
        var connection = new SqliteConnection($"DataSource=test_{Guid.NewGuid()};Mode=Memory;Cache=Shared");
        connection.Open();

        var services = new ServiceCollection();
        var efServiceProvider = new ServiceCollection().AddEntityFrameworkSqlite().BuildServiceProvider();
        services.AddDbContext<VanAnDbContext>(options =>
        {
            options.UseInternalServiceProvider(efServiceProvider);
            options.UseSqlite(connection);
            options.ConfigureWarnings(warnings => warnings.Ignore(InMemoryEventId.TransactionIgnoredWarning));
        });
        services.AddScoped<IVanAnDbContext>(sp => sp.GetRequiredService<VanAnDbContext>());
        // Mock ISocialCampaignService — avoids deep dependency chain (SocialCampaignRepository, etc.)
        var socialCampaignMock = new Mock<ISocialCampaignService>();
        socialCampaignMock
            .Setup(s => s.GetCampaignByTrackingCodeAsync(It.IsAny<string>()))
            .ReturnsAsync((SocialCampaign?)null);
        services.AddSingleton(socialCampaignMock.Object);
        services.AddScoped<ILoyaltyRewardsService, LoyaltyRewardsService>();
        services.AddScoped<VanAn.CoreHub.Repositories.ILoyaltyRewardsRepository, VanAn.CoreHub.Infrastructure.Repositories.LoyaltyRewardsRepository>();
        services.AddScoped<ICustomerRepository, CustomerRepository>();
        services.AddScoped<IOrderRepository, OrderRepository>();
        // INatsEventPublisher — mock (not connected, no-op publish)
        var natsMock = new Mock<INatsEventPublisher>();
        natsMock.SetupGet(n => n.IsConnected).Returns(false);
        services.AddSingleton(natsMock.Object);
        services.AddLogging(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Warning));
        services.Configure<LoyaltyPointsConfig>(opts =>
        {
            opts.AwardOnAllOrders = true;
            opts.PointsRate = 0.1m;
            opts.MinPointsPerOrder = 10;
        });

        var tenantProvider = new TestTenantProvider();
        tenantProvider.SetTenant(Guid.NewGuid());
        services.AddSingleton<ITenantProvider>(tenantProvider);

        // Mock the Alliance dependencies
        var modeResolverMock = new Mock<ILoyaltyModeResolver>();
        modeResolverMock
            .Setup(m => m.GetEffectiveModeAsync(It.IsAny<Guid>()))
            .ReturnsAsync(mode);
        modeResolverMock
            .Setup(m => m.IsAllianceMemberAsync(It.IsAny<Guid>()))
            .ReturnsAsync(isAllianceMember);

        var walletServiceMock = new Mock<IAllianceWalletService>();
        walletServiceMock
            .Setup(w => w.AddPointsAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<Guid?>()))
            .ReturnsAsync((true, 500, (string?)null));

        services.AddSingleton(modeResolverMock.Object);
        services.AddSingleton(walletServiceMock.Object);

        // Register OrderWorkflowService with all dependencies
        services.AddScoped<IOrderWorkflowService, OrderWorkflowService>();

        ServiceProvider sp = services.BuildServiceProvider();
        VanAnDbContext db = sp.GetRequiredService<VanAnDbContext>();
        _ = db.Database.EnsureCreated(); // Create schema on the shared connection
        IOrderWorkflowService workflow = sp.GetRequiredService<IOrderWorkflowService>();
        ILoyaltyRewardsService loyalty = sp.GetRequiredService<ILoyaltyRewardsService>();

        return (sp, db, workflow, modeResolverMock, walletServiceMock, loyalty, tenantProvider);
    }

    /// <summary>
    /// Seed a Customer + Product + Order (in "preparing" status) ready to be transitioned to "completed".
    /// Uses constructors + reflection for protected setters (test project cannot access protected members).
    /// </summary>
    private static async Task<(Customer customer, Order order)> SeedOrderAsync(
        VanAnDbContext db, Guid tenantId)
    {
        var tenantIdValue = new TenantId(tenantId);
        var deviceId = Guid.NewGuid();

        // Create Customer with DeviceId
        var customer = new Customer(tenantIdValue, "Test Customer", "0901234567");
        customer.UpdateCustomerDetails("Test Customer", "0901234567", null, "Bronze", deviceId, true);
        await db.Customers.AddAsync(customer);

        // Create Product via public constructor
        var product = new Product(tenantIdValue, "Test Product", "Test Description", 100000m, "Test Category");
        await db.Products.AddAsync(product);

        await db.SaveChangesAsync();

        // Create OrderItem via public constructor
        var orderItem = new OrderItem(
            tenantIdValue, Guid.Empty, product.Id, 1, 100000m, "Test Product", 0.10m);

        // Create Order via factory method (uses reflection internally for protected setters)
        var orderId = Guid.NewGuid();
        var order = Order.Create(orderId, tenantIdValue, customer.Id, new List<OrderItem> { orderItem });

        // Set CustomerDeviceId via domain method
        order.SetCustomerDeviceId(deviceId.ToString());

        // Use reflection to set Status to "preparing" (protected set — not accessible from test)
        typeof(Order)
            .GetProperty(nameof(Order.Status))!
            .SetValue(order, new OrderStatusId("preparing"));

        await db.Orders.AddAsync(order);
        await db.SaveChangesAsync();
        return (customer, order);
    }

    // ──────────────────────────────────────────────────────────
    // Test 1: Alliance mode + member → routes to AllianceWalletService
    // ──────────────────────────────────────────────────────────

    [Fact(DisplayName = "LA-OW-1: ProcessLoyaltyPoints — Alliance mode + member routes to AllianceWalletService")]
    public async Task ProcessLoyaltyPoints_AllianceMode_RoutesToAllianceWallet()
    {
        var (sp, db, workflow, modeResolverMock, walletServiceMock, loyalty, tenantProvider) =
            BuildServices(LoyaltyMode.Alliance, isAllianceMember: true);

        try
        {
            Guid tenantId = tenantProvider.TenantId;
            var (customer, order) = await SeedOrderAsync(db, tenantId);

            var result = await workflow.TransitionStatusAsync(order.Id, new OrderStatusId("completed"));

            Assert.NotNull(result);
            Assert.Equal("completed", result.Status.Value);

            // AllianceWalletService.AddPointsAsync MUST be called
            walletServiceMock.Verify(
                w => w.AddPointsAsync(
                    It.IsAny<Guid>(), tenantId, It.IsAny<int>(), It.IsAny<string>(), order.Id),
                Times.Once,
                "Alliance mode + member must route EARN to AllianceWalletService");

            // LoyaltyRewardsService should NOT have created rewards for this customer
            // (Alliance flow returns before reaching Silo code)
            var rewards = await loyalty.GetCustomerRewardsAsync(customer.Id);
            Assert.Null(rewards);
        }
        finally
        {
            await sp.DisposeAsync();
        }
    }

    // ──────────────────────────────────────────────────────────
    // Test 2: Silo mode → routes to LoyaltyRewardsService (existing flow)
    // ──────────────────────────────────────────────────────────

    [Fact(DisplayName = "LA-OW-2: ProcessLoyaltyPoints — Silo mode routes to LoyaltyRewardsService")]
    public async Task ProcessLoyaltyPoints_SiloMode_RoutesToLoyaltyRewards()
    {
        var (sp, db, workflow, modeResolverMock, walletServiceMock, loyalty, tenantProvider) =
            BuildServices(LoyaltyMode.Silo, isAllianceMember: false);

        try
        {
            Guid tenantId = tenantProvider.TenantId;
            var (customer, order) = await SeedOrderAsync(db, tenantId);

            var result = await workflow.TransitionStatusAsync(order.Id, new OrderStatusId("completed"));

            Assert.NotNull(result);
            Assert.Equal("completed", result.Status.Value);

            // AllianceWalletService.AddPointsAsync must NOT be called
            walletServiceMock.Verify(
                w => w.AddPointsAsync(
                    It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<Guid?>()),
                Times.Never,
                "Silo mode must NOT route to AllianceWalletService");

            // LoyaltyRewardsService MUST have been called — verify rewards exist
            var rewards = await loyalty.GetCustomerRewardsAsync(customer.Id);
            Assert.NotNull(rewards);
            Assert.True(rewards!.PointBalance > 0, "Silo flow must award points to LoyaltyRewards");
        }
        finally
        {
            await sp.DisposeAsync();
        }
    }

    // ──────────────────────────────────────────────────────────
    // Test 3: Alliance mode + tenant opt-out → falls through to Silo
    // ──────────────────────────────────────────────────────────

    [Fact(DisplayName = "LA-OW-3: ProcessLoyaltyPoints — Alliance mode + tenant opt-out falls to Silo")]
    public async Task ProcessLoyaltyPoints_AllianceMode_TenantOptOut_FallsToSilo()
    {
        var (sp, db, workflow, modeResolverMock, walletServiceMock, loyalty, tenantProvider) =
            BuildServices(LoyaltyMode.Alliance, isAllianceMember: false);

        try
        {
            Guid tenantId = tenantProvider.TenantId;
            var (customer, order) = await SeedOrderAsync(db, tenantId);

            var result = await workflow.TransitionStatusAsync(order.Id, new OrderStatusId("completed"));

            Assert.NotNull(result);
            Assert.Equal("completed", result.Status.Value);

            // AllianceWalletService must NOT be called (tenant opted out)
            walletServiceMock.Verify(
                w => w.AddPointsAsync(
                    It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<Guid?>()),
                Times.Never,
                "Tenant opt-out (IsAllianceMember=false) must NOT route to AllianceWalletService");

            // LoyaltyRewardsService MUST have been called — falls through to Silo
            var rewards = await loyalty.GetCustomerRewardsAsync(customer.Id);
            Assert.NotNull(rewards);
            Assert.True(rewards!.PointBalance > 0, "Opt-out tenant must still get points via Silo flow");
        }
        finally
        {
            await sp.DisposeAsync();
        }
    }
}
