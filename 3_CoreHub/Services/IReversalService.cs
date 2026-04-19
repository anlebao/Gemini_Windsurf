using VanAn.Shared.Domain;
using CoreAccountingEntry = VanAn.Shared.Domain.AccountingEntry;

namespace VanAn.CoreHub.Services;

/// <summary>
/// Service interface for Reversal operations - Week 1 implementation
/// Implements 5-layer protection: Domain, EF Core, Repository, Service, API
/// </summary>
public interface IReversalService
{
    Task<CoreAccountingEntry> CreateReversalEntryAsync(
        AccountingEntryId originalEntryId,
        TenantId tenantId,
        string reason,
        CancellationToken cancellationToken = default);
    
    Task<CoreAccountingEntry?> GetOriginalEntryAsync(
        AccountingEntryId originalEntryId,
        TenantId tenantId,
        CancellationToken cancellationToken = default);
    
    Task<IEnumerable<CoreAccountingEntry>> GetReversalChainAsync(
        AccountingEntryId originalEntryId,
        TenantId tenantId,
        CancellationToken cancellationToken = default);
    
    Task<bool> CanReverseEntryAsync(
        AccountingEntryId originalEntryId,
        TenantId tenantId,
        CancellationToken cancellationToken = default);
}
