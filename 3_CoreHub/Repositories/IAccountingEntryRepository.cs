using VanAn.Shared.Domain;
using CoreAccountingEntry = VanAn.Shared.Domain.AccountingEntry;

namespace VanAn.CoreHub.Repositories;

/// <summary>
/// Repository interface for AccountingEntry - No Update/Delete methods (immutable design)
/// Implements 5-layer protection: Domain, EF Core, Repository, Service, API
/// </summary>
public interface IAccountingEntryRepository
{
    Task<CoreAccountingEntry?> GetByIdAsync(AccountingEntryId id, CancellationToken cancellationToken = default);
    Task<IEnumerable<CoreAccountingEntry>> GetByTenantAsync(TenantId tenantId, CancellationToken cancellationToken = default);
    Task<IEnumerable<CoreAccountingEntry>> GetByTenantAndBookTypeAsync(
        TenantId tenantId, 
        AccountingBookType bookType, 
        CancellationToken cancellationToken = default);
    Task<IEnumerable<CoreAccountingEntry>> GetByTenantAndPeriodAsync(
        TenantId tenantId, 
        AccountingPeriod period, 
        CancellationToken cancellationToken = default);
    Task<IEnumerable<CoreAccountingEntry>> GetByTenantAndDateRangeAsync(
        TenantId tenantId, 
        DateTime startDate, 
        DateTime endDate, 
        CancellationToken cancellationToken = default);
    
    // Only Add operations - no Update/Delete methods (immutable design)
    Task AddAsync(CoreAccountingEntry entry, CancellationToken cancellationToken = default);
    Task AddRangeAsync(IEnumerable<CoreAccountingEntry> entries, CancellationToken cancellationToken = default);
}
