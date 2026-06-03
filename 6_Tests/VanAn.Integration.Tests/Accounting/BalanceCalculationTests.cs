using Xunit;
using VanAn.Shared.DTOs;
using VanAn.CoreHub.Services;
using VanAn.Integration.Tests.Infrastructure;

namespace VanAn.Integration.Tests.Accounting;

public class BalanceCalculationTests
{
    private readonly AccountingEntryServiceStub _service;

    public BalanceCalculationTests()
    {
        _service = new AccountingEntryServiceStub();
    }

    [Fact]
    public async Task GetBalanceSummary_ShouldCalculateTotalRevenue_WhenMultipleRevenueEntriesExist()
    {
        // Arrange
        var tenantId = Guid.NewGuid();

        await _service.CreateRevenueEntryAsync(tenantId, new RevenueEntryDto { Amount = 1000000, AccountCode = "511", Date = DateTime.Today });
        await _service.CreateRevenueEntryAsync(tenantId, new RevenueEntryDto { Amount = 2000000, AccountCode = "515", Date = DateTime.Today });

        // Act
        var summary = await _service.GetBalanceSummaryAsync(tenantId);

        // Assert
        Assert.Equal(3000000, summary.TotalRevenue);
    }

    [Fact]
    public async Task GetBalanceSummary_ShouldCalculateTotalExpenses_WhenMultipleExpenseEntriesExist()
    {
        // Arrange
        var tenantId = Guid.NewGuid();

        await _service.CreateExpenseEntryAsync(tenantId, new ExpenseEntryDto { Amount = 500000, AccountCode = "621", Date = DateTime.Today });
        await _service.CreateExpenseEntryAsync(tenantId, new ExpenseEntryDto { Amount = 300000, AccountCode = "622", Date = DateTime.Today });

        // Act
        var summary = await _service.GetBalanceSummaryAsync(tenantId);

        // Assert
        Assert.Equal(800000, summary.TotalExpenses);
    }

    [Fact]
    public async Task GetBalanceSummary_ShouldCalculateNetProfit_WhenRevenueAndExpensesExist()
    {
        // Arrange
        var tenantId = Guid.NewGuid();

        await _service.CreateRevenueEntryAsync(tenantId, new RevenueEntryDto { Amount = 1000000, AccountCode = "511", Date = DateTime.Today });
        await _service.CreateExpenseEntryAsync(tenantId, new ExpenseEntryDto { Amount = 300000, AccountCode = "621", Date = DateTime.Today });

        // Act
        var summary = await _service.GetBalanceSummaryAsync(tenantId);

        // Assert
        Assert.Equal(700000, summary.NetProfit);
    }
}
