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

            // Too low (< 0) — Issue #178 widened range to 0-0.5
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                new ProductReferralConfig(tenantId, productId, -0.01m, 20_000m));

            // Too high (> 0.5)
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                new ProductReferralConfig(tenantId, productId, 0.51m, 20_000m));
        }

        [Fact(DisplayName = "18b: ProductReferralConfig_Create_ZeroRate_Accepted (Issue #178 — Free/Charity)")]
        public void ProductReferralConfig_Create_ZeroRate_Accepted()
        {
            var tenantId = new TenantId(Guid.NewGuid());
            var productId = Guid.NewGuid();

            var config = new ProductReferralConfig(tenantId, productId, 0m, 0m, "P-FREE");

            Assert.Equal(0m, config.CommissionRate);
            Assert.Equal(0m, config.AppInstallBonus);
            Assert.True(config.IsActive);
        }
    }
}
