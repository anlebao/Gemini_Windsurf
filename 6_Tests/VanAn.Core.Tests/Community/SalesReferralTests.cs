using VanAn.Shared.Domain;
using Xunit;

namespace VanAn.Core.Tests.Community
{
    /// <summary>
    /// SalesReferral entity tests (Community Commerce Sprint 0 v1.1).
    /// Cases 15-16: composite referral, per-product commission, app-install bonus.
    /// </summary>
    public class SalesReferralTests
    {
        [Fact(DisplayName = "15: SalesReferral_AttachToOrder_CommissionFromProductConfig")]
        public void SalesReferral_AttachToOrder_CommissionFromProductConfig()
        {
            var tenantId = new TenantId(Guid.NewGuid());
            var salesmanId = Guid.NewGuid();
            var productId = Guid.NewGuid();
            var referral = new SalesReferral(tenantId, salesmanId, "ABC234", productId, "P001");

            var orderId = Guid.NewGuid();
            var customerId = Guid.NewGuid();
            var orderTotal = 100_000m;
            var commissionRate = 0.03m; // 3% per-product

            referral.AttachToOrder(orderId, customerId, orderTotal, commissionRate);

            Assert.Equal(orderId, referral.OrderId);
            Assert.Equal(customerId, referral.ReferredCustomerId);
            Assert.Equal(commissionRate, referral.CommissionRate);
            Assert.Equal(3_000m, referral.CommissionAmount); // 100,000 * 0.03
            Assert.Equal(CommissionStatus.Pending, referral.CommissionStatus);
        }

        [Fact(DisplayName = "16: SalesReferral_AttachAppInstallBonus_SetsBonusAmount")]
        public void SalesReferral_AttachAppInstallBonus_SetsBonusAmount()
        {
            var tenantId = new TenantId(Guid.NewGuid());
            var referral = new SalesReferral(tenantId, Guid.NewGuid(), "ABC234", Guid.NewGuid(), "P001");

            var attributionId = Guid.NewGuid();
            var bonusAmount = 20_000m;

            referral.AttachAppInstallBonus(attributionId, bonusAmount);

            Assert.Equal(attributionId, referral.AppInstallAttributionId);
            Assert.Equal(bonusAmount, referral.AppInstallBonusAmount);
            Assert.Equal(BonusStatus.Pending, referral.AppInstallBonusStatus);
        }
    }
}
