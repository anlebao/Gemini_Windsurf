using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using VanAn.CoreHub.Infrastructure;
using VanAn.CoreHub.Services;
using VanAn.Shared.Domain;
using VanAn.Shared.Domain.Aggregates.TenantAggregate;
using Xunit;
using Tenant = VanAn.Shared.Domain.Aggregates.TenantAggregate.Tenant;

using Microsoft.Extensions.DependencyInjection;
namespace VanAn.Core.Tests.Community;

/// <summary>
/// CC-S1-T1/T2 (Sprint 1): CommunityOrderService unit tests — Haversine, nearby orders, accept.
/// 10 test cases per detailed plan Section 3. Uses SQLite in-memory (kept open per test).
/// </summary>
public class CommunityOrderServiceTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly VanAnDbContext _context;
    private readonly CommunityOrderService _service;
    private static readonly Guid ShipperId = Guid.NewGuid();
    private static readonly Guid Tenant1Id = Guid.Parse("00000000-0000-0000-0000-000000000001");
    private static readonly Guid Tenant2Id = Guid.Parse("00000000-0000-0000-0000-000000000002");

    public CommunityOrderServiceTests()
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
        _service = new CommunityOrderService(_context, NullLogger<CommunityOrderService>.Instance);
    }

    public void Dispose()
    {
        _context.Dispose();
        _connection.Dispose();
    }

    private static Order CreateDeliveryOrder(
        Guid id, Guid tenantId, string status, decimal total = 100000,
        string? deliveryAddress = "123 Test St")
    {
        var order = new Order(new TenantId(tenantId), null, 0);
        SetProp(order, "Id", id);
        SetProp(order, "OrderId", new OrderId(id));
        SetProp(order, "OrderType", "DELIVERY");
        SetProp(order, "Status", new OrderStatusId(status));
        SetProp(order, "TotalAmount", total);
        SetProp(order, "DeliveryAddress", deliveryAddress);
        return order;
    }

    private static Order CreateDineInOrder(Guid id, Guid tenantId, string status)
    {
        var order = new Order(new TenantId(tenantId), null, 0);
        SetProp(order, "Id", id);
        SetProp(order, "OrderId", new OrderId(id));
        SetProp(order, "OrderType", "DINEIN");
        SetProp(order, "Status", new OrderStatusId(status));
        SetProp(order, "TotalAmount", 50000m);
        return order;
    }

    private static void SetProp<T>(T obj, string propName, object value)
    {
        typeof(T).GetProperty(propName)?.SetValue(obj, value);
    }

    private async Task SeedTenantAsync(Guid id, string name, double lat, double lng)
    {
        var tenant = Tenant.CreateCompany(new TenantId(id), name,
            TenantSettings.Empty().WithCoordinates(lat, lng));
        _context.Tenants.Add(tenant);
        await _context.SaveChangesAsync();
    }

    private async Task SeedOrderAsync(Order order)
    {
        _context.Orders.Add(order);
        await _context.SaveChangesAsync();
    }

    private async Task SeedDeliveryTaskAsync(DeliveryTask task)
    {
        _context.DeliveryTasks.Add(task);
        await _context.SaveChangesAsync();
    }

    // === Haversine tests ===

    [Fact(DisplayName = "T1: Haversine_SamePoint_ReturnsZero")]
    public async Task Haversine_SamePoint_ReturnsZero()
    {
        var lat = 10.8;
        var lng = 106.7;
        var orderId = Guid.NewGuid();
        await SeedTenantAsync(Tenant1Id, "Shop A", lat, lng);
        await SeedOrderAsync(CreateDeliveryOrder(orderId, Tenant1Id, "ready"));

        var result = await _service.GetNearbyOrdersAsync(lat, lng, 100, ShipperId);

        Assert.Single(result);
        Assert.Equal(0, result[0].DistanceKm);
    }

    [Fact(DisplayName = "T2: Haversine_KnownDistance_ReturnsCorrect (HCM→HN ~1080km)")]
    public async Task Haversine_KnownDistance_ReturnsCorrect()
    {
        // HCM: 10.7769, 106.7009 — HN: 21.0285, 105.8542 → ~1080km
        var hcmLat = 10.7769;
        var hcmLng = 106.7009;
        var hnLat = 21.0285;
        var hnLng = 105.8542;

        await SeedTenantAsync(Tenant1Id, "HN Shop", hnLat, hnLng);
        await SeedOrderAsync(CreateDeliveryOrder(Guid.NewGuid(), Tenant1Id, "ready"));

        var result = await _service.GetNearbyOrdersAsync(hcmLat, hcmLng, 2000, ShipperId);

        Assert.Single(result);
        // Allow tolerance — Haversine HCM→HN is ~1080km
        Assert.InRange(result[0].DistanceKm, 1000, 1200);
    }

    // === GetNearbyOrders filter tests ===

    [Fact(DisplayName = "T3: GetNearbyOrders_FiltersByRadius")]
    public async Task GetNearbyOrders_FiltersByRadius()
    {
        var shipperLat = 10.8;
        var shipperLng = 106.7;
        await SeedTenantAsync(Tenant1Id, "Near Shop", 10.801, 106.701);
        await SeedTenantAsync(Tenant2Id, "Far Shop", 11.0, 107.0);
        await SeedOrderAsync(CreateDeliveryOrder(Guid.NewGuid(), Tenant1Id, "ready"));
        await SeedOrderAsync(CreateDeliveryOrder(Guid.NewGuid(), Tenant2Id, "ready"));

        var result = await _service.GetNearbyOrdersAsync(shipperLat, shipperLng, 5, ShipperId);

        Assert.Single(result);
        Assert.Equal("Near Shop", result[0].ShopName);
    }

    [Fact(DisplayName = "T4: GetNearbyOrders_OnlyDeliveryType")]
    public async Task GetNearbyOrders_OnlyDeliveryType()
    {
        var lat = 10.8;
        var lng = 106.7;
        await SeedTenantAsync(Tenant1Id, "Shop A", lat, lng);
        await SeedOrderAsync(CreateDeliveryOrder(Guid.NewGuid(), Tenant1Id, "ready"));
        await SeedOrderAsync(CreateDineInOrder(Guid.NewGuid(), Tenant1Id, "ready")); // excluded

        var result = await _service.GetNearbyOrdersAsync(lat, lng, 100, ShipperId);

        Assert.Single(result);
    }

    [Fact(DisplayName = "T5: GetNearbyOrders_OnlyConfirmedOrReady")]
    public async Task GetNearbyOrders_OnlyConfirmedOrReady()
    {
        var lat = 10.8;
        var lng = 106.7;
        await SeedTenantAsync(Tenant1Id, "Shop A", lat, lng);
        await SeedOrderAsync(CreateDeliveryOrder(Guid.NewGuid(), Tenant1Id, "confirmed"));
        await SeedOrderAsync(CreateDeliveryOrder(Guid.NewGuid(), Tenant1Id, "ready"));
        await SeedOrderAsync(CreateDeliveryOrder(Guid.NewGuid(), Tenant1Id, "preparing")); // excluded
        await SeedOrderAsync(CreateDeliveryOrder(Guid.NewGuid(), Tenant1Id, "completed"));  // excluded

        var result = await _service.GetNearbyOrdersAsync(lat, lng, 100, ShipperId);

        Assert.Equal(2, result.Count);
        Assert.All(result, r => Assert.Contains(r.Status, new[] { "confirmed", "ready" }));
    }

    [Fact(DisplayName = "T6: GetNearbyOrders_ExcludesAssigned")]
    public async Task GetNearbyOrders_ExcludesAssigned()
    {
        var lat = 10.8;
        var lng = 106.7;
        var assignedOrderId = Guid.NewGuid();
        var freeOrderId = Guid.NewGuid();
        await SeedTenantAsync(Tenant1Id, "Shop A", lat, lng);
        await SeedOrderAsync(CreateDeliveryOrder(assignedOrderId, Tenant1Id, "ready"));
        await SeedOrderAsync(CreateDeliveryOrder(freeOrderId, Tenant1Id, "ready"));
        await SeedDeliveryTaskAsync(new DeliveryTask(new TenantId(Tenant1Id), assignedOrderId, Guid.NewGuid(), lat, lng));

        var result = await _service.GetNearbyOrdersAsync(lat, lng, 100, ShipperId);

        Assert.Single(result);
        Assert.Equal(freeOrderId, result[0].OrderId);
    }

    [Fact(DisplayName = "T7: GetNearbyOrders_SortsByDistance")]
    public async Task GetNearbyOrders_SortsByDistance()
    {
        var shipperLat = 10.8;
        var shipperLng = 106.7;
        await SeedTenantAsync(Tenant1Id, "Near Shop", 10.81, 106.71);
        await SeedTenantAsync(Tenant2Id, "Far Shop", 10.85, 106.75);
        await SeedOrderAsync(CreateDeliveryOrder(Guid.NewGuid(), Tenant1Id, "ready"));
        await SeedOrderAsync(CreateDeliveryOrder(Guid.NewGuid(), Tenant2Id, "ready"));

        var result = await _service.GetNearbyOrdersAsync(shipperLat, shipperLng, 100, ShipperId);

        Assert.Equal(2, result.Count);
        Assert.True(result[0].DistanceKm <= result[1].DistanceKm);
        Assert.Equal("Near Shop", result[0].ShopName);
    }

    // === AcceptOrder tests ===

    [Fact(DisplayName = "T8: AcceptOrder_CreatesDeliveryTask")]
    public async Task AcceptOrder_CreatesDeliveryTask()
    {
        var orderId = Guid.NewGuid();
        var lat = 10.8;
        var lng = 106.7;
        await SeedTenantAsync(Tenant1Id, "Shop A", lat, lng);
        await SeedOrderAsync(CreateDeliveryOrder(orderId, Tenant1Id, "ready"));

        var result = await _service.AcceptOrderAsync(orderId, ShipperId);

        Assert.NotNull(result);
        Assert.Equal(orderId, result!.OrderId);
        Assert.Equal(ShipperId, result.ShipperId);
        Assert.Equal(DeliveryTaskStatus.Assigned, result.Status);

        // Verify order updated in DB
        var updatedOrder = await _context.Orders.IgnoreQueryFilters().FirstAsync(o => o.Id == orderId);
        Assert.Equal(ShipperId, updatedOrder.ShipperId);
        Assert.Equal("delivering", updatedOrder.Status.Value);
    }

    [Fact(DisplayName = "T9: AcceptOrder_AlreadyAssigned_ReturnsNull")]
    public async Task AcceptOrder_AlreadyAssigned_ReturnsNull()
    {
        var orderId = Guid.NewGuid();
        var lat = 10.8;
        var lng = 106.7;
        await SeedTenantAsync(Tenant1Id, "Shop A", lat, lng);
        await SeedOrderAsync(CreateDeliveryOrder(orderId, Tenant1Id, "ready"));
        await SeedDeliveryTaskAsync(new DeliveryTask(new TenantId(Tenant1Id), orderId, Guid.NewGuid(), lat, lng));

        var result = await _service.AcceptOrderAsync(orderId, ShipperId);

        Assert.Null(result); // 409 Conflict
    }

    [Fact(DisplayName = "T10: AcceptOrder_InvalidStatus_ReturnsNull")]
    public async Task AcceptOrder_InvalidStatus_ReturnsNull()
    {
        var orderId = Guid.NewGuid();
        var lat = 10.8;
        var lng = 106.7;
        await SeedTenantAsync(Tenant1Id, "Shop A", lat, lng);
        await SeedOrderAsync(CreateDeliveryOrder(orderId, Tenant1Id, "completed")); // not accept-able

        var result = await _service.AcceptOrderAsync(orderId, ShipperId);

        Assert.Null(result);
    }
}
