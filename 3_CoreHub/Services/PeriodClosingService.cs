using Microsoft.Extensions.Logging;
using VanAn.Shared.Domain;
using VanAn.CoreHub.Repositories;

namespace VanAn.CoreHub.Services;

/// <summary>
/// Period closing service implementation.
/// AccountingEntry immutability is preserved — period reopening uses Reversal Entry pattern.
/// Multi-tenancy enforced at every query.
/// </summary>
public class PeriodClosingService : IPeriodClosingService
{
    private readonly IAccountingEntryRepository _entryRepository;
    private readonly IReversalService _reversalService;
    private readonly ILogger<PeriodClosingService> _logger;

    private static readonly Dictionary<(TenantId, AccountingPeriod), PeriodClosingStatus> _statusStore = new();

    public PeriodClosingService(
        IAccountingEntryRepository entryRepository,
        IReversalService reversalService,
        ILogger<PeriodClosingService> logger)
    {
        _entryRepository = entryRepository;
        _reversalService = reversalService;
        _logger = logger;
    }

    public async Task<PeriodClosingCheckResult> ValidatePeriodAsync(
        AccountingPeriod period,
        TenantId tenantId,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Validating period {Period} for tenant {TenantId}", period, tenantId.Value);

        var errors = new List<string>();
        var warnings = new List<string>();

        var entries = (await _entryRepository.GetByTenantAndPeriodAsync(tenantId, period, cancellationToken)).ToList();

        if (entries.Count == 0)
        {
            errors.Add($"No accounting entries found for period {period.Year}-{period.Month:D2}");
            return new PeriodClosingCheckResult(false, errors, warnings);
        }

        var revenueEntries = entries.Where(e => e.AccountingBookType == AccountingBookType.RevenueBook).ToList();
        var expenseEntries = entries.Where(e => e.AccountingBookType == AccountingBookType.ExpenseBook).ToList();

        var totalRevenue = revenueEntries.Sum(e => e.Amount);
        var totalExpense = expenseEntries.Sum(e => e.Amount);

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

        var key = (tenantId, period);
        if (_statusStore.TryGetValue(key, out var currentStatus) && currentStatus == PeriodClosingStatus.Closed)
        {
            throw new InvalidOperationException($"Period {period.Year}-{period.Month:D2} is already closed.");
        }

        var validation = await ValidatePeriodAsync(period, tenantId, cancellationToken);
        if (!validation.IsValid)
        {
            var errorSummary = string.Join("; ", validation.Errors);
            throw new InvalidOperationException($"Period validation failed. Cannot close period with errors: {errorSummary}");
        }

        var entries = await _entryRepository.GetByTenantAndPeriodAsync(tenantId, period, cancellationToken);
        var pendingCount = entries.Count(e => e.Amount == 0);
        if (pendingCount > 0)
        {
            throw new InvalidOperationException($"Cannot close period: {pendingCount} pending transactions exist.");
        }

        _statusStore[key] = PeriodClosingStatus.Closed;

        var closingEntry = new ClosingEntry(Guid.NewGuid(), period, DateTime.UtcNow, userId);
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

        var key = (tenantId, period);
        if (!_statusStore.TryGetValue(key, out var currentStatus) || currentStatus != PeriodClosingStatus.Closed)
        {
            throw new InvalidOperationException($"Cannot reopen period {period.Year}-{period.Month:D2}: it is not closed.");
        }

        _statusStore[key] = PeriodClosingStatus.Reopening;

        var entries = await _entryRepository.GetByTenantAndPeriodAsync(tenantId, period, cancellationToken);
        foreach (var entry in entries)
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
        var key = (tenantId, period);
        var status = _statusStore.TryGetValue(key, out var s) ? s : PeriodClosingStatus.Open;
        return Task.FromResult(status);
    }
}
