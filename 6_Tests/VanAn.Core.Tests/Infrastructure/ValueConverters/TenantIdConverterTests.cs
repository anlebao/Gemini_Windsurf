using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using VanAn.CoreHub.Infrastructure.ValueConverters;
using VanAn.Shared.Domain;
using Xunit;
using FluentAssertions;

namespace VanAn.Core.Tests.Infrastructure.ValueConverters;

public class TenantIdConverterTests
{
    private readonly TenantIdConverter _converter = new();

    [Fact]
    public void Should_Convert_To_Database_Value()
    {
        // Arrange
        var tenantId = new TenantId(Guid.NewGuid());
        
        // Act
        var result = _converter.ConvertToProvider(tenantId);
        
        // Assert
        result.Should().Be(tenantId.Value);
    }

    [Fact]
    public void Should_Convert_From_Database_Value()
    {
        // Arrange
        var guid = Guid.NewGuid();
        
        // Act
        var result = _converter.ConvertFromProvider(guid);
        
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
        // Act & Assert
        _converter.ConvertToProvider(null).Should().Be(Guid.Empty);
        _converter.ConvertFromProvider(Guid.Empty).Should().BeNull();
    }
}
