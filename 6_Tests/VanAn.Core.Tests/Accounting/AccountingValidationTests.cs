using Xunit;
using VanAn.Shared.DTOs;
using VanAn.CoreHub.Services;

namespace VanAn.Core.Tests.Accounting;

public class AccountingValidationTests
{
    private readonly AccountingValidationService validator = new();

    [Fact]
    public async Task DetectDuplicateEntry_ShouldReturnTrue_WhenSameAmountDateAccountWithin5Minutes()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var existingEntry = new AccountingEntryDto
        {
            Amount = 1000000,
            CreatedAt = DateTime.Now.AddMinutes(-2),
            Description = "Doanh thu bán hàng"
        };

        // Act
        var isDuplicate = await validator.IsDuplicateEntryAsync(tenantId, existingEntry);

        // Assert
        Assert.True(isDuplicate);
    }

    [Fact]
    public async Task DetectDuplicateEntry_ShouldReturnFalse_WhenSameEntryAfter5Minutes()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var existingEntry = new AccountingEntryDto
        {
            Amount = 1000000,
            CreatedAt = DateTime.Now.AddMinutes(-6),
            Description = "Doanh thu bán hàng"
        };

        // Act
        var isDuplicate = await validator.IsDuplicateEntryAsync(tenantId, existingEntry);

        // Assert
        Assert.False(isDuplicate);
    }

    [Fact]
    public void ValidatePeriod_ShouldReturnError_WhenDateOutsideCurrentPeriod()
    {
        // Arrange
        var currentDate = new DateTime(2026, 5, 20);
        var entryDate = new DateTime(2026, 4, 15); // Previous month

        // Act
        var isValid = validator.IsValidDateForPeriod(entryDate, currentDate);

        // Assert
        Assert.False(isValid);
    }

    [Fact]
    public void ValidatePeriod_ShouldReturnTrue_WhenDateInCurrentPeriod()
    {
        // Arrange
        var currentDate = new DateTime(2026, 5, 20);
        var entryDate = new DateTime(2026, 5, 15);

        // Act
        var isValid = validator.IsValidDateForPeriod(entryDate, currentDate);

        // Assert
        Assert.True(isValid);
    }

    [Fact]
    public void ValidateBalanceConstraint_ShouldReturnWarning_WhenExpensesExceedRevenue()
    {
        // Arrange
        var totalRevenue = 10000000;
        var totalExpenses = 16000000; // > 1.5 * revenue

        // Act
        var hasWarning = validator.HasBalanceWarning(totalRevenue, totalExpenses);

        // Assert
        Assert.True(hasWarning);
    }
}
