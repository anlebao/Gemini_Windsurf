using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Moq;
using VanAn.Shared.Domain;
using VanAn.CoreHub.Services;
using VanAn.Gateway.Controllers;
using VanAn.Shared.DTOs;
using CoreAccountingEntry = VanAn.Shared.Domain.AccountingEntry;
using Xunit;

namespace VanAn.Core.Tests.Accounting
{
    /// <summary>
    /// Integration Tests for AccountingEntriesController - Week 1 implementation
    /// Tests API layer with service layer integration
    /// </summary>
    public class AccountingEntriesControllerTests
    {
        private readonly Mock<IAccountingService> _mockAccountingService;
        private readonly Mock<IReversalService> _mockReversalService;
        private readonly Mock<IHKDBookService> _mockHKDBookService;
        private readonly Mock<ILogger<AccountingEntriesController>> _mockLogger;
        private readonly AccountingEntriesController _controller;

        public AccountingEntriesControllerTests()
        {
            _mockAccountingService = new Mock<IAccountingService>();
            _mockReversalService = new Mock<IReversalService>();
            _mockHKDBookService = new Mock<IHKDBookService>();
            _mockLogger = new Mock<ILogger<AccountingEntriesController>>();
            _controller = new AccountingEntriesController(
                _mockAccountingService.Object,
                _mockReversalService.Object,
                _mockHKDBookService.Object,
                _mockLogger.Object);
        }

        [Fact]
        public async Task CreateRevenueEntry_ShouldReturnCreated_WhenValidRequest()
        {
            // Arrange
            CreateRevenueEntryRequest request = new()
            {
                TenantId = Guid.NewGuid(),
                Year = 2024,
                Month = 1,
                Amount = 1000m,
                Currency = "VND",
                Description = "Test revenue"
            };

            CoreAccountingEntry expectedEntry = CoreAccountingEntry.CreateRevenue(
                new TenantId(request.TenantId),
                AccountingPeriod.Create(request.Year, request.Month),
                new Money(request.Amount, request.Currency),
                request.Description);

            AccountingEntryDto expectedDto = new()
            {
                Id = expectedEntry.Id,
                TenantId = expectedEntry.TenantId,
                Amount = expectedEntry.Amount,
                Description = expectedEntry.Description,
                EntryType = AccountingEntryType.Revenue,
                CreatedAt = expectedEntry.CreatedAt,
                AccountingBookType = expectedEntry.AccountingBookType,
                PeriodYear = expectedEntry.Period.Year,
                PeriodMonth = expectedEntry.Period.Month,
                ReversalEntryId = expectedEntry.ReversalEntryId,
                TransactionDate = expectedEntry.CreatedAt
            };

            _ = _mockAccountingService.Setup(s => s.CreateRevenueEntryAsync(
                It.IsAny<TenantId>(), It.IsAny<AccountingPeriod>(), It.IsAny<decimal>(), It.IsAny<string>()))
                .ReturnsAsync(expectedDto);

            // Set up tenant header
            _controller.ControllerContext.HttpContext = new DefaultHttpContext();
            _controller.ControllerContext.HttpContext.Request.Headers["X-Tenant-Id"] = request.TenantId.ToString();

            // Act
            ActionResult<CoreAccountingEntry> result = await _controller.CreateRevenueEntry(request);

            // Assert
            CreatedAtActionResult createdResult = Assert.IsType<CreatedAtActionResult>(result.Result);
            Assert.Equal(expectedDto, createdResult.Value);
            Assert.Equal(nameof(_controller.GetEntryById), createdResult.ActionName);
            Assert.Equal(expectedDto.Id, createdResult.RouteValues?["id"]);

            _mockAccountingService.Verify(s => s.CreateRevenueEntryAsync(
                It.IsAny<TenantId>(), It.IsAny<AccountingPeriod>(), It.IsAny<decimal>(), It.IsAny<string>()), Times.Once);
        }

        [Fact]
        public async Task CreateRevenueEntry_ShouldReturnBadRequest_WhenInvalidModel()
        {
            // Arrange
            CreateRevenueEntryRequest request = new()
            {
                TenantId = Guid.Empty, // Invalid
                Year = 2024,
                Month = 1,
                Amount = -1000m, // Invalid
                Currency = "VND",
                Description = ""
            };

            _controller.ModelState.AddModelError("TenantId", "Tenant ID is required");
            _controller.ModelState.AddModelError("Amount", "Amount must be positive");

            // Act
            ActionResult<CoreAccountingEntry> result = await _controller.CreateRevenueEntry(request);

            // Assert
            _ = Assert.IsType<BadRequestObjectResult>(result.Result);

            _mockAccountingService.Verify(s => s.CreateRevenueEntryAsync(
                It.IsAny<TenantId>(), It.IsAny<AccountingPeriod>(), It.IsAny<decimal>(), It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public async Task CreateExpenseEntry_ShouldReturnCreated_WhenValidRequest()
        {
            // Arrange
            CreateExpenseEntryRequest request = new()
            {
                TenantId = Guid.NewGuid(),
                Year = 2024,
                Month = 1,
                Amount = 500m,
                Currency = "VND",
                Description = "Test expense"
            };

            CoreAccountingEntry expectedEntry = CoreAccountingEntry.CreateExpense(
                new TenantId(request.TenantId),
                AccountingPeriod.Create(request.Year, request.Month),
                new Money(request.Amount, request.Currency),
                request.Description);

            AccountingEntryDto expectedDto = new()
            {
                Id = expectedEntry.Id,
                TenantId = expectedEntry.TenantId,
                Amount = expectedEntry.Amount,
                Description = expectedEntry.Description,
                EntryType = AccountingEntryType.Expense,
                CreatedAt = expectedEntry.CreatedAt,
                AccountingBookType = expectedEntry.AccountingBookType,
                PeriodYear = expectedEntry.Period.Year,
                PeriodMonth = expectedEntry.Period.Month,
                ReversalEntryId = expectedEntry.ReversalEntryId,
                TransactionDate = expectedEntry.CreatedAt
            };

            _ = _mockAccountingService.Setup(s => s.CreateExpenseEntryAsync(
                It.IsAny<TenantId>(), It.IsAny<AccountingPeriod>(), It.IsAny<decimal>(), It.IsAny<string>()))
                .ReturnsAsync(expectedDto);

            // Set up tenant header
            _controller.ControllerContext.HttpContext = new DefaultHttpContext();
            _controller.ControllerContext.HttpContext.Request.Headers["X-Tenant-Id"] = request.TenantId.ToString();

            // Act
            ActionResult<CoreAccountingEntry> result = await _controller.CreateExpenseEntry(request);

            // Assert
            CreatedAtActionResult createdResult = Assert.IsType<CreatedAtActionResult>(result.Result);
            Assert.Equal(expectedDto, createdResult.Value);

            _mockAccountingService.Verify(s => s.CreateExpenseEntryAsync(
                It.IsAny<TenantId>(), It.IsAny<AccountingPeriod>(), It.IsAny<decimal>(), It.IsAny<string>()), Times.Once);
        }

        [Fact]
        public async Task GetEntryById_ShouldReturnOk_WhenEntryExists()
        {
            // Arrange
            Guid entryId = Guid.NewGuid();
            Guid tenantId = Guid.NewGuid();
            AccountingEntryDto expectedDto = new()
            {
                Id = entryId,
                TenantId = tenantId,
                Amount = new Money(1000m, "VND"),
                Description = "Test entry",
                EntryType = AccountingEntryType.Revenue,
                CreatedAt = DateTime.UtcNow,
                AccountingBookType = AccountingBookType.RevenueBook,
                PeriodYear = 2024,
                PeriodMonth = 1
            };

            List<AccountingEntryDto> entriesList = [expectedDto];

            _ = _mockAccountingService.Setup(s => s.GetEntriesByDateRangeAsync(
                It.IsAny<Guid>(), It.IsAny<DateTime>(), It.IsAny<DateTime>()))
                .ReturnsAsync(entriesList);

            // Set up tenant header
            _controller.ControllerContext.HttpContext = new DefaultHttpContext();
            _controller.ControllerContext.HttpContext.Request.Headers["X-Tenant-Id"] = tenantId.ToString();

            // Act
            ActionResult<CoreAccountingEntry> result = await _controller.GetEntryById(entryId);

            // Assert
            OkObjectResult okResult = Assert.IsType<OkObjectResult>(result.Result);
            Assert.Equal(expectedDto, okResult.Value);

            _mockAccountingService.Verify(s => s.GetEntriesByDateRangeAsync(
                It.IsAny<Guid>(), It.IsAny<DateTime>(), It.IsAny<DateTime>()), Times.Once);
        }

        [Fact]
        public async Task GetEntryById_ShouldReturnUnauthorized_WhenTenantIdMissing()
        {
            // Arrange
            Guid entryId = Guid.NewGuid();

            // No tenant header set
            _controller.ControllerContext.HttpContext = new DefaultHttpContext();

            // Act
            ActionResult<CoreAccountingEntry> result = await _controller.GetEntryById(entryId);

            // Assert
            UnauthorizedObjectResult unauthorizedResult = Assert.IsType<UnauthorizedObjectResult>(result.Result);
            Assert.Equal("Tenant ID required", unauthorizedResult.Value);

            _mockAccountingService.Verify(s => s.GetEntryByIdAsync(
                It.IsAny<Guid>()), Times.Never);
        }

        [Fact]
        public async Task GetEntryById_ShouldReturnNotFound_WhenEntryDoesNotExist()
        {
            // Arrange
            Guid entryId = Guid.NewGuid();
            Guid tenantId = Guid.NewGuid();

            // Return empty list - entry not found
            _ = _mockAccountingService.Setup(s => s.GetEntriesByDateRangeAsync(
                It.IsAny<Guid>(), It.IsAny<DateTime>(), It.IsAny<DateTime>()))
                .ReturnsAsync(new List<AccountingEntryDto>());

            // Set up tenant header
            _controller.ControllerContext.HttpContext = new DefaultHttpContext();
            _controller.ControllerContext.HttpContext.Request.Headers["X-Tenant-Id"] = tenantId.ToString();

            // Act
            ActionResult<CoreAccountingEntry> result = await _controller.GetEntryById(entryId);

            // Assert
            _ = Assert.IsType<NotFoundResult>(result.Result);

            _mockAccountingService.Verify(s => s.GetEntriesByDateRangeAsync(
                It.IsAny<Guid>(), It.IsAny<DateTime>(), It.IsAny<DateTime>()), Times.Once);
        }

        [Fact]
        public async Task CreateReversalEntry_ShouldReturnCreated_WhenValidRequest()
        {
            // Arrange
            Guid originalEntryId = Guid.NewGuid();
            Guid tenantId = Guid.NewGuid();
            CreateReversalEntryRequest request = new()
            {
                Reason = "Test reversal"
            };

            CoreAccountingEntry originalEntry = CoreAccountingEntry.CreateRevenue(
                new TenantId(tenantId),
                AccountingPeriod.Create(2024, 1),
                new Money(1000m, "VND"),
                "Original entry");

            CoreAccountingEntry reversalEntry = CoreAccountingEntry.CreateReversal(originalEntry, request.Reason);

            _ = _mockReversalService.Setup(s => s.CanReverseEntryAsync(
                It.IsAny<AccountingEntryId>(), It.IsAny<TenantId>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);

            _ = _mockReversalService.Setup(s => s.CreateReversalEntryAsync(
                It.IsAny<AccountingEntryId>(), It.IsAny<TenantId>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(reversalEntry);

            // Set up tenant header
            _controller.ControllerContext.HttpContext = new DefaultHttpContext();
            _controller.ControllerContext.HttpContext.Request.Headers["X-Tenant-Id"] = tenantId.ToString();

            // Act
            ActionResult<CoreAccountingEntry> result = await _controller.CreateReversalEntry(originalEntryId, request);

            // Assert
            CreatedAtActionResult createdResult = Assert.IsType<CreatedAtActionResult>(result.Result);
            Assert.Equal(reversalEntry, createdResult.Value);
            Assert.Equal(nameof(_controller.GetEntryById), createdResult.ActionName);
            Assert.Equal(reversalEntry.Id, createdResult.RouteValues?["id"]);

            _mockReversalService.Verify(s => s.CanReverseEntryAsync(
                It.IsAny<AccountingEntryId>(), It.IsAny<TenantId>(), It.Is<CancellationToken>(c => c.IsCancellationRequested == false)), Times.Once);
            _mockReversalService.Verify(s => s.CreateReversalEntryAsync(
                It.IsAny<AccountingEntryId>(), It.IsAny<TenantId>(), It.IsAny<string>(), It.Is<CancellationToken>(c => c.IsCancellationRequested == false)), Times.Once);
        }

        [Fact]
        public async Task CreateReversalEntry_ShouldReturnBadRequest_WhenEntryCannotBeReversed()
        {
            // Arrange
            Guid originalEntryId = Guid.NewGuid();
            Guid tenantId = Guid.NewGuid();
            CreateReversalEntryRequest request = new()
            {
                Reason = "Test reversal"
            };

            _ = _mockReversalService.Setup(s => s.CanReverseEntryAsync(
                It.IsAny<AccountingEntryId>(), It.IsAny<TenantId>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(false);

            // Set up tenant header
            _controller.ControllerContext.HttpContext = new DefaultHttpContext();
            _controller.ControllerContext.HttpContext.Request.Headers["X-Tenant-Id"] = tenantId.ToString();

            // Act
            ActionResult<CoreAccountingEntry> result = await _controller.CreateReversalEntry(originalEntryId, request);

            // Assert
            BadRequestObjectResult badRequestResult = Assert.IsType<BadRequestObjectResult>(result.Result);
            Assert.Equal("Entry cannot be reversed", badRequestResult.Value);

            _mockReversalService.Verify(s => s.CanReverseEntryAsync(
                It.IsAny<AccountingEntryId>(), It.IsAny<TenantId>(), It.Is<CancellationToken>(c => c.IsCancellationRequested == false)), Times.Once);
            _mockReversalService.Verify(s => s.CreateReversalEntryAsync(
                It.IsAny<AccountingEntryId>(), It.IsAny<TenantId>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task GetRevenueSummary_ShouldReturnSummary_WhenValidRequest()
        {
            // Arrange
            Guid tenantId = Guid.NewGuid();
            int year = 2024;
            int month = 1;
            AccountingPeriod period = AccountingPeriod.Create(year, month);
            decimal totalRevenue = 5000m;
            List<CoreAccountingEntry> entries =
            [
                CoreAccountingEntry.CreateRevenue(new TenantId(tenantId), period, new Money(3000m, "VND"), "Revenue 1"),
                CoreAccountingEntry.CreateRevenue(new TenantId(tenantId), period, new Money(2000m, "VND"), "Revenue 2")
            ];

            _ = _mockHKDBookService.Setup(s => s.GetRevenueTotalAsync(It.IsAny<TenantId>(), It.IsAny<AccountingPeriod>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(totalRevenue);
            _ = _mockHKDBookService.Setup(s => s.GetRevenueEntriesAsync(It.IsAny<TenantId>(), It.IsAny<AccountingPeriod>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(entries);

            // Set up tenant header
            _controller.ControllerContext.HttpContext = new DefaultHttpContext();
            _controller.ControllerContext.HttpContext.Request.Headers["X-Tenant-Id"] = tenantId.ToString();

            // Act
            ActionResult<RevenueSummaryResponse> result = await _controller.GetRevenueSummary(year, month);

            // Assert
            OkObjectResult okResult = Assert.IsType<OkObjectResult>(result.Result);
            RevenueSummaryResponse summary = Assert.IsType<RevenueSummaryResponse>(okResult.Value);

            Assert.Equal($"{year}-{month:D2}", summary.Period);
            Assert.Equal(totalRevenue, summary.TotalRevenue);
            Assert.Equal(2, summary.EntryCount);
            Assert.Equal(2, summary.Entries.Count);

            _mockHKDBookService.Verify(s => s.GetRevenueTotalAsync(It.IsAny<TenantId>(), It.IsAny<AccountingPeriod>(), It.IsAny<CancellationToken>()), Times.Once);
            _mockHKDBookService.Verify(s => s.GetRevenueEntriesAsync(It.IsAny<TenantId>(), It.IsAny<AccountingPeriod>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task GetProfitSummary_ShouldReturnSummary_WhenValidRequest()
        {
            // Arrange
            Guid tenantId = Guid.NewGuid();
            int year = 2024;
            int month = 1;
            decimal profit = 2500m;
            decimal revenue = 5000m;
            decimal expense = 2500m;

            _ = _mockHKDBookService.Setup(s => s.GetProfitAsync(It.IsAny<TenantId>(), It.IsAny<AccountingPeriod>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(profit);
            _ = _mockHKDBookService.Setup(s => s.GetRevenueTotalAsync(It.IsAny<TenantId>(), It.IsAny<AccountingPeriod>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(revenue);
            _ = _mockHKDBookService.Setup(s => s.GetExpenseTotalAsync(It.IsAny<TenantId>(), It.IsAny<AccountingPeriod>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(expense);

            // Set up tenant header
            _controller.ControllerContext.HttpContext = new DefaultHttpContext();
            _controller.ControllerContext.HttpContext.Request.Headers["X-Tenant-Id"] = tenantId.ToString();

            // Act
            ActionResult<ProfitSummaryResponse> result = await _controller.GetProfitSummary(year, month);

            // Assert
            OkObjectResult okResult = Assert.IsType<OkObjectResult>(result.Result);
            ProfitSummaryResponse summary = Assert.IsType<ProfitSummaryResponse>(okResult.Value);

            Assert.Equal($"{year}-{month:D2}", summary.Period);
            Assert.Equal(profit, summary.Profit);
            Assert.Equal(revenue, summary.Revenue);
            Assert.Equal(expense, summary.Expense);

            _mockHKDBookService.Verify(s => s.GetProfitAsync(It.IsAny<TenantId>(), It.IsAny<AccountingPeriod>(), It.IsAny<CancellationToken>()), Times.Once);
            _mockHKDBookService.Verify(s => s.GetRevenueTotalAsync(It.IsAny<TenantId>(), It.IsAny<AccountingPeriod>(), It.IsAny<CancellationToken>()), Times.Once);
            _mockHKDBookService.Verify(s => s.GetExpenseTotalAsync(It.IsAny<TenantId>(), It.IsAny<AccountingPeriod>(), It.IsAny<CancellationToken>()), Times.Once);
        }
    }
}
