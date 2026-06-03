using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using VanAn.Shared.Domain;
using VanAn.Shared.Domain.Common;
using CoreAccountingEntry = VanAn.Shared.Domain.AccountingEntry;
using VanAn.CoreHub.Infrastructure;

namespace VanAn.CoreHub.Repositories;

/// <summary>
/// Repository implementation for AccountingEntry - No Update/Delete methods (immutable design)
/// Implements 5-layer protection: Domain, EF Core, Repository, Service, API
/// </summary>
public class AccountingEntryRepository : IAccountingEntryRepository
{
    private readonly IVanAnDbContext _context;
    private readonly ILogger<AccountingEntryRepository> _logger;
    
    public AccountingEntryRepository(IVanAnDbContext context, ILogger<AccountingEntryRepository> logger)
    {
        _context = context;
        _logger = logger;
    }
    
    public async Task<CoreAccountingEntry?> GetByIdAsync(AccountingEntryId id, CancellationToken cancellationToken = default)
    {
        return await _context.AccountingEntries
            .FirstOrDefaultAsync(e => e.Id == id.Value, cancellationToken);
    }
    
    public async Task<IEnumerable<CoreAccountingEntry>> GetByTenantAsync(TenantId tenantId, CancellationToken cancellationToken = default)
    {
        return await _context.AccountingEntries
            .Where(e => e.TenantId.Value == tenantId.Value)
            .OrderByDescending(e => e.CreatedAt)
            .ToListAsync(cancellationToken);
    }
    
    public async Task<IEnumerable<CoreAccountingEntry>> GetByTenantAndBookTypeAsync(
        TenantId tenantId, 
        AccountingBookType bookType, 
        CancellationToken cancellationToken = default)
    {
        return await _context.AccountingEntries
            .Where(e => e.TenantId == tenantId && e.AccountingBookType == bookType)
            .OrderByDescending(e => e.CreatedAt)
            .ToListAsync(cancellationToken);
    }
    
    public async Task<IEnumerable<CoreAccountingEntry>> GetByTenantAndDateRangeAsync(
    TenantId tenantId, 
    DateTime startDate, 
    DateTime endDate, 
    CancellationToken cancellationToken = default)
{
    try
    {
        // Input validation with specific exceptions
        if (tenantId == null) 
            throw new ArgumentNullException(nameof(tenantId), "TenantId cannot be null");
        if (startDate > endDate) 
            throw new ArgumentException("StartDate cannot be greater than EndDate", nameof(startDate));
        if (startDate == default(DateTime) || endDate == default(DateTime))
            throw new ArgumentException("Date parameters cannot be default values");
        
        // Build query with tenant filtering and date range
        var query = _context.AccountingEntries
            .Where(e => e.TenantId == tenantId && 
                       e.CreatedAt >= startDate && 
                       e.CreatedAt <= endDate);
            
        // Execute query with ordering
        var result = await query
            .OrderByDescending(e => e.CreatedAt)
            .ToListAsync(cancellationToken);
            
        _logger.LogDebug("Retrieved {Count} accounting entries for tenant {TenantId} from {StartDate} to {EndDate}", 
            result.Count, tenantId.Value, startDate, endDate);
            
        return result;
    }
    catch (OperationCanceledException ex)
    {
        _logger.LogWarning(ex, "GetByTenantAndDateRangeAsync was cancelled for tenant {TenantId}", tenantId.Value);
        throw;
    }
    catch (Exception ex) when (!(ex is ArgumentNullException || ex is ArgumentException || ex is OperationCanceledException))
    {
        _logger.LogError(ex, "Unexpected error in GetByTenantAndDateRangeAsync for tenant {TenantId} from {StartDate} to {EndDate}", 
            tenantId.Value, startDate, endDate);
        throw new InvalidOperationException($"Failed to retrieve accounting entries for tenant {tenantId.Value}", ex);
    }
}

public async Task<IEnumerable<CoreAccountingEntry>> GetByTenantAndPeriodAsync(
    TenantId tenantId, 
    AccountingPeriod period, 
    CancellationToken cancellationToken = default)
{
    try
    {
        // Input validation with specific exceptions
        if (tenantId == null) 
            throw new ArgumentNullException(nameof(tenantId), "TenantId cannot be null");
        if (period == null) 
            throw new ArgumentNullException(nameof(period), "AccountingPeriod cannot be null");
        
        // Convert AccountingPeriod to date range; normalize to UTC to match CreatedAt (DateTime.UtcNow)
        var startDate = DateTime.SpecifyKind(period.StartDate, DateTimeKind.Utc);
        var endDate = DateTime.SpecifyKind(period.EndDate, DateTimeKind.Utc);
        
        // Validate period date range
        if (startDate > endDate)
            throw new ArgumentException($"Invalid AccountingPeriod: StartDate {startDate} is greater than EndDate {endDate}", nameof(period));
        
        // Build query with tenant filtering and period range
        var query = _context.AccountingEntries
            .Where(e => e.TenantId == tenantId && 
                       e.CreatedAt >= startDate && 
                       e.CreatedAt <= endDate);
            
        // Execute query with ordering
        var result = await query
            .OrderByDescending(e => e.CreatedAt)
            .ToListAsync(cancellationToken);
            
        _logger.LogDebug("Retrieved {Count} accounting entries for tenant {TenantId} in period {Period}", 
            result.Count, tenantId, period.ToString());
            
        return result;
    }
    catch (OperationCanceledException ex)
    {
        _logger.LogWarning(ex, "GetByTenantAndPeriodAsync was cancelled for tenant {TenantId}, period {Period}", 
            tenantId, period?.ToString());
        throw;
    }
    catch (Exception ex) when (!(ex is ArgumentNullException || ex is ArgumentException || ex is OperationCanceledException))
    {
        _logger.LogError(ex, "Unexpected error in GetByTenantAndPeriodAsync for tenant {TenantId}, period {Period}", 
            tenantId, period?.ToString());
        throw new InvalidOperationException($"Failed to retrieve accounting entries for tenant {tenantId} in period {period?.ToString()}", ex);
    }
}
    
    public async Task AddAsync(CoreAccountingEntry entry, CancellationToken cancellationToken = default)
{
    try
    {
        // Input validation with specific exceptions
        if (entry == null) 
            throw new ArgumentNullException(nameof(entry), "AccountingEntry cannot be null");
        
        if (entry.Id == null || entry.Id == Guid.Empty)
            throw new ArgumentException("AccountingEntry must have valid ID", nameof(entry));
            
        if (entry.TenantId == null || entry.TenantId.Value == Guid.Empty)
            throw new ArgumentException("AccountingEntry must have valid TenantId", nameof(entry));
            
        if (entry.Amount == null)
            throw new ArgumentException("AccountingEntry must have valid Amount", nameof(entry));
            
        if (string.IsNullOrWhiteSpace(entry.Description))
            throw new ArgumentException("AccountingEntry must have valid Description", nameof(entry));
        
        // Check if entry with same ID already exists (append-only enforcement)
        var existingEntry = await _context.AccountingEntries
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.Id == entry.Id, cancellationToken);
            
        if (existingEntry != null)
        {
            throw new InvalidOperationException(
                $"AccountingEntry with ID {entry.Id} already exists. Cannot add duplicate entry (append-only enforcement).");
        }
        
        // Add entry to context (no tracking for immutability)
        await _context.AccountingEntries.AddAsync(entry, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
        
        _logger.LogInformation("Added accounting entry {EntryId} for tenant {TenantId} with amount {Amount}", 
            entry.Id, entry.TenantId.Value, entry.Amount);
    }
    catch (OperationCanceledException ex)
    {
        _logger.LogWarning(ex, "AddAsync was cancelled for entry {EntryId}", entry?.Id);
        throw;
    }
    catch (Exception ex) when (!(ex is ArgumentNullException || ex is ArgumentException || ex is InvalidOperationException || ex is OperationCanceledException))
    {
        _logger.LogError(ex, "Unexpected error in AddAsync for entry {EntryId}", entry?.Id);
        throw new InvalidOperationException($"Failed to add accounting entry {entry?.Id} to database", ex);
    }
}
    
    public async Task AddRangeAsync(IEnumerable<CoreAccountingEntry> entries, CancellationToken cancellationToken = default)
{
    try
    {
        // Input validation with specific exceptions
        if (entries == null) 
            throw new ArgumentNullException(nameof(entries), "Entries collection cannot be null");
        
        var entriesList = entries.ToList();
        if (!entriesList.Any()) 
        {
            _logger.LogDebug("AddRangeAsync called with empty collection - nothing to add");
            return; // Nothing to add
        }
        
        // Validate all entries belong to same tenant (append-only enforcement)
        var firstTenantId = entriesList.First().TenantId;
        if (firstTenantId == null)
            throw new InvalidOperationException("First entry has null TenantId");
            
        var mixedTenantEntries = entriesList.Where(e => e.TenantId != firstTenantId).ToList();
        if (mixedTenantEntries.Any())
        {
            var mixedTenantIds = mixedTenantEntries.Select(e => e.TenantId.Value.ToString()).Distinct();
            throw new InvalidOperationException(
                $"All entries must belong to the same tenant. Found entries from tenants: {string.Join(", ", mixedTenantIds)}");
        }
        
        // Check for duplicate IDs (append-only enforcement)
        var duplicateIds = entriesList
            .GroupBy(e => e.Id)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToList();
            
        if (duplicateIds.Any())
        {
            throw new InvalidOperationException(
                $"Duplicate entry IDs found: {string.Join(", ", duplicateIds)}. Cannot add duplicate entries (append-only enforcement).");
        }
        
        // Add entries to context (no tracking for immutability)
        await _context.AccountingEntries.AddRangeAsync(entriesList, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
        
        _logger.LogInformation("Added {Count} accounting entries for tenant {TenantId}", 
            entriesList.Count, firstTenantId);
        }
        catch (OperationCanceledException ex)
        {
            _logger.LogWarning(ex, "AddRangeAsync was cancelled for {Count} entries", entries?.Count() ?? 0);
            throw;
        }
        catch (Exception ex) when (!(ex is ArgumentNullException || ex is InvalidOperationException || ex is OperationCanceledException))
        {
            _logger.LogError(ex, "Unexpected error in AddRangeAsync for {Count} entries", entries?.Count() ?? 0);
            throw new InvalidOperationException($"Failed to add accounting entries to database", ex);
        }
    }
    
    public async Task<IEnumerable<CoreAccountingEntry>> GetByPeriodAsync(
        TenantId tenantId,
        AccountingPeriod period, 
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Input validation
            if (period == null) 
                throw new ArgumentNullException(nameof(period), "AccountingPeriod cannot be null");
            if (tenantId == null)
                throw new ArgumentNullException(nameof(tenantId), "TenantId cannot be null");
            
            // Build query for period-based filtering with tenant isolation
            // Normalize to UTC to match CreatedAt (DateTime.UtcNow)
            var startDate = DateTime.SpecifyKind(new DateTime(period.Year, period.Month, 1), DateTimeKind.Utc);
            var endDate = DateTime.SpecifyKind(startDate.AddMonths(1).AddDays(-1), DateTimeKind.Utc);
            
            var query = _context.AccountingEntries
                .Where(e => e.TenantId == tenantId &&
                           e.CreatedAt >= startDate && 
                           e.CreatedAt <= endDate);
            
            // Execute query with ordering
            var result = await query
                .OrderByDescending(e => e.CreatedAt)
                .ToListAsync(cancellationToken);
            
            _logger.LogDebug("Retrieved {Count} accounting entries for period {Period}", 
                result.Count, period.ToString());
            
            return result;
        }
        catch (OperationCanceledException ex)
        {
            _logger.LogWarning(ex, "GetByPeriodAsync was cancelled for period {Period}", period?.ToString());
            throw;
        }
        catch (Exception ex) when (!(ex is ArgumentNullException || ex is OperationCanceledException))
        {
            _logger.LogError(ex, "Unexpected error in GetByPeriodAsync for period {Period}", period?.ToString());
            throw new InvalidOperationException($"Failed to retrieve accounting entries for period {period?.ToString()}", ex);
        }
    }
}
