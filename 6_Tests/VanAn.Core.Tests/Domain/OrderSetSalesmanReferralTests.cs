using VanAn.Shared.Domain;
using Xunit;

namespace VanAn.Core.Tests.Domain
{
    /// <summary>
    /// CC-S4 fix: Unit tests for Order.SetSalesmanReferral — Sprint 0 created the
    /// SalesmanId/ReferralCode/ReferralProductId fields but no domain method to set them,
    /// so a scanned salesman referral QR could never be attributed to an order.
    /// </summary>
    public class OrderSetSalesmanReferralTests
    {
        private static Order CreateTestOrder()
        {
            TenantId tenantId = new(Guid.NewGuid());
            Guid orderId = Guid.NewGuid();
            OrderItem item = OrderItem.Create(Guid.NewGuid(), tenantId, orderId, Guid.NewGuid(), quantity: 1, unitPrice: 100m);
            return Order.Create(orderId, tenantId, null, [item]);
        }

        [Fact]
        public void SetSalesmanReferral_Sets_AllThreeFields()
        {
            Order order = CreateTestOrder();
            Guid salesmanId = Guid.NewGuid();
            Guid productId = Guid.NewGuid();

            order.SetSalesmanReferral(salesmanId, productId, "ABC123|TR-001");

            Assert.Equal(salesmanId, order.SalesmanId);
            Assert.Equal(productId, order.ReferralProductId);
            Assert.Equal("ABC123|TR-001", order.ReferralCode);
        }

        [Fact]
        public void SetSalesmanReferral_Defaults_Are_Null()
        {
            Order order = CreateTestOrder();

            Assert.Null(order.SalesmanId);
            Assert.Null(order.ReferralProductId);
            Assert.Null(order.ReferralCode);
        }

        [Fact]
        public void SetSalesmanReferral_Empty_SalesmanId_Throws()
        {
            Order order = CreateTestOrder();

            Assert.Throws<ArgumentException>(() =>
                order.SetSalesmanReferral(Guid.Empty, Guid.NewGuid(), "ABC123|TR-001"));
        }

        [Fact]
        public void SetSalesmanReferral_Empty_ProductId_Throws()
        {
            Order order = CreateTestOrder();

            Assert.Throws<ArgumentException>(() =>
                order.SetSalesmanReferral(Guid.NewGuid(), Guid.Empty, "ABC123|TR-001"));
        }
    }
}
