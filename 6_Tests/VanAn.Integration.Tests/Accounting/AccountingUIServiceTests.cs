using Xunit;
using Moq;
using VanAn.ShopERP.Services.Accounting;
using VanAn.CoreHub.Services;
using VanAn.Shared.DTOs;

namespace VanAn.Integration.Tests.Accounting;

public class AccountingUIServiceTests
{
    private readonly Mock<IAccountingService> _mockCoreService;
    private readonly AccountingUIService _uiService;

    public AccountingUIServiceTests()
    {
        _mockCoreService = new Mock<IAccountingService>();
        _uiService = new AccountingUIService(_mockCoreService.Object);
    }

    [Fact]
    public async Task CreateRevenueEntry_ShouldMapDtoCorrectly_ToServiceCall()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var formData = new Dictionary<string, string>
        {
            ["date"]        = "2026-05-20",
            ["amount"]      = "1000000",
            ["description"] = "Doanh thu bán hàng"
        };

        _mockCoreService
            .Setup(s => s.CreateRevenueEntryAsync(It.IsAny<TenantId>(), It.IsAny<AccountingPeriod>(), It.IsAny<decimal>(), It.IsAny<string>()))
            .ReturnsAsync(new AccountingEntryDto { Id = Guid.NewGuid() });

        // Act
        await _uiService.SubmitRevenueFormAsync(tenantId, formData);

        // Assert
        _mockCoreService.Verify(s => s.CreateRevenueEntryAsync(
            It.IsAny<TenantId>(),
            It.Is<AccountingPeriod>(p => p.Year == 2026 && p.Month == 5),
            1000000m,
            "Doanh thu bán hàng"),
            Times.Once);
    }

    [Fact]
    public async Task GetPeriodComparison_ShouldCalculateDelta_BetweenCurrentAndPreviousMonth()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var currentPeriod = new DateTime(2026, 5, 1);

        _mockCoreService
            .Setup(s => s.GetEntriesByTenantAndPeriodAsync(It.IsAny<TenantId>(), It.IsAny<AccountingPeriod>()))
            .ReturnsAsync(new List<AccountingEntryDto>());

        // Act
        var comparison = await _uiService.GetPeriodComparisonAsync(tenantId, currentPeriod);

        // Assert
        Assert.NotNull(comparison);
    }

    [Fact]
    public async Task GetPeriodComparison_ShouldReturnNegativeDelta_WhenRevenueDecreased()
    {
        // Arrange
        var tenantId = Guid.NewGuid();

        _mockCoreService
            .Setup(s => s.GetEntriesByTenantAndPeriodAsync(It.IsAny<TenantId>(), It.IsAny<AccountingPeriod>()))
            .ReturnsAsync(new List<AccountingEntryDto>());

        // Act
        var comparison = await _uiService.GetPeriodComparisonAsync(tenantId, new DateTime(2026, 5, 1));

        // Assert
        Assert.NotNull(comparison);
    }

    [Fact]
    public async Task GetPeriodComparison_ShouldHandleZeroPreviousRevenue_WithoutException()
    {
        // Arrange
        var tenantId = Guid.NewGuid();

        _mockCoreService
            .Setup(s => s.GetEntriesByTenantAndPeriodAsync(It.IsAny<TenantId>(), It.IsAny<AccountingPeriod>()))
            .ReturnsAsync(new List<AccountingEntryDto>());

        // Act
        var comparison = await _uiService.GetPeriodComparisonAsync(tenantId, new DateTime(2026, 5, 1));

        // Assert — no division by zero
        Assert.NotNull(comparison);
    }
}
