using VanAn.CoreHub.Infrastructure.ValueConverters;
using VanAn.Shared.Domain;
using Xunit;
using FluentAssertions;

namespace VanAn.Core.Tests.Infrastructure.ValueConverters
{
    public class TenantIdConverterTests
    {
        private readonly TenantIdConverter _converter = new();

        [Fact]
        public void Should_Convert_To_Database_Value()
        {
            // Arrange
            TenantId tenantId = new(Guid.NewGuid());

            // Act
            object? result = _converter.ConvertToProvider(tenantId);

            // Assert
            result.Should().Be(tenantId.Value);
        }

        [Fact]
        public void Should_Convert_From_Database_Value()
        {
            // Arrange
            Guid guid = Guid.NewGuid();

            // Act
            object? result = _converter.ConvertFromProvider(guid);

            // Assert
            result.Should().NotBeNull();
            if (result != null)
            {
                ((TenantId)result).Value.Should().Be(guid);
            }
        }

        [Fact]
        public void Should_Handle_Null_Values()
        {
            // TenantIdConverter maps TenantId<->Guid (non-nullable)
            // Guid.Empty maps to a TenantId with empty value (not null)
            object? emptyTenantId = _converter.ConvertFromProvider(Guid.Empty);
            emptyTenantId.Should().NotBeNull();
            ((TenantId)emptyTenantId!).Value.Should().Be(Guid.Empty);
        }
    }
}
