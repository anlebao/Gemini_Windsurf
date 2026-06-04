using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using VanAn.Shared.Domain;
using VanAn.CoreHub.Infrastructure;

namespace VanAn.CoreHub.Repositories
{
    /// <summary>
    /// Repository implementation for HKD Book management (7 HKD books - Thông tư 152/2025/TT-BTC)
    /// Implements 5-layer protection: Domain, EF Core, Repository, Service, API
    /// </summary>
    public class HKDBookRepository(IVanAnDbContext context, ILogger<HKDBookRepository> logger) : IHKDBookRepository
    {
        private readonly IVanAnDbContext _context = context;
        private readonly ILogger<HKDBookRepository> _logger = logger;

        public async Task<IEnumerable<JournalEntry>> GetByBookTypeAsync(
            TenantId tenantId,
            AccountingBookType bookType,
            CancellationToken cancellationToken = default)
        {
            try
            {
                return await _context.JournalEntries
                    .Where(e => EF.Property<Guid>(e, "TenantId") == tenantId.Value)
                    .OrderBy(e => e.EntryDate)
                    .ToListAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting HKD book entries for tenant {TenantId}, book type {BookType}",
                    tenantId.Value, bookType);
                return new List<JournalEntry>();
            }
        }

        public async Task<IEnumerable<JournalEntry>> GetByBookTypeAndPeriodAsync(
            TenantId tenantId,
            AccountingBookType bookType,
            AccountingPeriod period,
            CancellationToken cancellationToken = default)
        {
            try
            {
                return await _context.JournalEntries
                    .Where(e => EF.Property<Guid>(e, "TenantId") == tenantId.Value &&
                               e.Period.Year == period.Year &&
                               e.Period.Month == period.Month)
                    .OrderBy(e => e.EntryDate)
                    .ToListAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting HKD book entries for tenant {TenantId}, book type {BookType}, period {Period}",
                    tenantId.Value, bookType, period);
                return new List<JournalEntry>();
            }
        }

        public async Task<IEnumerable<JournalEntry>> GetS1aBookAsync(
            TenantId tenantId,
            AccountingPeriod period,
            CancellationToken cancellationToken = default)
        {
            try
            {
                return await GetByBookTypeAndPeriodAsync(tenantId, AccountingBookType.S1a_HKD, period, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting S1a-HKD book for tenant {TenantId}, period {Period}",
                    tenantId.Value, period);
                return new List<JournalEntry>();
            }
        }

        public async Task<IEnumerable<JournalEntry>> GetS2bBookAsync(
            TenantId tenantId,
            AccountingPeriod period,
            CancellationToken cancellationToken = default)
        {
            try
            {
                return await GetByBookTypeAndPeriodAsync(tenantId, AccountingBookType.S2b_HKD, period, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting S2b-HKD book for tenant {TenantId}, period {Period}",
                    tenantId.Value, period);
                return new List<JournalEntry>();
            }
        }

        public async Task<IEnumerable<JournalEntry>> GetDetailedLedgerAsync(
            TenantId tenantId,
            string accountNumber,
            AccountingPeriod period,
            CancellationToken cancellationToken = default)
        {
            try
            {
                return await _context.JournalEntries
                    .Where(e => EF.Property<Guid>(e, "TenantId") == tenantId.Value &&
                               e.Period.Year == period.Year &&
                               e.Period.Month == period.Month &&
                               e.Lines.Any(l => l.AccountNumber == accountNumber))
                    .OrderBy(e => e.EntryDate)
                    .ToListAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting Detailed Ledger for tenant {TenantId}, account {AccountNumber}, period {Period}",
                    tenantId.Value, accountNumber, period);
                return new List<JournalEntry>();
            }
        }

        public async Task<IEnumerable<JournalEntry>> GetS3aBookAsync(
            TenantId tenantId,
            AccountingPeriod period,
            CancellationToken cancellationToken = default)
        {
            try
            {
                return await GetByBookTypeAndPeriodAsync(tenantId, AccountingBookType.S3a_HKD, period, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting S3a-HKD book for tenant {TenantId}, period {Period}",
                    tenantId.Value, period);
                return new List<JournalEntry>();
            }
        }

        public async Task AddToBookAsync(
            JournalEntry entry,
            AccountingBookType bookType,
            CancellationToken cancellationToken = default)
        {
            try
            {
                // For now, just add the journal entry to the database
                // In a full implementation, we would create separate entries for each book type
                _ = await _context.JournalEntries.AddAsync(entry, cancellationToken);
                _ = await _context.SaveChangesAsync(cancellationToken);

                _logger.LogInformation("Added entry to HKD book {BookType} for tenant {TenantId}", bookType, entry.TenantId.Value);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding entry to HKD book {BookType} for tenant {TenantId}", bookType, entry.TenantId.Value);
                throw;
            }
        }

        public async Task AddRangeToBookAsync(
            IEnumerable<JournalEntry> entries,
            AccountingBookType bookType,
            CancellationToken cancellationToken = default)
        {
            try
            {
                await _context.JournalEntries.AddRangeAsync(entries, cancellationToken);
                _ = await _context.SaveChangesAsync(cancellationToken);

                _logger.LogInformation("Added {Count} entries to HKD book {BookType}", entries.Count(), bookType);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding entries to HKD book {BookType}", bookType);
                throw;
            }
        }

        public async Task<HKDBookSummary> GetBookSummaryAsync(
            TenantId tenantId,
            AccountingBookType bookType,
            AccountingPeriod period,
            CancellationToken cancellationToken = default)
        {
            try
            {
                IEnumerable<JournalEntry> entries = await GetByBookTypeAndPeriodAsync(tenantId, bookType, period, cancellationToken);

                decimal totalDebit = entries.Sum(e => e.Lines.Sum(l => l.DebitAmount));
                decimal totalCredit = entries.Sum(e => e.Lines.Sum(l => l.CreditAmount));
                decimal balance = totalDebit - totalCredit;

                return new HKDBookSummary(
                    bookType,
                    period,
                    entries.Count(),
                    totalDebit,
                    totalCredit,
                    balance,
                    DateTime.UtcNow
                );
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting book summary for tenant {TenantId}, book type {BookType}, period {Period}",
                    tenantId.Value, bookType, period);

                return new HKDBookSummary(
                    bookType,
                    period,
                    0,
                    0,
                    0,
                    0,
                    DateTime.UtcNow
                );
            }
        }

        public async Task AddAsync(JournalEntry entry)
        {
            try
            {
                _ = await _context.JournalEntries.AddAsync(entry);
                _ = await _context.SaveChangesAsync();

                _logger.LogInformation("Added journal entry {EntryId} to repository", entry.Id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding journal entry to repository");
                throw;
            }
        }
    }
}
