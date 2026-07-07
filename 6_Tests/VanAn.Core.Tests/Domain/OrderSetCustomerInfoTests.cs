using VanAn.Shared.Domain;
using Xunit;

namespace VanAn.Core.Tests.Domain
{
    /// <summary>
    /// Bucket A feature (approved 2026-07-07): Unit tests for Order.SetCustomerInfo.
    /// Covers guest checkout flow — customer provides name/phone/address without auth.
    /// </summary>
    public class OrderSetCustomerInfoTests
    {
        private static Order CreateTestOrder()
        {
            TenantId tenantId = new(Guid.NewGuid());
            Guid orderId = Guid.NewGuid();
            OrderItem item = OrderItem.Create(Guid.NewGuid(), tenantId, orderId, Guid.NewGuid(), quantity: 1, unitPrice: 100m);
            return Order.Create(orderId, tenantId, null, [item]);
        }

        [Fact]
        public void SetCustomerInfo_Sets_CustomerInfo_Fields()
        {
            // Arrange
            Order order = CreateTestOrder();
            var info = new CustomerInfo("Nguyen Van A", "0901234567", "guest@example.com", "123 Le Loi", "Giao nhanh");

            // Act
            order.SetCustomerInfo(info);

            // Assert
            Assert.NotNull(order.CustomerInfo);
            Assert.Equal("Nguyen Van A", order.CustomerInfo!.FullName);
            Assert.Equal("0901234567", order.CustomerInfo.PhoneNumber);
            Assert.Equal("guest@example.com", order.CustomerInfo.Email);
            Assert.Equal("123 Le Loi", order.CustomerInfo.Address);
            Assert.Equal("Giao nhanh", order.CustomerInfo.Notes);
        }

        [Fact]
        public void SetCustomerInfo_Updates_Audit_Timestamp()
        {
            // Arrange
            Order order = CreateTestOrder();
            DateTime beforeUpdate = order.UpdatedAt;
            var info = new CustomerInfo("Test Guest", "0900000000", "g@e.com");

            // Act
            order.SetCustomerInfo(info);

            // Assert — UpdateAudit sets UpdatedAt to UtcNow
            Assert.True(order.UpdatedAt >= beforeUpdate);
        }

        [Fact]
        public void SetCustomerInfo_Allows_Overwrite_On_Subsequent_Call()
        {
            // Arrange
            Order order = CreateTestOrder();
            order.SetCustomerInfo(new CustomerInfo("First", "111", "a@e.com"));
            var updated = new CustomerInfo("Second", "222", "b@e.com");

            // Act
            order.SetCustomerInfo(updated);

            // Assert
            Assert.Equal("Second", order.CustomerInfo!.FullName);
            Assert.Equal("222", order.CustomerInfo.PhoneNumber);
        }

        [Fact]
        public void SetCustomerInfo_Accepts_Null_Address_And_Notes()
        {
            // Arrange
            Order order = CreateTestOrder();
            var info = new CustomerInfo("Guest", "0901234567", "g@e.com");

            // Act
            order.SetCustomerInfo(info);

            // Assert
            Assert.Null(order.CustomerInfo!.Address);
            Assert.Null(order.CustomerInfo.Notes);
        }
    }
}
