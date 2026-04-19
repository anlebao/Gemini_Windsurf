using Microsoft.Extensions.Logging;
using Moq;
using VanAn.Shared.Domain;
using VanAn.CoreHub.Repositories;
using VanAn.CoreHub.Services;
using CoreAccountingEntry = VanAn.Shared.Domain.AccountingEntry;
using Xunit;

namespace VanAn.Core.Tests.Accounting;

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
        var tenantId = new TenantId(Guid.NewGuid());
        var period = new AccountingPeriod(2024, 1);
        var amount = new Money(1000m);
        var description = "Test revenue";
        
        // Act
        var result = await _service.CreateRevenueEntryAsync(tenantId, period, amount, description);
        
        // Assert
        Assert.NotNull(result);
        Assert.Equal(tenantId.Value, result.TenantId.Value);
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
        var tenantId = new TenantId(Guid.NewGuid());
        var period = new AccountingPeriod(2024, 1);
        var amount = new Money(500m);
        var description = "Test expense";
        
        // Act
        var result = await _service.CreateExpenseEntryAsync(tenantId, period, amount, description);
        
        // Assert
        Assert.NotNull(result);
        Assert.Equal(tenantId.Value, result.TenantId.Value);
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
        var tenantId = new TenantId(Guid.NewGuid());
        var entry = new CoreAccountingEntry
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Amount = 1000m,
            EntryType = AccountingEntryType.Revenue,
            Description = "Test",
            CreatedAt = DateTime.UtcNow,
            AccountingBookType = AccountingBookType.RevenueBook,
            PeriodYear = 2024,
            PeriodMonth = 1
        };
        
        _mockRepository.Setup(r => r.GetByIdAsync(entry.Id, It.IsAny<CancellationToken>())).ReturnsAsync(entry);
        
        // Act
        var result = await _service.GetEntryByIdAsync(entry.Id);
        
        // Assert
        Assert.NotNull(result);
        Assert.Equal(entry.Id, result.Id);
        
        _mockRepository.Verify(r => r.GetByIdAsync(entry.Id, It.IsAny<CancellationToken>()), Times.Once);
    }
    
    [Fact]
    public async Task GetEntryByIdAsync_ShouldReturnNull_WhenEntryDoesNotExist()
    {
        // Arrange
        var tenantId = new TenantId(Guid.NewGuid());
        var entryId = new AccountingEntryId(Guid.NewGuid());
        
        _mockRepository.Setup(r => r.GetByIdAsync(entryId, It.IsAny<CancellationToken>())).ReturnsAsync((CoreAccountingEntry?)null);
        
        // Act
        var result = await _service.GetEntryByIdAsync(entryId);
        
        // Assert
        Assert.Null(result);
        
        _mockRepository.Verify(r => r.GetByIdAsync(entryId, It.IsAny<CancellationToken>()), Times.Once);
    }
    
    [Fact]
    public async Task GetEntryByIdAsync_ShouldReturnNull_WhenEntryBelongsToDifferentTenant()
    {
        // Arrange
        var tenantId = new TenantId(Guid.NewGuid());
        var differentTenantId = new TenantId(Guid.NewGuid());
        var entryId = new AccountingEntryId(Guid.NewGuid());
        var entry = AccountingEntryFactory.CreateRevenueEntry(differentTenantId, AccountingPeriod.Create(2024, 1), new Money(1000m, "VND"), "Test");
        
        _mockRepository.Setup(r => r.GetByIdAsync(entryId, It.IsAny<CancellationToken>())).ReturnsAsync(entry);
        
        // Act
        var result = await _service.GetEntryByIdAsync(entryId);
        
        // Assert
        Assert.Null(result);
        
        _mockRepository.Verify(r => r.GetByIdAsync(entryId, It.IsAny<CancellationToken>()), Times.Once);
    }
    
    [Fact]
    public async Task GetEntriesByTenantAsync_ShouldReturnEntries_ForValidTenant()
    {
        // Arrange
        var tenantId = new TenantId(Guid.NewGuid());
        var entries = new List<CoreAccountingEntry>
        {
            AccountingEntryFactory.CreateRevenueEntry(tenantId, AccountingPeriod.Create(2024, 1), new Money(1000m, "VND"), "Test 1"),
            AccountingEntryFactory.CreateExpenseEntry(tenantId, AccountingPeriod.Create(2024, 1), new Money(500m, "VND"), "Test 2")
        };
        
        _mockRepository.Setup(r => r.GetByTenantAsync(tenantId, It.IsAny<CancellationToken>())).ReturnsAsync(entries);
        
        // Act
        var result = await _service.GetEntriesByTenantAsync(tenantId);
        
        // Assert
        Assert.NotNull(result);
        Assert.Equal(2, result.Count());
        
        _mockRepository.Verify(r => r.GetByTenantAsync(tenantId, It.IsAny<CancellationToken>()), Times.Once);
    }
    
    [Fact]
    public async Task GetEntriesByTenantAndBookTypeAsync_ShouldReturnFilteredEntries()
    {
        // Arrange
        var tenantId = new TenantId(Guid.NewGuid());
        var revenueEntries = new List<CoreAccountingEntry>
        {
            AccountingEntryFactory.CreateRevenueEntry(tenantId, AccountingPeriod.Create(2024, 1), new Money(1000m, "VND"), "Test 1"),
            AccountingEntryFactory.CreateRevenueEntry(tenantId, AccountingPeriod.Create(2024, 2), new Money(1500m, "VND"), "Test 2")
        };
        
        _mockRepository.Setup(r => r.GetByTenantAndBookTypeAsync(tenantId, AccountingBookType.RevenueBook, It.IsAny<CancellationToken>()))
            .ReturnsAsync(revenueEntries);
        
        // Act
        var result = await _service.GetEntriesByTenantAndBookTypeAsync(tenantId, AccountingBookType.RevenueBook);
        
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
        var tenantId = new TenantId(Guid.NewGuid());
        var period = AccountingPeriod.Create(2024, 1);
        var periodEntries = new List<CoreAccountingEntry>
        {
            AccountingEntryFactory.CreateRevenueEntry(tenantId, period, new Money(1000m, "VND"), "Test 1"),
            AccountingEntryFactory.CreateExpenseEntry(tenantId, period, new Money(500m, "VND"), "Test 2")
        };
        
        _mockRepository.Setup(r => r.GetByTenantAndPeriodAsync(tenantId, period, It.IsAny<CancellationToken>()))
            .ReturnsAsync(periodEntries);
        
        // Act
        var result = await _service.GetEntriesByTenantAndPeriodAsync(tenantId, period);
        
        // Assert
        Assert.NotNull(result);
        Assert.Equal(2, result.Count());
        Assert.All(result, e => 
        {
            Assert.Equal(period.Year, e.Period.Year);
            Assert.Equal(period.Month, e.Period.Month);
        });
        
        _mockRepository.Verify(r => r.GetByTenantAndPeriodAsync(tenantId, period, It.IsAny<CancellationToken>()), Times.Once);
    }
    
    [Fact]
    public async Task CreateRevenueEntryAsync_ShouldLogError_WhenRepositoryThrowsException()
    {
        // Arrange
        var tenantId = new TenantId(Guid.NewGuid());
        var period = AccountingPeriod.Create(2024, 1);
        var amount = new Money(1000m, "VND");
        var description = "Test revenue";
        
        _mockRepository.Setup(r => r.AddAsync(It.IsAny<CoreAccountingEntry>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Database error"));
        
        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _service.CreateRevenueEntryAsync(tenantId, period, amount, description));
        
        Assert.Equal("Database error", exception.Message);
    }
}
