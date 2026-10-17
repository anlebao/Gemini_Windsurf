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
    // Tenants nằm trên VanAnDbContext (PG) — pattern AuditLogRepository (_vanAnContext = context as VanAnDbContext).
    private readonly VanAnDbContext? _vanAnContext = db as VanAnDbContext;

    // TK công nợ — KHÔNG ghi JournalEntry (quyết định [G6] Thu chi & Công nợ).
    private const string ReceivableAccount = "131";
    private const string PayableAccount = "331";
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
            // TK đối ứng: các TK khác trong cùng bút toán (phía đối ứng của dòng này).
            string? counterpart = string.Join(", ",
                entry.Lines
                    .Where(l => l.AccountNumber != accountCode)
                    .Select(l => l.AccountNumber)
                    .Distinct());
            foreach (JournalEntryLine line in entry.Lines.Where(l => l.AccountNumber == accountCode))
            {
                running += line.DebitAmount - line.CreditAmount;
                rows.Add(new LedgerRowDto(
                    entry.EntryDate,
                    entry.JournalNo,
                    entry.Description,
                    line.DebitAmount,
                    line.CreditAmount,
                    running,
                    counterpart));
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

    public async Task<QuarterTotalsDto> GetQuarterTotalsAsync(
        TenantId tenantId,
        int year,
        int month,
        string accountCode,
        CancellationToken cancellationToken = default)
    {
        AccountingPeriod period = AccountingPeriod.FromDateTime(new DateTime(year, month, 1));
        // Đầu quý: tháng 1/4/7/10 của năm.
        int quarterStartMonth = ((month - 1) / 3) * 3 + 1;
        DateTime quarterStart = new(year, quarterStartMonth, 1);

        List<JournalEntry> quarterEntries = await db.JournalEntries
            .Where(e => e.TenantId == tenantId
                && e.EntryDate >= quarterStart
                && e.EntryDate <= period.EndDate
                && e.Lines.Any(l => l.AccountNumber == accountCode))
            .ToListAsync(cancellationToken);

        decimal debit = quarterEntries.SelectMany(e => e.Lines)
            .Where(l => l.AccountNumber == accountCode)
            .Sum(l => l.DebitAmount);
        decimal credit = quarterEntries.SelectMany(e => e.Lines)
            .Where(l => l.AccountNumber == accountCode)
            .Sum(l => l.CreditAmount);

        return new QuarterTotalsDto(debit, credit);
    }

    public async Task<TenantInfoDto?> GetTenantInfoAsync(
        TenantId tenantId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (_vanAnContext == null) return null;
            // Tenants tự tham chiếu (TenantId = Id của chính nó) — IgnoreQueryFilters để tra cứu
            // theo Id, tránh phụ thuộc CurrentTenantId ambient (pattern seed HKDBookGenerationService).
            var tenant = await _vanAnContext.Tenants
                .IgnoreQueryFilters()
                .AsNoTracking()
                .FirstOrDefaultAsync(t => t.Id == tenantId, cancellationToken);
            if (tenant == null) return null;
            return new TenantInfoDto(tenant.Name, tenant.Settings?.Address);
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "Tenant info lookup failed for {TenantId}", tenantId.Value);
            return null;
        }
    }

    // ===================== SỔ CHI TIẾT (P0.2-Đ2 — Mẫu S38-DN) =====================

    public async Task<SubLedgerReportDto> GetSubLedgerAsync(
        TenantId tenantId,
        int year,
        int month,
        string accountCode,
        string? doiTuong = null,
        CancellationToken cancellationToken = default)
    {
        SubLedgerData data = accountCode is ReceivableAccount or PayableAccount
            ? await LoadCongNoSubLedgerAsync(tenantId, year, month, accountCode, doiTuong, cancellationToken)
            : await LoadJournalSubLedgerAsync(tenantId, year, month, accountCode, doiTuong, cancellationToken);

        string? accountName = await GetAccountNameAsync(accountCode, cancellationToken);
        return new SubLedgerReportDto(
            accountCode,
            accountName,
            doiTuong,
            data.OpeningBalance,
            data.Rows.Sum(r => r.Debit),
            data.Rows.Sum(r => r.Credit),
            data.ClosingBalance,
            data.Rows);
    }

    public async Task<IReadOnlyList<string>> GetSubLedgerDoiTuongAsync(
        TenantId tenantId,
        int year,
        int month,
        string accountCode,
        CancellationToken cancellationToken = default)
    {
        // Toàn bộ đối tượng có Vendor trên TK (không giới hạn kỳ — để chọn cả đối tượng chỉ còn số dư đầu kỳ).
        return await db.AccountingEntries
            .Where(e => e.TenantId == tenantId && e.AccountCode == accountCode && e.Vendor != null)
            .Select(e => e.Vendor!)
            .Distinct()
            .OrderBy(v => v)
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// Sổ chi tiết 131/331 từ AccountingEntry (phiếu công nợ — Vendor = đối tượng, Amount dấu +/−).
    /// Convention Nợ−Có: 131 Amount>0 → Nợ · 331 Amount>0 → Có (dư Có → RunningBalance âm).
    /// </summary>
    private async Task<SubLedgerData> LoadCongNoSubLedgerAsync(
        TenantId tenantId,
        int year,
        int month,
        string accountCode,
        string? doiTuong,
        CancellationToken cancellationToken)
    {
        AccountingPeriod period = AccountingPeriod.FromDateTime(new DateTime(year, month, 1));
        bool isPayable = accountCode == PayableAccount;

        List<AccountingEntry> all = await db.AccountingEntries
            .Where(e => e.TenantId == tenantId && e.AccountCode == accountCode)
            .ToListAsync(cancellationToken);

        // Vendor của entry đảo resolve về Vendor entry gốc qua chuỗi ReversalEntryId
        // (reversal không copy Vendor — cùng quy tắc CongNoService).
        Dictionary<Guid, AccountingEntry> byId = all.ToDictionary(e => e.Id);
        string? ResolveDoiTuong(AccountingEntry entry)
        {
            AccountingEntry? cur = entry;
            while (cur != null && string.IsNullOrEmpty(cur.Vendor))
            {
                cur = cur.ReversalEntryId.HasValue && byId.TryGetValue(cur.ReversalEntryId.Value, out AccountingEntry? prev)
                    ? prev
                    : null;
            }
            return cur?.Vendor;
        }

        decimal Signed(AccountingEntry e) => isPayable ? -e.Amount : e.Amount;

        decimal opening = all
            .Where(e => e.TransactionDate < period.StartDate
                && (doiTuong == null || string.Equals(ResolveDoiTuong(e), doiTuong, StringComparison.Ordinal)))
            .Sum(Signed);

        var rows = new List<SubLedgerRowDto>();
        decimal running = opening;
        foreach (AccountingEntry e in all
            .Where(e => e.TransactionDate >= period.StartDate && e.TransactionDate <= period.EndDate)
            .OrderBy(e => e.TransactionDate)
            .ThenBy(e => e.CreatedAt))
        {
            string? party = ResolveDoiTuong(e);
            if (doiTuong != null && !string.Equals(party, doiTuong, StringComparison.Ordinal)) continue;

            decimal debit = isPayable ? (e.Amount < 0 ? -e.Amount : 0m) : (e.Amount > 0 ? e.Amount : 0m);
            decimal credit = isPayable ? (e.Amount > 0 ? e.Amount : 0m) : (e.Amount < 0 ? -e.Amount : 0m);
            running += debit - credit;
            rows.Add(new SubLedgerRowDto(
                e.TransactionDate,
                e.Reference ?? string.Empty,
                e.Description,
                CounterpartAccount: null,
                party,
                debit,
                credit,
                running));
        }

        return new SubLedgerData(opening, running, rows);
    }

    /// <summary>
    /// Sổ chi tiết TK thường từ JournalEntry — cùng logic Sổ Cái + đối tượng resolve
    /// qua ReferenceId → AccountingEntry.Vendor (JE nguồn ManualEntry/OpeningBalance).
    /// </summary>
    private async Task<SubLedgerData> LoadJournalSubLedgerAsync(
        TenantId tenantId,
        int year,
        int month,
        string accountCode,
        string? doiTuong,
        CancellationToken cancellationToken)
    {
        AccountingPeriod period = AccountingPeriod.FromDateTime(new DateTime(year, month, 1));

        List<JournalEntry> before = await db.JournalEntries
            .Where(e => e.TenantId == tenantId
                && e.EntryDate < period.StartDate
                && e.Lines.Any(l => l.AccountNumber == accountCode))
            .ToListAsync(cancellationToken);

        List<JournalEntry> inPeriod = await db.JournalEntries
            .Where(e => e.TenantId == tenantId
                && e.EntryDate >= period.StartDate
                && e.EntryDate <= period.EndDate
                && e.Lines.Any(l => l.AccountNumber == accountCode))
            .OrderBy(e => e.EntryDate)
            .ThenBy(e => e.CreatedAt)
            .ToListAsync(cancellationToken);

        List<Guid> refIds = before.Concat(inPeriod)
            .Where(e => e.ReferenceId.HasValue)
            .Select(e => e.ReferenceId!.Value)
            .Distinct()
            .ToList();
        Dictionary<Guid, string?> vendors = refIds.Count == 0
            ? new Dictionary<Guid, string?>()
            : await db.AccountingEntries
                .Where(e => e.TenantId == tenantId && refIds.Contains(e.Id))
                .ToDictionaryAsync(e => e.Id, e => e.Vendor, cancellationToken);

        string? ResolveDoiTuong(JournalEntry entry) =>
            entry.ReferenceId.HasValue && vendors.TryGetValue(entry.ReferenceId.Value, out string? vendor)
                ? vendor
                : null;

        decimal opening = 0m;
        foreach (JournalEntry entry in before)
        {
            if (doiTuong != null && !string.Equals(ResolveDoiTuong(entry), doiTuong, StringComparison.Ordinal)) continue;
            opening += entry.Lines.Where(l => l.AccountNumber == accountCode).Sum(l => l.DebitAmount - l.CreditAmount);
        }

        var rows = new List<SubLedgerRowDto>();
        decimal running = opening;
        foreach (JournalEntry entry in inPeriod)
        {
            string? party = ResolveDoiTuong(entry);
            if (doiTuong != null && !string.Equals(party, doiTuong, StringComparison.Ordinal)) continue;
            string? counterpart = string.Join(", ",
                entry.Lines
                    .Where(l => l.AccountNumber != accountCode)
                    .Select(l => l.AccountNumber)
                    .Distinct());
            foreach (JournalEntryLine line in entry.Lines.Where(l => l.AccountNumber == accountCode))
            {
                running += line.DebitAmount - line.CreditAmount;
                rows.Add(new SubLedgerRowDto(
                    entry.EntryDate,
                    entry.JournalNo,
                    entry.Description,
                    counterpart,
                    party,
                    line.DebitAmount,
                    line.CreditAmount,
                    running));
            }
        }

        return new SubLedgerData(opening, running, rows);
    }

    private sealed record SubLedgerData(
        decimal OpeningBalance,
        decimal ClosingBalance,
        List<SubLedgerRowDto> Rows);

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