using VanAn.Shared.Domain;
using Xunit;

namespace VanAn.Core.Tests.Domain
{
    /// <summary>
    /// CC-S1: Unit tests for Order.SetOrderType — covers OrderType DELIVERY support
    /// for community commerce shipper delivery flow.
    /// </summary>
    public class OrderSetOrderTypeTests
    {
        private static Order CreateTestOrder()
        {
            TenantId tenantId = new(Guid.NewGuid());
            Guid orderId = Guid.NewGuid();
            OrderItem item = OrderItem.Create(Guid.NewGuid(), tenantId, orderId, Guid.NewGuid(), quantity: 1, unitPrice: 100m);
            return Order.Create(orderId, tenantId, null, [item]);
        }

        [Fact]
        public void SetOrderType_Sets_DINEIN()
        {
            Order order = CreateTestOrder();
            order.SetOrderType("DINEIN");
            Assert.Equal("DINEIN", order.OrderType);
        }

        [Fact]
        public void SetOrderType_Sets_TAKEAWAY()
        {
            Order order = CreateTestOrder();
            order.SetOrderType("TAKEAWAY");
            Assert.Equal("TAKEAWAY", order.OrderType);
        }

        [Fact]
        public void SetOrderType_Sets_DELIVERY()
        {
            Order order = CreateTestOrder();
            order.SetOrderType("DELIVERY");
            Assert.Equal("DELIVERY", order.OrderType);
        }

        [Fact]
        public void SetOrderType_DELIVERY_Sets_DeliveryAddress()
        {
            Order order = CreateTestOrder();
            order.SetOrderType("DELIVERY", deliveryAddress: "123 Le Loi, Q1");
            Assert.Equal("DELIVERY", order.OrderType);
            Assert.Equal("123 Le Loi, Q1", order.DeliveryAddress);
        }

        [Fact]
        public void SetOrderType_DELIVERY_Sets_DeliveryLocation()
        {
            Order order = CreateTestOrder();
            order.SetOrderType("DELIVERY", deliveryLat: 10.966, deliveryLng: 106.594);
            Assert.Equal(10.966, order.DeliveryLat);
            Assert.Equal(106.594, order.DeliveryLng);
        }

        [Fact]
        public void SetOrderType_DELIVERY_Sets_ShippingFee()
        {
            Order order = CreateTestOrder();
            order.SetOrderType("DELIVERY", shippingFee: 25000m);
            Assert.Equal(25000m, order.ShippingFee);
        }

        [Fact]
        public void SetOrderType_Null_Keeps_Default_DINEIN()
        {
            Order order = CreateTestOrder();
            order.SetOrderType(null);
            Assert.Equal("DINEIN", order.OrderType);
        }

        [Fact]
        public void SetOrderType_Empty_Keeps_Default_DINEIN()
        {
            Order order = CreateTestOrder();
            order.SetOrderType("");
            Assert.Equal("DINEIN", order.OrderType);
        }

        [Fact]
        public void SetOrderType_CaseInsensitive_Accepts_Lowercase()
        {
            Order order = CreateTestOrder();
            order.SetOrderType("delivery");
            Assert.Equal("DELIVERY", order.OrderType);
        }

        [Fact]
        public void SetOrderType_Invalid_Throws_ArgumentException()
        {
            Order order = CreateTestOrder();
            Assert.Throws<ArgumentException>(() => order.SetOrderType("INVALID"));
        }

        [Fact]
        public void SetOrderType_Updates_Audit_Timestamp()
        {
            Order order = CreateTestOrder();
            DateTime beforeUpdate = order.UpdatedAt;
            order.SetOrderType("TAKEAWAY");
            Assert.True(order.UpdatedAt >= beforeUpdate);
        }

        [Fact]
        public void SetOrderType_DINEIN_Does_Not_Set_DeliveryFields()
        {
            Order order = CreateTestOrder();
            order.SetOrderType("DINEIN", deliveryAddress: "should be ignored", shippingFee: 999m);
            Assert.Equal("DINEIN", order.OrderType);
            Assert.Null(order.DeliveryAddress);
            Assert.Equal(0m, order.ShippingFee);
        }
    }
}
