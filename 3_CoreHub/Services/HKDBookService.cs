using Microsoft.Extensions.Logging;
using VanAn.Shared.Domain;
using VanAn.CoreHub.Domain;
using VanAn.CoreHub.Repositories;
using CoreAccountingEntry = VanAn.Shared.Domain.AccountingEntry;

namespace VanAn.CoreHub.Services;

/// <summary>
/// Service implementation for Household Business Book operations - Week 1 implementation
/// Implements 5-layer protection: Domain, EF Core, Repository, Service, API
/// </summary>
public class HKDBookService : IHKDBookService
{
    private readonly IAccountingEntryRepository _repository;
    private readonly ILogger<HKDBookService> _logger;
    
    public HKDBookService(
        IAccountingEntryRepository repository,
        ILogger<HKDBookService> logger)
    {
        _repository = repository;
        _logger = logger;
    }
    
    public async Task<AccountingEntry> RecordRevenueAsync(
        TenantId tenantId,
        decimal amount,
        string description,
        DateTime? transactionDate = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var date = transactionDate ?? DateTime.UtcNow;
            
            // Create immutable revenue entry using Factory
            var period = new AccountingPeriod(date.Year, date.Month);
            var entry = VanAn.Shared.Domain.AccountingEntryFactory.CreateRevenue(
                tenantId,
                period,
                amount,
                description);
            
            await _repository.AddAsync(entry, cancellationToken);
            
            _logger.LogInformation("Recorded revenue entry {EntryId} for tenant {TenantId}, amount {Amount}", 
                entry.Id, tenantId, amount);
            
            return entry;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error recording revenue for tenant {TenantId}, amount {Amount}", 
                tenantId, amount);
            throw;
        }
    }
    
    public async Task<AccountingEntry> RecordExpenseAsync(
        TenantId tenantId,
        decimal amount,
        string description,
        DateTime? transactionDate = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var date = transactionDate ?? DateTime.UtcNow;
            
            // Create immutable expense entry using Factory
            var period = new AccountingPeriod(date.Year, date.Month);
            var entry = VanAn.Shared.Domain.AccountingEntryFactory.CreateExpense(
                tenantId,
                period,
                amount,
                description);
            
            await _repository.AddAsync(entry, cancellationToken);
            
            _logger.LogInformation("Recorded expense entry {EntryId} for tenant {TenantId}, amount {Amount}", 
                entry.Id, tenantId, amount);
            
            return entry;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error recording expense for tenant {TenantId}, amount {Amount}", 
                tenantId, amount);
            throw;
        }
    }
    
    public async Task<decimal> GetRevenueTotalAsync(
        TenantId tenantId,
        AccountingPeriod period,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var entries = await _repository.GetByTenantAndPeriodAsync(tenantId, period, cancellationToken);
            
            return entries.Sum(e => e.Amount);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting revenue total for tenant {TenantId}, period {Period}", 
                tenantId, period.ToString());
            throw;
        }
    }
    
    public async Task<decimal> GetExpenseTotalAsync(
        TenantId tenantId,
        AccountingPeriod period,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var entries = await _repository.GetByTenantAndPeriodAsync(tenantId, period, cancellationToken);
            
            return entries.Where(e => e.EntryType == AccountingEntryType.Expense).Sum(e => e.Amount);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting expense total for tenant {TenantId}, period {Period}", 
                tenantId, period.ToString());
            throw;
        }
    }
    
    public async Task<decimal> GetProfitAsync(
        TenantId tenantId,
        AccountingPeriod period,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var revenue = await GetRevenueTotalAsync(tenantId, period, cancellationToken);
            var expense = await GetExpenseTotalAsync(tenantId, period, cancellationToken);
            
            return revenue - expense;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calculating profit for tenant {TenantId}, period {Period}", 
                tenantId.Value, period.ToString());
            throw;
        }
    }
    
    public async Task<IEnumerable<CoreAccountingEntry>> GetRevenueEntriesAsync(
        TenantId tenantId,
        AccountingPeriod period,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var entries = await _repository.GetByTenantAndPeriodAsync(tenantId, period, cancellationToken);
            
            return entries.Where(e => e.EntryType == AccountingEntryType.Revenue);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting revenue entries for tenant {TenantId}, period {Period}", 
                tenantId.Value, period.ToString());
            throw;
        }
    }
    
    public async Task<IEnumerable<CoreAccountingEntry>> GetExpenseEntriesAsync(
        TenantId tenantId,
        AccountingPeriod period,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var entries = await _repository.GetByTenantAndPeriodAsync(tenantId, period, cancellationToken);
            
            return entries.Where(e => e.EntryType == AccountingEntryType.Expense);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting expense entries for tenant {TenantId}, period {Period}", 
                tenantId.Value, period.ToString());
            throw;
        }
    }
}
