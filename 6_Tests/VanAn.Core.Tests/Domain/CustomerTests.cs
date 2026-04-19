using VanAn.Shared.Domain;
using Xunit;

namespace VanAn.Core.Tests.Domain;

/// <summary>
/// Unit tests for Customer entity
/// Tests domain behavior and compliance with Engineering Constitution
/// </summary>
public class CustomerTests
{
    [Fact]
    public void Customer_Should_Have_Default_IsDeleted_False()
    {
        // Arrange & Act
        var customer = new Customer();

        // Assert
        Assert.False(customer.IsDeleted);
    }

    [Fact]
    public void Customer_Should_Have_TenantId()
    {
        // Arrange & Act
        var customer = new Customer();
        var tenantId = new TenantId(Guid.NewGuid());
        customer.TenantId = tenantId;

        // Assert
        Assert.Equal(tenantId.Value, customer.TenantId.Value);
        Assert.NotEqual(Guid.Empty, customer.TenantId.Value);
    }

    [Fact]
    public void Customer_Should_Track_CreatedAt()
    {
        // Arrange & Act
        var beforeCreation = DateTime.UtcNow;
        var customer = new Customer();
        var afterCreation = DateTime.UtcNow;

        // Assert
        Assert.True(customer.CreatedAt >= beforeCreation);
        Assert.True(customer.CreatedAt <= afterCreation);
    }

    [Fact]
    public void Customer_Should_Have_Valid_CustomerId()
    {
        // Arrange & Act
        var customer = new Customer();

        // Assert
        Assert.NotEqual(Guid.Empty, customer.CustomerId.Value);
        Assert.NotNull(customer.CustomerId);
    }

    [Fact]
    public void Customer_Should_Initialize_With_Default_Values()
    {
        // Arrange & Act
        var customer = new Customer();

        // Assert
        Assert.Equal(string.Empty, customer.FullName);
        Assert.Equal(string.Empty, customer.PhoneNumber);
        Assert.Null(customer.Email);
        Assert.Equal(0, customer.LoyaltyPoints);
        Assert.Equal("Bronze", customer.CustomerTier);
        Assert.Null(customer.LastOrderDate);
        Assert.Equal(0m, customer.TotalSpent);
        Assert.True(customer.IsActive);
        Assert.Null(customer.DeviceId);
    }

    [Fact]
    public void Customer_Should_Support_Soft_Delete()
    {
        // Arrange
        var customer = new Customer();
        Assert.False(customer.IsDeleted);

        // Act
        customer.IsDeleted = true;

        // Assert
        Assert.True(customer.IsDeleted);
    }

    [Fact]
    public void Customer_Should_Track_UpdatedAt()
    {
        // Arrange
        var customer = new Customer();
        Assert.Null(customer.UpdatedAt);

        // Act
        var beforeUpdate = DateTime.UtcNow;
        customer.UpdatedAt = DateTime.UtcNow;
        var afterUpdate = DateTime.UtcNow;

        // Assert
        Assert.NotNull(customer.UpdatedAt);
        Assert.True(customer.UpdatedAt >= beforeUpdate);
        Assert.True(customer.UpdatedAt <= afterUpdate);
    }
}
