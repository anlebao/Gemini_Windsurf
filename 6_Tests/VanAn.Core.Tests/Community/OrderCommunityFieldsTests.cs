using VanAn.Shared.Domain;
using Xunit;

namespace VanAn.Core.Tests.Community
{
    /// <summary>
    /// Order community fields tests (Community Commerce Sprint 0).
    /// Case 14: 8 new nullable fields default to null (backward compatible).
    /// </summary>
    public class OrderCommunityFieldsTests
    {
        [Fact(DisplayName = "14: Order_NewFields_DefaultNull")]
        public void Order_NewFields_DefaultNull()
        {
            var tenantId = new TenantId(Guid.NewGuid());
            var order = new Order(tenantId, null, 0m);

            Assert.Null(order.ShipperId);
            Assert.Null(order.SalesmanId);
            Assert.Null(order.ReferralCode);
            Assert.Null(order.ReferralProductId);
            Assert.Null(order.DeliveryLat);
            Assert.Null(order.DeliveryLng);
            Assert.Null(order.CodAmount);
            Assert.Null(order.CodCollectedAt);
        }
    }
}
