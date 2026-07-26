using VanAn.Shared.Domain;
using Xunit;

namespace VanAn.Core.Tests.Community
{
    /// <summary>
    /// WalletTransaction entity tests (Community Commerce Sprint 0).
    /// Cases 11-13: BalanceAfter calculation, immutability (no update methods), Reversal entry.
    /// </summary>
    public class WalletTransactionTests
    {
        [Fact(DisplayName = "11: WalletTransaction_Create_BalanceAfterCorrect")]
        public void WalletTransaction_Create_BalanceAfterCorrect()
        {
            var tenantId = new TenantId(Guid.NewGuid());
            var ownerId = Guid.NewGuid();
            var balanceBefore = 100m;

            var tx = new WalletTransaction(tenantId, ownerId, WalletTransactionType.CODCollection, 50m, balanceBefore, "COD collection");

            Assert.Equal(150m, tx.BalanceAfter);
        }

        [Fact(DisplayName = "12: WalletTransaction_Immutable_NoUpdateMethod")]
        public void WalletTransaction_Immutable_NoUpdateMethod()
        {
            // Reflection check: no public method whose name starts with "Update"
            var publicMethods = typeof(WalletTransaction)
                .GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.DeclaredOnly)
                .Where(m => m.Name.StartsWith("Update", StringComparison.OrdinalIgnoreCase))
                .ToList();

            Assert.Empty(publicMethods);
        }

        [Fact(DisplayName = "13: WalletTransaction_Reversal_CreatesNegatingEntry")]
        public void WalletTransaction_Reversal_CreatesNegatingEntry()
        {
            var tenantId = new TenantId(Guid.NewGuid());
            var ownerId = Guid.NewGuid();

            // Original COD collection: +50, balance 100 → 150
            var original = new WalletTransaction(tenantId, ownerId, WalletTransactionType.CODCollection, 50m, 100m, "COD collection");

            // Reversal: -50, balance 150 → 100
            var reversal = new WalletTransaction(
                tenantId, ownerId, WalletTransactionType.Reversal, -original.Amount, original.BalanceAfter,
                "Reversal: wrong COD amount", relatedTransactionId: original.Id);

            Assert.Equal(WalletTransactionType.Reversal, reversal.Type);
            Assert.Equal(-50m, reversal.Amount);
            Assert.Equal(original.Id, reversal.RelatedTransactionId);
            Assert.Equal(100m, reversal.BalanceAfter);
        }
    }
}
