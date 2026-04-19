using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Moq;
using VanAn.Shared.Domain;
using VanAn.CoreHub.Services;
using VanAn.Gateway.Controllers;
using CoreAccountingEntry = VanAn.Shared.Domain.AccountingEntry;
using Xunit;

namespace VanAn.Core.Tests.Accounting;

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
        var request = new CreateRevenueEntryRequest
        {
            TenantId = Guid.NewGuid(),
            Year = 2024,
            Month = 1,
            Amount = 1000m,
            Currency = "VND",
            Description = "Test revenue"
        };
        
        var expectedEntry = AccountingEntryFactory.CreateRevenueEntry(
            new TenantId(request.TenantId),
            AccountingPeriod.Create(request.Year, request.Month),
            new Money(request.Amount, request.Currency),
            request.Description);
        
        _mockAccountingService.Setup(s => s.CreateRevenueEntryAsync(
            It.IsAny<TenantId>(), It.IsAny<AccountingPeriod>(), It.IsAny<decimal>(), It.IsAny<string>()))
            .ReturnsAsync(expectedEntry);
        
        // Set up tenant header
        _controller.ControllerContext.HttpContext = new DefaultHttpContext();
        _controller.ControllerContext.HttpContext.Request.Headers["X-Tenant-Id"] = request.TenantId.ToString();
        
        // Act
        var result = await _controller.CreateRevenueEntry(request);
        
        // Assert
        var createdResult = Assert.IsType<CreatedAtActionResult>(result.Result);
        Assert.Equal(expectedEntry, createdResult.Value);
        Assert.Equal(nameof(_controller.GetEntryById), createdResult.ActionName);
        Assert.Equal(expectedEntry.Id, createdResult.RouteValues?["id"]);
        
        _mockAccountingService.Verify(s => s.CreateRevenueEntryAsync(
            It.IsAny<TenantId>(), It.IsAny<AccountingPeriod>(), It.IsAny<decimal>(), It.IsAny<string>()), Times.Once);
    }
    
    [Fact]
    public async Task CreateRevenueEntry_ShouldReturnBadRequest_WhenInvalidModel()
    {
        // Arrange
        var request = new CreateRevenueEntryRequest
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
        var result = await _controller.CreateRevenueEntry(request);
        
        // Assert
        Assert.IsType<BadRequestObjectResult>(result.Result);
        
        _mockAccountingService.Verify(s => s.CreateRevenueEntryAsync(
            It.IsAny<TenantId>(), It.IsAny<AccountingPeriod>(), It.IsAny<decimal>(), It.IsAny<string>()), Times.Never);
    }
    
    [Fact]
    public async Task CreateExpenseEntry_ShouldReturnCreated_WhenValidRequest()
    {
        // Arrange
        var request = new CreateExpenseEntryRequest
        {
            TenantId = Guid.NewGuid(),
            Year = 2024,
            Month = 1,
            Amount = 500m,
            Currency = "VND",
            Description = "Test expense"
        };
        
        var expectedEntry = AccountingEntryFactory.CreateExpenseEntry(
            new TenantId(request.TenantId),
            AccountingPeriod.Create(request.Year, request.Month),
            new Money(request.Amount, request.Currency),
            request.Description);
        
        _mockAccountingService.Setup(s => s.CreateExpenseEntryAsync(
            It.IsAny<TenantId>(), It.IsAny<AccountingPeriod>(), It.IsAny<decimal>(), It.IsAny<string>()))
            .ReturnsAsync(expectedEntry);
        
        // Set up tenant header
        _controller.ControllerContext.HttpContext = new DefaultHttpContext();
        _controller.ControllerContext.HttpContext.Request.Headers["X-Tenant-Id"] = request.TenantId.ToString();
        
        // Act
        var result = await _controller.CreateExpenseEntry(request);
        
        // Assert
        var createdResult = Assert.IsType<CreatedAtActionResult>(result.Result);
        Assert.Equal(expectedEntry, createdResult.Value);
        
        _mockAccountingService.Verify(s => s.CreateExpenseEntryAsync(
            It.IsAny<TenantId>(), It.IsAny<AccountingPeriod>(), It.IsAny<decimal>(), It.IsAny<string>()), Times.Once);
    }
    
    [Fact]
    public async Task GetEntryById_ShouldReturnOk_WhenEntryExists()
    {
        // Arrange
        var entryId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var expectedEntry = AccountingEntryFactory.CreateRevenueEntry(
            new TenantId(tenantId),
            AccountingPeriod.Create(2024, 1),
            new Money(1000m, "VND"),
            "Test entry");
        
        _mockAccountingService.Setup(s => s.GetEntryByIdAsync(
            It.IsAny<Guid>()))
            .ReturnsAsync(expectedEntry);
        
        // Set up tenant header
        _controller.ControllerContext.HttpContext = new DefaultHttpContext();
        _controller.ControllerContext.HttpContext.Request.Headers["X-Tenant-Id"] = tenantId.ToString();
        
        // Act
        var result = await _controller.GetEntryById(entryId);
        
        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        Assert.Equal(expectedEntry, okResult.Value);
        
        _mockAccountingService.Verify(s => s.GetEntryByIdAsync(
            It.IsAny<Guid>()), Times.Once);
    }
    
    [Fact]
    public async Task GetEntryById_ShouldReturnUnauthorized_WhenTenantIdMissing()
    {
        // Arrange
        var entryId = Guid.NewGuid();
        
        // No tenant header set
        _controller.ControllerContext.HttpContext = new DefaultHttpContext();
        
        // Act
        var result = await _controller.GetEntryById(entryId);
        
        // Assert
        var unauthorizedResult = Assert.IsType<UnauthorizedObjectResult>(result.Result);
        Assert.Equal("Tenant ID required", unauthorizedResult.Value);
        
        _mockAccountingService.Verify(s => s.GetEntryByIdAsync(
            It.IsAny<Guid>()), Times.Never);
    }
    
    [Fact]
    public async Task GetEntryById_ShouldReturnNotFound_WhenEntryDoesNotExist()
    {
        // Arrange
        var entryId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        
        _mockAccountingService.Setup(s => s.GetEntryByIdAsync(
            It.IsAny<Guid>()))
            .ReturnsAsync((CoreAccountingEntry?)null);
        
        // Set up tenant header
        _controller.ControllerContext.HttpContext = new DefaultHttpContext();
        _controller.ControllerContext.HttpContext.Request.Headers["X-Tenant-Id"] = tenantId.ToString();
        
        // Act
        var result = await _controller.GetEntryById(entryId);
        
        // Assert
        Assert.IsType<NotFoundResult>(result.Result);
        
        _mockAccountingService.Verify(s => s.GetEntryByIdAsync(
            It.IsAny<Guid>()), Times.Once);
    }
    
    [Fact]
    public async Task CreateReversalEntry_ShouldReturnCreated_WhenValidRequest()
    {
        // Arrange
        var originalEntryId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var request = new CreateReversalEntryRequest
        {
            Reason = "Test reversal"
        };
        
        var originalEntry = AccountingEntryFactory.CreateRevenueEntry(
            new TenantId(tenantId),
            AccountingPeriod.Create(2024, 1),
            new Money(1000m, "VND"),
            "Original entry");
        
        var reversalEntry = VanAn.Shared.Domain.AccountingEntryFactory.CreateReversal(originalEntry, request.Reason);
        
        _mockReversalService.Setup(s => s.CanReverseEntryAsync(
            It.IsAny<AccountingEntryId>(), It.IsAny<TenantId>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        
        _mockReversalService.Setup(s => s.CreateReversalEntryAsync(
            It.IsAny<AccountingEntryId>(), It.IsAny<TenantId>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(reversalEntry);
        
        // Set up tenant header
        _controller.ControllerContext.HttpContext = new DefaultHttpContext();
        _controller.ControllerContext.HttpContext.Request.Headers["X-Tenant-Id"] = tenantId.ToString();
        
        // Act
        var result = await _controller.CreateReversalEntry(originalEntryId, request);
        
        // Assert
        var createdResult = Assert.IsType<CreatedAtActionResult>(result.Result);
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
        var originalEntryId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var request = new CreateReversalEntryRequest
        {
            Reason = "Test reversal"
        };
        
        _mockReversalService.Setup(s => s.CanReverseEntryAsync(
            It.IsAny<AccountingEntryId>(), It.IsAny<TenantId>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        
        // Set up tenant header
        _controller.ControllerContext.HttpContext = new DefaultHttpContext();
        _controller.ControllerContext.HttpContext.Request.Headers["X-Tenant-Id"] = tenantId.ToString();
        
        // Act
        var result = await _controller.CreateReversalEntry(originalEntryId, request);
        
        // Assert
        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result.Result);
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
        var tenantId = Guid.NewGuid();
        var year = 2024;
        var month = 1;
        var period = AccountingPeriod.Create(year, month);
        var totalRevenue = 5000m;
        var entries = new List<CoreAccountingEntry>
        {
            AccountingEntryFactory.CreateRevenueEntry(new TenantId(tenantId), period, new Money(3000m, "VND"), "Revenue 1"),
            AccountingEntryFactory.CreateRevenueEntry(new TenantId(tenantId), period, new Money(2000m, "VND"), "Revenue 2")
        };
        
        _mockHKDBookService.Setup(s => s.GetRevenueTotalAsync(It.IsAny<TenantId>(), It.IsAny<AccountingPeriod>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(totalRevenue);
        _mockHKDBookService.Setup(s => s.GetRevenueEntriesAsync(It.IsAny<TenantId>(), It.IsAny<AccountingPeriod>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(entries);
        
        // Set up tenant header
        _controller.ControllerContext.HttpContext = new DefaultHttpContext();
        _controller.ControllerContext.HttpContext.Request.Headers["X-Tenant-Id"] = tenantId.ToString();
        
        // Act
        var result = await _controller.GetRevenueSummary(year, month);
        
        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var summary = Assert.IsType<RevenueSummaryResponse>(okResult.Value);
        
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
        var tenantId = Guid.NewGuid();
        var year = 2024;
        var month = 1;
        var profit = 2500m;
        var revenue = 5000m;
        var expense = 2500m;
        
        _mockHKDBookService.Setup(s => s.GetProfitAsync(It.IsAny<TenantId>(), It.IsAny<AccountingPeriod>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(profit);
        _mockHKDBookService.Setup(s => s.GetRevenueTotalAsync(It.IsAny<TenantId>(), It.IsAny<AccountingPeriod>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(revenue);
        _mockHKDBookService.Setup(s => s.GetExpenseTotalAsync(It.IsAny<TenantId>(), It.IsAny<AccountingPeriod>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expense);
        
        // Set up tenant header
        _controller.ControllerContext.HttpContext = new DefaultHttpContext();
        _controller.ControllerContext.HttpContext.Request.Headers["X-Tenant-Id"] = tenantId.ToString();
        
        // Act
        var result = await _controller.GetProfitSummary(year, month);
        
        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var summary = Assert.IsType<ProfitSummaryResponse>(okResult.Value);
        
        Assert.Equal($"{year}-{month:D2}", summary.Period);
        Assert.Equal(profit, summary.Profit);
        Assert.Equal(revenue, summary.Revenue);
        Assert.Equal(expense, summary.Expense);
        
        _mockHKDBookService.Verify(s => s.GetProfitAsync(It.IsAny<TenantId>(), It.IsAny<AccountingPeriod>(), It.IsAny<CancellationToken>()), Times.Once);
        _mockHKDBookService.Verify(s => s.GetRevenueTotalAsync(It.IsAny<TenantId>(), It.IsAny<AccountingPeriod>(), It.IsAny<CancellationToken>()), Times.Once);
        _mockHKDBookService.Verify(s => s.GetExpenseTotalAsync(It.IsAny<TenantId>(), It.IsAny<AccountingPeriod>(), It.IsAny<CancellationToken>()), Times.Once);
    }
}
