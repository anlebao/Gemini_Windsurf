using VanAn.Shared.Domain;
using CoreAccountingEntry = VanAn.Shared.Domain.AccountingEntry;

namespace VanAn.CoreHub.Services;

/// <summary>
/// Service interface for Household Business Book operations - Week 1 implementation
/// Implements 5-layer protection: Domain, EF Core, Repository, Service, API
/// </summary>
public interface IHKDBookService
{
    Task<CoreAccountingEntry> RecordRevenueAsync(
        TenantId tenantId,
        decimal amount,
        string description,
        DateTime? transactionDate = null,
        CancellationToken cancellationToken = default);
    
    Task<CoreAccountingEntry> RecordExpenseAsync(
        TenantId tenantId,
        decimal amount,
        string description,
        DateTime? transactionDate = null,
        CancellationToken cancellationToken = default);
    
    Task<decimal> GetRevenueTotalAsync(
        TenantId tenantId,
        AccountingPeriod period,
        CancellationToken cancellationToken = default);
    
    Task<decimal> GetExpenseTotalAsync(
        TenantId tenantId,
        AccountingPeriod period,
        CancellationToken cancellationToken = default);
    
    Task<decimal> GetProfitAsync(
        TenantId tenantId,
        AccountingPeriod period,
        CancellationToken cancellationToken = default);
    
    Task<IEnumerable<CoreAccountingEntry>> GetRevenueEntriesAsync(
        TenantId tenantId,
        AccountingPeriod period,
        CancellationToken cancellationToken = default);
    
    Task<IEnumerable<CoreAccountingEntry>> GetExpenseEntriesAsync(
        TenantId tenantId,
        AccountingPeriod period,
        CancellationToken cancellationToken = default);
}
