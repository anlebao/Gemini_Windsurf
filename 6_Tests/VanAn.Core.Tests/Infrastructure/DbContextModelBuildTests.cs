using Microsoft.EntityFrameworkCore;
using VanAn.CoreHub.Infrastructure;
using VanAn.CoreHub.Tests.TestInfrastructure;
using Xunit;

namespace VanAn.Core.Tests.Infrastructure;

/// <summary>
/// STEP 4: Tripwire Test - Catches EF mapping errors before they hit business logic
/// This test ensures the EF Core model can be built without errors.
/// If this fails, it means there's a mapping configuration issue (Value Objects, computed properties, etc.)
/// </summary>
public class DbContextModelBuildTests
{
    [Fact]
    public void DbContext_Should_Not_Throw_On_ModelBuild()
    {
        // Arrange
        using var contextScope = VanAnDbContextTestFactory.Create();
        var context = contextScope.Context;

        // Act & Assert
        // If this throws, it means there's an EF mapping configuration error
        // Common issues:
        // - Value Objects mapped as entities (need OwnsOne or Ignore)
        // - Computed properties not ignored (need modelBuilder.Ignore)
        // - Missing Value Converters for Strongly Typed IDs
        // - Entities without parameterless constructors
        var model = context.Model;

        // Verify model was built successfully
        Assert.NotNull(model);
        
        // Verify critical entity types are mapped correctly
        Assert.Contains(model.GetEntityTypes(), e => e.ClrType == typeof(VanAn.Shared.Domain.Order));
        Assert.Contains(model.GetEntityTypes(), e => e.ClrType == typeof(VanAn.Shared.Domain.AccountingEntry));
    }

    [Fact]
    public void DbContext_Should_Have_ValueConverters_For_StronglyTypedIds()
    {
        // Arrange
        using var contextScope = VanAnDbContextTestFactory.Create();
        var context = contextScope.Context;

        // Act
        var model = context.Model;

        // Assert - Verify Value Converters are registered for Strongly Typed IDs
        var orderEntityType = model.FindEntityType(typeof(VanAn.Shared.Domain.Order));
        Assert.NotNull(orderEntityType);

        var orderIdProperty = orderEntityType.FindProperty(nameof(VanAn.Shared.Domain.Order.OrderId));
        Assert.NotNull(orderIdProperty);

        // Verify the property has a value converter (it should be mapped to Guid, not as a separate entity)
        var valueConverter = orderIdProperty.GetValueConverter();
        Assert.NotNull(valueConverter);
    }

    [Fact]
    public void DbContext_Should_Ignore_ComputedProperties()
    {
        // Arrange
        using var contextScope = VanAnDbContextTestFactory.Create();
        var context = contextScope.Context;

        // Act
        var model = context.Model;

        // Assert - AccountingPeriod should NOT be mapped as an entity
        var accountingPeriodEntityType = model.FindEntityType(typeof(VanAn.Shared.Domain.AccountingPeriod));
        Assert.Null(accountingPeriodEntityType);
    }
}
