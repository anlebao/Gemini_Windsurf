using VanAn.ShopERP.Services.Accounting;
using VanAn.Shared.Domain;
using Xunit;

namespace VanAn.ShopERP.Tests.Services.Accounting;

/// <summary>PH3 (khẩn — fix định khoản gợi ý sai hiện hữu): tax → 333 · interest → 635.</summary>
public class EntryFormAccountingHelperTests
{
    [Theory]
    [InlineData("tax", TenantType.HKD, "333")]
    [InlineData("tax", TenantType.Enterprise_SME, "333")]
    [InlineData("tax", TenantType.HTX, "333")]
    [InlineData("interest", TenantType.Enterprise_SME, "635")]
    [InlineData("interest", TenantType.HTX, "635")]
    [InlineData("interest", TenantType.Enterprise_Large, "635")]
    public void SuggestExpenseAccount_TaxInterest_Returns333_635(string category, TenantType? type, string expected)
    {
        Assert.Equal(expected, EntryFormAccountingHelper.SuggestExpenseAccount(category, type));
    }

    [Theory]
    [InlineData("materials", TenantType.HKD, "621")]
    [InlineData("materials", TenantType.HTX, "642")]
    [InlineData("rent", TenantType.Enterprise_SME, "627")]
    [InlineData("rent", TenantType.HTX, "642")]
    [InlineData(null, TenantType.HTX, "658")]
    [InlineData(null, TenantType.Enterprise_SME, "642")]
    public void SuggestExpenseAccount_ExistingCategories_Regression(string? category, TenantType? type, string expected)
    {
        Assert.Equal(expected, EntryFormAccountingHelper.SuggestExpenseAccount(category, type));
    }
}