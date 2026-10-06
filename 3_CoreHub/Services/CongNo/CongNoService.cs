using System.Text.Json;
using Microsoft.Extensions.Logging;
using VanAn.Shared.Domain;
using VanAn.Shared.Domain.Audit;
using VanAn.Shared.DTOs;
using VanAn.CoreHub.Repositories;
using CoreAccountingEntry = VanAn.Shared.Domain.AccountingEntry;

namespace VanAn.CoreHub.Services.CongNo
{
    /// <summary>
    /// THU CHI & CÔNG NỢ MVP — CongNoService (SRS v1.1, P1 Services core).
    /// Phiếu công nợ = AccountingEntry (immutable, EntryType.Adjustment + BookType.CashBankBook —
    /// P1 decision #1: KHÔNG lọt vào doanh thu/chi phí [G6]; CreateRevenue không nhận vendor,
    /// CreateExpense buộc expense semantics). KHÔNG thêm entity/bảng/migration.
    ///
    /// P1 decisions (ghi tại task card Phase 1):
    ///  #1 EntryType = Adjustment + BookType = CashBankBook (factory CreateDebt — additive Domain).
    ///  #2 Bỏ qua CheckDuplicateEntryAsync 5' cho phiếu công nợ — nhập tay chủ ý như bút toán
    ///     sổ cái; 2 khách cùng số tiền trong 5' là hợp lệ (open item task card). Period-closing
    ///     guard vẫn giữ.
    ///  #3 FIFO tie-break: TransactionDate + CreatedAt; reversal pairs (gốc + đảo) bị loại khỏi
    ///     stream khi net = 0 (đảo = hủy bút toán gốc — scenario C), reversal-of-reversal net ≠ 0
    ///     giữ nguyên.
    ///  #4 transactionDate truyền ngày user nhập; period suy ra từ transactionDate (không param
    ///     period riêng — tránh lệch kỳ). Tuổi nợ tính tại cuối kỳ báo cáo (không phải hôm nay).
    /// </summary>
    public class CongNoService(
        IAccountingEntryRepository repository,
        IPeriodClosingService periodClosingService,
        IAuditTrailService auditTrailService,
        ILogger<CongNoService> logger) : ICongNoService
    {
        private const string ReceivableCode = "131";
        private const string PayableCode = "331";
        private const string ReceivableName = "Phải thu khách hàng";
        private const string PayableName = "Phải trả người bán";

        private readonly IAccountingEntryRepository _repository = repository;
        private readonly IPeriodClosingService _periodClosingService = periodClosingService;
        private readonly IAuditTrailService _auditTrailService = auditTrailService;
        private readonly ILogger<CongNoService> _logger = logger;

        // ===================== CREATE PHIẾU CÔNG NỢ =====================

        public Task<AccountingEntryDto> CreateReceivableAsync(
            TenantId tenantId, decimal amount, string doiTuong, string? description,
            DateTime transactionDate, bool isPayment, string? mst = null, string? reference = null,
            CancellationToken cancellationToken = default)
            => CreateCongNoEntryAsync(tenantId, ReceivableCode, amount, doiTuong, description, transactionDate, isPayment, mst, reference, cancellationToken);

        public Task<AccountingEntryDto> CreatePayableAsync(
            TenantId tenantId, decimal amount, string doiTuong, string? description,
            DateTime transactionDate, bool isPayment, string? mst = null, string? reference = null,
            CancellationToken cancellationToken = default)
            => CreateCongNoEntryAsync(tenantId, PayableCode, amount, doiTuong, description, transactionDate, isPayment, mst, reference, cancellationToken);

        private async Task<AccountingEntryDto> CreateCongNoEntryAsync(
            TenantId tenantId,
            string accountCode,
            decimal amount,
            string doiTuong,
            string? description,
            DateTime transactionDate,
            bool isPayment,
            string? mst,
            string? reference,
            CancellationToken cancellationToken)
        {
            if (amount <= 0)
                throw new ArgumentException("Số tiền phải lớn hơn 0.", nameof(amount));

            if (string.IsNullOrWhiteSpace(doiTuong))
                throw new ArgumentException("Đối tượng công nợ là bắt buộc.", nameof(doiTuong));

            // P1 decision #4: period suy ra từ transactionDate (ngày user nhập — không UtcNow).
            AccountingPeriod period = new(transactionDate.Year, transactionDate.Month);

            // Period-closing guard (P1 decision #2: giữ guard, bỏ duplicate-check 5').
            PeriodClosingStatus periodStatus = await _periodClosingService.GetPeriodStatusAsync(period, tenantId);
            if (periodStatus == PeriodClosingStatus.Closed)
                throw new InvalidOperationException($"Kỳ kế toán {period.Year}/{period.Month:D2} đã đóng sổ. Không thể thêm bút toán mới.");

            string desc = string.IsNullOrWhiteSpace(description)
                ? BuildDefaultDescription(accountCode, doiTuong, isPayment, mst)
                : description;

            // Dấu: dương = tăng công nợ (ghi nhận nợ), âm = giảm công nợ (thu/trả nợ) [G9].
            decimal signed = isPayment ? -Math.Abs(amount) : Math.Abs(amount);

            CoreAccountingEntry entry = CoreAccountingEntry.CreateDebt(
                tenantId, period, new Money(signed), desc,
                accountCode: accountCode, vendor: doiTuong, reference: reference, transactionDate: transactionDate);

            await _repository.AddAsync(entry, cancellationToken);

            var newValues = JsonSerializer.Serialize(new
            {
                Amount = signed,
                Description = desc,
                AccountCode = accountCode,
                Vendor = doiTuong,
                Period = period.ToString(),
                IsPayment = isPayment,
                Mst = mst
            });
            await _auditTrailService.LogCreateAsync(
                AuditableEntityType.AccountingEntry,
                entry.Id,
                newValues,
                correlationId: entry.Id.ToString());

            _logger.LogInformation("Created cong-no entry {EntryId} for tenant {TenantId}: {AccountCode} {Vendor} {Signed}",
                entry.Id, tenantId.Value, accountCode, doiTuong, signed);

            return ToDto(entry);
        }

        private static string BuildDefaultDescription(string accountCode, string doiTuong, bool isPayment, string? mst)
        {
            // [G11]: MST ghi kèm diễn giải để truy vết.
            string mstSuffix = string.IsNullOrWhiteSpace(mst) ? string.Empty : $" (MST {mst.Trim()})";
            string prefix = accountCode switch
            {
                ReceivableCode => isPayment ? "Thu tiền khách trả nợ" : "Bán chịu",
                PayableCode => isPayment ? "Trả tiền người bán" : "Mua chịu",
                _ => isPayment ? "Trả nợ" : "Ghi nhận nợ"
            };
            return $"{prefix} — {doiTuong}{mstSuffix}";
        }

        // ===================== BÁO CÁO TỔNG HỢP (FR-7) =====================

        public async Task<CongNoReportDto> GetCongNoReportAsync(TenantId tenantId, int year, int month, CancellationToken cancellationToken = default)
        {
            if (year < 2000 || year > 2100)
                throw new ArgumentOutOfRangeException(nameof(year), "Năm không hợp lệ.");
            if (month < 1 || month > 12)
                throw new ArgumentOutOfRangeException(nameof(month), "Tháng không hợp lệ.");

            DateTime periodStart = new(year, month, 1);
            DateTime periodEnd = periodStart.AddMonths(1);
            DateTime asOfDate = periodEnd.AddDays(-1); // tuổi nợ tính tại cuối kỳ (P1 decision #4)

            List<DebtStreamEntry> stream = await BuildDebtStreamAsync(tenantId, [ReceivableCode, PayableCode], cancellationToken);

            var report = new CongNoReportDto
            {
                Year = year,
                Month = month,
                ReportDate = asOfDate
            };

            report.Groups.Add(BuildGroup(ReceivableCode, ReceivableName, stream, periodStart, periodEnd, asOfDate));
            report.Groups.Add(BuildGroup(PayableCode, PayableName, stream, periodStart, periodEnd, asOfDate));

            return report;
        }

        private static CongNoGroupDto BuildGroup(
            string accountCode,
            string accountName,
            List<DebtStreamEntry> stream,
            DateTime periodStart,
            DateTime periodEnd,
            DateTime asOfDate)
        {
            var group = new CongNoGroupDto { AccountCode = accountCode, AccountName = accountName };

            var byVendor = stream
                .Where(e => e.AccountCode == accountCode)
                .GroupBy(e => e.Vendor, StringComparer.Ordinal)
                .OrderBy(g => g.Key, StringComparer.Ordinal)
                .ToList();

            foreach (IGrouping<string, DebtStreamEntry> vendorGroup in byVendor)
            {
                List<DebtStreamEntry> ordered = vendorGroup.OrderBy(e => e.TransactionDate).ThenBy(e => e.CreatedAt).ToList();

                decimal dauKy = ordered.Where(e => e.TransactionDate < periodStart).Sum(e => e.Amount);
                decimal phatSinh = ordered.Where(e => e.TransactionDate >= periodStart && e.TransactionDate < periodEnd && e.Amount > 0).Sum(e => e.Amount);
                decimal daThuTra = Math.Abs(ordered.Where(e => e.TransactionDate >= periodStart && e.TransactionDate < periodEnd && e.Amount < 0).Sum(e => e.Amount));
                decimal cuoiKy = dauKy + phatSinh - daThuTra;

                if (dauKy == 0 && phatSinh == 0 && daThuTra == 0 && cuoiKy == 0)
                    continue; // đối tượng không có hoạt động trong kỳ

                group.Rows.Add(new CongNoRowDto
                {
                    DoiTuong = vendorGroup.Key,
                    DauKy = dauKy,
                    PhatSinh = phatSinh,
                    DaThuTra = daThuTra,
                    CuoiKy = cuoiKy,
                    Aging = ComputeAging(ordered, asOfDate)
                });

                group.TongDauKy += dauKy;
                group.TongPhatSinh += phatSinh;
                group.TongDaThuTra += daThuTra;
                group.TongCuoiKy += cuoiKy;
            }

            group.TongAging = SumAging(group.Rows.Select(r => r.Aging).ToList());
            return group;
        }

        private static CongNoAgingDto SumAging(List<CongNoAgingDto> agings)
        {
            var total = new CongNoAgingDto();
            foreach (CongNoAgingDto a in agings)
            {
                total.Duoi30 += a.Duoi30;
                total.Tu30Den60 += a.Tu30Den60;
                total.Tu60Den90 += a.Tu60Den90;
                total.Tren90 += a.Tren90;
            }
            return total;
        }

        // ===================== SỔ ĐỐI TƯỢNG (FR-8) =====================

        public async Task<DoiTuongLedgerDto> GetDoiTuongLedgerAsync(
            TenantId tenantId, string accountCode, string doiTuong, int year, int month, CancellationToken cancellationToken = default)
        {
            if (accountCode != ReceivableCode && accountCode != PayableCode)
                throw new ArgumentException("AccountCode phải là 131 hoặc 331.", nameof(accountCode));
            if (string.IsNullOrWhiteSpace(doiTuong))
                throw new ArgumentException("Đối tượng công nợ là bắt buộc.", nameof(doiTuong));
            if (month < 1 || month > 12)
                throw new ArgumentOutOfRangeException(nameof(month), "Tháng không hợp lệ.");

            DateTime periodStart = new(year, month, 1);
            DateTime periodEnd = periodStart.AddMonths(1);

            // Sổ chi tiết hiển thị entry GỐC (kể cả cặp đảo bút toán — audit-friendly),
            // vendor của entry đảo resolve về vendor entry gốc (reversal không copy Vendor).
            List<LedgerViewEntry> all = await BuildLedgerViewAsync(tenantId, [accountCode], cancellationToken);

            List<LedgerViewEntry> vendorEntries = all
                .Where(e => e.AccountCode == accountCode && string.Equals(e.Vendor, doiTuong, StringComparison.Ordinal))
                .OrderBy(e => e.TransactionDate)
                .ThenBy(e => e.CreatedAt)
                .ToList();

            var dto = new DoiTuongLedgerDto
            {
                AccountCode = accountCode,
                DoiTuong = doiTuong,
                Year = year,
                Month = month,
                DauKy = vendorEntries.Where(e => e.TransactionDate < periodStart).Sum(e => e.Amount)
            };

            decimal running = dto.DauKy;
            foreach (LedgerViewEntry entry in vendorEntries.Where(e => e.TransactionDate >= periodStart && e.TransactionDate < periodEnd))
            {
                running += entry.Amount;
                dto.Lines.Add(new DoiTuongLedgerLineDto
                {
                    EntryId = entry.EntryId,
                    TransactionDate = entry.TransactionDate,
                    Description = entry.Description,
                    Tang = entry.Amount > 0 ? entry.Amount : 0m,
                    Giam = entry.Amount < 0 ? -entry.Amount : 0m,
                    SoDu = running,
                    IsReversal = entry.IsReversal
                });
            }

            dto.CuoiKy = running;
            return dto;
        }

        // ===================== LỊCH SỬ THANH TOÁN KHOẢN NỢ (FR-8.1) =====================

        public async Task<KhoanNoPaymentsDto?> GetKhoanNoPaymentsAsync(TenantId tenantId, Guid khoanNoEntryId, CancellationToken cancellationToken = default)
        {
            List<DebtStreamEntry> stream = await BuildDebtStreamAsync(tenantId, [ReceivableCode, PayableCode], cancellationToken);

            DebtStreamEntry? khoan = stream.FirstOrDefault(e => e.EntryId == khoanNoEntryId && e.Amount > 0);
            if (khoan == null)
                return null; // không tồn tại / đã bị đảo (net = 0) / không phải khoản ghi nợ

            List<DebtStreamEntry> groupEntries = stream
                .Where(e => e.AccountCode == khoan.AccountCode && string.Equals(e.Vendor, khoan.Vendor, StringComparison.Ordinal))
                .OrderBy(e => e.TransactionDate)
                .ThenBy(e => e.CreatedAt)
                .ToList();

            // FIFO động: thanh toán trừ vào khoản phát sinh cũ nhất còn dư [G12].
            var khoanItems = new List<KhoanNoFifoItem>();
            foreach (DebtStreamEntry entry in groupEntries)
            {
                if (entry.Amount > 0)
                {
                    khoanItems.Add(new KhoanNoFifoItem(entry, entry.Amount));
                    continue;
                }

                decimal toAllocate = -entry.Amount;
                foreach (KhoanNoFifoItem item in khoanItems)
                {
                    if (toAllocate <= 0)
                        break;
                    decimal take = Math.Min(item.Remaining, toAllocate);
                    if (take <= 0)
                        continue;
                    item.Payments.Add((entry, take));
                    item.Remaining -= take;
                    toAllocate -= take;
                }
                // toAllocate > 0 → trả trước (âm dư) — G9 cho phép, không chặn
            }

            KhoanNoFifoItem target = khoanItems.First(i => i.EntryId == khoanNoEntryId);
            decimal daThanhToan = target.Payments.Sum(p => p.Amount);

            var dto = new KhoanNoPaymentsDto
            {
                KhoanNoEntryId = khoan.EntryId,
                AccountCode = khoan.AccountCode,
                DoiTuong = khoan.Vendor,
                TransactionDate = khoan.TransactionDate,
                Description = khoan.Description,
                SoTien = khoan.Amount,
                DaThanhToan = daThanhToan,
                ConLai = khoan.Amount - daThanhToan
            };

            decimal remaining = khoan.Amount;
            foreach ((DebtStreamEntry payment, decimal take) in target.Payments)
            {
                remaining -= take;
                dto.Payments.Add(new KhoanNoPaymentLineDto
                {
                    EntryId = payment.EntryId,
                    TransactionDate = payment.TransactionDate,
                    Description = payment.Description,
                    SoTien = take,
                    SoDuConLai = remaining
                });
            }

            return dto;
        }

        // ===================== FIFO ENGINE (dùng chung) =====================

        /// <summary>
        /// Tuổi nợ FIFO động [G12]: thanh toán trừ vào khoản cũ nhất còn dư, tại asOfDate (cuối kỳ).
        /// Nhóm <30 · 30-60 · 60-90 · &gt;90 ngày (age = asOfDate − TransactionDate).
        /// </summary>
        private static CongNoAgingDto ComputeAging(List<DebtStreamEntry> entries, DateTime asOfDate)
        {
            var khoanItems = new List<KhoanNoFifoItem>();
            foreach (DebtStreamEntry entry in entries)
            {
                if (entry.Amount > 0)
                {
                    khoanItems.Add(new KhoanNoFifoItem(entry, entry.Amount));
                    continue;
                }

                decimal toAllocate = -entry.Amount;
                foreach (KhoanNoFifoItem item in khoanItems)
                {
                    if (toAllocate <= 0)
                        break;
                    decimal take = Math.Min(item.Remaining, toAllocate);
                    if (take <= 0)
                        continue;
                    item.Payments.Add((entry, take));
                    item.Remaining -= take;
                    toAllocate -= take;
                }
            }

            var aging = new CongNoAgingDto();
            foreach (KhoanNoFifoItem item in khoanItems)
            {
                if (item.Remaining <= 0)
                    continue;
                int age = (asOfDate - item.TransactionDate.Date).Days;
                if (age < 0)
                    age = 0; // khoản nhập tương lai — xem như <30
                if (age < 30)
                    aging.Duoi30 += item.Remaining;
                else if (age < 60)
                    aging.Tu30Den60 += item.Remaining;
                else if (age < 90)
                    aging.Tu60Den90 += item.Remaining;
                else
                    aging.Tren90 += item.Remaining;
            }
            return aging;
        }

        /// <summary>
        /// Build stream công nợ: entries 131/331 của tenant, nhóm theo "gốc" (chuỗi reversal).
        /// Cặp (gốc + đảo) net = 0 → loại cả hai khỏi stream (P1 decision #3 — đảo = hủy bút toán);
        /// net ≠ 0 (reversal-of-reversal) → giữ 1 entry tổng hợp với ngày/diễn giải của gốc.
        /// </summary>
        private async Task<List<DebtStreamEntry>> BuildDebtStreamAsync(TenantId tenantId, string[] accountCodes, CancellationToken cancellationToken)
        {
            IEnumerable<CoreAccountingEntry> all = await _repository.GetByTenantAndAccountCodesAsync(tenantId, accountCodes, cancellationToken);
            Dictionary<Guid, CoreAccountingEntry> byId = all.ToDictionary(e => e.Id);

            // rootId → nhóm entry (gốc + mọi entry đảo tham chiếu tới gốc qua chuỗi ReversalEntryId)
            var groups = new Dictionary<Guid, List<CoreAccountingEntry>>();
            foreach (CoreAccountingEntry entry in all)
            {
                Guid rootId = ResolveRootId(entry, byId);
                if (!groups.TryGetValue(rootId, out List<CoreAccountingEntry>? list))
                {
                    list = [];
                    groups[rootId] = list;
                }
                list.Add(entry);
            }

            var stream = new List<DebtStreamEntry>();
            foreach ((Guid rootId, List<CoreAccountingEntry> group) in groups)
            {
                decimal net = group.Sum(e => e.Amount);
                if (net == 0m)
                    continue; // bị đảo trọn vẹn — không ảnh hưởng công nợ

                CoreAccountingEntry root = group.First(e => e.Id == rootId);
                stream.Add(new DebtStreamEntry
                {
                    EntryId = rootId,
                    AccountCode = root.AccountCode ?? string.Empty,
                    Vendor = root.Vendor ?? string.Empty,
                    TransactionDate = root.TransactionDate,
                    CreatedAt = root.CreatedAt,
                    Amount = net,
                    Description = root.Description
                });
            }

            return stream.OrderBy(e => e.TransactionDate).ThenBy(e => e.CreatedAt).ToList();
        }

        private static Guid ResolveRootId(CoreAccountingEntry entry, Dictionary<Guid, CoreAccountingEntry> byId)
        {
            Guid rootId = entry.Id;
            CoreAccountingEntry cursor = entry;
            int hops = 0;
            while (cursor.ReversalEntryId.HasValue && byId.TryGetValue(cursor.ReversalEntryId.Value, out CoreAccountingEntry? parent) && hops < 16)
            {
                rootId = parent.Id;
                cursor = parent;
                hops++;
            }
            return rootId;
        }

        /// <summary>
        /// Sổ chi tiết hiển thị entry gốc (kể cả cặp đảo): reversal entry resolve vendor/account
        /// về entry gốc (CreateReversal không copy Vendor).
        /// </summary>
        private async Task<List<LedgerViewEntry>> BuildLedgerViewAsync(TenantId tenantId, string[] accountCodes, CancellationToken cancellationToken)
        {
            IEnumerable<CoreAccountingEntry> all = await _repository.GetByTenantAndAccountCodesAsync(tenantId, accountCodes, cancellationToken);
            Dictionary<Guid, CoreAccountingEntry> byId = all.ToDictionary(e => e.Id);

            var result = new List<LedgerViewEntry>();
            foreach (CoreAccountingEntry entry in all)
            {
                CoreAccountingEntry display = entry;
                if (entry.ReversalEntryId.HasValue && byId.TryGetValue(entry.ReversalEntryId.Value, out CoreAccountingEntry? original))
                {
                    display = original;
                }

                result.Add(new LedgerViewEntry
                {
                    EntryId = entry.Id,
                    AccountCode = display.AccountCode ?? entry.AccountCode ?? string.Empty,
                    Vendor = display.Vendor ?? entry.Vendor ?? string.Empty,
                    TransactionDate = entry.TransactionDate,
                    CreatedAt = entry.CreatedAt,
                    Amount = entry.Amount,
                    Description = entry.Description,
                    IsReversal = entry.ReversalEntryId.HasValue
                });
            }

            return result;
        }

        private static AccountingEntryDto ToDto(CoreAccountingEntry entry)
        {
            return new AccountingEntryDto
            {
                Id = entry.Id,
                TenantId = entry.TenantId.Value,
                Amount = entry.Amount,
                Description = entry.Description,
                AccountCode = entry.AccountCode ?? string.Empty,
                Vendor = entry.Vendor,
                Reference = entry.Reference,
                EntryType = entry.EntryType,
                CreatedAt = entry.CreatedAt,
                AccountingBookType = entry.AccountingBookType,
                PeriodYear = entry.PeriodYear,
                PeriodMonth = entry.PeriodMonth,
                ReversalEntryId = entry.ReversalEntryId,
                TransactionDate = entry.TransactionDate
            };
        }

        // ===================== INTERNAL MODELS =====================

        private sealed class DebtStreamEntry
        {
            public Guid EntryId { get; init; }
            public string AccountCode { get; init; } = string.Empty;
            public string Vendor { get; init; } = string.Empty;
            public DateTime TransactionDate { get; init; }
            public DateTime CreatedAt { get; init; }
            public decimal Amount { get; init; }
            public string Description { get; init; } = string.Empty;
        }

        private sealed class LedgerViewEntry
        {
            public Guid EntryId { get; init; }
            public string AccountCode { get; init; } = string.Empty;
            public string Vendor { get; init; } = string.Empty;
            public DateTime TransactionDate { get; init; }
            public DateTime CreatedAt { get; init; }
            public decimal Amount { get; init; }
            public string Description { get; init; } = string.Empty;
            public bool IsReversal { get; init; }
        }

        private sealed class KhoanNoFifoItem(DebtStreamEntry entry, decimal remaining)
        {
            public Guid EntryId { get; } = entry.EntryId;
            public DateTime TransactionDate { get; } = entry.TransactionDate;
            public decimal Amount { get; } = entry.Amount;
            public decimal Remaining { get; set; } = remaining;
            public List<(DebtStreamEntry Payment, decimal Amount)> Payments { get; } = [];
        }
    }
}
