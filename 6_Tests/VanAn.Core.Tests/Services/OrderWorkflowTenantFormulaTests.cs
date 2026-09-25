using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
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
/// 2026-09-25 — Regression tests for the production loyalty incident:
/// ProcessLoyaltyPointsAsync used to resolve the per-tenant formula via
/// <c>customer.TenantId</c> instead of <c>order.TenantId</c>. A guest/stub customer can
/// belong to a different tenant than the order (device-identity reuse across shops), so
/// the award picked up the WRONG tenant's rate/max/AwardOnAllOrders:
/// order on tenant A (awardOnAll=false, rate=0.001) + customer on tenant B
/// (awardOnAll=true, rate=1.0, max=5000) awarded 5000 pts instead of 0.
/// </summary>
public class OrderWorkflowTenantFormulaTests
{
    private static readonly Guid OrderTenantId = Guid.Parse("00000000-0000-0000-0000-0000000000aa");
    private static readonly Guid CustomerTenantId = Guid.Parse("00000000-0000-0000-0000-0000000000bb");

    /// <summary>Per-tenant ShopFeatureSettings stub — mirrors the prod incident config.</summary>
    private sealed class FakeShopFeatureSettingsService : IShopFeatureSettingsService
    {
        public Dictionary<Guid, ShopFeatureSettingsDto> Settings { get; } = new();
        public List<Guid> LookedUpTenantIds { get; } = new();

        public Task<ShopFeatureSettingsDto> GetSettingsAsync(Guid tenantId, CancellationToken ct = default)
        {
            LookedUpTenantIds.Add(tenantId);
            return Task.FromResult(Settings.TryGetValue(tenantId, out var s) ? s : new ShopFeatureSettingsDto());
        }

        public Task<ShopFeatureSettingsDto> UpdateSettingsAsync(Guid tenantId, ShopFeatureSettingsDto settings, CancellationToken ct = default)
            => Task.FromResult(settings);

        public Task<bool> IsEnabledAsync(Guid tenantId, string toggleName, CancellationToken ct = default)
            => Task.FromResult(true);
    }

    /// <summary>
    /// Test-only decorator: <see cref="OrderRepository.GetByIdWithIncludesAsync"/> keeps the global
    /// IMustHaveTenant query filter (BaseEntity implements it), which hides an order whose tenant
    /// differs from the ambient one. The prod incident awarded a cross-tenant order (order tenant
    /// 0001, customer tenant a5b6) through the ShopERP replica where the order row is visible
    /// regardless of ambient tenant — so this test loads the order with IgnoreQueryFilters while
    /// keeping CUSTOMER resolution tenant-scoped (the exact condition that reproduced the bug:
    /// customer found, but its TenantId ≠ order.TenantId).
    /// </summary>
    private sealed class CrossTenantOrderRepository : IOrderRepository
    {
        private readonly IOrderRepository _inner;
        private readonly IVanAnDbContext _context;

        public CrossTenantOrderRepository(IOrderRepository inner, IVanAnDbContext context)
        {
            _inner = inner;
            _context = context;
        }

        public Task<Order?> GetByIdWithIncludesAsync(Guid orderId, CancellationToken cancellationToken = default)
            => _context.Orders
                .IgnoreQueryFilters()
                .Include(o => o.Items)
                .ThenInclude(i => i.Product)
                .FirstOrDefaultAsync(o => o.Id == orderId, cancellationToken);

        public Task<Order?> GetByIdAsync(OrderId id, TenantId tenantId, CancellationToken cancellationToken = default)
            => _inner.GetByIdAsync(id, tenantId, cancellationToken);

        public Task<Order?> GetByIdWithIncludesIgnoreFiltersAsync(Guid orderId, CancellationToken cancellationToken = default)
            => _inner.GetByIdWithIncludesIgnoreFiltersAsync(orderId, cancellationToken);

        public Task<IEnumerable<Order>> GetByTenantAsync(TenantId tenantId, CancellationToken cancellationToken = default)
            => _inner.GetByTenantAsync(tenantId, cancellationToken);

        public Task<IEnumerable<Order>> GetByDateRangeAsync(TenantId tenantId, DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default)
            => _inner.GetByDateRangeAsync(tenantId, startDate, endDate, cancellationToken);

        public Task<IEnumerable<Order>> GetByStatusAsync(TenantId tenantId, string status, CancellationToken cancellationToken = default)
            => _inner.GetByStatusAsync(tenantId, status, cancellationToken);

        public Task<IEnumerable<Order>> GetTodayOrdersAsync(TenantId tenantId, CancellationToken cancellationToken = default)
            => _inner.GetTodayOrdersAsync(tenantId, cancellationToken);

        public Task<Order> AddAsync(Order order, CancellationToken cancellationToken = default)
            => _inner.AddAsync(order, cancellationToken);

        public Task<Order> AddAsyncNoSave(Order order, CancellationToken cancellationToken = default)
            => _inner.AddAsyncNoSave(order, cancellationToken);

        public Task<Order> UpdateAsync(Order order, CancellationToken cancellationToken = default)
            => _inner.UpdateAsync(order, cancellationToken);

        public Task SaveChangesAsync(CancellationToken cancellationToken = default)
            => _inner.SaveChangesAsync(cancellationToken);

        public Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default)
            => _inner.BeginTransactionAsync(cancellationToken);

        public Task<int> GetCountByDateRangeAsync(TenantId tenantId, DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default)
            => _inner.GetCountByDateRangeAsync(tenantId, startDate, endDate, cancellationToken);

        public Task<IEnumerable<Order>> GetByDeviceIdAndNullCustomerAsync(string deviceId, CancellationToken cancellationToken = default)
            => _inner.GetByDeviceIdAndNullCustomerAsync(deviceId, cancellationToken);

        public Task<int> BulkAssignCustomerAsync(string deviceId, Guid customerId, CancellationToken cancellationToken = default)
            => _inner.BulkAssignCustomerAsync(deviceId, customerId, cancellationToken);
    }

    private static (ServiceProvider sp, VanAnDbContext db, IOrderWorkflowService workflow,
        ILoyaltyRewardsService loyalty, FakeShopFeatureSettingsService settings)
        BuildServices()
    {
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

        var socialCampaignMock = new Mock<ISocialCampaignService>();
        socialCampaignMock
            .Setup(s => s.GetCampaignByTrackingCodeAsync(It.IsAny<string>()))
            .ReturnsAsync((SocialCampaign?)null);
        services.AddSingleton(socialCampaignMock.Object);

        services.AddScoped<ILoyaltyRewardsService, LoyaltyRewardsService>();
        services.AddScoped<VanAn.CoreHub.Repositories.ILoyaltyRewardsRepository, VanAn.CoreHub.Infrastructure.Repositories.LoyaltyRewardsRepository>();
        services.AddScoped<ICustomerRepository, CustomerRepository>();
        services.AddScoped<IOrderRepository>(sp =>
        {
            var context = sp.GetRequiredService<IVanAnDbContext>();
            var inner = new OrderRepository(context, sp.GetRequiredService<ILogger<OrderRepository>>());
            return new CrossTenantOrderRepository(inner, context);
        });

        var natsMock = new Mock<INatsEventPublisher>();
        natsMock.SetupGet(n => n.IsConnected).Returns(false);
        services.AddSingleton(natsMock.Object);
        services.AddLogging(b => b.AddConsole().SetMinimumLevel(LogLevel.Warning));

        var settings = new FakeShopFeatureSettingsService();
        services.AddSingleton<IShopFeatureSettingsService>(settings);

        // Global fallback — irrelevant once tenant settings exist, kept sane anyway.
        services.Configure<LoyaltyPointsConfig>(opts =>
        {
            opts.AwardOnAllOrders = true;
            opts.PointsRate = 0.1m;
            opts.MinPointsPerOrder = 10;
        });

        var tenantProvider = new TestTenantProvider();
        // Ambient = CUSTOMER tenant — mirrors the prod incident: award ran on the ShopERP scope of
        // the shop's own tenant (a5b6…), where the cross-tenant customer is visible to the
        // IMustHaveTenant query filter while the ORDER tenant (0001…) is different.
        tenantProvider.SetTenant(CustomerTenantId);
        services.AddSingleton<ITenantProvider>(tenantProvider);

        var modeResolverMock = new Mock<ILoyaltyModeResolver>();
        modeResolverMock.Setup(m => m.GetEffectiveModeAsync(It.IsAny<Guid>())).ReturnsAsync(LoyaltyMode.Silo);
        modeResolverMock.Setup(m => m.IsAllianceMemberAsync(It.IsAny<Guid>())).ReturnsAsync(false);
        services.AddSingleton(modeResolverMock.Object);

        services.AddScoped<IOrderWorkflowService, OrderWorkflowService>();

        ServiceProvider sp = services.BuildServiceProvider();
        VanAnDbContext db = sp.GetRequiredService<VanAnDbContext>();
        _ = db.Database.EnsureCreated();
        return (sp, db, sp.GetRequiredService<IOrderWorkflowService>(),
            sp.GetRequiredService<ILoyaltyRewardsService>(), settings);
    }

    /// <summary>Seed order on <paramref name="orderTenant"/> owned by a customer on <paramref name="customerTenant"/>.</summary>
    private static async Task<(Customer customer, Order order)> SeedCrossTenantOrderAsync(
        VanAnDbContext db, Guid orderTenant, Guid customerTenant, decimal unitPrice)
    {
        var deviceId = Guid.NewGuid();
        var customer = new Customer(new TenantId(customerTenant), "Cross-Tenant Customer", "0901234567");
        customer.UpdateCustomerDetails("Cross-Tenant Customer", "0901234567", null, "Bronze", deviceId, true);
        await db.Customers.AddAsync(customer);

        var product = new Product(new TenantId(orderTenant), "Test Product", "Desc", unitPrice, "Cat");
        await db.Products.AddAsync(product);
        await db.SaveChangesAsync();

        var orderItem = new OrderItem(new TenantId(orderTenant), Guid.Empty, product.Id, 1, unitPrice, "Test Product", 0.10m);
        var order = Order.Create(Guid.NewGuid(), new TenantId(orderTenant), customer.Id, new List<OrderItem> { orderItem });
        order.SetCustomerDeviceId(deviceId.ToString());
        typeof(Order).GetProperty(nameof(Order.Status))!.SetValue(order, new OrderStatusId("preparing"));

        await db.Orders.AddAsync(order);
        await db.SaveChangesAsync();
        return (customer, order);
    }

    [Fact(DisplayName = "Prod regression: AwardOnAllOrders=false on ORDER tenant blocks award even when CUSTOMER tenant has awardOnAll=true")]
    public async Task Award_UsesOrderTenant_AwardOnAllOrders()
    {
        var (sp, db, workflow, loyalty, settings) = BuildServices();
        try
        {
            // Prod incident shape: order tenant restrictive, customer tenant permissive.
            settings.Settings[OrderTenantId] = new ShopFeatureSettingsDto
            {
                Loyalty_Program_Enabled = true,
                Loyalty_AwardOnAllOrders = false,   // order tenant: no tracking code → no award
                Loyalty_PointsRate = 0.001m,
                Loyalty_MinPointsPerOrder = 1,
            };
            settings.Settings[CustomerTenantId] = new ShopFeatureSettingsDto
            {
                Loyalty_Program_Enabled = true,
                Loyalty_AwardOnAllOrders = true,    // customer tenant: would award 5000 (rate 1.0, max 5000)
                Loyalty_PointsRate = 1.0m,
                Loyalty_MaxPointsPerOrder = 5000,
            };

            var (customer, order) = await SeedCrossTenantOrderAsync(db, OrderTenantId, CustomerTenantId, 50000m);

            var result = await workflow.TransitionStatusAsync(order.Id, new OrderStatusId("completed"));
            Assert.NotNull(result);

            // No award — ORDER tenant forbids awarding orders without a TrackingCode.
            // Query IgnoreQueryFilters: the rewards row (if written) carries customer.TenantId,
            // which differs from the ambient OrderTenantId — a tenant-scoped read would hide a
            // wrong-tenant award and pass trivially.
            var rewards = await db.LoyaltyRewards.IgnoreQueryFilters()
                .FirstOrDefaultAsync(r => r.CustomerId == customer.Id);
            Assert.True(rewards == null || rewards.PointBalance == 0,
                "Award must follow ORDER tenant settings (awardOnAll=false → skip). " +
                "Old bug read customer.TenantId (awardOnAll=true) and awarded 5000.");
        }
        finally
        {
            await sp.DisposeAsync();
        }
    }

    [Fact(DisplayName = "Prod regression: points computed with ORDER tenant rate/max, not customer tenant's")]
    public async Task Award_UsesOrderTenant_RateAndMax()
    {
        var (sp, db, workflow, loyalty, settings) = BuildServices();
        try
        {
            settings.Settings[OrderTenantId] = new ShopFeatureSettingsDto
            {
                Loyalty_Program_Enabled = true,
                Loyalty_AwardOnAllOrders = true,
                Loyalty_PointsRate = 0.001m,          // 1 pt / 1000đ → 50,000đ = 50 pts
                Loyalty_MinPointsPerOrder = 1,
                Loyalty_MaxPointsPerOrder = 10000,
            };
            settings.Settings[CustomerTenantId] = new ShopFeatureSettingsDto
            {
                Loyalty_Program_Enabled = true,
                Loyalty_AwardOnAllOrders = true,
                Loyalty_PointsRate = 1.0m,            // would give 50,000 → clamp 5000 (the prod bug)
                Loyalty_MaxPointsPerOrder = 5000,
            };

            var (customer, order) = await SeedCrossTenantOrderAsync(db, OrderTenantId, CustomerTenantId, 50000m);

            var result = await workflow.TransitionStatusAsync(order.Id, new OrderStatusId("completed"));
            Assert.NotNull(result);

            // IgnoreQueryFilters: rewards row is written under customer.TenantId (legacy Silo path
            // calls AddPointsAsync with customer.TenantId) — ambient filter would hide it.
            var rewards = await db.LoyaltyRewards.IgnoreQueryFilters()
                .FirstOrDefaultAsync(r => r.CustomerId == customer.Id);
            Assert.NotNull(rewards);
            Assert.Equal(50, rewards!.PointBalance); // 50,000 × 0.001 — NOT 5000 (customer-tenant bug)
        }
        finally
        {
            await sp.DisposeAsync();
        }
    }
}
