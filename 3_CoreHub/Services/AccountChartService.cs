using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using VanAn.CoreHub.Infrastructure;
using VanAn.CoreHub.Infrastructure.Entities;
using VanAn.Shared.Domain;
// W3: Alias to disambiguate from the legacy VanAn.CoreHub.Services.AccountType (7 values, includes OtherIncome/OtherExpense).
// The Domain AccountType (5 values, no Contra) is the W2-approved enum used by AccountChartEntry.
using DomainAccountType = VanAn.Shared.Domain.AccountType;

namespace VanAn.CoreHub.Services;

/// <summary>
/// W3: Implementation of <see cref="IAccountChartService"/>.
/// Queries the <c>AccountCharts</c> reference-data table and returns immutable
/// <see cref="AccountChartEntry"/> Domain records.
///
/// Reference data is global (NOT tenant-scoped) — no multi-tenancy filter applied
/// because <see cref="AccountChartEntity"/> does not implement IMustHaveTenant.
/// </summary>
public class AccountChartService : IAccountChartService
{
    private readonly IAccountingDbContext _dbContext;
    private readonly ILogger<AccountChartService> _logger;

    public AccountChartService(IAccountingDbContext dbContext, ILogger<AccountChartService> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<string> GetAccountNameAsync(string accountCode, AccountingStandard standard, CancellationToken ct = default)
    {
        AccountChartEntry? entry = await GetAccountAsync(accountCode, standard, ct).ConfigureAwait(false);
        return entry?.AccountName ?? $"Tài khoản {accountCode}";
    }

    /// <inheritdoc />
    public async Task<List<AccountChartEntry>> GetAccountsByTypeAsync(DomainAccountType type, AccountingStandard standard, CancellationToken ct = default)
    {
        List<AccountChartEntity> entities = await _dbContext.AccountCharts
            .AsNoTracking()
            .Where(e => e.Standard == standard && e.Type == type)
            .OrderBy(e => e.AccountCode)
            .ToListAsync(ct)
            .ConfigureAwait(false);

        return entities.Select(e => e.ToDomainRecord()).ToList();
    }

    /// <inheritdoc />
    public async Task<DomainAccountType> GetAccountTypeAsync(string accountCode, AccountingStandard standard, CancellationToken ct = default)
    {
        AccountChartEntry? entry = await GetAccountAsync(accountCode, standard, ct).ConfigureAwait(false);
        return entry?.Type ?? DomainAccountType.Expense; // safe default — W4 services should null-check via GetAccountAsync
    }

    /// <inheritdoc />
    public async Task<AccountChartEntry?> GetAccountAsync(string accountCode, AccountingStandard standard, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(accountCode))
            return null;

        AccountChartEntity? entity = await _dbContext.AccountCharts
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.Standard == standard && e.AccountCode == accountCode, ct)
            .ConfigureAwait(false);

        return entity?.ToDomainRecord();
    }

    /// <inheritdoc />
    public async Task<List<AccountChartEntry>> GetAllAccountsAsync(AccountingStandard standard, CancellationToken ct = default)
    {
        List<AccountChartEntity> entities = await _dbContext.AccountCharts
            .AsNoTracking()
            .Where(e => e.Standard == standard)
            .OrderBy(e => e.AccountCode)
            .ToListAsync(ct)
            .ConfigureAwait(false);

        return entities.Select(e => e.ToDomainRecord()).ToList();
    }
}
