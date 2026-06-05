using Xunit;
using VanAn.Shared.DTOs;
using VanAn.CoreHub.Services;
using VanAn.Integration.Tests.Infrastructure;

namespace VanAn.Integration.Tests.Accounting;

public class MultiTenancyTests
{
    private readonly AccountingEntryServiceStub _service;

    public MultiTenancyTests()
    {
        _service = new AccountingEntryServiceStub();
    }

    [Fact]
    public async Task CreateRevenueEntry_ShouldNotLeakToOtherTenants()
    {
        // Arrange
        var tenant1 = Guid.NewGuid();
        var tenant2 = Guid.NewGuid();

        // Act
        await _service.CreateRevenueEntryAsync(tenant1, new RevenueEntryDto { Amount = 1000, AccountCode = "511", Date = DateTime.Today });

        // Assert
        var entries1 = await _service.GetEntriesAsync(tenant1, null, null, null);
        var entries2 = await _service.GetEntriesAsync(tenant2, null, null, null);

        Assert.Single(entries1);
        Assert.Empty(entries2);
    }

    [Fact]
    public async Task GetBalanceSummary_ShouldNotAggregateAcrossTenants()
    {
        // Arrange
        var tenant1 = Guid.NewGuid();
        var tenant2 = Guid.NewGuid();

        await _service.CreateRevenueEntryAsync(tenant1, new RevenueEntryDto { Amount = 1000000, AccountCode = "511", Date = DateTime.Today });
        await _service.CreateRevenueEntryAsync(tenant2, new RevenueEntryDto { Amount = 2000000, AccountCode = "511", Date = DateTime.Today });

        // Act
        var summary1 = await _service.GetBalanceSummaryAsync(tenant1);
        var summary2 = await _service.GetBalanceSummaryAsync(tenant2);

        // Assert
        Assert.Equal(1000000, summary1.TotalRevenue);
        Assert.Equal(2000000, summary2.TotalRevenue);
    }
}
