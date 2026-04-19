using VanAn.Shared.Domain;
using CoreAccountingEntry = VanAn.Shared.Domain.AccountingEntry;

namespace VanAn.CoreHub.Services;

/// <summary>
/// Service interface for Accounting Event operations - Week 1 implementation
/// Handles event publishing for Eventual Consistency pattern
/// </summary>
public interface IAccountingEventService
{
    Task PublishAccountingEntryCreatedAsync(CoreAccountingEntry entry, CancellationToken cancellationToken = default);
    Task PublishAccountingEntryReversedAsync(CoreAccountingEntry originalEntry, CoreAccountingEntry reversalEntry, CancellationToken cancellationToken = default);
}
