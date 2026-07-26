using System.Reflection;
using Xunit;

namespace VanAn.Architecture.Tests
{
    /// <summary>
    /// WalletTransaction immutability tests (Community Commerce Sprint 0 v1.1/v1.2).
    /// Cases 21-22: no public setters on mutable fields, no public Update* methods.
    /// </summary>
    public class WalletTransactionImmutabilityTests
    {
        [Fact(DisplayName = "21: WalletTransaction_Immutable_NoPublicSetter")]
        public void WalletTransaction_Immutable_NoPublicSetter()
        {
            var mutablePropertyNames = new[]
            {
                nameof(VanAn.Shared.Domain.WalletTransaction.OwnerId),
                nameof(VanAn.Shared.Domain.WalletTransaction.Type),
                nameof(VanAn.Shared.Domain.WalletTransaction.Amount),
                nameof(VanAn.Shared.Domain.WalletTransaction.Description),
                nameof(VanAn.Shared.Domain.WalletTransaction.RelatedOrderId),
                nameof(VanAn.Shared.Domain.WalletTransaction.RelatedTransactionId),
                nameof(VanAn.Shared.Domain.WalletTransaction.BalanceAfter),
            };

            foreach (var propName in mutablePropertyNames)
            {
                var prop = typeof(VanAn.Shared.Domain.WalletTransaction).GetProperty(propName);
                Assert.NotNull(prop);
                var setter = prop!.GetSetMethod(nonPublic: true);
                Assert.NotNull(setter);
                // Setter must NOT be public (must be protected/internal)
                Assert.False(setter!.IsPublic,
                    $"Property {propName} must have non-public setter (immutable entity). Found public setter.");
            }
        }

        [Fact(DisplayName = "22: WalletTransaction_NoUpdateMethod")]
        public void WalletTransaction_NoUpdateMethod()
        {
            var publicMethods = typeof(VanAn.Shared.Domain.WalletTransaction)
                .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                .Where(m => m.Name.StartsWith("Update", StringComparison.OrdinalIgnoreCase))
                .ToList();

            Assert.Empty(publicMethods);
        }
    }
}
