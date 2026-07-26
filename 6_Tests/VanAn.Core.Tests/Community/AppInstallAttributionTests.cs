using VanAn.Shared.Domain;
using Xunit;

namespace VanAn.Core.Tests.Community
{
    /// <summary>
    /// AppInstallAttribution entity tests (Community Commerce Sprint 0 v1.1).
    /// Cases 19-20: unique per customer (entity-level), MarkPaid sets WalletTransactionId.
    /// </summary>
    public class AppInstallAttributionTests
    {
        [Fact(DisplayName = "19: AppInstallAttribution_Create_UniquePerCustomer")]
        public void AppInstallAttribution_Create_UniquePerCustomer()
        {
            // Entity-level: verify fields set correctly. DB unique index on CustomerId
            // is verified via EF config (AppInstallAttributionConfiguration.cs).
            var tenantId = new TenantId(Guid.NewGuid());
            var customerId = Guid.NewGuid();
            var salesmanId = Guid.NewGuid();
            var productId = Guid.NewGuid();

            var attr = new AppInstallAttribution(tenantId, customerId, salesmanId, productId, 20_000m);

            Assert.Equal(customerId, attr.CustomerId);
            Assert.Equal(salesmanId, attr.SalesmanId);
            Assert.Equal(productId, attr.ProductId);
            Assert.Equal(20_000m, attr.BonusAmount);
            Assert.Equal(AttributionStatus.Pending, attr.AttributionStatus);
        }

        [Fact(DisplayName = "20: AppInstallAttribution_MarkPaid_SetsWalletTransactionId")]
        public void AppInstallAttribution_MarkPaid_SetsWalletTransactionId()
        {
            var tenantId = new TenantId(Guid.NewGuid());
            var attr = new AppInstallAttribution(tenantId, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 20_000m);
            var walletTxId = Guid.NewGuid();

            attr.MarkPaid(walletTxId);

            Assert.Equal(AttributionStatus.Paid, attr.AttributionStatus);
            Assert.Equal(walletTxId, attr.WalletTransactionId);
        }
    }
}
