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
    /// Unit Tests for HKDBookService - Week 1 implementation
    /// Tests Household Business Book operations with period calculations
    /// </summary>
    public class HKDBookServiceTests
    {
        private readonly Mock<IAccountingEntryRepository> _mockRepository;
        private readonly Mock<IHKDBookRepository> _mockHKDBookRepository;
        private readonly Mock<ILogger<HKDBookService>> _mockLogger;
        private readonly HKDBookService _service;

        public HKDBookServiceTests()
        {
            _mockRepository = new Mock<IAccountingEntryRepository>();
            _mockHKDBookRepository = new Mock<IHKDBookRepository>();
            _mockLogger = new Mock<ILogger<HKDBookService>>();
            _service = new HKDBookService(_mockRepository.Object, _mockHKDBookRepository.Object, _mockLogger.Object);
        }

        [Fact]
        public async Task RecordRevenueAsync_ShouldCreateEntry_WithCorrectPeriod()
        {
            // Arrange
            TenantId tenantId = new(Guid.NewGuid());
            decimal amount = 1000m;
            string description = "Test revenue";
            DateTime transactionDate = new(2024, 1, 15);

            // Act
            CoreAccountingEntry result = await _service.RecordRevenueAsync(tenantId, amount, description, transactionDate);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(tenantId.Value, result.TenantId.Value);
            Assert.Equal(2024, result.Period.Year);
            Assert.Equal(1, result.Period.Month);
            Assert.Equal(amount, result.Amount);
            Assert.Equal(description, result.Description);
            Assert.Equal(AccountingBookType.RevenueBook, result.AccountingBookType);

            _mockRepository.Verify(r => r.AddAsync(It.IsAny<CoreAccountingEntry>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task RecordRevenueAsync_ShouldCreateEntry_WithCurrentDateWhenNotProvided()
        {
            // Arrange
            TenantId tenantId = new(Guid.NewGuid());
            decimal amount = 500m;
            string description = "Test revenue";

            // Act
            CoreAccountingEntry result = await _service.RecordRevenueAsync(tenantId, amount, description);

            // Assert
            Assert.NotNull(result);
            AccountingPeriod currentPeriod = AccountingPeriod.Create(DateTime.UtcNow.Year, DateTime.UtcNow.Month);
            Assert.Equal(currentPeriod.Year, result.Period.Year);
            Assert.Equal(currentPeriod.Month, result.Period.Month);

            _mockRepository.Verify(r => r.AddAsync(It.IsAny<CoreAccountingEntry>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task RecordExpenseAsync_ShouldCreateEntry_WithCorrectPeriod()
        {
            // Arrange
            TenantId tenantId = new(Guid.NewGuid());
            decimal amount = 500m;
            string description = "Test expense";
            DateTime transactionDate = new(2024, 2, 10);

            // Act
            CoreAccountingEntry result = await _service.RecordExpenseAsync(tenantId, amount, description, transactionDate);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(tenantId.Value, result.TenantId.Value);
            Assert.Equal(2024, result.Period.Year);
            Assert.Equal(2, result.Period.Month);
            Assert.Equal(amount, result.Amount);
            Assert.Equal(description, result.Description);
            Assert.Equal(AccountingBookType.ExpenseBook, result.AccountingBookType);

            _mockRepository.Verify(r => r.AddAsync(It.IsAny<CoreAccountingEntry>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task GetRevenueTotalAsync_ShouldReturnCorrectTotal_ForPeriod()
        {
            // Arrange
            TenantId tenantId = new(Guid.NewGuid());
            AccountingPeriod period = AccountingPeriod.Create(2024, 1);
            List<CoreAccountingEntry> revenueEntries =
            [
                CoreAccountingEntry.CreateRevenue(tenantId, period, new Money(1000m, "VND"), "Revenue 1"),
                CoreAccountingEntry.CreateRevenue(tenantId, period, new Money(1500m, "VND"), "Revenue 2"),
                CoreAccountingEntry.CreateRevenue(tenantId, AccountingPeriod.Create(2024, 2), new Money(2000m, "VND"), "Revenue 3") // Different period
            ];

            _ = _mockRepository.Setup(r => r.GetByTenantAndBookTypeAsync(tenantId, AccountingBookType.RevenueBook, It.IsAny<CancellationToken>()))
                .ReturnsAsync(revenueEntries);

            // Act
            decimal result = await _service.GetRevenueTotalAsync(tenantId, period);

            // Assert
            Assert.Equal(2500m, result); // Only entries from period 2024-1

            _mockRepository.Verify(r => r.GetByTenantAndBookTypeAsync(tenantId, AccountingBookType.RevenueBook, It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task GetExpenseTotalAsync_ShouldReturnCorrectTotal_ForPeriod()
        {
            // Arrange
            TenantId tenantId = new(Guid.NewGuid());
            AccountingPeriod period = AccountingPeriod.Create(2024, 1);
            List<CoreAccountingEntry> expenseEntries =
            [
                CoreAccountingEntry.CreateExpense(tenantId, period, new Money(500m, "VND"), "Expense 1"),
                CoreAccountingEntry.CreateExpense(tenantId, period, new Money(300m, "VND"), "Expense 2"),
                CoreAccountingEntry.CreateExpense(tenantId, AccountingPeriod.Create(2024, 2), new Money(700m, "VND"), "Expense 3") // Different period
            ];

            _ = _mockRepository.Setup(r => r.GetByTenantAndBookTypeAsync(tenantId, AccountingBookType.ExpenseBook, It.IsAny<CancellationToken>()))
                .ReturnsAsync(expenseEntries);

            // Act
            decimal result = await _service.GetExpenseTotalAsync(tenantId, period);

            // Assert
            Assert.Equal(800m, result); // Only entries from period 2024-1

            _mockRepository.Verify(r => r.GetByTenantAndBookTypeAsync(tenantId, AccountingBookType.ExpenseBook, It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task GetProfitAsync_ShouldReturnCorrectProfit_ForPeriod()
        {
            // Arrange
            TenantId tenantId = new(Guid.NewGuid());
            AccountingPeriod period = AccountingPeriod.Create(2024, 1);
            List<CoreAccountingEntry> revenueEntries =
            [
                CoreAccountingEntry.CreateRevenue(tenantId, period, new Money(3000m, "VND"), "Revenue 1"),
                CoreAccountingEntry.CreateRevenue(tenantId, period, new Money(2000m, "VND"), "Revenue 2")
            ];
            List<CoreAccountingEntry> expenseEntries =
            [
                CoreAccountingEntry.CreateExpense(tenantId, period, new Money(1000m, "VND"), "Expense 1"),
                CoreAccountingEntry.CreateExpense(tenantId, period, new Money(1500m, "VND"), "Expense 2")
            ];

            _ = _mockRepository.Setup(r => r.GetByTenantAndBookTypeAsync(tenantId, AccountingBookType.RevenueBook, It.IsAny<CancellationToken>()))
                .ReturnsAsync(revenueEntries);
            _ = _mockRepository.Setup(r => r.GetByTenantAndBookTypeAsync(tenantId, AccountingBookType.ExpenseBook, It.IsAny<CancellationToken>()))
                .ReturnsAsync(expenseEntries);

            // Act
            decimal result = await _service.GetProfitAsync(tenantId, period);

            // Assert
            Assert.Equal(2500m, result); // 5000 - 2500

            _mockRepository.Verify(r => r.GetByTenantAndBookTypeAsync(tenantId, AccountingBookType.RevenueBook, It.IsAny<CancellationToken>()), Times.Once);
            _mockRepository.Verify(r => r.GetByTenantAndBookTypeAsync(tenantId, AccountingBookType.ExpenseBook, It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task GetProfitAsync_ShouldReturnNegativeProfit_WhenExpensesExceedRevenue()
        {
            // Arrange
            TenantId tenantId = new(Guid.NewGuid());
            AccountingPeriod period = AccountingPeriod.Create(2024, 1);
            List<CoreAccountingEntry> revenueEntries =
            [
                CoreAccountingEntry.CreateRevenue(tenantId, period, new Money(1000m, "VND"), "Revenue 1")
            ];
            List<CoreAccountingEntry> expenseEntries =
            [
                CoreAccountingEntry.CreateExpense(tenantId, period, new Money(2000m, "VND"), "Expense 1")
            ];

            _ = _mockRepository.Setup(r => r.GetByTenantAndBookTypeAsync(tenantId, AccountingBookType.RevenueBook, It.IsAny<CancellationToken>()))
                .ReturnsAsync(revenueEntries);
            _ = _mockRepository.Setup(r => r.GetByTenantAndBookTypeAsync(tenantId, AccountingBookType.ExpenseBook, It.IsAny<CancellationToken>()))
                .ReturnsAsync(expenseEntries);

            // Act
            decimal result = await _service.GetProfitAsync(tenantId, period);

            // Assert
            Assert.Equal(-1000m, result); // 1000 - 2000 = -1000

            _mockRepository.Verify(r => r.GetByTenantAndBookTypeAsync(tenantId, AccountingBookType.RevenueBook, It.IsAny<CancellationToken>()), Times.Once);
            _mockRepository.Verify(r => r.GetByTenantAndBookTypeAsync(tenantId, AccountingBookType.ExpenseBook, It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task GetRevenueEntriesAsync_ShouldReturnEntries_ForPeriod()
        {
            // Arrange
            TenantId tenantId = new(Guid.NewGuid());
            AccountingPeriod period = AccountingPeriod.Create(2024, 1);
            List<CoreAccountingEntry> allRevenueEntries =
            [
                CoreAccountingEntry.CreateRevenue(tenantId, period, new Money(1000m, "VND"), "Revenue 1"),
                CoreAccountingEntry.CreateRevenue(tenantId, period, new Money(1500m, "VND"), "Revenue 2"),
                CoreAccountingEntry.CreateRevenue(tenantId, AccountingPeriod.Create(2024, 2), new Money(2000m, "VND"), "Revenue 3") // Different period
            ];

            _ = _mockRepository.Setup(r => r.GetByTenantAndBookTypeAsync(tenantId, AccountingBookType.RevenueBook, It.IsAny<CancellationToken>()))
                .ReturnsAsync(allRevenueEntries);

            // Act
            IEnumerable<CoreAccountingEntry> result = await _service.GetRevenueEntriesAsync(tenantId, period);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(2, result.Count()); // Only entries from period 2024-1
            Assert.All(result, e =>
            {
                Assert.Equal(period.Year, e.Period.Year);
                Assert.Equal(period.Month, e.Period.Month);
                Assert.Equal(AccountingBookType.RevenueBook, e.AccountingBookType);
            });

            _mockRepository.Verify(r => r.GetByTenantAndBookTypeAsync(tenantId, AccountingBookType.RevenueBook, It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task GetExpenseEntriesAsync_ShouldReturnEntries_ForPeriod()
        {
            // Arrange
            TenantId tenantId = new(Guid.NewGuid());
            AccountingPeriod period = AccountingPeriod.Create(2024, 1);
            List<CoreAccountingEntry> allExpenseEntries =
            [
                CoreAccountingEntry.CreateExpense(tenantId, period, new Money(500m, "VND"), "Expense 1"),
                CoreAccountingEntry.CreateExpense(tenantId, period, new Money(300m, "VND"), "Expense 2"),
                CoreAccountingEntry.CreateExpense(tenantId, AccountingPeriod.Create(2024, 2), new Money(700m, "VND"), "Expense 3") // Different period
            ];

            _ = _mockRepository.Setup(r => r.GetByTenantAndBookTypeAsync(tenantId, AccountingBookType.ExpenseBook, It.IsAny<CancellationToken>()))
                .ReturnsAsync(allExpenseEntries);

            // Act
            IEnumerable<CoreAccountingEntry> result = await _service.GetExpenseEntriesAsync(tenantId, period);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(2, result.Count()); // Only entries from period 2024-1
            Assert.All(result, e =>
            {
                Assert.Equal(period.Year, e.Period.Year);
                Assert.Equal(period.Month, e.Period.Month);
                Assert.Equal(AccountingBookType.ExpenseBook, e.AccountingBookType);
            });

            _mockRepository.Verify(r => r.GetByTenantAndBookTypeAsync(tenantId, AccountingBookType.ExpenseBook, It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task GetRevenueTotalAsync_ShouldReturnZero_WhenNoRevenueEntries()
        {
            // Arrange
            TenantId tenantId = new(Guid.NewGuid());
            AccountingPeriod period = AccountingPeriod.Create(2024, 1);
            List<CoreAccountingEntry> emptyRevenueEntries = [];

            _ = _mockRepository.Setup(r => r.GetByTenantAndBookTypeAsync(tenantId, AccountingBookType.RevenueBook, It.IsAny<CancellationToken>()))
                .ReturnsAsync(emptyRevenueEntries);

            // Act
            decimal result = await _service.GetRevenueTotalAsync(tenantId, period);

            // Assert
            Assert.Equal(0m, result);

            _mockRepository.Verify(r => r.GetByTenantAndBookTypeAsync(tenantId, AccountingBookType.RevenueBook, It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task GetExpenseTotalAsync_ShouldReturnZero_WhenNoExpenseEntries()
        {
            // Arrange
            TenantId tenantId = new(Guid.NewGuid());
            AccountingPeriod period = AccountingPeriod.Create(2024, 1);
            List<CoreAccountingEntry> emptyExpenseEntries = [];

            _ = _mockRepository.Setup(r => r.GetByTenantAndBookTypeAsync(tenantId, AccountingBookType.ExpenseBook, It.IsAny<CancellationToken>()))
                .ReturnsAsync(emptyExpenseEntries);

            // Act
            decimal result = await _service.GetExpenseTotalAsync(tenantId, period);

            // Assert
            Assert.Equal(0m, result);

            _mockRepository.Verify(r => r.GetByTenantAndBookTypeAsync(tenantId, AccountingBookType.ExpenseBook, It.IsAny<CancellationToken>()), Times.Once);
        }
    }
}
