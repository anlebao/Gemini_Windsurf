using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using VanAn.CoreHub.Infrastructure.ValueConverters;
using VanAn.Shared.Domain;
using Xunit;
using FluentAssertions;

namespace VanAn.Core.Tests.Infrastructure.ValueConverters;

public class MoneyConverterTests
{
    private readonly MoneyConverter _converter = new();

    [Fact]
    public void Should_Convert_To_Database_Value()
    {
        // Arrange
        var money = new Money(1000.50m);
        
        // Act
        var result = _converter.ConvertToProvider(money);
        
        // Assert
        result.Should().Be(1000.50m);
    }

    [Fact]
    public void Should_Convert_From_Database_Value()
    {
        // Arrange
        var value = 1000.50m;
        
        // Act
        var result = _converter.ConvertFromProvider(value);
        
        // Assert
        result.Should().NotBeNull();
        if (result != null)
        {
            ((Money)result).Value.Should().Be(value);
        }
    }

    [Fact]
    public void Should_Handle_Null_Values()
    {
        // Act & Assert
        _converter.ConvertToProvider(null).Should().Be(0m);
        _converter.ConvertFromProvider(0m).Should().BeNull();
    }

    [Fact]
    public void Should_Handle_Zero_Value()
    {
        // Arrange
        var money = new Money(0m);
        
        // Act
        var result = _converter.ConvertToProvider(money);
        
        // Assert
        result.Should().Be(0m);
    }
}
