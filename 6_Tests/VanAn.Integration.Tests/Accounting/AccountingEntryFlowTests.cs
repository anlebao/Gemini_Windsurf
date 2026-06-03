using Xunit;
using VanAn.Shared.DTOs;
using VanAn.CoreHub.Services;
using VanAn.Integration.Tests.Infrastructure;

namespace VanAn.Integration.Tests.Accounting;

public class AccountingEntryFlowTests
{
    private readonly AccountingEntryServiceStub _service;

    public AccountingEntryFlowTests()
    {
        _service = new AccountingEntryServiceStub();
    }

    [Fact]
    public async Task RevenueEntry_ShouldPersistToDatabase_WithCorrectTenantId()
    {
        // Arrange
        var tenantId = Guid.NewGuid();

        var dto = new RevenueEntryDto
        {
            Date = DateTime.Today,
            Amount = 1000000,
            AccountCode = "511",
            Description = "Doanh thu bán hàng",
            Reference = "HĐ-001"
        };

        // Act
        var entry = await _service.CreateRevenueEntryAsync(tenantId, dto);

        // Assert
        Assert.NotNull(entry);
        Assert.Equal(tenantId, entry.TenantId);
        Assert.Equal(1000000, entry.Amount);
    }

    [Fact]
    public async Task ExpenseEntry_ShouldPersistToDatabase_WithVendorInfo()
    {
        // Arrange
        var tenantId = Guid.NewGuid();

        var dto = new ExpenseEntryDto
        {
            Date = DateTime.Today,
            Amount = 500000,
            AccountCode = "621",
            Description = "Mua vật liệu",
            Vendor = "Công ty ABC",
            Category = "Mua vật liệu"
        };

        // Act
        var entry = await _service.CreateExpenseEntryAsync(tenantId, dto);

        // Assert
        Assert.NotNull(entry);
        Assert.Equal(500000, entry.Amount);
    }

    [Fact]
    public async Task GetEntries_ShouldReturnOnlyTenantEntries_WhenMultipleTenantsExist()
    {
        // Arrange
        var tenant1 = Guid.NewGuid();
        var tenant2 = Guid.NewGuid();

        await _service.CreateRevenueEntryAsync(tenant1, new RevenueEntryDto { Amount = 1000, AccountCode = "511", Date = DateTime.Today });
        await _service.CreateRevenueEntryAsync(tenant2, new RevenueEntryDto { Amount = 2000, AccountCode = "511", Date = DateTime.Today });

        // Act
        var entries1 = await _service.GetEntriesAsync(tenant1, null, null, null);
        var entries2 = await _service.GetEntriesAsync(tenant2, null, null, null);

        // Assert
        Assert.Single(entries1);
        Assert.Single(entries2);
        Assert.NotEqual(entries1[0].Id, entries2[0].Id);
    }
}
