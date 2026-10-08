using Microsoft.Extensions.Logging;
using VanAn.Shared.Domain;
using VanAn.Shared.DTOs;
using VanAn.CoreHub.Repositories;
using VanAn.CoreHub.Services.Journal;
using CoreAccountingEntry = VanAn.Shared.Domain.AccountingEntry;

namespace VanAn.CoreHub.Services.CongNo
{
    /// <summary>
    /// NHẬP LIỆU & SỔ SÁCH P3 (#2 — user chốt Q2): khai báo SỐ DƯ ĐẦU KỲ cho tenant mới bắt đầu
    /// dùng phần mềm kế toán (dữ liệu cũ từ Excel).
    /// (a) Số dư các tài khoản (111/112/156/211/311/333/421...) → 1 AccountingEntry tổng (Adjustment,
    ///     "Số dư đầu kỳ") + 1 JournalEntry "OpeningBalance" (Nợ TK dư Nợ / Có TK dư Có) → BalanceSheet/B01.
    /// (b) Công nợ cũ theo đối tượng (131/331 per khách/người bán) → phiếu công nợ qua ICongNoService
    ///     (Vendor + ngày khai báo) → báo cáo công nợ Đầu kỳ + tuổi nợ đúng theo đối tượng — KHÔNG JE [G6/Q1].
    /// Ràng buộc: ΣDebit == ΣCredit (chênh → user nhập 421 bù — Q2) · TK hợp lệ (chặn 5xx/6xx/7xx/8xx —
    ///     kết quả KD không có số dư đầu kỳ) · kỳ TRƯỚC mốc bắt đầu phải Open (period-closing) ·
    ///     khai 1 lần/kỳ (chặn khai lại — sửa = đảo các bút toán cũ rồi khai lại).
    /// </summary>
    public interface IOpeningBalanceService
    {
        Task<OpeningBalanceSaveResultDto> SaveAsync(TenantId tenantId, OpeningBalanceRequestDto request, CancellationToken cancellationToken = default);
        Task<OpeningBalanceDto> GetAsync(TenantId tenantId, int year, int month, CancellationToken cancellationToken = default);
    }

    public sealed class OpeningBalanceService(
        IAccountingEntryRepository accountingEntryRepository,
        IHKDBookRepository hkdBookRepository,
        IPeriodClosingService periodClosingService,
        ICongNoService congNoService,
        ILogger<OpeningBalanceService> logger) : IOpeningBalanceService
    {
        private const string OpeningBalanceDescriptionPrefix = "Số dư đầu kỳ";

        private readonly IAccountingEntryRepository _accountingEntryRepository = accountingEntryRepository;
        private readonly IHKDBookRepository _hkdBookRepository = hkdBookRepository;
        private readonly IPeriodClosingService _periodClosingService = periodClosingService;
        private readonly ICongNoService _congNoService = congNoService;
        private readonly ILogger<OpeningBalanceService> _logger = logger;

        /// <inheritdoc />
        public async Task<OpeningBalanceSaveResultDto> SaveAsync(TenantId tenantId, OpeningBalanceRequestDto request, CancellationToken cancellationToken = default)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            if (request.Year < 2000 || request.Year > 2100 || request.Month < 1 || request.Month > 12)
            {
                throw new ArgumentException("Kỳ bắt đầu không hợp lệ.");
            }

            // TransactionDate của số dư đầu kỳ = NGÀY CUỐI kỳ TRƯỚC mốc bắt đầu → nằm ở "đầu kỳ" của báo cáo
            DateTime openingDate = new DateTime(request.Year, request.Month, 1).AddDays(-1);
            AccountingPeriod openingPeriod = new(openingDate.Year, openingDate.Month);

            // Period-closing guard: kỳ trước mốc bắt đầu phải OPEN
            PeriodClosingStatus status = await _periodClosingService.GetPeriodStatusAsync(openingPeriod, tenantId, cancellationToken);
            if (status == PeriodClosingStatus.Closed)
            {
                throw new InvalidOperationException(
                    $"Kỳ {openingPeriod.Year}/{openingPeriod.Month:D2} (trước mốc bắt đầu {request.Month:D2}/{request.Year}) đã đóng sổ — không khai được số dư đầu kỳ. Mở lại kỳ hoặc chọn mốc khác.");
            }

            // Idempotent: 1 lần/kỳ — entry tổng "Số dư đầu kỳ" đã tồn tại trong kỳ trước
            await EnsureNotAlreadyOpenedAsync(tenantId, openingPeriod, cancellationToken);

            // Validate + tách lines
            ValidateLines(request);
            var accountLines = request.Lines.Where(l => l.Debit > 0 || l.Credit > 0).ToList();
            decimal totalDebit = accountLines.Sum(l => l.Debit);
            decimal totalCredit = accountLines.Sum(l => l.Credit);

            // (a) 1 AccountingEntry tổng + 1 JournalEntry "OpeningBalance"
            Guid? openingEntryId = null;
            if (accountLines.Count > 0)
            {
                string description = $"{OpeningBalanceDescriptionPrefix} {request.Month:D2}/{request.Year}";
                CoreAccountingEntry openingEntry = CoreAccountingEntry.CreateDebt(
                    tenantId, openingPeriod, new Money(totalDebit), description,
                    accountCode: string.Empty, vendor: null, transactionDate: openingDate);
                await _accountingEntryRepository.AddAsync(openingEntry, cancellationToken);
                openingEntryId = openingEntry.Id;

                JournalEntry journalEntry = new(
                    tenantId, openingDate, description,
                    ManualEntryJournalBridge.OpeningBalanceReferenceType, openingEntry.Id);
                foreach (OpeningBalanceLineDto line in accountLines)
                {
                    if (line.Debit > 0)
                    {
                        journalEntry.AddLine(line.AccountCode, line.Debit, 0, $"{OpeningBalanceDescriptionPrefix} — {line.AccountName}");
                    }
                    if (line.Credit > 0)
                    {
                        journalEntry.AddLine(line.AccountCode, 0, line.Credit, $"{OpeningBalanceDescriptionPrefix} — {line.AccountName}");
                    }
                }
                await _hkdBookRepository.AddToBookAsync(journalEntry, AccountingBookType.S2b_HKD, cancellationToken);
            }

            // (b) Công nợ cũ theo đối tượng → phiếu 131/331 (KHÔNG JE — Q1/G6)
            int congNoCount = 0;
            foreach (OpeningBalanceCongNoLineDto line in request.CongNoLines)
            {
                if (string.IsNullOrWhiteSpace(line.DoiTuong) || line.Amount <= 0)
                {
                    throw new ArgumentException($"Công nợ cũ dòng '{line.DoiTuong}' không hợp lệ (thiếu đối tượng hoặc số tiền <= 0).");
                }

                string description = $"{OpeningBalanceDescriptionPrefix} — {line.DoiTuong.Trim()}";
                if (line.AccountCode == "131")
                {
                    _ = await _congNoService.CreateReceivableAsync(tenantId, line.Amount, line.DoiTuong.Trim(), description, openingDate, isPayment: false, cancellationToken: cancellationToken);
                }
                else if (line.AccountCode == "331")
                {
                    _ = await _congNoService.CreatePayableAsync(tenantId, line.Amount, line.DoiTuong.Trim(), description, openingDate, isPayment: false, cancellationToken: cancellationToken);
                }
                else
                {
                    throw new ArgumentException($"Công nợ cũ chỉ dùng TK 131 hoặc 331 (dòng '{line.DoiTuong}').");
                }

                congNoCount++;
            }

            _logger.LogInformation("OpeningBalance saved: tenant {TenantId}, kỳ {Month}/{Year} — {AccountLines} TK, {CongNo} công nợ",
                tenantId.Value, request.Month, request.Year, accountLines.Count, congNoCount);

            return new OpeningBalanceSaveResultDto
            {
                Year = request.Year,
                Month = request.Month,
                JournalEntryCount = accountLines.Count > 0 ? 1 : 0,
                CongNoEntryCount = congNoCount,
                Message = $"Đã lưu số dư đầu kỳ {request.Month:D2}/{request.Year}: {accountLines.Count} tài khoản + {congNoCount} đối tượng công nợ."
            };
        }

        /// <inheritdoc />
        public async Task<OpeningBalanceDto> GetAsync(TenantId tenantId, int year, int month, CancellationToken cancellationToken = default)
        {
            DateTime openingDate = new DateTime(year, month, 1).AddDays(-1);
            AccountingPeriod openingPeriod = new(openingDate.Year, openingDate.Month);

            IEnumerable<CoreAccountingEntry> periodEntries =
                await _accountingEntryRepository.GetByTenantAndTransactionDatePeriodAsync(tenantId, openingPeriod, cancellationToken);

            var dto = new OpeningBalanceDto { Year = year, Month = month, Exists = false };
            foreach (CoreAccountingEntry entry in periodEntries)
            {
                if (entry.Description.StartsWith(OpeningBalanceDescriptionPrefix, StringComparison.Ordinal))
                {
                    dto.Exists = true;
                    if (entry.Vendor == null)
                    {
                        dto.Lines.Add(new OpeningBalanceLineDto { AccountCode = entry.AccountCode ?? string.Empty, Debit = Math.Max(entry.Amount, 0), Credit = Math.Max(-entry.Amount, 0) });
                    }
                    else
                    {
                        dto.CongNoLines.Add(new OpeningBalanceCongNoLineDto { AccountCode = entry.AccountCode ?? "131", DoiTuong = entry.Vendor, Amount = Math.Abs(entry.Amount) });
                    }
                }
            }
            return dto;
        }

        private async Task EnsureNotAlreadyOpenedAsync(TenantId tenantId, AccountingPeriod openingPeriod, CancellationToken cancellationToken)
        {
            IEnumerable<CoreAccountingEntry> periodEntries =
                await _accountingEntryRepository.GetByTenantAndTransactionDatePeriodAsync(tenantId, openingPeriod, cancellationToken);
            if (periodEntries.Any(e => e.Description.StartsWith(OpeningBalanceDescriptionPrefix, StringComparison.Ordinal)))
            {
                throw new InvalidOperationException(
                    $"Kỳ {openingPeriod.Year}/{openingPeriod.Month:D2} đã khai số dư đầu kỳ. Chỉ khai 1 lần/kỳ — muốn sửa: đảo các bút toán 'Số dư đầu kỳ' cũ rồi khai lại.");
            }
        }

        private static void ValidateLines(OpeningBalanceRequestDto request)
        {
            foreach (OpeningBalanceLineDto line in request.Lines)
            {
                if (string.IsNullOrWhiteSpace(line.AccountCode) || line.AccountCode.Length != 3 || !line.AccountCode.All(char.IsDigit))
                {
                    throw new ArgumentException($"Tài khoản '{line.AccountCode}' không hợp lệ (phải 3 chữ số).");
                }

                if (line.Debit < 0 || line.Credit < 0)
                {
                    throw new ArgumentException($"Tài khoản {line.AccountCode}: số tiền không được âm.");
                }

                if (line.Debit > 0 && line.Credit > 0)
                {
                    throw new ArgumentException($"Tài khoản {line.AccountCode}: không được có cả Nợ và Có cùng lúc.");
                }

                // TK kết quả kinh doanh không có số dư đầu kỳ
                if (line.AccountCode.StartsWith("5") || line.AccountCode.StartsWith("6") ||
                    line.AccountCode.StartsWith("7") || line.AccountCode.StartsWith("8"))
                {
                    throw new ArgumentException($"Tài khoản {line.AccountCode} là TK doanh thu/chi phí — không có số dư đầu kỳ.");
                }
            }

            decimal totalDebit = request.Lines.Sum(l => l.Debit);
            decimal totalCredit = request.Lines.Sum(l => l.Credit);
            if (Math.Abs(totalDebit - totalCredit) > 0.01m)
            {
                throw new ArgumentException(
                    $"Tổng Nợ ({totalDebit:N0}) khác tổng Có ({totalCredit:N0}) — chênh {Math.Abs(totalDebit - totalCredit):N0}. Thêm dòng TK 421 (Lợi nhuận chưa phân phối) để bù (Q2).");
            }
        }
    }
}
