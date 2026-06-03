using Xunit;
using System;
using VanAn.Shared.Domain;

namespace VanAn.Core.Tests;

public class TryParseSafetyTests
{
    [Fact(DisplayName = "Guid.TryParse - Valid Input")]
    public void GuidTryParse_ValidInput_ReturnsTrue()
    {
        // Arrange
        var validGuidString = "11111111-1111-1111-1111-111111111111";

        // Act
        var result = Guid.TryParse(validGuidString, out Guid parsedGuid);

        // Assert
        Assert.True(result);
        Assert.NotEqual(Guid.Empty, parsedGuid);
        Assert.Equal(validGuidString, parsedGuid.ToString());
    }

    [Fact(DisplayName = "Guid.TryParse - Invalid Input")]
    public void GuidTryParse_InvalidInput_ReturnsFalse()
    {
        // Arrange
        var invalidGuidString = "not-a-guid";

        // Act
        var result = Guid.TryParse(invalidGuidString, out Guid parsedGuid);

        // Assert
        Assert.False(result);
        Assert.Equal(Guid.Empty, parsedGuid);
    }

    [Fact(DisplayName = "Guid.TryParse - Null Input")]
    public void GuidTryParse_NullInput_ReturnsFalse()
    {
        // Arrange & Act
        var result = Guid.TryParse(null, out Guid parsedGuid);

        // Assert
        Assert.False(result);
        Assert.Equal(Guid.Empty, parsedGuid);
    }

    [Fact(DisplayName = "Guid.TryParse - Empty Input")]
    public void GuidTryParse_EmptyInput_ReturnsFalse()
    {
        // Arrange
        var emptyInput = string.Empty;

        // Act
        var result = Guid.TryParse(emptyInput, out Guid parsedGuid);

        // Assert
        Assert.False(result);
        Assert.Equal(Guid.Empty, parsedGuid);
    }

    [Fact(DisplayName = "int.TryParse - Valid Positive Integer")]
    public void IntTryParse_ValidPositiveInteger_ReturnsTrue()
    {
        // Arrange
        var validIntString = "12345";

        // Act
        var result = int.TryParse(validIntString, out int parsedInt);

        // Assert
        Assert.True(result);
        Assert.Equal(12345, parsedInt);
    }

    [Fact(DisplayName = "int.TryParse - Valid Negative Integer")]
    public void IntTryParse_ValidNegativeInteger_ReturnsTrue()
    {
        // Arrange
        var validIntString = "-12345";

        // Act
        var result = int.TryParse(validIntString, out int parsedInt);

        // Assert
        Assert.True(result);
        Assert.Equal(-12345, parsedInt);
    }

    [Fact(DisplayName = "int.TryParse - Invalid Input")]
    public void IntTryParse_InvalidInput_ReturnsFalse()
    {
        // Arrange
        var invalidIntString = "not-an-integer";

        // Act
        var result = int.TryParse(invalidIntString, out int parsedInt);

        // Assert
        Assert.False(result);
        Assert.Equal(0, parsedInt);
    }

    [Fact(DisplayName = "int.TryParse - Null Input")]
    public void IntTryParse_NullInput_ReturnsFalse()
    {
        // Arrange & Act
        var result = int.TryParse((string?)null, out int parsedInt);

        // Assert
        Assert.False(result);
        Assert.Equal(0, parsedInt);
    }

    [Fact(DisplayName = "int.TryParse - Empty Input")]
    public void IntTryParse_EmptyInput_ReturnsFalse()
    {
        // Arrange
        var emptyInput = string.Empty;

        // Act
        var result = int.TryParse(emptyInput, out int parsedInt);

        // Assert
        Assert.False(result);
        Assert.Equal(0, parsedInt);
    }

    [Fact(DisplayName = "int.TryParse - Overflow Large Number")]
    public void IntTryParse_OverflowLargeNumber_ReturnsFalse()
    {
        // Arrange
        var overflowInput = "999999999999999999999999999999";

        // Act
        var result = int.TryParse(overflowInput, out int parsedInt);

        // Assert
        Assert.False(result);
        Assert.Equal(0, parsedInt);
    }

    [Fact(DisplayName = "DateTime.TryParse - Valid Date")]
    public void DateTimeTryParse_ValidDate_ReturnsTrue()
    {
        // Arrange
        var validDateString = "2026-04-03";

        // Act
        var result = DateTime.TryParse(validDateString, out DateTime parsedDate);

        // Assert
        Assert.True(result);
        Assert.Equal(2026, parsedDate.Year);
        Assert.Equal(4, parsedDate.Month);
        Assert.Equal(3, parsedDate.Day);
    }

    [Fact(DisplayName = "DateTime.TryParse - Invalid Input")]
    public void DateTimeTryParse_InvalidInput_ReturnsFalse()
    {
        // Arrange
        var invalidDateString = "not-a-date";

        // Act
        var result = DateTime.TryParse(invalidDateString, out DateTime parsedDate);

        // Assert
        Assert.False(result);
        Assert.Equal(default(DateTime), parsedDate);
    }

    [Fact(DisplayName = "DateTime.TryParse - Null Input")]
    public void DateTimeTryParse_NullInput_ReturnsFalse()
    {
        // Arrange & Act
        var result = DateTime.TryParse(null, out DateTime parsedDate);

        // Assert
        Assert.False(result);
        Assert.Equal(default(DateTime), parsedDate);
    }

    [Fact(DisplayName = "DateTime.TryParse - Empty Input")]
    public void DateTimeTryParse_EmptyInput_ReturnsFalse()
    {
        // Arrange
        var emptyInput = string.Empty;

        // Act
        var result = DateTime.TryParse(emptyInput, out DateTime parsedDate);

        // Assert
        Assert.False(result);
        Assert.Equal(default(DateTime), parsedDate);
    }

    [Fact(DisplayName = "Enum.TryParse<UserRole> - Valid Role")]
    public void EnumTryParse_ValidRole_ReturnsTrue()
    {
        // Arrange
        var validRoleString = "Owner";

        // Act
        var result = Enum.TryParse<UserRole>(validRoleString, out UserRole parsedRole);

        // Assert
        Assert.True(result);
        Assert.Equal(UserRole.Owner, parsedRole);
    }

    [Fact(DisplayName = "Enum.TryParse<UserRole> - Invalid Role")]
    public void EnumTryParse_InvalidRole_ReturnsFalse()
    {
        // Arrange
        var invalidRoleString = "InvalidRole";

        // Act
        var result = Enum.TryParse<UserRole>(invalidRoleString, out UserRole parsedRole);

        // Assert
        Assert.False(result);
        Assert.Equal(default(UserRole), parsedRole);
    }

    [Fact(DisplayName = "Enum.TryParse<UserRole> - Case Insensitive")]
    public void EnumTryParse_CaseInsensitive_ReturnsTrue()
    {
        // Arrange
        var lowercaseRoleString = "owner";

        // Act
        var result = Enum.TryParse<UserRole>(lowercaseRoleString, true, out UserRole parsedRole);

        // Assert
        Assert.True(result);
        Assert.Equal(UserRole.Owner, parsedRole);
    }
}
