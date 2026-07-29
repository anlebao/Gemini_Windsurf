using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using VanAn.CoreHub.Infrastructure;
using VanAn.CoreHub.Services;
using VanAn.Shared.Domain;
using VanAn.Shared.Domain.Aggregates.TenantAggregate;
using Xunit;
using Tenant = VanAn.Shared.Domain.Aggregates.TenantAggregate.Tenant;

namespace VanAn.Core.Tests.Community;

/// <summary>
/// CC-S2 (Sprint 2): DeliveryWorkflowService unit tests — state machine transitions + GPS location recording.
/// 10 test cases per detailed plan Section 6. Uses SQLite in-memory (kept open per test).
/// </summary>
public class DeliveryWorkflowServiceTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly VanAnDbContext _context;
    private readonly DeliveryWorkflowService _service;
    private static readonly Guid ShipperId = Guid.NewGuid();
    private static readonly Guid TenantId = Guid.Parse("00000000-0000-0000-0000-000000000001");

    public DeliveryWorkflowServiceTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<VanAnDbContext>()
            .UseSqlite(_connection)
            .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        _context = new VanAnDbContext(options);
        _context.Database.EnsureCreated();
        _service = new DeliveryWorkflowService(_context, NullLogger<DeliveryWorkflowService>.Instance);
    }

    public void Dispose()
    {
        _context.Dispose();
        _connection.Dispose();
    }

    private static Order CreateDeliveryOrder(Guid id, Guid tenantId, string status)
    {
        var order = new Order(new TenantId(tenantId), null, 0);
        SetProp(order, "Id", id);
        SetProp(order, "OrderId", new OrderId(id));
        SetProp(order, "OrderType", "DELIVERY");
        SetProp(order, "Status", new OrderStatusId(status));
        SetProp(order, "TotalAmount", 100000m);
        SetProp(order, "DeliveryAddress", "123 Test St");
        return order;
    }

    private static void SetProp<T>(T obj, string propName, object value)
    {
        typeof(T).GetProperty(propName)?.SetValue(obj, value);
    }

    private async Task SeedTenantAsync()
    {
        var tenant = Tenant.CreateCompany(new TenantId(TenantId), "Shop A",
            TenantSettings.Empty().WithCoordinates(10.8, 106.7));
        _context.Tenants.Add(tenant);
        await _context.SaveChangesAsync();
    }

    private async Task<(Order order, DeliveryTask task)> SeedOrderWithTaskAsync(string orderStatus = "delivering", DeliveryTaskStatus taskStatus = DeliveryTaskStatus.Assigned)
    {
        var orderId = Guid.NewGuid();
        var order = CreateDeliveryOrder(orderId, TenantId, orderStatus);
        _context.Orders.Add(order);

        var task = new DeliveryTask(new TenantId(TenantId), orderId, ShipperId, 10.8, 106.7, 10.81, 106.71);
        SetProp(task, "Status", taskStatus);
        _context.DeliveryTasks.Add(task);

        await _context.SaveChangesAsync();
        return (order, task);
    }

    // === T1: Transition_PickedUp_FromAssigned_Success ===
    [Fact(DisplayName = "T1: Transition_PickedUp_FromAssigned_Success")]
    public async Task Transition_PickedUp_FromAssigned_Success()
    {
        await SeedTenantAsync();
        var (order, task) = await SeedOrderWithTaskAsync("delivering", DeliveryTaskStatus.Assigned);

        var result = await _service.TransitionStatusAsync(order.Id, DeliveryTaskStatus.PickedUp);

        Assert.NotNull(result);
        Assert.Equal(DeliveryTaskStatus.PickedUp, result!.Status);
        Assert.NotNull(result.PickedUpAt);
    }

    // === T2: Transition_OutForDelivery_FromPickedUp_Success ===
    [Fact(DisplayName = "T2: Transition_OutForDelivery_FromPickedUp_Success")]
    public async Task Transition_OutForDelivery_FromPickedUp_Success()
    {
        await SeedTenantAsync();
        var (order, task) = await SeedOrderWithTaskAsync("delivering", DeliveryTaskStatus.PickedUp);

        var result = await _service.TransitionStatusAsync(order.Id, DeliveryTaskStatus.OutForDelivery);

        Assert.NotNull(result);
        Assert.Equal(DeliveryTaskStatus.OutForDelivery, result!.Status);
        Assert.NotNull(result.OutForDeliveryAt);
    }

    // === T3: Transition_Delivered_FromOutForDelivery_Success ===
    [Fact(DisplayName = "T3: Transition_Delivered_FromOutForDelivery_Success — Order.Completed called")]
    public async Task Transition_Delivered_FromOutForDelivery_Success()
    {
        await SeedTenantAsync();
        var (order, task) = await SeedOrderWithTaskAsync("delivering", DeliveryTaskStatus.OutForDelivery);

        var result = await _service.TransitionStatusAsync(order.Id, DeliveryTaskStatus.Delivered);

        Assert.NotNull(result);
        Assert.Equal(DeliveryTaskStatus.Delivered, result!.Status);
        Assert.NotNull(result.DeliveredAt);

        // Verify Order status updated to "completed"
        var updatedOrder = await _context.Orders.IgnoreQueryFilters().FirstOrDefaultAsync(o => o.Id == order.Id);
        Assert.NotNull(updatedOrder);
        Assert.Equal("completed", updatedOrder!.Status.Value);
    }

    // === T4: Transition_Failed_WithReason ===
    [Fact(DisplayName = "T4: Transition_Failed_WithReason")]
    public async Task Transition_Failed_WithReason()
    {
        await SeedTenantAsync();
        var (order, task) = await SeedOrderWithTaskAsync("delivering", DeliveryTaskStatus.OutForDelivery);

        var result = await _service.TransitionStatusAsync(order.Id, DeliveryTaskStatus.Failed, "Customer not home");

        Assert.NotNull(result);
        Assert.Equal(DeliveryTaskStatus.Failed, result!.Status);
        Assert.Equal("Customer not home", result.FailureReason);
        Assert.NotNull(result.FailedAt);
    }

    // === T5: Transition_InvalidState_Throws (Assigned→Delivered) ===
    [Fact(DisplayName = "T5: Transition_InvalidState_Throws (Assigned→Delivered)")]
    public async Task Transition_InvalidState_Throws()
    {
        await SeedTenantAsync();
        var (order, task) = await SeedOrderWithTaskAsync("delivering", DeliveryTaskStatus.Assigned);

        // Assigned → Delivered is invalid (must go through PickedUp → OutForDelivery → Delivered)
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service.TransitionStatusAsync(order.Id, DeliveryTaskStatus.Delivered));
    }

    // === T6: RecordLocation_CreatesTracking ===
    [Fact(DisplayName = "T6: RecordLocation_CreatesTracking")]
    public async Task RecordLocation_CreatesTracking()
    {
        await SeedTenantAsync();
        var (order, task) = await SeedOrderWithTaskAsync("delivering", DeliveryTaskStatus.OutForDelivery);

        await _service.RecordLocationAsync(task.Id, 10.81, 106.71);

        var trackings = await _context.DeliveryTrackings.IgnoreQueryFilters().ToListAsync();
        Assert.Single(trackings);
        Assert.Equal(10.81, trackings[0].Latitude);
        Assert.Equal(106.71, trackings[0].Longitude);
    }

    // === T7: RecordLocation_AppendOnly (multiple records preserved) ===
    [Fact(DisplayName = "T7: RecordLocation_AppendOnly")]
    public async Task RecordLocation_AppendOnly()
    {
        await SeedTenantAsync();
        var (order, task) = await SeedOrderWithTaskAsync("delivering", DeliveryTaskStatus.OutForDelivery);

        await _service.RecordLocationAsync(task.Id, 10.81, 106.71);
        await _service.RecordLocationAsync(task.Id, 10.82, 106.72);
        await _service.RecordLocationAsync(task.Id, 10.83, 106.73);

        var trackings = await _context.DeliveryTrackings.IgnoreQueryFilters().ToListAsync();
        Assert.Equal(3, trackings.Count);
    }

    // === T8: GetTrackingHistory_SortsByRecordedAt ===
    [Fact(DisplayName = "T8: GetTrackingHistory_SortsByRecordedAt")]
    public async Task GetTrackingHistory_SortsByRecordedAt()
    {
        await SeedTenantAsync();
        var (order, task) = await SeedOrderWithTaskAsync("delivering", DeliveryTaskStatus.OutForDelivery);

        await _service.RecordLocationAsync(task.Id, 10.81, 106.71);
        await Task.Delay(50); // ensure distinct timestamps
        await _service.RecordLocationAsync(task.Id, 10.82, 106.72);
        await Task.Delay(50);
        await _service.RecordLocationAsync(task.Id, 10.83, 106.73);

        var history = await _service.GetTrackingHistoryAsync(task.Id);

        Assert.Equal(3, history.Count);
        // Verify chronological order (ascending by RecordedAt)
        Assert.True(history[0].RecordedAt <= history[1].RecordedAt);
        Assert.True(history[1].RecordedAt <= history[2].RecordedAt);
        // Verify coordinates in insertion order
        Assert.Equal(10.81, history[0].Latitude);
        Assert.Equal(10.83, history[2].Latitude);
    }

    // === T9: Transition_OrderNotFound_ReturnsNull ===
    [Fact(DisplayName = "T9: Transition_OrderNotFound_ReturnsNull")]
    public async Task Transition_OrderNotFound_ReturnsNull()
    {
        await SeedTenantAsync();
        var fakeOrderId = Guid.NewGuid();

        var result = await _service.TransitionStatusAsync(fakeOrderId, DeliveryTaskStatus.PickedUp);

        Assert.Null(result);
    }

    // === T10: Transition_NoActiveTask_ReturnsNull ===
    [Fact(DisplayName = "T10: Transition_NoActiveTask_ReturnsNull (task already Delivered)")]
    public async Task Transition_NoActiveTask_ReturnsNull()
    {
        await SeedTenantAsync();
        var (order, task) = await SeedOrderWithTaskAsync("delivering", DeliveryTaskStatus.OutForDelivery);

        // First transition to Delivered (removes from active set)
        await _service.TransitionStatusAsync(order.Id, DeliveryTaskStatus.Delivered);

        // Now try another transition — no active task
        var result = await _service.TransitionStatusAsync(order.Id, DeliveryTaskStatus.PickedUp);

        Assert.Null(result);
    }
}
