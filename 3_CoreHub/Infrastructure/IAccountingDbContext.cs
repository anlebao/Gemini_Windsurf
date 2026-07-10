using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using VanAn.Shared.Domain;
using VanAn.Shared.Domain.Audit;

namespace VanAn.CoreHub.Infrastructure
{
    /// <summary>
    /// Accounting-only DbContext abstraction — always PostgreSQL (online).
    /// Enforces ADR-001: "accounting always online" + ADR-003 immutability compliance.
    /// Implemented by VanAnDbContext (PostgreSQL). NOT implemented by ShopERPDbContext (SQLite — business only).
    /// </summary>
    public interface IAccountingDbContext : IDisposable
    {
        DbSet<AccountingEntry> AccountingEntries { get; }
        DbSet<JournalEntry> JournalEntries { get; }
        DbSet<AuditLog> AuditLogs { get; }
        DbSet<PendingInvoiceQueue> PendingInvoiceQueues { get; }
        DbSet<VanAn.CoreHub.Infrastructure.Entities.AccountChartEntity> AccountCharts { get; }
        DbSet<VanAn.CoreHub.Infrastructure.Entities.PeriodClosingStatusEntity> PeriodClosingStatuses { get; }

        Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
        Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default);
    }
}
