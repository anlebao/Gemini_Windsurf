using VanAn.CoreHub.Services;
using VanAn.Shared.Domain;
using Xunit;

namespace VanAn.Core.Tests.Community;

/// <summary>
/// CC-S1-T0 (v1.3): "delivering" status domain modification tests.
/// Verifies OrderStatuses.Default[] contains "delivering" + transition rules allow shipper accept flow.
/// </summary>
public class DeliveringStatusTests
{
    // T0.1: OrderStatuses.Default contains "delivering" with Sequence=5.
    [Fact(DisplayName = "T0.1: OrderStatuses.Default contains delivering with Sequence=5 (CC-S1-T0 v1.3)")]
    public void OrderStatuses_Default_Contains_Delivering()
    {
        var delivering = OrderStatuses.Default.FirstOrDefault(s => s.Id == new OrderStatusId("delivering"));
        Assert.NotNull(delivering);
        Assert.Equal("delivering", delivering!.Id.Value);
        Assert.Equal("Đang giao", delivering.DisplayName);
        Assert.Equal(5, delivering.Sequence);
        Assert.True(delivering.IsActive);

        // Verify completed shifted to 6, cancelled shifted to 7
        var completed = OrderStatuses.Default.First(s => s.Id == new OrderStatusId("completed"));
        Assert.Equal(6, completed.Sequence);
        var cancelled = OrderStatuses.Default.First(s => s.Id == new OrderStatusId("cancelled"));
        Assert.Equal(7, cancelled.Sequence);
    }

    // T0.2: ready → delivering is valid (shipper accept flow).
    // T0.3: delivering → delivered is valid (shipper completes delivery).
    // These tests instantiate OrderWorkflowService directly with minimal deps (no DB needed for IsTransitionValidAsync).
    // IsTransitionValidAsync(current, new) public overload defaults kitchenEnabled=true (no tenant context).

    [Fact(DisplayName = "T0.2: IsTransitionValid ready→delivering returns true (CC-S1-T0 v1.3)")]
    public async Task IsTransitionValid_Ready_To_Delivering_ReturnsTrue()
    {
        var service = CreateWorkflowService();
        var result = await service.IsTransitionValidAsync(
            new OrderStatusId("ready"),
            new OrderStatusId("delivering"));
        Assert.True(result);
    }

    [Fact(DisplayName = "T0.3: IsTransitionValid delivering→delivered returns true (CC-S1-T0 v1.3)")]
    public async Task IsTransitionValid_Delivering_To_Delivered_ReturnsTrue()
    {
        var service = CreateWorkflowService();
        var result = await service.IsTransitionValidAsync(
            new OrderStatusId("delivering"),
            new OrderStatusId("delivered"));
        Assert.True(result);
    }

    [Fact(DisplayName = "T0.4: IsTransitionValid confirmed→delivering returns true (shipper accept from confirmed)")]
    public async Task IsTransitionValid_Confirmed_To_Delivering_ReturnsTrue()
    {
        var service = CreateWorkflowService();
        var result = await service.IsTransitionValidAsync(
            new OrderStatusId("confirmed"),
            new OrderStatusId("delivering"));
        Assert.True(result);
    }

    [Fact(DisplayName = "T0.5: IsTransitionValid delivering→completed returns true (direct complete)")]
    public async Task IsTransitionValid_Delivering_To_Completed_ReturnsTrue()
    {
        var service = CreateWorkflowService();
        var result = await service.IsTransitionValidAsync(
            new OrderStatusId("delivering"),
            new OrderStatusId("completed"));
        Assert.True(result);
    }

    [Fact(DisplayName = "T0.6: IsTransitionValid delivering→cancelled returns true (cancel in transit)")]
    public async Task IsTransitionValid_Delivering_To_Cancelled_ReturnsTrue()
    {
        var service = CreateWorkflowService();
        var result = await service.IsTransitionValidAsync(
            new OrderStatusId("delivering"),
            new OrderStatusId("cancelled"));
        Assert.True(result);
    }

    /// <summary>
    /// Create OrderWorkflowService with minimal dependencies for IsTransitionValidAsync testing.
    /// IsTransitionValidAsync(current, new) public overload doesn't need tenant context — defaults kitchenEnabled=true.
    /// Only the transition table is used — no DB/repo access in this code path.
    /// </summary>
    private static OrderWorkflowService CreateWorkflowService()
    {
        var logger = Microsoft.Extensions.Logging.Abstractions.NullLogger<OrderWorkflowService>.Instance;
        return new OrderWorkflowService(
            orderRepository: null!,
            logger: logger,
            socialCampaignService: null!,
            loyaltyRewardsService: null!,
            customerRepository: null!,
            natsEventPublisher: null,
            shopFeatureSettingsService: null);
    }
}
