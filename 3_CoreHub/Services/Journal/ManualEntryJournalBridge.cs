using Microsoft.Extensions.Logging;
using VanAn.Shared.Domain;
using VanAn.CoreHub.Repositories;
using VanAn.CoreHub.Infrastructure.Entities;
using VanAn.Shared.Services;
using CoreAccountingEntry = VanAn.Shared.Domain.AccountingEntry;

namespace VanAn.CoreHub.Services.Journal
{
    /// <summary>
    /// NHẬP LIỆU & SỔ SÁCH P2 (#4 — user chốt Q1/Q5): phiếu nhập tay → JournalEntry
    /// để vào Sổ HKD B01-B09 + Báo cáo tài chính (DN) (các báo cáo này đọc JournalEntry —
    /// trước đây phiếu tay KHÔNG vào).
    /// - Thu doanh thu (5xx/7xx): Nợ 111 / Có {TK}
    /// - Chi phí (6xx): Nợ {TK} / Có 111
    /// - Công nợ (131/331 — EntryType Adjustment): KHÔNG tạo JE [G6 — HKD/DNSN cash-basis]
    /// - Idempotent: ReferenceType "ManualEntry"/"ManualReversal" + ReferenceId = AccountingEntry.Id
    /// - Fail-safe: lỗi KHÔNG throw (phiếu vẫn lưu — kế toán đơn ưu tiên ghi sổ đúng)
    /// Tách riêng service (rollback an toàn — revert 1 file là hết hook).
    ///
    /// SRS v1.2 PHASE A (2026-10-30):
    /// - PostingStatus upsert qua EntryPostingStatuses (R2 — observable thay vì silent).
    /// - ResolveAccounts async: DN (Enterprise_SME/Large) + HTX + flag "AccrualReceivableJE"
    ///   → công nợ cũng tạo JE accrual: receivable Nợ131/Có511 · thu nợ Nợ111/Có131 ·
    ///   trả nợ Nợ331/Có111 · payable-create → Skipped("needs-counter-account") [G-C4].
    ///   HKD + DNSN (Enterprise_SuperSmall) giữ cash-basis — không JE cho Adjustment.
    /// - JE.PostingPurpose = entry.PostingPurpose ?? "original" (unique-index discriminator).
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
        ILogger<ManualEntryJournalBridge> logger,
        IVasFeatureFlagService? vasFeatureFlagService = null,
        IShopFeatureSettingsService? featureSettings = null) : IManualEntryJournalBridge
    {
        public const string ManualEntryReferenceType = "ManualEntry";
        public const string ManualReversalReferenceType = "ManualReversal";
        // NHẬP LIỆU & SỔ SÁCH P3 (#2): số dư đầu kỳ — JE "OpeningBalance" + reversal tương ứng
        public const string OpeningBalanceReferenceType = "OpeningBalance";
        public const string OpeningBalanceReversalReferenceType = "OpeningBalanceReversal";
        public const string CashAccount = "111";
        public const string AccrualReceivableFlag = "AccrualReceivableJE";

        private readonly IHKDBookRepository _hkdBookRepository = hkdBookRepository;
        private readonly ILogger<ManualEntryJournalBridge> _logger = logger;
        private readonly IVasFeatureFlagService? _vasFeatureFlagService = vasFeatureFlagService;
        private readonly IShopFeatureSettingsService? _featureSettings = featureSettings;

        /// <inheritdoc />
        public async Task<bool> CreateForAccountingEntryAsync(CoreAccountingEntry entry, CancellationToken cancellationToken = default)
        {
            try
            {
                if (entry.ReversalEntryId.HasValue)
                {
                    await SetStatusAsync(entry, EntryPostingStatuses.Skipped, "reversal-entry", cancellationToken);
                    return false; // phiếu đảo — JE reversal được tạo riêng (CreateReversalForAsync)
                }

                (string Debit, string Credit)? accounts = await ResolveAccountsAsync(entry, cancellationToken);
                if (accounts == null)
                {
                    await SetStatusAsync(entry, EntryPostingStatuses.Skipped, "not-journalable", cancellationToken);
                    return false; // không phải phiếu thu/chi (công nợ chưa accrual / Adjustment...) — [G6]
                }

                if (await _hkdBookRepository.ExistsByReferenceAsync(entry.TenantId, ManualEntryReferenceType, entry.Id, cancellationToken))
                {
                    await SetStatusAsync(entry, EntryPostingStatuses.Posted, null, cancellationToken);
                    return false; // idempotent — JE đã tồn tại (retry an toàn)
                }

                decimal amount = Math.Abs(entry.Amount);
                JournalEntry journalEntry = new(
                    entry.TenantId,
                    entry.TransactionDate,
                    entry.Description,
                    ManualEntryReferenceType,
                    entry.Id,
                    postingPurpose: entry.PostingPurpose ?? AccountingPostingPurposes.Original);

                journalEntry.AddLine(accounts.Value.Debit, amount, 0, entry.Description);
                journalEntry.AddLine(accounts.Value.Credit, 0, amount, entry.Description);

                await _hkdBookRepository.AddToBookAsync(journalEntry, AccountingBookType.S2b_HKD, cancellationToken);
                await SetStatusAsync(entry, EntryPostingStatuses.Posted, null, cancellationToken);
                return true;
            }
            catch (Exception ex)
            {
                // Fail-safe: phiếu vẫn lưu — lỗi JE không chặn kế toán đơn.
                // Phase A: KHÔNG silent — ghi Failed để retry worker vá.
                _logger.LogWarning(ex,
                    "ManualEntryJournalBridge: lỗi tạo JournalEntry cho phiếu {EntryId} (tenant {TenantId}) — status=Failed, retry sẽ xử lý",
                    entry.Id, entry.TenantId.Value);
                await SafeSetStatusAsync(entry, EntryPostingStatuses.Failed, ex.Message, cancellationToken);
                return false;
            }
        }

        /// <inheritdoc />
        public async Task<bool> CreateReversalForAsync(CoreAccountingEntry originalEntry, CancellationToken cancellationToken = default)
        {
            try
            {
                // Tìm JE gốc: phiếu tay (ManualEntry) HOẶC số dư đầu kỳ (OpeningBalance — P3)
                JournalEntry? originalJournalEntry = await _hkdBookRepository.GetByReferenceAsync(
                    originalEntry.TenantId, ManualEntryReferenceType, originalEntry.Id, cancellationToken)
                    ?? await _hkdBookRepository.GetByReferenceAsync(
                    originalEntry.TenantId, OpeningBalanceReferenceType, originalEntry.Id, cancellationToken);
                if (originalJournalEntry == null)
                {
                    await SetStatusAsync(originalEntry, EntryPostingStatuses.Skipped, "no-original-je", cancellationToken);
                    return false; // phiếu không có JE gốc (công nợ / phiếu cũ trước P2) — không cần JE reversal
                }

                string reversalReferenceType = originalJournalEntry.ReferenceType == OpeningBalanceReferenceType
                    ? OpeningBalanceReversalReferenceType
                    : ManualReversalReferenceType;

                if (await _hkdBookRepository.ExistsByReferenceAsync(originalEntry.TenantId, reversalReferenceType, originalEntry.Id, cancellationToken))
                {
                    await SetStatusAsync(originalEntry, EntryPostingStatuses.Posted, null, cancellationToken);
                    return false; // idempotent
                }

                JournalEntry reversalJournalEntry = new(
                    originalEntry.TenantId,
                    originalEntry.TransactionDate,
                    $"Reversal of: {originalEntry.Description}",
                    reversalReferenceType,
                    originalEntry.Id,
                    isReversal: true,
                    reversedJournalId: originalJournalEntry.JournalEntryId,
                    postingPurpose: AccountingPostingPurposes.Reversal);

                foreach (JournalEntryLine line in originalJournalEntry.Lines)
                {
                    reversalJournalEntry.AddLine(line.AccountNumber, line.CreditAmount, line.DebitAmount, $"Reversal of: {line.Description}");
                }

                await _hkdBookRepository.AddToBookAsync(reversalJournalEntry, AccountingBookType.S2b_HKD, cancellationToken);
                await SetStatusAsync(originalEntry, EntryPostingStatuses.Posted, null, cancellationToken);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "ManualEntryJournalBridge: lỗi tạo JournalEntry reversal cho phiếu {EntryId} — status=Failed",
                    originalEntry.Id);
                await SafeSetStatusAsync(originalEntry, EntryPostingStatuses.Failed, ex.Message, cancellationToken);
                return false;
            }
        }

        /// <summary>
        /// Xác định cặp Nợ/Có cho phiếu tay. Trả null = không journalable.
        /// SRS Phase A (G-C4): Adjustment công nợ → JE accrual CHỈ khi tenant DN/HTX
        /// (KHÔNG HKD/DNSN — chế độ sổ) VÀ flag AccrualReceivableJE bật.
        /// payable-create → null (counter-account hàng mua không suy luận an toàn — §18.4.1.5).
        /// </summary>
        private async Task<(string Debit, string Credit)?> ResolveAccountsAsync(CoreAccountingEntry entry, CancellationToken cancellationToken)
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

            // Issue #195 follow-up (2026-10-29, user approve — deposit offset 3387 làm luôn):
            // cọc booking đã thu (BookingAccountingService.RecordDepositPaidAsync, TK 3387 —
            // bản chất riêng KHÔNG doanh thu) → tại thanh toán cuối, cọc chuyển thành doanh thu:
            // Nợ 3387 / Có {accountCode = TK doanh thu 511/512}. Bridge map (3387, accountCode).
            if (entry.EntryType == AccountingEntryType.DepositOffset)
            {
                return ("3387", accountCode); // Đối trừ cọc: Nợ 3387 / Có 5xx/7xx
            }

            // SRS Phase A (G-C4): DN/HTX accrual — công nợ cũng tạo JE khi flag bật.
            if (entry.EntryType == AccountingEntryType.Adjustment
                && entry.PostingPurpose is AccountingPostingPurposes.Receivable
                    or AccountingPostingPurposes.ReceivablePayment
                    or AccountingPostingPurposes.PayablePayment
                && await IsAccrualEnabledAsync(entry.TenantId, cancellationToken))
            {
                return entry.PostingPurpose switch
                {
                    AccountingPostingPurposes.Receivable => (accountCode, "511"),        // bán chịu: Nợ 131 / Có 511
                    AccountingPostingPurposes.ReceivablePayment => (CashAccount, accountCode), // thu nợ: Nợ 111 / Có 131
                    AccountingPostingPurposes.PayablePayment => (accountCode, CashAccount),    // trả nợ: Nợ 331 / Có 111
                    _ => null
                };
            }

            return null; // payable-create + non-debt Adjustment + HKD/DNSN: không JE
        }

        /// <summary>G-C4: accrual chỉ cho DN (SME TT133 / Large TT99) + HTX (TT71) + flag per-tenant.</summary>
        private async Task<bool> IsAccrualEnabledAsync(TenantId tenantId, CancellationToken cancellationToken)
        {
            if (_vasFeatureFlagService == null || _featureSettings == null)
            {
                return false; // deps optional (tests/legacy DI) → giữ hành vi cũ
            }
            TenantType? type = await _vasFeatureFlagService.GetTenantTypeAsync(tenantId, cancellationToken);
            if (type is null or TenantType.HKD or TenantType.Enterprise_SuperSmall)
            {
                return false; // HKD + DNSN = chế độ sổ (cash-basis), không accrual JE
            }
            return await _featureSettings.IsEnabledAsync(tenantId.Value, AccrualReceivableFlag, cancellationToken);
        }

        private async Task SetStatusAsync(CoreAccountingEntry entry, int status, string? reason, CancellationToken cancellationToken)
        {
            await _hkdBookRepository.UpsertPostingStatusAsync(entry.Id, entry.TenantId, status, reason, incrementAttempts: status == EntryPostingStatuses.Failed, cancellationToken);
        }

        /// <summary>Status write trong catch path — tự fail-safe (lỗi status không nuốt lỗi gốc).</summary>
        private async Task SafeSetStatusAsync(CoreAccountingEntry entry, int status, string? reason, CancellationToken cancellationToken)
        {
            try { await SetStatusAsync(entry, status, reason, cancellationToken); }
            catch (Exception ex2) { _logger.LogWarning(ex2, "PostingStatus write failed for entry {EntryId}", entry.Id); }
        }
    }
}
