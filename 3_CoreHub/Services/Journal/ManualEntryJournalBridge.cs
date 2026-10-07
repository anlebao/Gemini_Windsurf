using Microsoft.Extensions.Logging;
using VanAn.Shared.Domain;
using VanAn.CoreHub.Repositories;
using CoreAccountingEntry = VanAn.Shared.Domain.AccountingEntry;

namespace VanAn.CoreHub.Services.Journal
{
    /// <summary>
    /// NHẬP LIỆU & SỔ SÁCH P2 (#4 — user chốt Q1/Q5): phiếu nhập tay → JournalEntry
    /// để vào Sổ HKD B01-B09 + Báo cáo tài chính (DN) (các báo cáo này đọc JournalEntry —
    /// trước đây phiếu tay KHÔNG vào).
    /// - Thu doanh thu (5xx/7xx): Nợ 111 / Có {TK}
    /// - Chi phí (6xx): Nợ {TK} / Có 111
    /// - Công nợ (131/331 — EntryType Adjustment): KHÔNG tạo JE [G6 — bán chịu không ghi doanh thu]
    /// - Idempotent: ReferenceType "ManualEntry"/"ManualReversal" + ReferenceId = AccountingEntry.Id
    /// - Fail-safe: lỗi KHÔNG throw (phiếu vẫn lưu — kế toán đơn ưu tiên ghi sổ đúng)
    /// Tách riêng service (rollback an toàn — revert 1 file là hết hook).
    /// </summary>
    public interface IManualEntryJournalBridge
    {
        /// <summary>Tạo JE cho phiếu tay. Trả true nếu tạo JE mới; false nếu skip (không phải thu/chi, đã có JE, hoặc lỗi fail-safe).</summary>
        Task<bool> CreateForAccountingEntryAsync(CoreAccountingEntry entry, CancellationToken cancellationToken = default);

        /// <summary>Tạo JE reversal khi đảo phiếu tay (nếu phiếu gốc CÓ JE gốc). Trả true nếu tạo mới.</summary>
        Task<bool> CreateReversalForAsync(CoreAccountingEntry originalEntry, CancellationToken cancellationToken = default);
    }

    public sealed class ManualEntryJournalBridge(
        IHKDBookRepository hkdBookRepository,
        ILogger<ManualEntryJournalBridge> logger) : IManualEntryJournalBridge
    {
        public const string ManualEntryReferenceType = "ManualEntry";
        public const string ManualReversalReferenceType = "ManualReversal";
        public const string CashAccount = "111";

        private readonly IHKDBookRepository _hkdBookRepository = hkdBookRepository;
        private readonly ILogger<ManualEntryJournalBridge> _logger = logger;

        /// <inheritdoc />
        public async Task<bool> CreateForAccountingEntryAsync(CoreAccountingEntry entry, CancellationToken cancellationToken = default)
        {
            try
            {
                if (entry.ReversalEntryId.HasValue)
                {
                    return false; // phiếu đảo — JE reversal được tạo riêng (CreateReversalForAsync)
                }

                (string Debit, string Credit)? accounts = ResolveAccounts(entry);
                if (accounts == null)
                {
                    return false; // không phải phiếu thu/chi (công nợ 131/331 / Adjustment...) — [G6]
                }

                if (await _hkdBookRepository.ExistsByReferenceAsync(entry.TenantId, ManualEntryReferenceType, entry.Id, cancellationToken))
                {
                    return false; // idempotent — JE đã tồn tại (retry an toàn)
                }

                decimal amount = Math.Abs(entry.Amount);
                JournalEntry journalEntry = new(
                    entry.TenantId,
                    entry.TransactionDate,
                    entry.Description,
                    ManualEntryReferenceType,
                    entry.Id);

                journalEntry.AddLine(accounts.Value.Debit, amount, 0, entry.Description);
                journalEntry.AddLine(accounts.Value.Credit, 0, amount, entry.Description);

                await _hkdBookRepository.AddToBookAsync(journalEntry, AccountingBookType.S2b_HKD, cancellationToken);
                return true;
            }
            catch (Exception ex)
            {
                // Fail-safe: phiếu vẫn lưu — lỗi JE không chặn kế toán đơn
                _logger.LogWarning(ex,
                    "ManualEntryJournalBridge: lỗi tạo JournalEntry cho phiếu {EntryId} (tenant {TenantId}) — bỏ qua (fail-safe)",
                    entry.Id, entry.TenantId.Value);
                return false;
            }
        }

        /// <inheritdoc />
        public async Task<bool> CreateReversalForAsync(CoreAccountingEntry originalEntry, CancellationToken cancellationToken = default)
        {
            try
            {
                JournalEntry? originalJournalEntry = await _hkdBookRepository.GetByReferenceAsync(
                    originalEntry.TenantId, ManualEntryReferenceType, originalEntry.Id, cancellationToken);
                if (originalJournalEntry == null)
                {
                    return false; // phiếu không có JE gốc (công nợ / phiếu cũ trước P2) — không cần JE reversal
                }

                if (await _hkdBookRepository.ExistsByReferenceAsync(originalEntry.TenantId, ManualReversalReferenceType, originalEntry.Id, cancellationToken))
                {
                    return false; // idempotent
                }

                JournalEntry reversalJournalEntry = new(
                    originalEntry.TenantId,
                    originalEntry.TransactionDate,
                    $"Reversal of: {originalEntry.Description}",
                    ManualReversalReferenceType,
                    originalEntry.Id,
                    isReversal: true,
                    reversedJournalId: originalJournalEntry.JournalEntryId);

                foreach (JournalEntryLine line in originalJournalEntry.Lines)
                {
                    reversalJournalEntry.AddLine(line.AccountNumber, line.CreditAmount, line.DebitAmount, $"Reversal of: {line.Description}");
                }

                await _hkdBookRepository.AddToBookAsync(reversalJournalEntry, AccountingBookType.S2b_HKD, cancellationToken);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "ManualEntryJournalBridge: lỗi tạo JournalEntry reversal cho phiếu {EntryId} — bỏ qua (fail-safe)",
                    originalEntry.Id);
                return false;
            }
        }

        /// <summary>
        /// Xác định cặp Nợ/Có cho phiếu tay. Trả null = không phải phiếu thu doanh thu / chi phí
        /// (công nợ 131/331, Adjustment...) — KHÔNG tạo JE [G6].
        /// </summary>
        private static (string Debit, string Credit)? ResolveAccounts(CoreAccountingEntry entry)
        {
            string? accountCode = entry.AccountCode;
            if (string.IsNullOrWhiteSpace(accountCode))
            {
                return null;
            }

            if (entry.EntryType == AccountingEntryType.Revenue && (accountCode.StartsWith("5") || accountCode.StartsWith("7")))
            {
                return (CashAccount, accountCode); // Thu doanh thu: Nợ 111 / Có 5xx/7xx
            }

            if (entry.EntryType == AccountingEntryType.Expense && accountCode.StartsWith("6"))
            {
                return (accountCode, CashAccount); // Chi phí: Nợ 6xx / Có 111
            }

            return null;
        }
    }
}
