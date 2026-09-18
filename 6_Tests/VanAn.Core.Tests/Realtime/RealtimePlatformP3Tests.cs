using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using VanAn.CoreHub.Infrastructure;
using VanAn.CoreHub.Services;
using VanAn.Shared.Domain;
using Xunit;
using TenantAggregate = VanAn.Shared.Domain.Aggregates.TenantAggregate;

namespace VanAn.Core.Tests.Realtime;

/// <summary>
/// Realtime Platform P3 (2026-09-17): the subject → tenant resolver that gives generic pings a
/// tenant without trusting the caller. SQLite in-memory, same harness as the P2 tests.
/// </summary>
public class RealtimePlatformP3Tests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly VanAnDbContext _context;
    private readonly RealtimeSubjectResolver _resolver;

    private static readonly Guid TenantGuid = Guid.Parse("00000000-0000-0000-0000-000000000001");
    private static readonly TenantId Tenant = new(TenantGuid);
    private static readonly Guid ShipperId = Guid.NewGuid();
    private static readonly Guid CustomerId = Guid.NewGuid();

    public RealtimePlatformP3Tests()
    {
        _connection = new SqliteConnection($"DataSource=test_{Guid.NewGuid()};Mode=Memory;Cache=Shared");
        _connection.Open();

        var efServiceProvider = new ServiceCollection().AddEntityFrameworkSqlite().BuildServiceProvider();

        var options = new DbContextOptionsBuilder<VanAnDbContext>()
            .UseInternalServiceProvider(efServiceProvider).UseSqlite(_connection)
            .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        _context = new VanAnDbContext(options);
        _context.Database.EnsureCreated();

        _resolver = new RealtimeSubjectResolver(_context, NullLogger<RealtimeSubjectResolver>.Instance);
    }

    public void Dispose()
    {
        _context.Dispose();
        _connection.Dispose();
    }

    // === T1: an Order subject resolves to the order's tenant ===
    [Fact(DisplayName = "T1: ResolveTenant_OrderSubject_ReturnsOrderTenant")]
    public async Task ResolveTenant_OrderSubject_ReturnsOrderTenant()
    {
        var orderId = await SeedOrderAsync();

        var tenant = await _resolver.ResolveTenantAsync(RealtimeSubjectType.Order, orderId);

        Assert.NotNull(tenant);
        Assert.Equal(TenantGuid, tenant!.Value);
    }

    // === T2: a Delivery subject resolves to the DeliveryTask's tenant ===
    [Fact(DisplayName = "T2: ResolveTenant_DeliverySubject_ReturnsTaskTenant")]
    public async Task ResolveTenant_DeliverySubject_ReturnsTaskTenant()
    {
        var orderId = await SeedOrderAsync();
        var task = new DeliveryTask(Tenant, orderId, ShipperId, 10.8, 106.7);
        _context.DeliveryTasks.Add(task);
        await _context.SaveChangesAsync();

        var tenant = await _resolver.ResolveTenantAsync(RealtimeSubjectType.Delivery, task.Id);

        Assert.NotNull(tenant);
        Assert.Equal(TenantGuid, tenant!.Value);
    }

    // === T3: a Shop subject IS the tenant — accepted only when that tenant exists ===
    [Fact(DisplayName = "T3: ResolveTenant_ShopSubject_ReturnsTenantOnlyWhenItExists")]
    public async Task ResolveTenant_ShopSubject_ReturnsTenantOnlyWhenItExists()
    {
        var shopTenantId = Guid.NewGuid();
        _context.Tenants.Add(TenantAggregate.Tenant.CreateCompany(new TenantId(shopTenantId), "Shop A"));
        await _context.SaveChangesAsync();

        var found = await _resolver.ResolveTenantAsync(RealtimeSubjectType.Shop, shopTenantId);
        var missing = await _resolver.ResolveTenantAsync(RealtimeSubjectType.Shop, Guid.NewGuid());

        Assert.NotNull(found);
        Assert.Equal(shopTenantId, found!.Value);
        Assert.Null(missing);
    }

    // === T4: unknown / unresolvable subjects yield null, never a guessed tenant ===
    [Fact(DisplayName = "T4: ResolveTenant_UnknownOrMissingSubject_ReturnsNull")]
    public async Task ResolveTenant_UnknownOrMissingSubject_ReturnsNull()
    {
        // Subject type with no entity behind it yet (R3 reserved values).
        Assert.Null(await _resolver.ResolveTenantAsync(RealtimeSubjectType.JobApplication, Guid.NewGuid()));
        Assert.Null(await _resolver.ResolveTenantAsync(RealtimeSubjectType.Custom, Guid.NewGuid()));

        // Known subject type, unknown id.
        Assert.Null(await _resolver.ResolveTenantAsync(RealtimeSubjectType.Order, Guid.NewGuid()));

        // Empty id is rejected outright.
        Assert.Null(await _resolver.ResolveTenantAsync(RealtimeSubjectType.Order, Guid.Empty));
    }

    private async Task<Guid> SeedOrderAsync()
    {
        var customer = new Customer(Tenant, "Test Customer", "0901234567");
        SetProp(customer, "Id", CustomerId);
        _context.Customers.Add(customer);

        var orderId = Guid.NewGuid();
        var order = new Order(Tenant, null, 0);
        SetProp(order, "Id", orderId);
        SetProp(order, "OrderId", new OrderId(orderId));
        SetProp(order, "OrderType", "DELIVERY");
        SetProp(order, "CustomerId", CustomerId);
        _context.Orders.Add(order);

        await _context.SaveChangesAsync();
        return orderId;
    }

    private static void SetProp<T>(T obj, string propName, object value)
        => typeof(T).GetProperty(propName)?.SetValue(obj, value);
}
