using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using VanAn.Shared.Domain;
using VanAn.Shared.Domain.Audit;
using VanAn.CoreHub.Repositories;

namespace VanAn.CoreHub.Services.Journal
{
    /// <summary>Một dòng định khoản trên phiếu hạch toán (TK + Nợ xor Có + diễn giải dòng).</summary>
    public sealed record JournalVoucherLine(string AccountNumber, decimal Debit, decimal Credit, string? Description = null);

    /// <summary>Kết quả tạo phiếu hạch toán — VoucherId = ReferenceId của JE (định danh chứng từ).</summary>
    public sealed record JournalVoucherResult(Guid JournalEntryId, Guid VoucherId, string? VoucherNumber, int LineCount);

    /// <summary>
    /// Task card 2026-11-01 (P2 — SRS v1.2 Tầng 3): Phiếu hạch toán / chứng từ ghi sổ (CTGS) —
    /// bút toán tay multi-line cho nghiệp vụ phức tạp không đi qua 2 form thu/chi.
    /// Tạo JournalEntry TRỰC TIẾP (ReferenceType "JournalVoucher", PostingPurpose "other",
    /// voucher CTGS-*) — KHÔNG qua AccountingEntry → hiện trong Nhật ký chung + Sổ Cái
    /// (không hiện TransactionHistory — đúng bản chất chứng từ ghi sổ).
    /// Không entity/bảng/migration mới (Gate 5 — dùng JournalEntry + VoucherSequences có sẵn).
    /// </summary>
    public interface IManualJournalVoucherService
    {
        /// <summary>Tạo phiếu hạch toán + ghi JournalEntry. Throw ValidationException khi dữ liệu sai; InvalidOperationException khi kỳ đã đóng.</summary>
        Task<JournalVoucherResult> CreateAsync(
            TenantId tenantId,
            DateTime entryDate,
            string description,
            IReadOnlyList<JournalVoucherLine> lines,
            CancellationToken cancellationToken = default);
    }

    public sealed class ManualJournalVoucherService(
        IHKDBookRepository hkdBookRepository,
        IPeriodClosingService periodClosingService,
        IAuditTrailService auditTrailService,
        ILogger<ManualJournalVoucherService> logger,
        IVoucherNumberService? voucherNumberService = null) : IManualJournalVoucherService
    {
        public const string JournalVoucherReferenceType = "JournalVoucher";

        private static readonly Regex AccountCodeRegex = new(@"^[1-9]\d{0,3}$", RegexOptions.Compiled);

        private readonly IHKDBookRepository _hkdBookRepository = hkdBookRepository;
        private readonly IPeriodClosingService _periodClosingService = periodClosingService;
        private readonly IAuditTrailService _auditTrailService = auditTrailService;
        private readonly ILogger<ManualJournalVoucherService> _logger = logger;
        private readonly IVoucherNumberService? _voucherNumberService = voucherNumberService;

        /// <inheritdoc />
        public async Task<JournalVoucherResult> CreateAsync(
            TenantId tenantId,
            DateTime entryDate,
            string description,
            IReadOnlyList<JournalVoucherLine> lines,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(description))
            {
                throw new ValidationException("Diễn giải phiếu hạch toán là bắt buộc.");
            }

            ArgumentNullException.ThrowIfNull(lines);
            if (lines.Count < 2)
            {
                throw new ValidationException("Phiếu hạch toán phải có ít nhất 2 dòng định khoản.");
            }

            decimal totalDebit = 0m;
            decimal totalCredit = 0m;
            foreach (JournalVoucherLine line in lines)
            {
                if (string.IsNullOrWhiteSpace(line.AccountNumber) || !AccountCodeRegex.IsMatch(line.AccountNumber))
                {
                    throw new ValidationException($"Tài khoản '{line.AccountNumber}' không hợp lệ (1-4 chữ số, bắt đầu bằng 1-9).");
                }
                if (line.Debit < 0 || line.Credit < 0)
                {
                    throw new ValidationException($"Dòng TK {line.AccountNumber}: số tiền không được âm.");
                }
                if (line.Debit > 0 && line.Credit > 0)
                {
                    throw new ValidationException($"Dòng TK {line.AccountNumber}: không được ghi đồng thời Nợ và Có.");
                }
                if (line.Debit == 0 && line.Credit == 0)
                {
                    throw new ValidationException($"Dòng TK {line.AccountNumber}: phải ghi Nợ hoặc Có (> 0).");
                }
                totalDebit += line.Debit;
                totalCredit += line.Credit;
            }

            if (totalDebit != totalCredit)
            {
                throw new ValidationException($"Bút toán mất cân đối: ΣNợ {totalDebit:N0} != ΣCó {totalCredit:N0}.");
            }

            // Period-closing guard (nhất quán CongNoService/AccountingEntryService —
            // period suy ra từ ngày nghiệp vụ user nhập).
            AccountingPeriod period = new(entryDate.Year, entryDate.Month);
            PeriodClosingStatus periodStatus = await _periodClosingService.GetPeriodStatusAsync(period, tenantId, cancellationToken);
            if (periodStatus == PeriodClosingStatus.Closed)
            {
                throw new InvalidOperationException($"Kỳ kế toán {period.Year}/{period.Month:D2} đã đóng sổ. Không thể thêm bút toán mới.");
            }

            // Số chứng từ CTGS-YYYY-NNNNN — fail-safe (phiếu vẫn ghi sổ nếu lỗi cấp số).
            string? voucherNumber = await NextVoucherSafeAsync(tenantId, entryDate.Year);

            Guid voucherId = Guid.NewGuid();
            JournalEntry journalEntry = new(
                tenantId,
                entryDate,
                description.Trim(),
                JournalVoucherReferenceType,
                voucherId,
                postingPurpose: AccountingPostingPurposes.Other);
            foreach (JournalVoucherLine line in lines)
            {
                journalEntry.AddLine(line.AccountNumber, line.Debit, line.Credit, line.Description);
            }

            await _hkdBookRepository.AddToBookAsync(journalEntry, AccountingBookType.S2b_HKD, cancellationToken);

            // Audit — fail-safe ngoài (JE đã ghi sổ; lỗi audit không làm mất phiếu).
            try
            {
                var newValues = JsonSerializer.Serialize(new
                {
                    VoucherNumber = voucherNumber,
                    Description = description,
                    EntryDate = entryDate,
                    Lines = lines.Select(l => new { l.AccountNumber, l.Debit, l.Credit, l.Description }),
                    TotalDebit = totalDebit,
                    TotalCredit = totalCredit
                });
                _ = await _auditTrailService.LogCreateAsync(
                    AuditableEntityType.AccountingEntry,
                    voucherId,
                    newValues,
                    correlationId: voucherId.ToString(),
                    cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Audit log failed for journal voucher {VoucherId} — JE đã ghi sổ (fail-safe)", voucherId);
            }

            _logger.LogInformation(
                "Created journal voucher {VoucherId} ({VoucherNumber}) JE {JournalNo} tenant {TenantId}: {LineCount} dòng, Σ={Total}",
                voucherId, voucherNumber, journalEntry.JournalNo, tenantId.Value, lines.Count, totalDebit);

            return new JournalVoucherResult(journalEntry.JournalEntryId.Value, voucherId, voucherNumber, lines.Count);
        }

        /// <summary>Cấp số CTGS — fail-safe (thiếu service/lỗi → null, phiếu vẫn ghi sổ).</summary>
        private async Task<string?> NextVoucherSafeAsync(TenantId tenantId, int year)
        {
            if (_voucherNumberService == null)
            {
                return null;
            }
            try
            {
                return await _voucherNumberService.NextNumberAsync(
                    tenantId, VoucherNumberService.TypeJournalVoucher, year, cancellationToken: CancellationToken.None);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Voucher numbering failed (CTGS/{Year}, tenant {TenantId}) — phiếu ghi sổ không số", year, tenantId.Value);
                return null;
            }
        }
    }
}
