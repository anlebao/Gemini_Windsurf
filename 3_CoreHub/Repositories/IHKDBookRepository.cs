using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using VanAn.Shared.Domain;

namespace VanAn.CoreHub.Repositories;

/// <summary>
/// Repository interface for HKD Book management (7 HKD books - Thông tư 152/2025/TT-BTC)
/// Implements 5-layer protection: Domain, EF Core, Repository, Service, API
/// </summary>
public interface IHKDBookRepository
{
    /// <summary>
    /// Gets journal entries by book type for a tenant
    /// </summary>
    Task<IEnumerable<JournalEntry>> GetByBookTypeAsync(
        TenantId tenantId, 
        AccountingBookType bookType, 
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Gets journal entries by book type and period for a tenant
    /// </summary>
    Task<IEnumerable<JournalEntry>> GetByBookTypeAndPeriodAsync(
        TenantId tenantId, 
        AccountingBookType bookType, 
        AccountingPeriod period, 
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Gets S1a-HKD book entries (Không chịu thuế GTGT) for a tenant
    /// </summary>
    Task<IEnumerable<JournalEntry>> GetS1aBookAsync(
        TenantId tenantId, 
        AccountingPeriod period, 
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Gets S2b-HKD book entries (Sổ doanh thu bán hàng hóa) for a tenant
    /// </summary>
    Task<IEnumerable<JournalEntry>> GetS2bBookAsync(
        TenantId tenantId, 
        AccountingPeriod period, 
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Gets Detailed Ledger entries (Sổ Chi tiết) for a tenant and account
    /// </summary>
    Task<IEnumerable<JournalEntry>> GetDetailedLedgerAsync(
        TenantId tenantId, 
        string accountNumber, 
        AccountingPeriod period, 
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Gets S3a-HKD book entries (Hoạt động chịu thuế khác) for a tenant
    /// </summary>
    Task<IEnumerable<JournalEntry>> GetS3aBookAsync(
        TenantId tenantId, 
        AccountingPeriod period, 
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Add single entry (for compatibility)
    /// </summary>
    Task AddAsync(JournalEntry entry);
    
    /// <summary>
    /// Adds journal entries to specific HKD book
    /// </summary>
    Task AddToBookAsync(
        JournalEntry entry, 
        AccountingBookType bookType, 
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Adds multiple journal entries to specific HKD book
    /// </summary>
    Task AddRangeToBookAsync(
        IEnumerable<JournalEntry> entries, 
        AccountingBookType bookType, 
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Gets book summary for reporting
    /// </summary>
    Task<HKDBookSummary> GetBookSummaryAsync(
        TenantId tenantId, 
        AccountingBookType bookType, 
        AccountingPeriod period, 
        CancellationToken cancellationToken = default);
}

/// <summary>
/// HKD Book summary for reporting
/// </summary>
public record HKDBookSummary(
    AccountingBookType BookType,
    AccountingPeriod Period,
    int TotalEntries,
    decimal TotalDebit,
    decimal TotalCredit,
    decimal Balance,
    DateTime LastUpdated
);
