using Xunit;
using VanAn.CoreHub.Services;

namespace VanAn.Core.Tests.Accounting;

public class MoneyFormattingTests
{
    [Theory]
    [InlineData(1000)]
    [InlineData(10000)]
    [InlineData(100000)]
    [InlineData(1000000)]
    [InlineData(10000000)]
    public void FormatCurrency_ShouldReturnCorrectFormat_ForVietnameseDong(decimal amount)
    {
        // Act
        var formatted = MoneyFormatter.FormatVND(amount);

        // Assert
        Assert.Contains("₫", formatted);
    }

    [Fact]
    public void ParseCurrency_ShouldReturnDecimal_WhenInputHasSeparator()
    {
        // Arrange
        var input = "1.000.000 ₫";

        // Act
        var parsed = MoneyFormatter.ParseVND(input);

        // Assert
        Assert.Equal(1000000, parsed);
    }

    [Fact]
    public void FormatCurrency_ShouldHandleZero()
    {
        // Arrange
        var amount = 0m;

        // Act
        var formatted = MoneyFormatter.FormatVND(amount);

        // Assert
        Assert.Equal("0 ₫", formatted);
    }
}
