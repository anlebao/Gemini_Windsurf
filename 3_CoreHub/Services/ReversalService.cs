using Microsoft.Extensions.Logging;
using VanAn.Shared.Domain;
using VanAn.CoreHub.Domain;
using VanAn.CoreHub.Repositories;
using CoreAccountingEntry = VanAn.Shared.Domain.AccountingEntry;

namespace VanAn.CoreHub.Services;

/// <summary>
/// Service implementation for Reversal operations - Week 1 implementation
/// Implements 5-layer protection: Domain, EF Core, Repository, Service, API
/// </summary>
public class ReversalService : IReversalService
{
    private readonly IAccountingEntryRepository _repository;
    private readonly ILogger<ReversalService> _logger;
    
    public ReversalService(
        IAccountingEntryRepository repository,
        ILogger<ReversalService> logger)
    {
        _repository = repository;
        _logger = logger;
    }
    
    public async Task<CoreAccountingEntry> CreateReversalEntryAsync(
        AccountingEntryId originalEntryId,
        TenantId tenantId,
        string reason,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Get the original entry
            var originalEntry = await _repository.GetByIdAsync(originalEntryId, cancellationToken);
            
            if (originalEntry == null)
            {
                throw new InvalidOperationException($"Original entry {originalEntryId} not found");
            }
            
            // Multi-tenancy check: ensure original entry belongs to requesting tenant
            if (!originalEntry.TenantId.Equals(tenantId))
            {
                throw new UnauthorizedAccessException($"Entry {originalEntryId} does not belong to tenant {tenantId.Value}");
            }
            
            // Check if entry can be reversed - strengthen the check to prevent multiple reversals
            if (originalEntry.ReversalEntryId != null)
            {
                throw new InvalidOperationException($"Entry {originalEntryId} is already reversed and cannot be reversed again");
            }
            
            // Create reversal entry ONLY - no updates to the original entry
            // The Factory handles setting the ReversalEntryId to link to the original
            var reversalEntry = VanAn.Shared.Domain.AccountingEntryFactory.CreateReversal(originalEntry, reason);
            
            // Add only the new reversal entry - never modify the original
            await _repository.AddAsync(reversalEntry, cancellationToken);
            
            _logger.LogInformation("Reversal entry created: {ReversalId} for original entry {OriginalId}, reason: {Reason}", 
                reversalEntry.Id, originalEntryId, reason);
            
            return reversalEntry;
        }
        catch (Exception ex) when (!(ex is InvalidOperationException || ex is UnauthorizedAccessException))
        {
            _logger.LogError(ex, "Error creating reversal entry for original entry {OriginalId}", originalEntryId);
            throw;
        }
    }
    
    public async Task<CoreAccountingEntry?> GetOriginalEntryAsync(
        AccountingEntryId originalEntryId,
        TenantId tenantId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var entry = await _repository.GetByIdAsync(originalEntryId, cancellationToken);
            
            // Multi-tenancy check: ensure entry belongs to requesting tenant
            if (entry != null && !entry.TenantId.Equals(tenantId))
            {
                _logger.LogWarning("Access denied: Entry {EntryId} does not belong to tenant {TenantId}", 
                    originalEntryId, tenantId.Value);
                return null;
            }
            
            return entry;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving original entry {EntryId} for tenant {TenantId}", 
                originalEntryId, tenantId);
            throw;
        }
    }
    
    public async Task<IEnumerable<CoreAccountingEntry>> GetReversalChainAsync(
        AccountingEntryId originalEntryId,
        TenantId tenantId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Get all tenant entries once to optimize performance
            var tenantEntries = await _repository.GetByTenantAsync(tenantId, cancellationToken);
            var entryDict = tenantEntries.ToDictionary(e => e.Id, e => e);
            
            var allEntries = new List<CoreAccountingEntry>();
            var currentId = originalEntryId;
            var visitedIds = new HashSet<Guid>(); // Prevent infinite loops
            
            while (currentId != null && visitedIds.Add(currentId))
            {
                if (!entryDict.TryGetValue(currentId, out var entry))
                {
                    break;
                }
                
                allEntries.Add(entry);
                
                if (entry.ReversalEntryId != null)
                {
                    // This is a reversal entry, get the original
                    currentId = entry.ReversalEntryId;
                }
                else
                {
                    // This is the original entry, find reversals pointing to it
                    var reversal = tenantEntries.FirstOrDefault(e => e.ReversalEntryId == currentId.Value);
                    currentId = reversal?.Id;
                }
            }
            
            return allEntries.OrderBy(e => e.CreatedAt);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving reversal chain for entry {EntryId} and tenant {TenantId}", 
                originalEntryId, tenantId);
            throw;
        }
    }
    
    public async Task<bool> CanReverseEntryAsync(
        AccountingEntryId originalEntryId,
        TenantId tenantId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var entry = await _repository.GetByIdAsync(originalEntryId, cancellationToken);
            
            if (entry == null)
            {
                return false;
            }
            
            // Multi-tenancy check
            if (!entry.TenantId.Equals(tenantId))
            {
                return false;
            }
            
            // Entry can be reversed only if it hasn't been reversed yet
            return entry.ReversalEntryId == null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking if entry {EntryId} can be reversed for tenant {TenantId}", 
                originalEntryId, tenantId);
            return false;
        }
    }
}
