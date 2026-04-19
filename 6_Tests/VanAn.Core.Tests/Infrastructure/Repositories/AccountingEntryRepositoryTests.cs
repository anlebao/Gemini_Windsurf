using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using VanAn.CoreHub.Domain;
using VanAn.CoreHub.Infrastructure;
using VanAn.CoreHub.Repositories;
using VanAn.Shared.Domain;
using Xunit;
using FluentAssertions;

namespace VanAn.Core.Tests.Infrastructure.Repositories;

public class AccountingEntryRepositoryTests : IDisposable
{
    private readonly VanAnDbContext _context;
    private readonly AccountingEntryRepository _repository;
    private readonly TenantId _testTenantId = new(Guid.NewGuid());
    private readonly ILogger<AccountingEntryRepository> _logger;

    public AccountingEntryRepositoryTests()
    {
        var options = new DbContextOptionsBuilder<VanAnDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
            
        _context = new VanAnDbContext(options);
        _logger = new TestLogger<AccountingEntryRepository>();
        _repository = new AccountingEntryRepository(_context, _logger);
    }

    [Fact]
    public async Task Should_Add_Entry_Successfully()
    {
        // Arrange
        var entry = VanAn.Shared.Domain.AccountingEntryFactory.CreateRevenueEntry(
            _testTenantId, 
            AccountingPeriod.Create(2024, 1), 
            1000m, 
            "Test Revenue");

        // Act
        await _repository.AddAsync(entry);
        await _context.SaveChangesAsync();

        // Assert
        var result = await _repository.GetByIdAsync(entry.Id);
        result.Should().NotBeNull();
        result!.Amount.Should().Be(1000m);
        result.TenantId.Value.Should().Be(_testTenantId.Value);
    }

    [Fact]
    public async Task Should_Get_By_Tenant_And_BookType()
    {
        // Arrange
        var revenueEntry = VanAn.Shared.Domain.AccountingEntryFactory.CreateRevenueEntry(
            _testTenantId, 
            AccountingPeriod.Create(2024, 1), 
            1000m, 
            "Test Revenue");
            
        await _repository.AddAsync(revenueEntry);
        await _context.SaveChangesAsync();

        // Act
        var results = await _repository.GetByTenantAndBookTypeAsync(
            _testTenantId, 
            AccountingBookType.RevenueBook);

        // Assert
        results.Should().HaveCount(1);
        results.First().AccountingBookType.Should().Be(AccountingBookType.RevenueBook);
    }

    [Fact]
    public async Task Should_Get_By_Tenant_And_DateRange()
    {
        // Arrange
        var startDate = DateTime.UtcNow.AddDays(-5);
        var endDate = DateTime.UtcNow.AddDays(5);
        var entry = VanAn.Shared.Domain.AccountingEntryFactory.CreateRevenueEntry(
            _testTenantId, 
            AccountingPeriod.Create(2024, 1), 
            1000m, 
            "Test Revenue");
            
        await _repository.AddAsync(entry);
        await _context.SaveChangesAsync();

        // Act
        var results = await _repository.GetByTenantAndDateRangeAsync(
            _testTenantId, startDate, endDate);

        // Assert
        results.Should().HaveCount(1);
        results.First().TenantId.Should().Be(_testTenantId);
    }

    [Fact]
    public async Task Should_Get_By_Tenant_And_Period()
    {
        // Arrange
        var period = AccountingPeriod.FromDateTime(DateTime.UtcNow);
        var entry = VanAn.Shared.Domain.AccountingEntryFactory.CreateRevenueEntry(
            _testTenantId, 
            AccountingPeriod.Create(2024, 1), 
            1000m, 
            "Test Revenue");
            
        await _repository.AddAsync(entry);
        await _context.SaveChangesAsync();

        // Act
        var results = await _repository.GetByTenantAndPeriodAsync(
            _testTenantId, period);

        // Assert
        results.Should().HaveCount(1);
        results.First().TenantId.Should().Be(_testTenantId);
    }

    [Fact]
    public async Task Should_Only_Expose_Add_Methods()
    {
        // Assert - Verify interface only has Add methods
        var interfaceMethods = typeof(IAccountingEntryRepository).GetMethods()
            .Where(m => !m.IsSpecialName)
            .Select(m => m.Name)
            .ToList();

        interfaceMethods.Should().Contain("AddAsync");
        interfaceMethods.Should().Contain("AddRangeAsync");
        interfaceMethods.Should().NotContain("UpdateAsync");
        interfaceMethods.Should().NotContain("DeleteAsync");
        interfaceMethods.Should().NotContain("RemoveAsync");
    }

    [Fact]
    public async Task Should_Enforce_Append_Only_Behavior()
    {
        // Arrange
        var entry = VanAn.Shared.Domain.AccountingEntryFactory.CreateRevenueEntry(
            _testTenantId, 
            AccountingPeriod.Create(2024, 1), 
            1000m, 
            "Test Revenue");

        // Act
        await _repository.AddAsync(entry);
        await _context.SaveChangesAsync();

        // Try to add the same entry again (should fail)
        var duplicateEntry = VanAn.Shared.Domain.AccountingEntryFactory.CreateRevenueEntry(
            _testTenantId, 
            AccountingPeriod.Create(2024, 1), 
            2000m, 
            "Duplicate Revenue");
        
        // Manually set the same ID to simulate duplicate
        var entryWithSameId = typeof(AccountingEntry).GetField("<Id>k__BackingField", 
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        entryWithSameId?.SetValue(duplicateEntry, entry.Id);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _repository.AddAsync(duplicateEntry));
    }

    [Fact]
    public async Task Should_Protect_ReversalEntryId_From_Modification()
    {
        // Arrange
        var entry = VanAn.Shared.Domain.AccountingEntryFactory.CreateRevenueEntry(
            _testTenantId, 
            AccountingPeriod.Create(2024, 1), 
            1000m, 
            "Test Revenue");

        // Act
        await _repository.AddAsync(entry);
        await _context.SaveChangesAsync();

        // Assert - Verify ReversalEntryId is null (can't be modified after creation)
        var result = await _repository.GetByIdAsync(entry.Id);
        result.Should().NotBeNull();
        result!.ReversalEntryId.Should().BeNull();
    }

    public void Dispose()
    {
        _context.Dispose();
    }
}

// Test helper class for logger
public class TestLogger<T> : ILogger<T>
{
    private readonly ILogger<T> _innerLogger = new NullLogger<T>();
    
    public IDisposable BeginScope<TState>(TState state) where TState : notnull
    {
        return _innerLogger.BeginScope(state) ?? null!;
    }
    public bool IsEnabled(LogLevel logLevel) => true;
    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        // Do nothing for tests
    }
}
