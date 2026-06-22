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
        private readonly Mock<IAuditTrailService> _mockAuditTrail;
        private readonly Mock<IPeriodClosingService> _mockPeriodClosing;
        private readonly Mock<ILogger<AccountingEntryService>> _mockLogger;
        private readonly AccountingEntryService _service;

        public AccountingEntryServiceTests()
        {
            _mockRepository = new Mock<IAccountingEntryRepository>();
            _mockAuditTrail = new Mock<IAuditTrailService>();
            _mockPeriodClosing = new Mock<IPeriodClosingService>();
            _mockLogger = new Mock<ILogger<AccountingEntryService>>();

            // Default: period is Open (no guard triggered)
            _mockPeriodClosing
                .Setup(p => p.GetPeriodStatusAsync(It.IsAny<AccountingPeriod>(), It.IsAny<TenantId>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(PeriodClosingStatus.Open);

            // Default: no recent entries (no duplicate)
            _mockRepository
                .Setup(r => r.GetByTenantAndDateRangeAsync(It.IsAny<TenantId>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync([]);

            _service = new AccountingEntryService(_mockRepository.Object, _mockAuditTrail.Object, _mockPeriodClosing.Object, _mockLogger.Object);
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

        // ===== Sprint C — C-1: Server-side Duplicate Detection =====

        [Fact]
        public async Task CreateRevenueEntryAsync_SC4_ShouldThrow_WhenDuplicateEntryWithinWindow()
        {
            // Arrange — an identical entry already exists within the 5-minute window
            TenantId tenantId = new(Guid.NewGuid());
            AccountingPeriod period = new(2024, 1);
            decimal amount = 1000m;
            string? accountCode = "511";

            CoreAccountingEntry existingEntry = CoreAccountingEntry.CreateRevenue(tenantId, period, new Money(amount), "Previous entry",
                accountCode: accountCode);

            _mockPeriodClosing
                .Setup(p => p.GetPeriodStatusAsync(period, tenantId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(PeriodClosingStatus.Open);

            _mockRepository
                .Setup(r => r.GetByTenantAndDateRangeAsync(tenantId, It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync([existingEntry]);

            // Act & Assert
            InvalidOperationException ex = await Assert.ThrowsAsync<InvalidOperationException>(
                () => _service.CreateRevenueEntryAsync(tenantId, period, amount, "Duplicate entry", accountCode: accountCode));

            Assert.Contains("Bút toán trùng lặp", ex.Message);
            _mockRepository.Verify(r => r.AddAsync(It.IsAny<CoreAccountingEntry>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task CreateRevenueEntryAsync_SC5_ShouldSucceed_WhenSameAmountButOutsideWindow()
        {
            // Arrange — identical amount + accountCode but CreatedAt > 5 minutes ago (simulated by no entry in window)
            TenantId tenantId = new(Guid.NewGuid());
            AccountingPeriod period = new(2024, 1);
            decimal amount = 1000m;
            string? accountCode = "511";

            // Repository returns empty — simulates the old entry is outside the 5-minute window
            _mockPeriodClosing
                .Setup(p => p.GetPeriodStatusAsync(period, tenantId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(PeriodClosingStatus.Open);

            _mockRepository
                .Setup(r => r.GetByTenantAndDateRangeAsync(tenantId, It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync([]);

            // Act — should NOT throw
            VanAn.Shared.DTOs.AccountingEntryDto result = await _service.CreateRevenueEntryAsync(tenantId, period, amount, "Entry after window", accountCode: accountCode);

            // Assert
            Assert.NotNull(result);
            _mockRepository.Verify(r => r.AddAsync(It.IsAny<CoreAccountingEntry>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task CreateRevenueEntryAsync_SC6_ShouldSucceed_WhenSameAmountButDifferentAccountCode()
        {
            // Arrange — same amount but different accountCode → not a duplicate
            TenantId tenantId = new(Guid.NewGuid());
            AccountingPeriod period = new(2024, 1);
            decimal amount = 1000m;

            // Existing entry has accountCode "511"
            CoreAccountingEntry existingEntry = CoreAccountingEntry.CreateRevenue(tenantId, period, new Money(amount), "Previous entry",
                accountCode: "511");

            _mockPeriodClosing
                .Setup(p => p.GetPeriodStatusAsync(period, tenantId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(PeriodClosingStatus.Open);

            _mockRepository
                .Setup(r => r.GetByTenantAndDateRangeAsync(tenantId, It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync([existingEntry]);

            // Act — new entry has accountCode "512" → should NOT throw
            VanAn.Shared.DTOs.AccountingEntryDto result = await _service.CreateRevenueEntryAsync(tenantId, period, amount, "Different account entry", accountCode: "512");

            // Assert
            Assert.NotNull(result);
            _mockRepository.Verify(r => r.AddAsync(It.IsAny<CoreAccountingEntry>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        // ===== Sprint C — C-2: Period Closing Guard =====

        [Fact]
        public async Task CreateRevenueEntryAsync_SC11_ShouldThrow_WhenPeriodIsClosed()
        {
            // Arrange
            TenantId tenantId = new(Guid.NewGuid());
            AccountingPeriod period = new(2024, 1);

            _mockPeriodClosing
                .Setup(p => p.GetPeriodStatusAsync(period, tenantId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(PeriodClosingStatus.Closed);

            // Act & Assert
            InvalidOperationException ex = await Assert.ThrowsAsync<InvalidOperationException>(
                () => _service.CreateRevenueEntryAsync(tenantId, period, 1000m, "Should fail"));

            Assert.Contains("đã đóng sổ", ex.Message);
            Assert.Contains("2024/01", ex.Message);
            _mockRepository.Verify(r => r.AddAsync(It.IsAny<CoreAccountingEntry>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task CreateRevenueEntryAsync_SC12_ShouldSucceed_WhenPeriodIsOpen()
        {
            // Arrange
            TenantId tenantId = new(Guid.NewGuid());
            AccountingPeriod period = new(2024, 1);

            _mockPeriodClosing
                .Setup(p => p.GetPeriodStatusAsync(period, tenantId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(PeriodClosingStatus.Open);

            // Act
            VanAn.Shared.DTOs.AccountingEntryDto result = await _service.CreateRevenueEntryAsync(tenantId, period, 1000m, "Open period entry");

            // Assert
            Assert.NotNull(result);
            _mockRepository.Verify(r => r.AddAsync(It.IsAny<CoreAccountingEntry>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task CreateRevenueEntryAsync_SC13_ShouldSucceed_WhenTargetPeriodIsOpenButAnotherIsClosed()
        {
            // Arrange — period 2024/01 is Open, period 2024/02 is Closed
            TenantId tenantId = new(Guid.NewGuid());
            AccountingPeriod openPeriod = new(2024, 1);
            AccountingPeriod closedPeriod = new(2024, 2);

            _mockPeriodClosing
                .Setup(p => p.GetPeriodStatusAsync(openPeriod, tenantId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(PeriodClosingStatus.Open);

            _mockPeriodClosing
                .Setup(p => p.GetPeriodStatusAsync(closedPeriod, tenantId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(PeriodClosingStatus.Closed);

            // Act — create entry into openPeriod → should succeed
            VanAn.Shared.DTOs.AccountingEntryDto result = await _service.CreateRevenueEntryAsync(tenantId, openPeriod, 500m, "Entry in open period");

            // Assert
            Assert.NotNull(result);
            _mockRepository.Verify(r => r.AddAsync(It.IsAny<CoreAccountingEntry>(), It.IsAny<CancellationToken>()), Times.Once);
        }
    }
}
