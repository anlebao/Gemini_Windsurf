using VanAn.CoreHub.Infrastructure.ValueConverters;
using VanAn.Shared.Domain;
using Xunit;
using FluentAssertions;

namespace VanAn.Core.Tests.Infrastructure.ValueConverters
{
    public class AccountingEntryIdConverterTests
    {
        private readonly AccountingEntryIdConverter _converter = new();

        [Fact]
        public void Should_Convert_To_Database_Value()
        {
            // Arrange
            AccountingEntryId entryId = new(Guid.NewGuid());

            // Act
            object? result = _converter.ConvertToProvider(entryId);

            // Assert
            _ = result.Should().Be(entryId.Value);
        }

        [Fact]
        public void Should_Convert_From_Database_Value()
        {
            // Arrange
            Guid guid = Guid.NewGuid();

            // Act
            object? result = _converter.ConvertFromProvider(guid);

            // Assert
            _ = result.Should().NotBeNull();
            if (result != null)
            {
                _ = ((AccountingEntryId)result).Value.Should().Be(guid);
            }
        }

        [Fact]
        public void Should_Handle_Null_Values()
        {
            // AccountingEntryIdConverter maps AccountingEntryId<->Guid (non-nullable)
            // Guid.Empty maps to an AccountingEntryId with empty value (not null)
            object? emptyEntryId = _converter.ConvertFromProvider(Guid.Empty);
            _ = emptyEntryId.Should().NotBeNull();
            _ = ((AccountingEntryId)emptyEntryId!).Value.Should().Be(Guid.Empty);
        }
    }
}
