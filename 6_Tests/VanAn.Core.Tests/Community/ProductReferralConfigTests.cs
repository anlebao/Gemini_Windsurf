using VanAn.Shared.Domain;
using Xunit;

namespace VanAn.Core.Tests.Community
{
    /// <summary>
    /// ProductReferralConfig entity tests (Community Commerce Sprint 0 v1.1).
    /// Cases 17-18: valid creation, invalid commission rate throws.
    /// </summary>
    public class ProductReferralConfigTests
    {
        [Fact(DisplayName = "17: ProductReferralConfig_Create_ValidFields")]
        public void ProductReferralConfig_Create_ValidFields()
        {
            var tenantId = new TenantId(Guid.NewGuid());
            var productId = Guid.NewGuid();
            var config = new ProductReferralConfig(tenantId, productId, 0.03m, 20_000m, "P001");

            Assert.Equal(productId, config.ProductId);
            Assert.Equal(0.03m, config.CommissionRate);
            Assert.Equal(20_000m, config.AppInstallBonus);
            Assert.Equal("P001", config.ProductShortCode);
            Assert.True(config.IsActive);
        }

        [Fact(DisplayName = "18: ProductReferralConfig_Create_InvalidRate_Throws")]
        public void ProductReferralConfig_Create_InvalidRate_Throws()
        {
            var tenantId = new TenantId(Guid.NewGuid());
            var productId = Guid.NewGuid();

            // Too low (< 0.02)
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                new ProductReferralConfig(tenantId, productId, 0.01m, 20_000m));

            // Too high (> 0.05)
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                new ProductReferralConfig(tenantId, productId, 0.06m, 20_000m));
        }
    }
}
