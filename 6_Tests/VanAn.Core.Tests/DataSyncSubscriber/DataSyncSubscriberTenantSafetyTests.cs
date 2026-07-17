using System.Text.Json;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using VanAn.CoreHub.Infrastructure;
using VanAn.Gateway.Services;
using VanAn.Shared.Domain;
using VanAn.Shared.Domain.Common;
using Xunit;

namespace VanAn.Tests.DataSyncSubscriberTests;

/// <summary>
/// Unit tests for DataSyncSubscriber sync handlers.
/// Uses SQLite in-memory provider so VanAnDbContext.OnModelCreating runs fully
/// (including auto HasQueryFilter on IMustHaveTenant entities).
///
/// Verifies:
///   1. Product sync (upsert + delete) works correctly with SetTenant pattern
///   2. Order status sync works correctly with SetTenant pattern
///   3. FAIL-SAFE: missing tenantId → handler refuses to process (no bypass, no silent update)
///   4. Query filter enforces tenant isolation — wrong tenant context → entity not found
/// </summary>
public class DataSyncSubscriberTenantSafetyTests : IDisposable
{
    private static readonly Guid TenantA = Guid.Parse("00000000-0000-0000-0000-000000000001");
    private static readonly Guid TenantB = Guid.Parse("00000000-0000-0000-0000-000000000002");

    private readonly SqliteConnection _connection;
    private readonly ServiceProvider _rootSp;
    private readonly TestTenantProvider _tenantProvider;

    public DataSyncSubscriberTenantSafetyTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        var services = new ServiceCollection();
        _tenantProvider = new TestTenantProvider();
        services.AddSingleton<ITenantProvider>(_tenantProvider);
        services.AddDbContext<VanAnDbContext>(opt => opt.UseSqlite(_connection));
        services.AddScoped<IVanAnDbContext>(sp => sp.GetRequiredService<VanAnDbContext>());
        _rootSp = services.BuildServiceProvider();

        // EnsureCreated runs OnModelCreating → auto HasQueryFilter on IMustHaveTenant entities
        using (var scope = _rootSp.CreateScope())
        {
            var ctx = scope.ServiceProvider.GetRequiredService<VanAnDbContext>();
            ctx.Database.EnsureCreated();
        }
    }

    public void Dispose()
    {
        _rootSp.Dispose();
        _connection.Dispose();
    }

    private (IServiceScope scope, IVanAnDbContext dbContext) CreateScope()
    {
        var scope = _rootSp.CreateScope();
        return (scope, scope.ServiceProvider.GetRequiredService<IVanAnDbContext>());
    }

    /// <summary>
    /// Seed a Product with a specific productId by constructing then syncing both Id + ProductId.
    /// Product constructor sets Id = ProductId.Value (auto-gen Guid), so we override both.
    /// </summary>
    private static Product SeedProduct(Guid productId, Guid tenantId, string name = "Test Product", decimal price = 10000m)
    {
        var product = new Product(new TenantId(tenantId), name, price, "Test");
        typeof(Product).GetProperty("ProductId")!.SetValue(product, new ProductId(productId));
        typeof(BaseEntity).GetProperty("Id")!.SetValue(product, productId);
        return product;
    }

    private static DataSyncSubscriber CreateSubscriber()
    {
        var config = new Microsoft.Extensions.Configuration.ConfigurationBuilder().Build();
        return new DataSyncSubscriber(
            serviceProvider: null!,
            configuration: config,
            logger: new Mock<ILogger<DataSyncSubscriber>>().Object);
    }

    private static JsonElement ParseJson(string json) => JsonSerializer.Deserialize<JsonElement>(json)!;

    // ──────────────────────────────────────────────────────────────
    // PRODUCT SYNC TESTS
    // ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task ProductSync_Upsert_New_Product_With_Correct_Tenant_Creates_Product()
    {
        var (scope, dbContext) = CreateScope();
        var subscriber = CreateSubscriber();

        Guid productId = Guid.NewGuid();
        string json = $$"""{"ProductId":"{{productId}}","TenantId":"{{TenantA}}","Name":"Cà phê","Description":"Đen đá","Price":25000,"CostPrice":15000,"Category":"Đồ uống","IsActive":true,"ImageUrl":null,"VatRate":0.08}""";

        await subscriber.SyncProductUpsertAsync(ParseJson(json), dbContext, scope.ServiceProvider, CancellationToken.None);

        Product? created = await dbContext.Products.IgnoreQueryFilters()
            .FirstOrDefaultAsync(p => p.Id == productId);
        Assert.NotNull(created);
        Assert.Equal("Cà phê", created!.Name);
        Assert.Equal(25000m, created.Price);
        Assert.Equal(TenantA, created.TenantId.Value);
        Assert.Equal(TenantA, _tenantProvider.TenantId);
    }

    [Fact]
    public async Task ProductSync_Upsert_Existing_Product_With_Correct_Tenant_Updates_Product()
    {
        var (scope, dbContext) = CreateScope();

        Guid productId = Guid.NewGuid();
        var seeded = SeedProduct(productId, TenantA, "Old Name", 20000m);
        dbContext.Products.Add(seeded);
        await dbContext.SaveChangesAsync();

        var subscriber = CreateSubscriber();
        string json = $$"""{"ProductId":"{{productId}}","TenantId":"{{TenantA}}","Name":"New Name","Description":"Updated","Price":30000,"CostPrice":18000,"Category":"Đồ uống","IsActive":true,"ImageUrl":null,"VatRate":0.1}""";

        await subscriber.SyncProductUpsertAsync(ParseJson(json), dbContext, scope.ServiceProvider, CancellationToken.None);

        Product? updated = await dbContext.Products.IgnoreQueryFilters()
            .FirstOrDefaultAsync(p => p.Id == productId);
        Assert.NotNull(updated);
        Assert.Equal("New Name", updated!.Name);
        Assert.Equal(30000m, updated.Price);
    }

    [Fact]
    public async Task ProductSync_Upsert_Missing_TenantId_Refuses_To_Process_FailSafe()
    {
        var (scope, dbContext) = CreateScope();
        var subscriber = CreateSubscriber();

        Guid productId = Guid.NewGuid();
        string json = $$"""{"ProductId":"{{productId}}","Name":"Test","Price":10000,"Category":"Test"}""";

        await subscriber.SyncProductUpsertAsync(ParseJson(json), dbContext, scope.ServiceProvider, CancellationToken.None);

        Product? product = await dbContext.Products.IgnoreQueryFilters()
            .FirstOrDefaultAsync(p => p.Id == productId);
        Assert.Null(product); // Not created — fail-safe
        Assert.Equal(Guid.Empty, _tenantProvider.TenantId); // No bypass
    }

    [Fact]
    public async Task ProductSync_Delete_With_Correct_Tenant_Soft_Deletes_Product()
    {
        var (scope, dbContext) = CreateScope();

        Guid productId = Guid.NewGuid();
        var seeded = SeedProduct(productId, TenantA);
        dbContext.Products.Add(seeded);
        await dbContext.SaveChangesAsync();

        var subscriber = CreateSubscriber();
        string json = $$"""{"ProductId":"{{productId}}","TenantId":"{{TenantA}}"}""";

        await subscriber.SyncProductDeletedAsync(ParseJson(json), dbContext, scope.ServiceProvider, CancellationToken.None);

        Product? deleted = await dbContext.Products.IgnoreQueryFilters()
            .FirstOrDefaultAsync(p => p.Id == productId);
        Assert.NotNull(deleted);
        Assert.True(deleted!.IsDeleted);
    }

    [Fact]
    public async Task ProductSync_Delete_Missing_TenantId_Refuses_To_Process_FailSafe()
    {
        var (scope, dbContext) = CreateScope();

        Guid productId = Guid.NewGuid();
        var seeded = SeedProduct(productId, TenantA);
        dbContext.Products.Add(seeded);
        await dbContext.SaveChangesAsync();

        var subscriber = CreateSubscriber();
        string json = $$"""{"ProductId":"{{productId}}"}""";

        await subscriber.SyncProductDeletedAsync(ParseJson(json), dbContext, scope.ServiceProvider, CancellationToken.None);

        Product? product = await dbContext.Products.IgnoreQueryFilters()
            .FirstOrDefaultAsync(p => p.Id == productId);
        Assert.NotNull(product);
        Assert.False(product!.IsDeleted); // Not deleted — fail-safe
    }

    [Fact]
    public async Task ProductSync_Delete_Wrong_Tenant_Does_Not_Find_Product_QueryFilter()
    {
        var (scope, dbContext) = CreateScope();

        Guid productId = Guid.NewGuid();
        var seeded = SeedProduct(productId, TenantA);
        dbContext.Products.Add(seeded);
        await dbContext.SaveChangesAsync();

        var subscriber = CreateSubscriber();
        // TenantB context → query filter hides TenantA's product
        string json = $$"""{"ProductId":"{{productId}}","TenantId":"{{TenantB}}"}""";

        await subscriber.SyncProductDeletedAsync(ParseJson(json), dbContext, scope.ServiceProvider, CancellationToken.None);

        Product? product = await dbContext.Products.IgnoreQueryFilters()
            .FirstOrDefaultAsync(p => p.Id == productId);
        Assert.NotNull(product);
        Assert.False(product!.IsDeleted); // Not deleted — tenant isolation enforced
    }

    // ──────────────────────────────────────────────────────────────
    // ORDER STATUS SYNC TESTS
    // ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task OrderSync_Status_Change_With_Correct_Tenant_Updates_Status()
    {
        var (scope, dbContext) = CreateScope();

        Guid orderId = Guid.NewGuid();
        var order = Order.Create(orderId, new TenantId(TenantA), null, new List<OrderItem>());
        dbContext.Orders.Add(order);
        await dbContext.SaveChangesAsync();

        var subscriber = CreateSubscriber();
        string json = $$"""{"orderId":"{{orderId}}","tenantId":"{{TenantA}}","oldStatus":"pending","newStatus":"confirmed","timestamp":"2026-07-18T01:00:00Z"}""";

        await subscriber.SyncOrderStatusAsync(ParseJson(json), dbContext, scope.ServiceProvider, CancellationToken.None);

        Order? updated = await dbContext.Orders.IgnoreQueryFilters()
            .FirstOrDefaultAsync(o => o.Id == orderId);
        Assert.NotNull(updated);
        Assert.Equal("confirmed", updated!.Status.Value);
    }

    [Fact]
    public async Task OrderSync_Status_Change_Missing_TenantId_Refuses_To_Process_FailSafe()
    {
        var (scope, dbContext) = CreateScope();

        Guid orderId = Guid.NewGuid();
        var order = Order.Create(orderId, new TenantId(TenantA), null, new List<OrderItem>());
        dbContext.Orders.Add(order);
        await dbContext.SaveChangesAsync();

        var subscriber = CreateSubscriber();
        string json = $$"""{"orderId":"{{orderId}}","oldStatus":"pending","newStatus":"confirmed","timestamp":"2026-07-18T01:00:00Z"}""";

        await subscriber.SyncOrderStatusAsync(ParseJson(json), dbContext, scope.ServiceProvider, CancellationToken.None);

        Order? orderCheck = await dbContext.Orders.IgnoreQueryFilters()
            .FirstOrDefaultAsync(o => o.Id == orderId);
        Assert.NotNull(orderCheck);
        Assert.Equal("pending", orderCheck!.Status.Value); // Still pending — fail-safe
    }

    [Fact]
    public async Task OrderSync_Status_Change_Wrong_Tenant_Does_Not_Find_Order_QueryFilter()
    {
        var (scope, dbContext) = CreateScope();

        Guid orderId = Guid.NewGuid();
        var order = Order.Create(orderId, new TenantId(TenantA), null, new List<OrderItem>());
        dbContext.Orders.Add(order);
        await dbContext.SaveChangesAsync();

        var subscriber = CreateSubscriber();
        string json = $$"""{"orderId":"{{orderId}}","tenantId":"{{TenantB}}","oldStatus":"pending","newStatus":"confirmed","timestamp":"2026-07-18T01:00:00Z"}""";

        await subscriber.SyncOrderStatusAsync(ParseJson(json), dbContext, scope.ServiceProvider, CancellationToken.None);

        Order? orderCheck = await dbContext.Orders.IgnoreQueryFilters()
            .FirstOrDefaultAsync(o => o.Id == orderId);
        Assert.NotNull(orderCheck);
        Assert.Equal("pending", orderCheck!.Status.Value); // Still pending — tenant isolation
    }

    [Fact]
    public async Task OrderSync_Completed_Missing_TenantId_Refuses_To_Process_FailSafe()
    {
        var (scope, dbContext) = CreateScope();

        Guid orderId = Guid.NewGuid();
        var order = Order.Create(orderId, new TenantId(TenantA), null, new List<OrderItem>());
        dbContext.Orders.Add(order);
        await dbContext.SaveChangesAsync();

        var subscriber = CreateSubscriber();
        string json = $$"""{"OrderId":"{{orderId}}","CustomerId":null,"TotalAmount":50000}""";

        await subscriber.SyncOrderCompletedAsync(ParseJson(json), dbContext, scope.ServiceProvider, CancellationToken.None);

        Order? orderCheck = await dbContext.Orders.IgnoreQueryFilters()
            .FirstOrDefaultAsync(o => o.Id == orderId);
        Assert.NotNull(orderCheck);
        Assert.NotEqual("completed", orderCheck!.Status.Value);
    }

    [Fact]
    public async Task OrderSync_Created_Missing_TenantId_Refuses_To_Process_FailSafe()
    {
        var (scope, dbContext) = CreateScope();

        Guid orderId = Guid.NewGuid();
        var subscriber = CreateSubscriber();
        string json = $$"""{"OrderId":"{{orderId}}","Status":"pending","TotalAmount":50000,"Items":[]}""";

        await subscriber.SyncOrderCreatedAsync(ParseJson(json), dbContext, scope.ServiceProvider, CancellationToken.None);

        Order? orderCheck = await dbContext.Orders.IgnoreQueryFilters()
            .FirstOrDefaultAsync(o => o.Id == orderId);
        Assert.Null(orderCheck); // Not created — fail-safe
    }
}

/// <summary>
/// Test ITenantProvider implementation — supports SetTenant for sync handler tests.
/// </summary>
internal class TestTenantProvider : ITenantProvider
{
    public Guid TenantId { get; private set; } = Guid.Empty;
    public string? CurrentUser => "test-user";
    public bool HasTenant => TenantId != Guid.Empty;
    public void SetTenant(Guid tenantId) => TenantId = tenantId;
}

