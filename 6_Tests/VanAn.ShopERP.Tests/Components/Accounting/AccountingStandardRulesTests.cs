using VanAn.Shared.Domain;
using VanAn.ShopERP.Services.Accounting;
using Xunit;

namespace VanAn.ShopERP.Tests.Components.Accounting
{
    /// <summary>
    /// Regime-lock (2026-10-30): AccountingStandardRules — allowed set + effective + voluntary
    /// theo Tenant.Type + AccountingStandard override. Q1 siêu nhỏ được chọn TT133 ·
    /// Q2 Type=null giữ đủ 4 · Q3 HKD chưa bầu chọn → rỗng (BCTC không áp dụng).
    /// </summary>
    public class AccountingStandardRulesTests
    {
        [Theory]
        [InlineData(TenantType.Enterprise_Large, AccountingStandard.TT99_2025)]
        [InlineData(TenantType.Enterprise_SME, AccountingStandard.TT133_2016)]
        [InlineData(TenantType.HTX, AccountingStandard.TT71_2024)]
        [InlineData(TenantType.Enterprise_SuperSmall, AccountingStandard.TT58_2026)]
        [InlineData(null, AccountingStandard.TT133_2016)]
        public void DefaultStandard_MapsType(TenantType? type, AccountingStandard expected)
            => Assert.Equal(expected, AccountingStandardRules.DefaultStandard(type));

        [Fact]
        public void Allowed_LockedTypes_SingleOption()
        {
            Assert.Equal(new[] { AccountingStandard.TT99_2025 },
                AccountingStandardRules.AllowedStandards(TenantType.Enterprise_Large, null));
            Assert.Equal(new[] { AccountingStandard.TT133_2016 },
                AccountingStandardRules.AllowedStandards(TenantType.Enterprise_SME, null));
            Assert.Equal(new[] { AccountingStandard.TT71_2024 },
                AccountingStandardRules.AllowedStandards(TenantType.HTX, null));
        }

        [Fact]
        public void Allowed_SuperSmall_TT58AndTT133()
        {
            var allowed = AccountingStandardRules.AllowedStandards(TenantType.Enterprise_SuperSmall, null);
            Assert.Equal(new[] { AccountingStandard.TT58_2026, AccountingStandard.TT133_2016 }, allowed);
        }

        [Fact]
        public void Allowed_HKD_EmptyWithoutElection_TT58WhenElected()
        {
            Assert.Empty(AccountingStandardRules.AllowedStandards(TenantType.HKD, null));
            Assert.Empty(AccountingStandardRules.AllowedStandards(TenantType.HKD, AccountingStandard.TT99_2025));
            Assert.Equal(new[] { AccountingStandard.TT58_2026 },
                AccountingStandardRules.AllowedStandards(TenantType.HKD, AccountingStandard.TT58_2026));
        }

        [Fact]
        public void Allowed_NullType_AllFour()
        {
            var allowed = AccountingStandardRules.AllowedStandards(null, null);
            Assert.Equal(4, allowed.Count);
        }

        [Fact]
        public void Effective_OverrideWins_OnlyIfAllowed()
        {
            Assert.Equal(AccountingStandard.TT133_2016,
                AccountingStandardRules.ResolveEffective(TenantType.Enterprise_SuperSmall, AccountingStandard.TT133_2016));
            // Override ngoài allowed → về default
            Assert.Equal(AccountingStandard.TT58_2026,
                AccountingStandardRules.ResolveEffective(TenantType.Enterprise_SuperSmall, AccountingStandard.TT99_2025));
            Assert.Equal(AccountingStandard.TT133_2016,
                AccountingStandardRules.ResolveEffective(TenantType.Enterprise_SME, AccountingStandard.TT58_2026));
        }

        [Fact]
        public void Effective_HKD_NullWithoutElection_TT58WhenElected()
        {
            Assert.Null(AccountingStandardRules.ResolveEffective(TenantType.HKD, null));
            Assert.Equal(AccountingStandard.TT58_2026,
                AccountingStandardRules.ResolveEffective(TenantType.HKD, AccountingStandard.TT58_2026));
        }

        [Fact]
        public void IsVoluntary_TrueWhenNotDefault()
        {
            Assert.True(AccountingStandardRules.IsVoluntary(TenantType.Enterprise_SuperSmall, AccountingStandard.TT133_2016));
            Assert.True(AccountingStandardRules.IsVoluntary(TenantType.HKD, AccountingStandard.TT58_2026));
            Assert.False(AccountingStandardRules.IsVoluntary(TenantType.Enterprise_SME, AccountingStandard.TT133_2016));
        }

        [Fact]
        public void Resolver_DelegatesToRules()
        {
            Assert.Equal(AccountingStandard.TT71_2024, AccountingStandardResolver.Resolve(TenantType.HTX));
            Assert.Equal("TT 58/2026 (DN siêu nhỏ)", AccountingStandardResolver.DropdownLabel(AccountingStandard.TT58_2026));
        }
    }
}
