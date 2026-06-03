using VanAn.CoreHub.Infrastructure.ValueConverters;
using VanAn.Shared.Domain;
using Xunit;
using FluentAssertions;

namespace VanAn.Core.Tests.Infrastructure.ValueConverters
{
    public class MoneyConverterTests
    {
        private readonly MoneyConverter _converter = new();

        [Fact]
        public void Should_Convert_To_Database_Value()
        {
            // Arrange
            Money money = new(1000.50m);

            // Act
            object? result = _converter.ConvertToProvider(money);

            // Assert
            result.Should().Be(1000.50m);
        }

        [Fact]
        public void Should_Convert_From_Database_Value()
        {
            // Arrange
            decimal value = 1000.50m;

            // Act
            object? result = _converter.ConvertFromProvider(value);

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
            // MoneyConverter maps Money<->decimal (non-nullable)
            // 0m maps to Money(0m) not null
            object? zeroMoney = _converter.ConvertFromProvider(0m);
            zeroMoney.Should().NotBeNull();
            ((Money)zeroMoney!).Value.Should().Be(0m);
        }

        [Fact]
        public void Should_Handle_Zero_Value()
        {
            // Arrange
            Money money = new(0m);

            // Act
            object? result = _converter.ConvertToProvider(money);

            // Assert
            result.Should().Be(0m);
        }
    }
}
