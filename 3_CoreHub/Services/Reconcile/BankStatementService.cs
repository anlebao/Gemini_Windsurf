using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using VanAn.CoreHub.Infrastructure;
using VanAn.CoreHub.Infrastructure.Entities;
using VanAn.Shared.Domain;

namespace VanAn.CoreHub.Services.Reconcile
{
    /// <summary>Dòng sao kê đã parse (CSV) — trước khi persist.</summary>
    public sealed record BankStatementCsvRow(DateTime TxnDate, decimal Amount, string? Reference, string? Description);

    /// <summary>Báo cáo đối chiếu: matched cặp / unmatched sao kê / unmatched JE lines.</summary>
    public sealed record BankReconcileReport(
        int TotalStatementLines,
        int MatchedCount,
        IReadOnlyList<Guid> MatchedStatementLineIds,
        IReadOnlyList<BankStatementLineEntity> UnmatchedStatementLines,
        IReadOnlyList<int> UnmatchedJournalLineIds);

    /// <summary>
    /// SRS v1.2 W5/F (FR-RECNC-001): đối chiếu sao kê ngân hàng — import CSV per BankAccount,
    /// match vs JournalEntryLine trên TK 112x theo Amount + Date±3d (+ref khi có).
    /// Scope tối thiểu: import + match report — KHÔNG auto-post entry.
    /// </summary>
    public interface IBankStatementService
    {
        /// <summary>Parse CSV lines "Date,Amount,Ref,Desc" (dd/MM/yyyy hoặc yyyy-MM-dd; amount hỗ trợ dấu . và ,).</summary>
        IReadOnlyList<BankStatementCsvRow> ParseCsv(string csv);

        /// <summary>Import + persist statement lines cho BankAccount.</summary>
        Task<int> ImportCsvAsync(TenantId tenantId, Guid bankAccountId, string csv, CancellationToken ct = default);

        /// <summary>Match statement lines chưa match vs journal lines trên TK của BankAccount trong khoảng date-range.</summary>
        Task<BankReconcileReport> ReconcileAsync(TenantId tenantId, Guid bankAccountId, CancellationToken ct = default);
    }

    public sealed class BankStatementService(
        IAccountingDbContext dbContext,
        ILogger<BankStatementService> logger) : IBankStatementService
    {
        private readonly IAccountingDbContext _dbContext = dbContext;
        private readonly ILogger<BankStatementService> _logger = logger;

        private static readonly string[] DateFormats = ["dd/MM/yyyy", "d/M/yyyy", "yyyy-MM-dd", "MM/dd/yyyy"];

        /// <inheritdoc />
        public IReadOnlyList<BankStatementCsvRow> ParseCsv(string csv)
        {
            var rows = new List<BankStatementCsvRow>();
            foreach (var raw in csv.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                // Bỏ header nếu có.
                if (raw.StartsWith("date", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }
                string[] parts = raw.Split(';');
                if (parts.Length < 2)
                {
                    parts = raw.Split(',');
                }
                if (parts.Length < 2
                    || !DateTime.TryParseExact(parts[0].Trim(), DateFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime date)
                    || !decimal.TryParse(parts[1].Trim().Replace(".", "").Replace(",", "."), NumberStyles.Number, CultureInfo.InvariantCulture, out decimal amount))
                {
                    continue; // dòng không parse được — skip (import log ở caller nếu cần)
                }
                rows.Add(new BankStatementCsvRow(date, amount,
                    parts.Length > 2 ? parts[2].Trim() : null,
                    parts.Length > 3 ? parts[3].Trim() : null));
            }
            return rows;
        }

        /// <inheritdoc />
        public async Task<int> ImportCsvAsync(TenantId tenantId, Guid bankAccountId, string csv, CancellationToken ct = default)
        {
            var account = await _dbContext.BankAccounts
                .FirstOrDefaultAsync(b => b.TenantId == tenantId && b.Id == bankAccountId, ct)
                ?? throw new KeyNotFoundException($"BankAccount {bankAccountId} not found for tenant");
            if (!account.AccountCode.StartsWith("112"))
            {
                throw new ArgumentException("Sao kê chỉ áp dụng cho tài khoản ngân hàng (112x).");
            }

            var rows = ParseCsv(csv);
            foreach (var row in rows)
            {
                _dbContext.BankStatementLines.Add(new BankStatementLineEntity(
                    tenantId, bankAccountId, row.TxnDate, row.Amount, row.Reference, row.Description));
            }
            if (rows.Count > 0)
            {
                await _dbContext.SaveChangesAsync(ct);
                _logger.LogInformation("BankStatement import: {Count} lines → account {AccountCode} ({BankAccountId})",
                    rows.Count, account.AccountCode, bankAccountId);
            }
            return rows.Count;
        }

        /// <inheritdoc />
        public async Task<BankReconcileReport> ReconcileAsync(TenantId tenantId, Guid bankAccountId, CancellationToken ct = default)
        {
            var account = await _dbContext.BankAccounts
                .FirstOrDefaultAsync(b => b.TenantId == tenantId && b.Id == bankAccountId, ct)
                ?? throw new KeyNotFoundException($"BankAccount {bankAccountId} not found for tenant");

            var statementLines = await _dbContext.BankStatementLines
                .Where(s => s.TenantId == tenantId && s.BankAccountId == bankAccountId)
                .ToListAsync(ct);

            // Journal lines trên TK ngân hàng trong khoảng [minTxn-3d, maxTxn+3d] của sao kê.
            DateTime? minDate = statementLines.Count > 0 ? statementLines.Min(s => s.TxnDate).AddDays(-3) : null;
            DateTime? maxDate = statementLines.Count > 0 ? statementLines.Max(s => s.TxnDate).AddDays(3) : null;
            var journalLines = minDate == null
                ? []
                : await _dbContext.JournalEntries
                    .AsNoTracking()
                    .Where(j => j.TenantId == tenantId && j.EntryDate >= minDate && j.EntryDate <= maxDate)
                    .SelectMany(j => j.Lines)
                    .Where(l => l.AccountNumber == account.AccountCode)
                    .ToListAsync(ct);

            var matchedStatementIds = new List<Guid>();
            var unmatchedLineIds = new HashSet<int>(journalLines.Select(l => l.Id));
            // Match theo object reference — JournalEntryLine.Id (int) có thể trùng giữa các JE.
            var usedLines = new HashSet<JournalEntryLine>();

            foreach (var stmt in statementLines.Where(s => s.MatchedJournalLineId == null))
            {
                // Match: tiền vào (+) ↔ Nợ TK bank; tiền ra (−) ↔ Có TK bank; Date ±3d.
                var candidates = journalLines
                    .Where(l => !usedLines.Contains(l)
                        && ((stmt.Amount > 0 && l.DebitAmount == stmt.Amount)
                            || (stmt.Amount < 0 && l.CreditAmount == -stmt.Amount)))
                    .ToList();
                // Ưu tiên ref match trong description nếu có.
                var hit = candidates.FirstOrDefault(l =>
                    !string.IsNullOrWhiteSpace(stmt.Reference)
                    && l.Description != null
                    && l.Description.Contains(stmt.Reference, StringComparison.OrdinalIgnoreCase))
                    ?? candidates.FirstOrDefault();
                if (hit != null)
                {
                    stmt.MarkMatched(hit.Id);
                    matchedStatementIds.Add(stmt.Id);
                    usedLines.Add(hit);
                    unmatchedLineIds.Remove(hit.Id);
                }
            }
            if (matchedStatementIds.Count > 0)
            {
                await _dbContext.SaveChangesAsync(ct);
            }

            _logger.LogInformation(
                "BankReconcile: account {AccountCode} — {Matched}/{Total} statement lines matched, {Unmatched} JE lines unmatched",
                account.AccountCode, matchedStatementIds.Count, statementLines.Count, unmatchedLineIds.Count);

            return new BankReconcileReport(
                statementLines.Count,
                matchedStatementIds.Count,
                matchedStatementIds,
                statementLines.Where(s => s.MatchedJournalLineId == null).ToList(),
                unmatchedLineIds.ToList());
        }
    }
}
