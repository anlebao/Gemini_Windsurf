using VanAn.Shared.Domain;
using Xunit;

namespace VanAn.Core.Tests.Community
{
    /// <summary>
    /// DeliveryTask entity tests (Community Commerce Sprint 0).
    /// Cases 5-10: state machine transitions, invalid transitions, MarkFailed.
    /// </summary>
    public class DeliveryTaskTests
    {
        private static DeliveryTask CreateTask()
        {
            var tenantId = new TenantId(Guid.NewGuid());
            return new DeliveryTask(tenantId, Guid.NewGuid(), Guid.NewGuid(), 10.0, 106.0);
        }

        [Fact(DisplayName = "5: DeliveryTask_Create_Status_Assigned")]
        public void DeliveryTask_Create_Status_Assigned()
        {
            var task = CreateTask();
            Assert.Equal(DeliveryTaskStatus.Assigned, task.Status);
            Assert.NotEqual(DateTime.MinValue, task.AssignedAt);
        }

        [Fact(DisplayName = "6: DeliveryTask_Transition_AssignedToPickedUp")]
        public void DeliveryTask_Transition_AssignedToPickedUp()
        {
            var task = CreateTask();
            task.MarkPickedUp();
            Assert.Equal(DeliveryTaskStatus.PickedUp, task.Status);
            Assert.NotNull(task.PickedUpAt);
        }

        [Fact(DisplayName = "7: DeliveryTask_Transition_PickedUpToOutForDelivery")]
        public void DeliveryTask_Transition_PickedUpToOutForDelivery()
        {
            var task = CreateTask();
            task.MarkPickedUp();
            task.MarkOutForDelivery();
            Assert.Equal(DeliveryTaskStatus.OutForDelivery, task.Status);
            Assert.NotNull(task.OutForDeliveryAt);
        }

        [Fact(DisplayName = "8: DeliveryTask_Transition_OutForDeliveryToDelivered")]
        public void DeliveryTask_Transition_OutForDeliveryToDelivered()
        {
            var task = CreateTask();
            task.MarkPickedUp();
            task.MarkOutForDelivery();
            task.MarkDelivered();
            Assert.Equal(DeliveryTaskStatus.Delivered, task.Status);
            Assert.NotNull(task.DeliveredAt);
        }

        [Fact(DisplayName = "9: DeliveryTask_Transition_InvalidThrows")]
        public void DeliveryTask_Transition_InvalidThrows()
        {
            var task = CreateTask();
            task.MarkPickedUp();
            task.MarkOutForDelivery();
            task.MarkDelivered();

            // Delivered → PickedUp is invalid
            Assert.Throws<InvalidOperationException>(() => task.MarkPickedUp());
        }

        [Fact(DisplayName = "10: DeliveryTask_MarkFailed_WithReason")]
        public void DeliveryTask_MarkFailed_WithReason()
        {
            var task = CreateTask();
            task.MarkPickedUp();
            task.MarkFailed("Customer not at home");
            Assert.Equal(DeliveryTaskStatus.Failed, task.Status);
            Assert.Equal("Customer not at home", task.FailureReason);
            Assert.NotNull(task.FailedAt);
        }
    }
}
