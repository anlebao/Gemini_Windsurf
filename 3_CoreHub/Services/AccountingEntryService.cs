using Microsoft.Extensions.Logging;
using VanAn.Shared.Domain;
using VanAn.CoreHub.Domain;
using VanAn.CoreHub.Repositories;
using CoreAccountingEntry = VanAn.Shared.Domain.AccountingEntry;

namespace VanAn.CoreHub.Services;

/// <summary>
/// Service implementation for Accounting Entry operations - Week 1 implementation
/// Implements immutable AccountingEntry pattern with append-only operations
/// </summary>
public class AccountingEntryService : IAccountingService
{
    private readonly IAccountingEntryRepository _repository;
    private readonly ILogger<AccountingEntryService> _logger;
    
    public AccountingEntryService(
        IAccountingEntryRepository repository,
        ILogger<AccountingEntryService> logger)
    {
        _repository = repository;
        _logger = logger;
    }
    
    public async Task<decimal> GetTodayRevenueAsync(Guid tenantId)
    {
        try
        {
            var tenantIdObj = new TenantId(tenantId);
            var today = DateTime.UtcNow.Date;
            var entries = await _repository.GetByTenantAndDateRangeAsync(
                tenantIdObj, 
                today, 
                today.AddDays(1).AddTicks(-1), 
                CancellationToken.None);
            
            return entries
                .Where(e => e.EntryType == AccountingEntryType.Revenue)
                .Sum(e => e.Amount);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting today's revenue for tenant {TenantId}", tenantId);
            throw;
        }
    }
    
    public async Task<decimal> GetRevenueByDateRangeAsync(Guid tenantId, DateTime startDate, DateTime endDate)
    {
        try
        {
            var tenantIdObj = new TenantId(tenantId);
            var entries = await _repository.GetByTenantAndDateRangeAsync(
                tenantIdObj, 
                startDate, 
                endDate, 
                CancellationToken.None);
            
            return entries
                .Where(e => e.EntryType == AccountingEntryType.Revenue)
                .Sum(e => e.Amount);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting revenue for tenant {TenantId} from {StartDate} to {EndDate}", 
                tenantId, startDate, endDate);
            throw;
        }
    }
    
    public async Task<AccountingEntry> CreateEntryAsync(AccountingEntry entry)
    {
        try
        {
            // Create immutable AccountingEntry using Factory
            var period = new AccountingPeriod(entry.TransactionDate.Year, entry.TransactionDate.Month);
            
            var coreEntry = new CoreAccountingEntry
            {
                Id = Guid.NewGuid(),
                TenantId = new TenantId(entry.TenantId),
                Amount = entry.Amount,
                Description = entry.Description,
                EntryType = entry.EntryType,
                CreatedAt = DateTime.UtcNow,
                AccountingBookType = entry.AccountingBookType,
                PeriodYear = entry.PeriodYear,
                PeriodMonth = entry.PeriodMonth,
                ReversalEntryId = entry.ReversalEntryId
            };
            
            await _repository.AddAsync(coreEntry);
            
            return entry;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating accounting entry for tenant {TenantId}", entry.TenantId);
            throw;
        }
    }
    
    public async Task<AccountingEntry?> GetEntryByIdAsync(Guid entryId)
    {
        try
        {
            var entry = await _repository.GetByIdAsync(entryId);
            if (entry == null) return null;
            
            return new AccountingEntry
            {
                Id = entry.Id,
                TenantId = entry.TenantId.Value,
                Amount = entry.Amount,
                Description = entry.Description,
                EntryType = entry.EntryType,
                CreatedAt = entry.CreatedAt,
                AccountingBookType = entry.AccountingBookType,
                PeriodYear = entry.PeriodYear,
                PeriodMonth = entry.PeriodMonth,
                ReversalEntryId = entry.ReversalEntryId
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting entry by ID {EntryId}", entryId);
            throw;
        }
    }
    
    public async Task<AccountingEntry> CreateRevenueEntryAsync(TenantId tenantId, AccountingPeriod period, decimal amount, string description)
    {
        try
        {
            var entry = VanAn.Shared.Domain.AccountingEntryFactory.CreateRevenueEntry(tenantId, period, amount, description);
            await _repository.AddAsync(entry);
            
            return new AccountingEntry
            {
                Id = entry.Id,
                TenantId = entry.TenantId.Value,
                Amount = entry.Amount,
                Description = entry.Description,
                EntryType = entry.EntryType,
                CreatedAt = entry.CreatedAt,
                AccountingBookType = entry.AccountingBookType,
                PeriodYear = entry.PeriodYear,
                PeriodMonth = entry.PeriodMonth,
                ReversalEntryId = entry.ReversalEntryId
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating revenue entry for tenant {TenantId}", tenantId.Value);
            throw;
        }
    }
    
    public async Task<AccountingEntry> CreateExpenseEntryAsync(TenantId tenantId, AccountingPeriod period, decimal amount, string description)
    {
        try
        {
            var entry = VanAn.Shared.Domain.AccountingEntryFactory.CreateExpense(tenantId, period, amount, description);
            await _repository.AddAsync(entry);
            
            return new AccountingEntry
            {
                Id = entry.Id,
                TenantId = entry.TenantId.Value,
                Amount = entry.Amount,
                Description = entry.Description,
                EntryType = entry.EntryType,
                CreatedAt = entry.CreatedAt,
                AccountingBookType = entry.AccountingBookType,
                PeriodYear = entry.PeriodYear,
                PeriodMonth = entry.PeriodMonth,
                ReversalEntryId = entry.ReversalEntryId
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating expense entry for tenant {TenantId}", tenantId.Value);
            throw;
        }
    }
    
    public async Task<IEnumerable<AccountingEntry>> GetEntriesByTenantAsync(TenantId tenantId)
    {
        try
        {
            var entries = await _repository.GetByTenantAsync(tenantId, CancellationToken.None);
            return entries.Select(e => new AccountingEntry
            {
                Id = e.Id,
                TenantId = e.TenantId.Value,
                Amount = e.Amount,
                Description = e.Description,
                EntryType = e.EntryType,
                CreatedAt = e.CreatedAt,
                AccountingBookType = e.AccountingBookType,
                PeriodYear = e.PeriodYear,
                PeriodMonth = e.PeriodMonth,
                ReversalEntryId = e.ReversalEntryId
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting entries for tenant {TenantId}", tenantId.Value);
            throw;
        }
    }
    
    public async Task<IEnumerable<AccountingEntry>> GetEntriesByTenantAndBookTypeAsync(TenantId tenantId, AccountingBookType bookType)
    {
        try
        {
            var entries = await _repository.GetByTenantAndBookTypeAsync(tenantId, bookType, CancellationToken.None);
            return entries.Select(e => new AccountingEntry
            {
                Id = e.Id,
                TenantId = e.TenantId.Value,
                Amount = e.Amount,
                Description = e.Description,
                EntryType = e.EntryType,
                CreatedAt = e.CreatedAt,
                AccountingBookType = e.AccountingBookType,
                PeriodYear = e.PeriodYear,
                PeriodMonth = e.PeriodMonth,
                ReversalEntryId = e.ReversalEntryId
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting entries for tenant {TenantId} and book type {BookType}", tenantId.Value, bookType);
            throw;
        }
    }
    
    public async Task<IEnumerable<AccountingEntry>> GetEntriesByTenantAndPeriodAsync(TenantId tenantId, AccountingPeriod period)
    {
        try
        {
            var entries = await _repository.GetByTenantAndPeriodAsync(tenantId, period, CancellationToken.None);
            return entries.Select(e => new AccountingEntry
            {
                Id = e.Id,
                TenantId = e.TenantId.Value,
                Amount = e.Amount,
                Description = e.Description,
                EntryType = e.EntryType,
                CreatedAt = e.CreatedAt,
                AccountingBookType = e.AccountingBookType,
                PeriodYear = e.PeriodYear,
                PeriodMonth = e.PeriodMonth,
                ReversalEntryId = e.ReversalEntryId
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting entries for tenant {TenantId} and period {Period}", tenantId.Value, period);
            throw;
        }
    }
    
    public async Task<AccountingEntry> CreateReversalEntryAsync(Guid originalEntryId, string reason, Guid tenantId)
    {
        try
        {
            var originalId = new AccountingEntryId(originalEntryId);
            var tenantIdObj = new TenantId(tenantId);
            
            // Get original entry
            var originalEntry = await _repository.GetByIdAsync(originalId, CancellationToken.None);
            if (originalEntry == null)
            {
                throw new InvalidOperationException($"Original entry {originalEntryId} not found");
            }
            
            // Multi-tenancy check
            if (originalEntry.TenantId.Value != tenantId)
            {
                throw new UnauthorizedAccessException($"Entry {originalEntryId} does not belong to tenant {tenantId}");
            }
            
            // Create reversal entry using Factory
            var reversalEntry = VanAn.Shared.Domain.AccountingEntryFactory.CreateReversal(originalEntry, reason);
            await _repository.AddAsync(reversalEntry, CancellationToken.None);
            
            _logger.LogInformation("Created reversal entry {ReversalId} for original entry {OriginalId}", 
                reversalEntry.Id, originalEntryId);
            
            // Convert back to shared domain for return
            return new AccountingEntry
            {
                Id = reversalEntry.Id,
                TenantId = reversalEntry.TenantId.Value,
                Amount = reversalEntry.Amount,
                Description = reversalEntry.Description,
                EntryType = reversalEntry.EntryType,
                CreatedAt = reversalEntry.CreatedAt,
                AccountingBookType = reversalEntry.AccountingBookType,
                PeriodYear = reversalEntry.PeriodYear,
                PeriodMonth = reversalEntry.PeriodMonth,
                ReversalEntryId = reversalEntry.ReversalEntryId
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating reversal entry for original entry {OriginalEntryId}", originalEntryId);
            throw;
        }
    }
    
    public async Task<IEnumerable<AccountingEntry>> GetEntriesByDateRangeAsync(Guid tenantId, DateTime startDate, DateTime endDate)
    {
        try
        {
            var tenantIdObj = new TenantId(tenantId);
            var entries = await _repository.GetByTenantAndDateRangeAsync(
                tenantIdObj, 
                startDate, 
                endDate, 
                CancellationToken.None);
            
            return entries.Select(e => new AccountingEntry
            {
                Id = e.Id,
                TenantId = e.TenantId.Value,
                Amount = e.Amount,
                Description = e.Description,
                EntryType = e.EntryType,
                CreatedAt = e.CreatedAt,
                AccountingBookType = e.AccountingBookType,
                PeriodYear = e.PeriodYear,
                PeriodMonth = e.PeriodMonth,
                ReversalEntryId = e.ReversalEntryId
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting entries for tenant {TenantId} from {StartDate} to {EndDate}", 
                tenantId, startDate, endDate);
            throw;
        }
    }
    
    public async Task<decimal> CalculateVatAsync(decimal revenue, VatRate vatRate)
    {
        try
        {
            // VAT calculation for Vietnamese Household Businesses (Direct Method)
            var vatAmount = revenue * ((decimal)vatRate / 100m);
            
            _logger.LogDebug("Calculated VAT amount {VatAmount} for revenue {Revenue} with rate {VatRate}%", 
                vatAmount, revenue, (decimal)vatRate);
            
            return vatAmount;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calculating VAT for revenue {Revenue} with rate {VatRate}", 
                revenue, (decimal)vatRate);
            throw;
        }
    }
}
