using Xunit;
using VanAn.Shared.DTOs;
using VanAn.CoreHub.Services;
using VanAn.Integration.Tests.Infrastructure;

namespace VanAn.Integration.Tests.Accounting;

public class TransactionHistoryQueryTests
{
    private readonly AccountingEntryServiceStub _service;

    public TransactionHistoryQueryTests()
    {
        _service = new AccountingEntryServiceStub();
    }

    [Fact]
    public async Task GetEntries_ShouldFilterByDescription_WhenSearchTermProvided()
    {
        // Arrange
        var tenantId = Guid.NewGuid();

        await _service.CreateRevenueEntryAsync(tenantId, new RevenueEntryDto { Amount = 1000, AccountCode = "511", Description = "Doanh thu bán hàng", Date = DateTime.Today });
        await _service.CreateRevenueEntryAsync(tenantId, new RevenueEntryDto { Amount = 2000, AccountCode = "515", Description = "Doanh thu dịch vụ", Date = DateTime.Today });

        // Act
        var entries = await _service.GetEntriesAsync(tenantId, "bán hàng", null, null);

        // Assert
        Assert.Single(entries);
        Assert.Contains("bán hàng", entries[0].Description);
    }

    [Fact]
    public async Task GetEntries_ShouldFilterByPeriod_WhenDateRangeProvided()
    {
        // Arrange
        var tenantId = Guid.NewGuid();

        await _service.CreateRevenueEntryAsync(tenantId, new RevenueEntryDto { Amount = 1000, AccountCode = "511", Date = new DateTime(2026, 5, 15) });
        await _service.CreateRevenueEntryAsync(tenantId, new RevenueEntryDto { Amount = 2000, AccountCode = "511", Date = new DateTime(2026, 4, 15) });

        // Act
        var entries = await _service.GetEntriesAsync(tenantId, null, new DateTime(2026, 5, 1), new DateTime(2026, 5, 31));

        // Assert
        Assert.Single(entries);
        Assert.Equal(5, entries[0].TransactionDate.Month);
    }

    [Fact]
    public async Task GetEntries_ShouldReturnEmpty_WhenNoMatchingEntries()
    {
        // Arrange
        var tenantId = Guid.NewGuid();

        // Act
        var entries = await _service.GetEntriesAsync(tenantId, "nonexistent", null, null);

        // Assert
        Assert.Empty(entries);
    }
}
