using Microsoft.Extensions.Logging;
using VanAn.Shared.Domain;
using VanAn.CoreHub.Repositories;

namespace VanAn.CoreHub.Services
{
    /// <summary>
    /// Period closing service implementation.
    /// AccountingEntry immutability is preserved — period reopening uses Reversal Entry pattern.
    /// Multi-tenancy enforced at every query.
    /// </summary>
    public class PeriodClosingService(
        IAccountingEntryRepository entryRepository,
        IReversalService reversalService,
        ILogger<PeriodClosingService> logger) : IPeriodClosingService
    {
        private readonly IAccountingEntryRepository _entryRepository = entryRepository;
        private readonly IReversalService _reversalService = reversalService;
        private readonly ILogger<PeriodClosingService> _logger = logger;

        private static readonly Dictionary<(TenantId, AccountingPeriod), PeriodClosingStatus> _statusStore = [];

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

            (TenantId tenantId, AccountingPeriod period) key = (tenantId, period);
            if (_statusStore.TryGetValue(key, out PeriodClosingStatus currentStatus) && currentStatus == PeriodClosingStatus.Closed)
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

            _statusStore[key] = PeriodClosingStatus.Closed;

            ClosingEntry closingEntry = new(Guid.NewGuid(), period, DateTime.UtcNow, userId);
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

            (TenantId tenantId, AccountingPeriod period) key = (tenantId, period);
            if (!_statusStore.TryGetValue(key, out PeriodClosingStatus currentStatus) || currentStatus != PeriodClosingStatus.Closed)
            {
                throw new InvalidOperationException($"Cannot reopen period {period.Year}-{period.Month:D2}: it is not closed.");
            }

            _statusStore[key] = PeriodClosingStatus.Reopening;

            IEnumerable<AccountingEntry> entries = await _entryRepository.GetByTenantAndPeriodAsync(tenantId, period, cancellationToken);
            foreach (AccountingEntry entry in entries)
            {
                await _reversalService.CreateReversalEntryAsync(
                    new AccountingEntryId(entry.Id),
                    tenantId,
                    $"Period reopening: {reason}",
                    cancellationToken);
            }

            _statusStore[key] = PeriodClosingStatus.Open;
            _logger.LogInformation("Period {Period} reopened by user {UserId}", period, userId);
        }

        public Task<PeriodClosingStatus> GetPeriodStatusAsync(
            AccountingPeriod period,
            TenantId tenantId,
            CancellationToken cancellationToken = default)
        {
            (TenantId tenantId, AccountingPeriod period) key = (tenantId, period);
            PeriodClosingStatus status = _statusStore.TryGetValue(key, out PeriodClosingStatus s) ? s : PeriodClosingStatus.Open;
            return Task.FromResult(status);
        }
    }
}
