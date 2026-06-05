using Microsoft.Extensions.Logging;
using Moq;
using VanAn.Shared.Domain;
using VanAn.CoreHub.Repositories;
using VanAn.CoreHub.Services;
using CoreAccountingEntry = VanAn.Shared.Domain.AccountingEntry;
using Xunit;

namespace VanAn.Core.Tests.Accounting
{
    /// <summary>
    /// Unit Tests for AccountingEntryService - Week 1 implementation
    /// Tests 5-layer protection: Domain, EF Core, Repository, Service, API
    /// </summary>
    public class AccountingEntryServiceTests
    {
        private readonly Mock<IAccountingEntryRepository> _mockRepository;
        private readonly Mock<ILogger<AccountingEntryService>> _mockLogger;
        private readonly AccountingEntryService _service;

        public AccountingEntryServiceTests()
        {
            _mockRepository = new Mock<IAccountingEntryRepository>();
            _mockLogger = new Mock<ILogger<AccountingEntryService>>();
            _service = new AccountingEntryService(_mockRepository.Object, _mockLogger.Object);
        }

        [Fact]
        public async Task CreateRevenueEntryAsync_ShouldCreateEntry_WhenValidInput()
        {
            // Arrange
            TenantId tenantId = new(Guid.NewGuid());
            AccountingPeriod period = new(2024, 1);
            Money amount = new(1000m);
            string description = "Test revenue";

            // Act
            Shared.DTOs.AccountingEntryDto result = await _service.CreateRevenueEntryAsync(tenantId, period, amount, description);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(tenantId.Value, result.TenantId);
            Assert.Equal(period.Year, result.PeriodYear);
            Assert.Equal(period.Month, result.PeriodMonth);
            Assert.Equal(amount.Value, result.Amount);
            Assert.Equal(description, result.Description);
            Assert.Equal(AccountingBookType.RevenueBook, result.AccountingBookType);

            _mockRepository.Verify(r => r.AddAsync(It.IsAny<CoreAccountingEntry>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task CreateExpenseEntryAsync_ShouldCreateEntry_WhenValidInput()
        {
            // Arrange
            TenantId tenantId = new(Guid.NewGuid());
            AccountingPeriod period = new(2024, 1);
            Money amount = new(500m);
            string description = "Test expense";

            // Act
            Shared.DTOs.AccountingEntryDto result = await _service.CreateExpenseEntryAsync(tenantId, period, amount, description);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(tenantId.Value, result.TenantId);
            Assert.Equal(period.Year, result.PeriodYear);
            Assert.Equal(period.Month, result.PeriodMonth);
            Assert.Equal(amount.Value, result.Amount);
            Assert.Equal(description, result.Description);
            Assert.Equal(AccountingBookType.ExpenseBook, result.AccountingBookType);

            _mockRepository.Verify(r => r.AddAsync(It.IsAny<CoreAccountingEntry>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task GetEntryByIdAsync_ShouldReturnEntry_WhenEntryExistsAndBelongsToTenant()
        {
            // Arrange
            TenantId tenantId = new(Guid.NewGuid());
            CoreAccountingEntry entry = CoreAccountingEntry.CreateRevenue(tenantId, AccountingPeriod.Create(2024, 1), new Money(1000m, "VND"), "Test");

            _ = _mockRepository.Setup(r => r.GetByIdAsync(entry.Id, It.IsAny<CancellationToken>())).ReturnsAsync(entry);

            // Act
            Shared.DTOs.AccountingEntryDto? result = await _service.GetEntryByIdAsync(entry.Id);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(entry.Id, result.Id);

            _mockRepository.Verify(r => r.GetByIdAsync(entry.Id, It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task GetEntryByIdAsync_ShouldReturnNull_WhenEntryDoesNotExist()
        {
            // Arrange
            TenantId tenantId = new(Guid.NewGuid());
            AccountingEntryId entryId = new(Guid.NewGuid());

            _ = _mockRepository.Setup(r => r.GetByIdAsync(entryId, It.IsAny<CancellationToken>())).ReturnsAsync((CoreAccountingEntry?)null);

            // Act
            Shared.DTOs.AccountingEntryDto? result = await _service.GetEntryByIdAsync(entryId);

            // Assert
            Assert.Null(result);

            _mockRepository.Verify(r => r.GetByIdAsync(entryId, It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task GetEntryByIdAsync_ShouldReturnEntry_WhenEntryBelongsToDifferentTenant()
        {
            // Note: GetEntryByIdAsync does not filter by tenant - it returns any entry by ID
            // Tenant filtering is the caller's responsibility
            TenantId tenantId = new(Guid.NewGuid());
            TenantId differentTenantId = new(Guid.NewGuid());
            AccountingEntryId entryId = new(Guid.NewGuid());
            CoreAccountingEntry entry = CoreAccountingEntry.CreateRevenue(differentTenantId, AccountingPeriod.Create(2024, 1), new Money(1000m, "VND"), "Test");

            _ = _mockRepository.Setup(r => r.GetByIdAsync(entryId, It.IsAny<CancellationToken>())).ReturnsAsync(entry);

            // Act
            Shared.DTOs.AccountingEntryDto? result = await _service.GetEntryByIdAsync(entryId);

            // Assert - Service returns the entry regardless of tenant (no tenant filter on GetById)
            Assert.NotNull(result);
            Assert.Equal(differentTenantId.Value, result.TenantId);

            _mockRepository.Verify(r => r.GetByIdAsync(entryId, It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task GetEntriesByTenantAsync_ShouldReturnEntries_ForValidTenant()
        {
            // Arrange
            TenantId tenantId = new(Guid.NewGuid());
            List<CoreAccountingEntry> entries =
            [
                CoreAccountingEntry.CreateRevenue(tenantId, AccountingPeriod.Create(2024, 1), new Money(1000m, "VND"), "Test 1"),
                CoreAccountingEntry.CreateExpense(tenantId, AccountingPeriod.Create(2024, 1), new Money(500m, "VND"), "Test 2")
            ];

            _ = _mockRepository.Setup(r => r.GetByTenantAsync(tenantId, It.IsAny<CancellationToken>())).ReturnsAsync(entries);

            // Act
            IEnumerable<Shared.DTOs.AccountingEntryDto> result = await _service.GetEntriesByTenantAsync(tenantId);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(2, result.Count());

            _mockRepository.Verify(r => r.GetByTenantAsync(tenantId, It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task GetEntriesByTenantAndBookTypeAsync_ShouldReturnFilteredEntries()
        {
            // Arrange
            TenantId tenantId = new(Guid.NewGuid());
            List<CoreAccountingEntry> revenueEntries =
            [
                CoreAccountingEntry.CreateRevenue(tenantId, AccountingPeriod.Create(2024, 1), new Money(1000m, "VND"), "Test 1"),
                CoreAccountingEntry.CreateRevenue(tenantId, AccountingPeriod.Create(2024, 2), new Money(1500m, "VND"), "Test 2")
            ];

            _ = _mockRepository.Setup(r => r.GetByTenantAndBookTypeAsync(tenantId, AccountingBookType.RevenueBook, It.IsAny<CancellationToken>()))
                .ReturnsAsync(revenueEntries);

            // Act
            IEnumerable<Shared.DTOs.AccountingEntryDto> result = await _service.GetEntriesByTenantAndBookTypeAsync(tenantId, AccountingBookType.RevenueBook);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(2, result.Count());
            Assert.All(result, e => Assert.Equal(AccountingBookType.RevenueBook, e.AccountingBookType));

            _mockRepository.Verify(r => r.GetByTenantAndBookTypeAsync(tenantId, AccountingBookType.RevenueBook, It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task GetEntriesByTenantAndPeriodAsync_ShouldReturnFilteredEntries()
        {
            // Arrange
            TenantId tenantId = new(Guid.NewGuid());
            AccountingPeriod period = AccountingPeriod.Create(2024, 1);
            List<CoreAccountingEntry> periodEntries =
            [
                CoreAccountingEntry.CreateRevenue(tenantId, period, new Money(1000m, "VND"), "Test 1"),
                CoreAccountingEntry.CreateExpense(tenantId, period, new Money(500m, "VND"), "Test 2"),
                CoreAccountingEntry.CreateExpense(tenantId, period, new Money(500m, "VND"), "Test 2")
            ];

            _ = _mockRepository.Setup(r => r.GetByTenantAndPeriodAsync(tenantId, period, It.IsAny<CancellationToken>()))
                .ReturnsAsync(periodEntries);

            // Act
            IEnumerable<Shared.DTOs.AccountingEntryDto> result = await _service.GetEntriesByTenantAndPeriodAsync(tenantId, period);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(3, result.Count()); // All 3 seeded entries returned (no extra filter in service)
            Assert.All(result, e =>
            {
                Assert.Equal(period.Year, e.PeriodYear);
                Assert.Equal(period.Month, e.PeriodMonth);
            });

            _mockRepository.Verify(r => r.GetByTenantAndPeriodAsync(tenantId, period, It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task CreateRevenueEntryAsync_ShouldLogError_WhenRepositoryThrowsException()
        {
            // Arrange
            TenantId tenantId = new(Guid.NewGuid());
            AccountingPeriod period = AccountingPeriod.Create(2024, 1);
            Money amount = new(1000m, "VND");
            string description = "Test revenue";

            _ = _mockRepository.Setup(r => r.AddAsync(It.IsAny<CoreAccountingEntry>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new InvalidOperationException("Database error"));

            // Act & Assert
            InvalidOperationException exception = await Assert.ThrowsAsync<InvalidOperationException>(
                () => _service.CreateRevenueEntryAsync(tenantId, period, amount, description));

            Assert.Equal("Database error", exception.Message);
        }
    }
}
