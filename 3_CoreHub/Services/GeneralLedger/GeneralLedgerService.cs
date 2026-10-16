using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using VanAn.CoreHub.Infrastructure;
using VanAn.Shared.Domain;

namespace VanAn.CoreHub.Services.GeneralLedger;

/// <summary>
/// P0.2 (2026-10-16 — master plan accounting-legal-compliance): Sổ Nhật ký chung + Sổ Cái cho DN.
/// Đọc thuần từ JournalEntry — KHÔNG ghi, KHÔNG đụng Domain.cs.
/// Filter theo EntryDate range (Known Error Pattern #5 — AccountingPeriod VO không translate).
/// </summary>
public class GeneralLedgerService(IAccountingDbContext db, ILogger<GeneralLedgerService> logger) : IGeneralLedgerService
{
    public async Task<IReadOnlyList<JournalEntryDto>> GetJournalAsync(
        TenantId tenantId,
        int year,
        int month,
        CancellationToken cancellationToken = default)
    {
        AccountingPeriod period = AccountingPeriod.FromDateTime(new DateTime(year, month, 1));

        List<JournalEntry> entries = await db.JournalEntries
            .Where(e => e.TenantId == tenantId
                && e.EntryDate >= period.StartDate
                && e.EntryDate <= period.EndDate)
            .OrderBy(e => e.EntryDate)
            .ThenBy(e => e.CreatedAt)
            .ToListAsync(cancellationToken);

        return entries.Select(MapJournalEntry).ToList();
    }

    public async Task<LedgerReportDto> GetLedgerAsync(
        TenantId tenantId,
        int year,
        int month,
        string accountCode,
        CancellationToken cancellationToken = default)
    {
        AccountingPeriod period = AccountingPeriod.FromDateTime(new DateTime(year, month, 1));

        List<JournalEntry> before = await db.JournalEntries
            .Where(e => e.TenantId == tenantId
                && e.EntryDate < period.StartDate
                && e.Lines.Any(l => l.AccountNumber == accountCode))
            .ToListAsync(cancellationToken);

        decimal opening = before.SelectMany(e => e.Lines)
            .Where(l => l.AccountNumber == accountCode)
            .Sum(l => l.DebitAmount - l.CreditAmount);

        List<JournalEntry> inPeriod = await db.JournalEntries
            .Where(e => e.TenantId == tenantId
                && e.EntryDate >= period.StartDate
                && e.EntryDate <= period.EndDate
                && e.Lines.Any(l => l.AccountNumber == accountCode))
            .OrderBy(e => e.EntryDate)
            .ThenBy(e => e.CreatedAt)
            .ToListAsync(cancellationToken);

        var rows = new List<LedgerRowDto>();
        decimal running = opening;
        foreach (JournalEntry entry in inPeriod)
        {
            foreach (JournalEntryLine line in entry.Lines.Where(l => l.AccountNumber == accountCode))
            {
                running += line.DebitAmount - line.CreditAmount;
                rows.Add(new LedgerRowDto(
                    entry.EntryDate,
                    entry.JournalNo,
                    entry.Description,
                    line.DebitAmount,
                    line.CreditAmount,
                    running));
            }
        }

        string? accountName = await GetAccountNameAsync(accountCode, cancellationToken);

        return new LedgerReportDto(
            accountCode,
            accountName,
            opening,
            rows.Sum(r => r.Debit),
            rows.Sum(r => r.Credit),
            running,
            rows);
    }

    public async Task<IReadOnlyList<AccountBalanceDto>> GetBalancesAsync(
        TenantId tenantId,
        int year,
        int month,
        CancellationToken cancellationToken = default)
    {
        AccountingPeriod period = AccountingPeriod.FromDateTime(new DateTime(year, month, 1));

        List<JournalEntry> before = await db.JournalEntries
            .Where(e => e.TenantId == tenantId && e.EntryDate < period.StartDate)
            .ToListAsync(cancellationToken);

        List<JournalEntry> inPeriod = await db.JournalEntries
            .Where(e => e.TenantId == tenantId
                && e.EntryDate >= period.StartDate
                && e.EntryDate <= period.EndDate)
            .ToListAsync(cancellationToken);

        Dictionary<string, decimal> opening = SumByAccount(before);
        Dictionary<string, (decimal Debit, decimal Credit)> movements = SumMovementsByAccount(inPeriod);

        List<string> accountCodes = opening.Keys.Union(movements.Keys).OrderBy(c => c).ToList();

        var result = new List<AccountBalanceDto>(accountCodes.Count);
        foreach (string code in accountCodes)
        {
            opening.TryGetValue(code, out decimal op);
            movements.TryGetValue(code, out var mv);
            result.Add(new AccountBalanceDto(
                code,
                await GetAccountNameAsync(code, cancellationToken),
                op,
                mv.Debit,
                mv.Credit,
                op + mv.Debit - mv.Credit));
        }

        return result;
    }

    public async Task<IReadOnlyList<AccountActivityDto>> GetAccountsAsync(
        TenantId tenantId,
        int year,
        int month,
        CancellationToken cancellationToken = default)
    {
        AccountingPeriod period = AccountingPeriod.FromDateTime(new DateTime(year, month, 1));

        List<JournalEntry> inPeriod = await db.JournalEntries
            .Where(e => e.TenantId == tenantId
                && e.EntryDate >= period.StartDate
                && e.EntryDate <= period.EndDate)
            .ToListAsync(cancellationToken);

        var groups = inPeriod
            .SelectMany(e => e.Lines)
            .GroupBy(l => l.AccountNumber)
            .OrderBy(g => g.Key)
            .ToList();

        var result = new List<AccountActivityDto>(groups.Count);
        foreach (var group in groups)
        {
            result.Add(new AccountActivityDto(
                group.Key,
                await GetAccountNameAsync(group.Key, cancellationToken),
                group.Sum(l => l.DebitAmount),
                group.Sum(l => l.CreditAmount)));
        }

        return result;
    }

    private static JournalEntryDto MapJournalEntry(JournalEntry entry) => new(
        entry.Id,
        entry.JournalNo,
        entry.EntryDate,
        entry.Description,
        entry.ReferenceType,
        entry.IsReversal,
        entry.ReversedJournalId?.Value,
        entry.Lines.Select(l => new JournalEntryLineDto(
            l.AccountNumber,
            l.DebitAmount,
            l.CreditAmount,
            l.Description)).ToList());

    private static Dictionary<string, decimal> SumByAccount(IEnumerable<JournalEntry> entries) =>
        entries.SelectMany(e => e.Lines)
            .GroupBy(l => l.AccountNumber)
            .ToDictionary(g => g.Key, g => g.Sum(l => l.DebitAmount - l.CreditAmount));

    private static Dictionary<string, (decimal Debit, decimal Credit)> SumMovementsByAccount(IEnumerable<JournalEntry> entries) =>
        entries.SelectMany(e => e.Lines)
            .GroupBy(l => l.AccountNumber)
            .ToDictionary(g => g.Key, g => (g.Sum(l => l.DebitAmount), g.Sum(l => l.CreditAmount)));

    private async Task<string?> GetAccountNameAsync(string accountCode, CancellationToken cancellationToken)
    {
        try
        {
            var entity = await db.AccountCharts
                .AsNoTracking()
                .FirstOrDefaultAsync(a => a.AccountCode == accountCode, cancellationToken);
            return entity?.AccountName;
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "Account name lookup failed for {AccountCode}", accountCode);
            return null;
        }
    }
}