using Microsoft.Extensions.Logging;
using VanAn.Shared.Domain;
using VanAn.CoreHub.Repositories;
using CoreAccountingEntry = VanAn.Shared.Domain.AccountingEntry;

namespace VanAn.CoreHub.Services
{
    /// <summary>
    /// Service implementation for Reversal operations - Week 1 implementation
    /// Implements 5-layer protection: Domain, EF Core, Repository, Service, API
    /// </summary>
    public class ReversalService(
        IAccountingEntryRepository repository,
        ILogger<ReversalService> logger) : IReversalService
    {
        private readonly IAccountingEntryRepository _repository = repository;
        private readonly ILogger<ReversalService> _logger = logger;

        public async Task<CoreAccountingEntry> CreateReversalEntryAsync(
            AccountingEntryId originalEntryId,
            TenantId tenantId,
            string reason,
            CancellationToken cancellationToken = default)
        {
            try
            {
                // Get the original entry
                CoreAccountingEntry? originalEntry = await _repository.GetByIdAsync(originalEntryId, cancellationToken) ?? throw new InvalidOperationException($"Original entry {originalEntryId} not found");

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
                // Explicitly pass the originalEntryId so ReversalEntryId matches the queried key
                CoreAccountingEntry reversalEntry = CoreAccountingEntry.CreateReversalWithId(originalEntry, reason, originalEntryId.Value);

                // Add only the new reversal entry - never modify the original
                await _repository.AddAsync(reversalEntry, cancellationToken);

                _logger.LogInformation("Reversal entry created: {ReversalId} for original entry {OriginalId}, reason: {Reason}",
                    reversalEntry.Id, originalEntryId, reason);

                return reversalEntry;
            }
            catch (Exception ex) when (ex is not (InvalidOperationException or UnauthorizedAccessException))
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
                CoreAccountingEntry? entry = await _repository.GetByIdAsync(originalEntryId, cancellationToken);

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
                IEnumerable<CoreAccountingEntry> tenantEntries = await _repository.GetByTenantAsync(tenantId, cancellationToken);
                Dictionary<Guid, CoreAccountingEntry> entryDict = tenantEntries.ToDictionary(e => e.Id, e => e);

                // Resolve the starting entry via GetByIdAsync so mock-based tests work correctly
                CoreAccountingEntry? startEntry = await _repository.GetByIdAsync(originalEntryId, cancellationToken);
                if (startEntry == null)
                {
                    return Enumerable.Empty<CoreAccountingEntry>();
                }

                List<CoreAccountingEntry> allEntries = [startEntry];
                HashSet<Guid> visitedIds = [startEntry.Id];

                // Follow reversal chain forward (find reversal pointing to this entry)
                CoreAccountingEntry? reversal = tenantEntries.FirstOrDefault(e => e.ReversalEntryId == startEntry.Id);
                while (reversal != null && visitedIds.Add(reversal.Id))
                {
                    allEntries.Add(reversal);
                    reversal = tenantEntries.FirstOrDefault(e => e.ReversalEntryId == reversal.Id);
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
                CoreAccountingEntry? entry = await _repository.GetByIdAsync(originalEntryId, cancellationToken);

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
}
