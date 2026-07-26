using VanAn.Shared.Domain;
using Xunit;

namespace VanAn.Core.Tests.Community
{
    /// <summary>
    /// FraudFlag entity tests (Community Commerce Sprint 0 v1.2).
    /// Cases 27-29: creation, Confirm, Dismiss.
    /// </summary>
    public class FraudFlagTests
    {
        private static FraudFlag CreateFlag()
        {
            var tenantId = new TenantId(Guid.NewGuid());
            return new FraudFlag(
                tenantId,
                FraudEntityType.SalesReferral,
                Guid.NewGuid(),
                Guid.NewGuid(),
                FraudFlagType.SelfDeal,
                80,
                "{}",
                "Salesman and customer share fingerprint");
        }

        [Fact(DisplayName = "27: FraudFlag_Create_Status_Pending")]
        public void FraudFlag_Create_Status_Pending()
        {
            var flag = CreateFlag();
            Assert.Equal(FraudFlagStatus.Pending, flag.Status);
            Assert.NotEqual(Guid.Empty, flag.EntityId);
        }

        [Fact(DisplayName = "28: FraudFlag_Confirm_SetsStatusConfirmed")]
        public void FraudFlag_Confirm_SetsStatusConfirmed()
        {
            var flag = CreateFlag();
            var reviewer = Guid.NewGuid();

            flag.Confirm(reviewer, "Confirmed fraud — 3-strike ban applied");

            Assert.Equal(FraudFlagStatus.Confirmed, flag.Status);
            Assert.Equal(reviewer, flag.ReviewedBy);
            Assert.NotNull(flag.ReviewedAt);
            Assert.Equal("Confirmed fraud — 3-strike ban applied", flag.ReviewNote);
        }

        [Fact(DisplayName = "29: FraudFlag_Dismiss_SetsStatusDismissed")]
        public void FraudFlag_Dismiss_SetsStatusDismissed()
        {
            var flag = CreateFlag();
            var reviewer = Guid.NewGuid();

            flag.Dismiss(reviewer, "False positive — same household");

            Assert.Equal(FraudFlagStatus.Dismissed, flag.Status);
            Assert.Equal(reviewer, flag.ReviewedBy);
            Assert.NotNull(flag.ReviewedAt);
            Assert.Equal("False positive — same household", flag.ReviewNote);
        }
    }
}
