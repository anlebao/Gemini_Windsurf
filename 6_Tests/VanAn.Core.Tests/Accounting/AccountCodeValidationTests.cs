using Xunit;
using VanAn.CoreHub.Services;

namespace VanAn.Core.Tests.Accounting;

public class AccountCodeValidationTests
{
    [Theory]
    [InlineData("511")]  // Doanh thu bán hàng
    [InlineData("515")]  // Doanh thu dịch vụ
    [InlineData("711")]  // Thu nhập khác
    [InlineData("621")]  // Chi phí vật liệu
    [InlineData("622")]  // Chi phí nhân công
    [InlineData("627")]  // Chi phí sản xuất chung
    [InlineData("641")]  // Chi phí bán hàng
    [InlineData("111")]  // Tiền mặt
    [InlineData("112")]  // Tiền gửi ngân hàng
    public void ValidateAccountCode_ShouldReturnTrue_ForValidVNAccountCodes(string accountCode)
    {
        // Act
        var isValid = AccountCodeValidator.IsValidVNAccountCode(accountCode);

        // Assert
        Assert.True(isValid);
    }

    [Theory]
    [InlineData("999")]  // Invalid code
    [InlineData("51")]   // Too short
    [InlineData("5111")] // Too long
    [InlineData("ABC")]  // Non-numeric
    [InlineData("")]     // Empty
    public void ValidateAccountCode_ShouldReturnFalse_ForInvalidAccountCodes(string accountCode)
    {
        // Act
        var isValid = AccountCodeValidator.IsValidVNAccountCode(accountCode);

        // Assert
        Assert.False(isValid);
    }

    [Fact]
    public void GetAccountType_ShouldReturnRevenue_For5xxCodes()
    {
        // Arrange
        var accountCode = "511";

        // Act
        var accountType = AccountCodeValidator.GetAccountType(accountCode);

        // Assert
        Assert.Equal(AccountType.Revenue, accountType);
    }

    [Fact]
    public void GetAccountType_ShouldReturnExpense_For6xxCodes()
    {
        // Arrange
        var accountCode = "621";

        // Act
        var accountType = AccountCodeValidator.GetAccountType(accountCode);

        // Assert
        Assert.Equal(AccountType.Expense, accountType);
    }
}
