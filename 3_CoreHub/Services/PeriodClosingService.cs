using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using VanAn.CoreHub.Infrastructure;
using VanAn.CoreHub.Infrastructure.Entities;
using VanAn.CoreHub.Repositories;
using VanAn.Shared.Domain;

namespace VanAn.CoreHub.Services
{
    /// <summary>
    /// Period closing service implementation.
    /// AccountingEntry immutability is preserved — period reopening uses Reversal Entry pattern.
    /// Multi-tenancy enforced at every query.
    ///
    /// W5: Replaced in-memory <c>static Dictionary</c> with DB-persisted <see cref="PeriodClosingStatusEntity"/>.
    /// Period close/reopen state now survives application restarts.
    /// </summary>
    public class PeriodClosingService(
        IAccountingEntryRepository entryRepository,
        IReversalService reversalService,
        IAuditTrailService auditTrailService,
        IAccountingDbContext dbContext,
        ILogger<PeriodClosingService> logger) : IPeriodClosingService
    {
        private readonly IAccountingEntryRepository _entryRepository = entryRepository;
        private readonly IReversalService _reversalService = reversalService;
        private readonly IAuditTrailService _auditTrailService = auditTrailService;
        private readonly IAccountingDbContext _dbContext = dbContext;
        private readonly ILogger<PeriodClosingService> _logger = logger;

        public async Task<PeriodClosingCheckResult> ValidatePeriodAsync(
            AccountingPeriod period,
            TenantId tenantId,
            CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("Validating period {Period} for tenant {TenantId}", period, tenantId.Value);

            List<string> errors = [];
            List<string> warnings = [];

            List<AccountingEntry> entries = (await _entryRepository.GetByTenantAndPeriodAsync(tenantId, period, cancellationToken)).ToList();

            if (entries.Count == 0)
            {
                errors.Add($"No accounting entries found for period {period.Year}-{period.Month:D2}");
                return new PeriodClosingCheckResult(false, errors, warnings);
            }

            List<AccountingEntry> revenueEntries = entries.Where(e => e.AccountingBookType == AccountingBookType.RevenueBook).ToList();
            List<AccountingEntry> expenseEntries = entries.Where(e => e.AccountingBookType == AccountingBookType.ExpenseBook).ToList();

            decimal totalRevenue = revenueEntries.Sum(e => e.Amount);
            decimal totalExpense = expenseEntries.Sum(e => e.Amount);

            if (Math.Abs(totalRevenue + totalExpense) > 0.01m && revenueEntries.Count > 0 && expenseEntries.Count > 0)
            {
                warnings.Add($"Revenue/Expense ratio check: Revenue={totalRevenue:N0}, Expense={totalExpense:N0}");
            }

            _logger.LogInformation("Period {Period} validation passed. Entries: {Count}", period, entries.Count);
            return new PeriodClosingCheckResult(true, errors, warnings);
        }

        public async Task<ClosingEntry> ClosePeriodAsync(
            AccountingPeriod period,
            TenantId tenantId,
            Guid userId,
            CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("Closing period {Period} for tenant {TenantId}", period, tenantId.Value);

            PeriodClosingStatusEntity? statusEntity = await GetOrCreateStatusEntityAsync(period, tenantId, cancellationToken);

            if (statusEntity.Status == PeriodClosingStatus.Closed)
            {
                throw new InvalidOperationException($"Period {period.Year}-{period.Month:D2} is already closed.");
            }

            PeriodClosingCheckResult validation = await ValidatePeriodAsync(period, tenantId, cancellationToken);
            if (!validation.IsValid)
            {
                string errorSummary = string.Join("; ", validation.Errors);
                throw new InvalidOperationException($"Period validation failed. Cannot close period with errors: {errorSummary}");
            }

            IEnumerable<AccountingEntry> entries = await _entryRepository.GetByTenantAndPeriodAsync(tenantId, period, cancellationToken);
            int pendingCount = entries.Count(e => e.Amount == 0);
            if (pendingCount > 0)
            {
                throw new InvalidOperationException($"Cannot close period: {pendingCount} pending transactions exist.");
            }

            // W5: Persist closed status to DB (survives app restart)
            statusEntity.MarkClosed(userId.ToString());
            _ = await _dbContext.SaveChangesAsync(cancellationToken);

            ClosingEntry closingEntry = new(Guid.NewGuid(), period, DateTime.UtcNow, userId);

            // Audit log: Period closing
            await _auditTrailService.LogPeriodCloseAsync(
                period,
                "Period closed after validation",
                correlationId: closingEntry.PeriodId.ToString(),
                cancellationToken: cancellationToken);

            _logger.LogInformation("Period {Period} closed successfully by user {UserId}", period, userId);

            return closingEntry;
        }

        public async Task ReopenPeriodAsync(
            AccountingPeriod period,
            TenantId tenantId,
            Guid userId,
            string reason,
            CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("Reopening period {Period} for tenant {TenantId}. Reason: {Reason}", period, tenantId.Value, reason);

            // Tracked query (no AsNoTracking) — entity will be modified + saved
            PeriodClosingStatusEntity? statusEntity = await _dbContext.PeriodClosingStatuses
                .FirstOrDefaultAsync(
                    e => e.TenantId == tenantId && e.PeriodYear == period.Year && e.PeriodMonth == period.Month,
                    cancellationToken);
            if (statusEntity == null || statusEntity.Status != PeriodClosingStatus.Closed)
            {
                throw new InvalidOperationException($"Cannot reopen period {period.Year}-{period.Month:D2}: it is not closed.");
            }

            // W5: Persist reopening transition to DB
            statusEntity.MarkReopening(reason);
            _ = await _dbContext.SaveChangesAsync(cancellationToken);

            IEnumerable<AccountingEntry> entries = await _entryRepository.GetByTenantAndPeriodAsync(tenantId, period, cancellationToken);
            foreach (AccountingEntry entry in entries)
            {
                _ = await _reversalService.CreateReversalEntryAsync(
                    new AccountingEntryId(entry.Id),
                    tenantId,
                    $"Period reopening: {reason}",
                    cancellationToken);
            }

            // W5: Complete reopen transition (Reopening → Open)
            statusEntity.MarkReopened();
            _ = await _dbContext.SaveChangesAsync(cancellationToken);

            // Audit log: Period reopening
            await _auditTrailService.LogPeriodReopenAsync(
                period,
                reason,
                cancellationToken: cancellationToken);

            _logger.LogInformation("Period {Period} reopened by user {UserId}", period, userId);
        }

        public async Task<PeriodClosingStatus> GetPeriodStatusAsync(
            AccountingPeriod period,
            TenantId tenantId,
            CancellationToken cancellationToken = default)
        {
            PeriodClosingStatusEntity? statusEntity = await GetStatusEntityAsync(period, tenantId, cancellationToken);
            return statusEntity?.Status ?? PeriodClosingStatus.Open;
        }

        /// <summary>
        /// Query the DB for an existing period status record. Returns null if not found (period is implicitly Open).
        /// </summary>
        private async Task<PeriodClosingStatusEntity?> GetStatusEntityAsync(
            AccountingPeriod period,
            TenantId tenantId,
            CancellationToken cancellationToken)
        {
            return await _dbContext.PeriodClosingStatuses
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    e => e.TenantId == tenantId && e.PeriodYear == period.Year && e.PeriodMonth == period.Month,
                    cancellationToken);
        }

        /// <summary>
        /// Get an existing status entity for update (tracked), or create a new Open-status entity if none exists.
        /// The new entity is NOT yet saved — caller must call SaveChangesAsync after mutation.
        /// </summary>
        private async Task<PeriodClosingStatusEntity> GetOrCreateStatusEntityAsync(
            AccountingPeriod period,
            TenantId tenantId,
            CancellationToken cancellationToken)
        {
            PeriodClosingStatusEntity? existing = await _dbContext.PeriodClosingStatuses
                .FirstOrDefaultAsync(
                    e => e.TenantId == tenantId && e.PeriodYear == period.Year && e.PeriodMonth == period.Month,
                    cancellationToken);

            if (existing != null)
            {
                return existing;
            }

            PeriodClosingStatusEntity created = new(tenantId, period.Year, period.Month);
            _ = await _dbContext.PeriodClosingStatuses.AddAsync(created, cancellationToken);
            _ = await _dbContext.SaveChangesAsync(cancellationToken);
            return created;
        }
    }
}
