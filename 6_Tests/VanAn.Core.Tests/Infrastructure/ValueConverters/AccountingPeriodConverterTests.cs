using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using VanAn.CoreHub.Infrastructure.ValueConverters;
using VanAn.Shared.Domain;
using Xunit;
using FluentAssertions;

namespace VanAn.Core.Tests.Infrastructure.ValueConverters;

public class AccountingPeriodConverterTests
{
    private readonly AccountingPeriodConverter _converter = new();

    [Fact]
    public void Should_Convert_To_Database_Value()
    {
        // Arrange
        var period = AccountingPeriod.FromDateTime(new DateTime(2024, 3, 15));
        
        // Act
        var result = _converter.ConvertToProvider(period);
        
        // Assert
        result.Should().Be("2024-03");
    }

    [Fact]
    public void Should_Convert_From_Database_Value()
    {
        // Arrange
        var value = "2024-03";
        
        // Act
        var result = _converter.ConvertFromProvider(value);
        
        // Assert
        var period = (AccountingPeriod?)result;
        Assert.NotNull(period);
        Assert.Equal(2024, period!.Year);   // dùng ! vì sudah check NotNull
        Assert.Equal(4, period!.Month);
    }

    [Fact]
    public void Should_Handle_Null_Values()
    {
        // Act & Assert
        _converter.ConvertToProvider(null).Should().BeNull();
        _converter.ConvertFromProvider(null).Should().BeNull();
        _converter.ConvertFromProvider("").Should().BeNull();
    }

    [Fact]
    public void Should_Handle_Single_Digit_Month()
    {
        // Arrange
        var period = AccountingPeriod.FromDateTime(new DateTime(2024, 1, 15));
        
        // Act
        var result = _converter.ConvertToProvider(period);
        
        // Assert
        result.Should().Be("2024-01");
    }

    [Fact]
    public void Should_Handle_Double_Digit_Month()
    {
        // Arrange
        var period = AccountingPeriod.FromDateTime(new DateTime(2024, 12, 15));
        
        // Act
        var result = _converter.ConvertToProvider(period);
        
        // Assert
        result.Should().Be("2024-12");
    }
}
