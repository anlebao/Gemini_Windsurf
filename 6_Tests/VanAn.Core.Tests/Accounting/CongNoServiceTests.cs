using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using VanAn.Shared.Domain;
using VanAn.Shared.DTOs;
using VanAn.CoreHub.Repositories;
using VanAn.CoreHub.Services;
using VanAn.CoreHub.Services.CongNo;
using VanAn.CoreHub.Services.Template;
using CoreAccountingEntry = VanAn.Shared.Domain.AccountingEntry;
using Xunit;
using FluentAssertions;

namespace VanAn.Core.Tests.Accounting
{
    /// <summary>
    /// THU CHI & CÔNG NỢ MVP — P1.4 Tests (task card Phase 1, SRS v1.1 §7 kịch bản A/B/C/D + D2):
    /// A bán chịu + thu nợ · B mua chịu + trả nợ · C reversal · D tuổi nợ FIFO · D2 lịch sử thanh
    /// toán khoản · isolation tenant · [G6] phiếu công nợ không lọt vào doanh thu/chi phí.
    /// </summary>
    public class CongNoServiceTests
    {
        private readonly TenantId _tenantA = new(Guid.NewGuid());
        private readonly TenantId _tenantB = new(Guid.NewGuid());

        private readonly Mock<IAccountingEntryRepository> _repo = new();
        private readonly Mock<IPeriodClosingService> _periodClosing = new();
        private readonly List<CoreAccountingEntry> _entries = [];

        private readonly CongNoService _service;

        public CongNoServiceTests()
        {
            // P1 decision #2: phiếu công nợ bỏ qua duplicate-check 5' — AddAsync chỉ ghi entry.
            _ = _repo.Setup(r => r.AddAsync(It.IsAny<CoreAccountingEntry>(), It.IsAny<CancellationToken>()))
                .Callback<CoreAccountingEntry, CancellationToken>((e, _) => _entries.Add(e))
                .Returns(Task.CompletedTask);

            // Repo lọc theo tenant + accountCode (mô phỏng GetByTenantAndAccountCodesAsync).
            _ = _repo.Setup(r => r.GetByTenantAndAccountCodesAsync(It.IsAny<TenantId>(), It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
                .Returns((TenantId tenant, IEnumerable<string> codes, CancellationToken _) =>
                    Task.FromResult(_entries
                        .Where(e => e.TenantId == tenant && e.AccountCode != null && codes.Contains(e.AccountCode))
                        .ToList() as IEnumerable<CoreAccountingEntry>));

            _ = _periodClosing.Setup(p => p.GetPeriodStatusAsync(It.IsAny<AccountingPeriod>(), It.IsAny<TenantId>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(PeriodClosingStatus.Open);

            _service = new CongNoService(_repo.Object, _periodClosing.Object,
                Mock.Of<VanAn.CoreHub.Services.IAuditTrailService>(), NullLogger<CongNoService>.Instance);
        }

        // ===================== KỊCH BẢN A — BÁN CHỊU + THU NỢ =====================

        [Fact]
        public async Task ScenarioA_BanChiuVaThuNo_CuoiKyDung()
        {
            // 01/10 bán chịu 5tr "Khách A" → 15/10 thu 2tr
            _ = await _service.CreateReceivableAsync(_tenantA, 5_000_000m, "Khách A", null, new DateTime(2026, 10, 1), isPayment: false);
            _ = await _service.CreateReceivableAsync(_tenantA, 2_000_000m, "Khách A", null, new DateTime(2026, 10, 15), isPayment: true);

            CongNoReportDto report = await _service.GetCongNoReportAsync(_tenantA, 2026, 10);

            CongNoGroupDto phaiThu = report.Groups.Single(g => g.AccountCode == "131");
            phaiThu.Rows.Should().ContainSingle();
            CongNoRowDto row = phaiThu.Rows[0];
            row.DoiTuong.Should().Be("Khách A");
            row.DauKy.Should().Be(0m);
            row.PhatSinh.Should().Be(5_000_000m);
            row.DaThuTra.Should().Be(2_000_000m);
            row.CuoiKy.Should().Be(3_000_000m);
            // Tuổi nợ tại 31/10: khoản 01/10 = 30 ngày → nhóm 30-60 (SRS mockup FR-7: "30-60: 3tr")
            row.Aging.Duoi30.Should().Be(0m);
            row.Aging.Tu30Den60.Should().Be(3_000_000m);

            phaiThu.TongDauKy.Should().Be(0m);
            phaiThu.TongPhatSinh.Should().Be(5_000_000m);
            phaiThu.TongDaThuTra.Should().Be(2_000_000m);
            phaiThu.TongCuoiKy.Should().Be(3_000_000m);

            // Sổ chi tiết: 2 dòng cộng dồn 5tr → 3tr
            DoiTuongLedgerDto ledger = await _service.GetDoiTuongLedgerAsync(_tenantA, "131", "Khách A", 2026, 10);
            ledger.DauKy.Should().Be(0m);
            ledger.Lines.Should().HaveCount(2);
            ledger.Lines[0].Tang.Should().Be(5_000_000m);
            ledger.Lines[0].Giam.Should().Be(0m);
            ledger.Lines[0].SoDu.Should().Be(5_000_000m);
            ledger.Lines[1].Tang.Should().Be(0m);
            ledger.Lines[1].Giam.Should().Be(2_000_000m);
            ledger.Lines[1].SoDu.Should().Be(3_000_000m);
            ledger.CuoiKy.Should().Be(3_000_000m);
        }

        [Fact]
        public async Task ScenarioA_Aging_NamTrongNhóm3060_ChoKhoanCuoiThang()
        {
            _ = await _service.CreateReceivableAsync(_tenantA, 5_000_000m, "Khách A", null, new DateTime(2026, 10, 1), isPayment: false);

            CongNoReportDto report = await _service.GetCongNoReportAsync(_tenantA, 2026, 10);

            CongNoRowDto row = report.Groups.Single(g => g.AccountCode == "131").Rows[0];
            // asOf = 31/10/2026; age = 30 ngày → nhóm 30-60 (biên 30 ngày = Tu30Den60)
            row.Aging.Duoi30.Should().Be(0m);
            row.Aging.Tu30Den60.Should().Be(5_000_000m);
            row.Aging.Tu60Den90.Should().Be(0m);
            row.Aging.Tren90.Should().Be(0m);
        }

        // ===================== KỊCH BẢN B — MUA CHỊU + TRẢ NỢ =====================

        [Fact]
        public async Task ScenarioB_MuaChiuVaTraNo_CuoiKyDung()
        {
            // 05/10 mua chịu 3tr "Nhà cung cấp Y" → 20/10 trả 1tr
            _ = await _service.CreatePayableAsync(_tenantA, 3_000_000m, "Nhà cung cấp Y", null, new DateTime(2026, 10, 5), isPayment: false);
            _ = await _service.CreatePayableAsync(_tenantA, 1_000_000m, "Nhà cung cấp Y", null, new DateTime(2026, 10, 20), isPayment: true);

            CongNoReportDto report = await _service.GetCongNoReportAsync(_tenantA, 2026, 10);

            CongNoGroupDto phaiTra = report.Groups.Single(g => g.AccountCode == "331");
            phaiTra.Rows.Should().ContainSingle();
            CongNoRowDto row = phaiTra.Rows[0];
            row.DoiTuong.Should().Be("Nhà cung cấp Y");
            row.PhatSinh.Should().Be(3_000_000m);
            row.DaThuTra.Should().Be(1_000_000m);
            row.CuoiKy.Should().Be(2_000_000m);
            row.Aging.Duoi30.Should().Be(2_000_000m);

            phaiTra.TongCuoiKy.Should().Be(2_000_000m);
            // Khối 131 trống (không có phát sinh)
            report.Groups.Single(g => g.AccountCode == "131").Rows.Should().BeEmpty();
        }

        // ===================== KỊCH BẢN C — ĐẢO BÚT TOÁN =====================

        [Fact]
        public async Task ScenarioC_Reversal_SoDuTuKhop()
        {
            // Phiếu nhập nhầm 5tr (đúng 500k) → đảo → tạo lại phiếu đúng → cuối kỳ 500k
            AccountingEntryDto wrong = await _service.CreateReceivableAsync(_tenantA, 5_000_000m, "Khách A", null, new DateTime(2026, 10, 1), isPayment: false);

            // Reversal: tái dùng cơ chế reversal hiện có (AccountingEntry.CreateReversal — gốc + đảo net = 0 → loại khỏi stream)
            CoreAccountingEntry reversal = CoreAccountingEntry.CreateReversal(
                _entries.Single(e => e.Id == wrong.Id), "Nhập sai số tiền");
            await _repo.Object.AddAsync(reversal);

            _ = await _service.CreateReceivableAsync(_tenantA, 500_000m, "Khách A", null, new DateTime(2026, 10, 3), isPayment: false);

            CongNoReportDto report = await _service.GetCongNoReportAsync(_tenantA, 2026, 10);

            CongNoRowDto row = report.Groups.Single(g => g.AccountCode == "131").Rows.Single();
            row.PhatSinh.Should().Be(500_000m); // phiếu 5tr + đảo bị loại khỏi stream
            row.CuoiKy.Should().Be(500_000m);
            row.Aging.Duoi30.Should().Be(500_000m);
        }

        [Fact]
        public async Task ScenarioC_ReversalCuaPhiếuThanhToan_PhucHoiKhoan()
        {
            // Bán chịu 5tr → thu 2tr nhầm → đảo phiếu thu → khoản quay về 5tr
            _ = await _service.CreateReceivableAsync(_tenantA, 5_000_000m, "Khách A", null, new DateTime(2026, 10, 1), isPayment: false);
            AccountingEntryDto thuNham = await _service.CreateReceivableAsync(_tenantA, 2_000_000m, "Khách A", null, new DateTime(2026, 10, 5), isPayment: true);

            CoreAccountingEntry reversal = CoreAccountingEntry.CreateReversal(
                _entries.Single(e => e.Id == thuNham.Id), "Thu nhầm");
            await _repo.Object.AddAsync(reversal);

            CongNoReportDto report = await _service.GetCongNoReportAsync(_tenantA, 2026, 10);

            CongNoRowDto row = report.Groups.Single(g => g.AccountCode == "131").Rows.Single();
            row.PhatSinh.Should().Be(5_000_000m);
            row.DaThuTra.Should().Be(0m); // cặp thu 2tr + đảo bị loại
            row.CuoiKy.Should().Be(5_000_000m);
        }

        // ===================== KỊCH BẢN D — TUỔI NỢ FIFO =====================

        [Fact]
        public async Task ScenarioD_FifoAging_DungNhom()
        {
            // 01/07 bán chịu 5tr · 15/08 bán chịu 3tr · 20/09 trả 2tr
            _ = await _service.CreateReceivableAsync(_tenantA, 5_000_000m, "Khách A", null, new DateTime(2026, 7, 1), isPayment: false);
            _ = await _service.CreateReceivableAsync(_tenantA, 3_000_000m, "Khách A", null, new DateTime(2026, 8, 15), isPayment: false);
            _ = await _service.CreateReceivableAsync(_tenantA, 2_000_000m, "Khách A", null, new DateTime(2026, 9, 20), isPayment: true);

            CongNoReportDto report = await _service.GetCongNoReportAsync(_tenantA, 2026, 9);

            CongNoRowDto row = report.Groups.Single(g => g.AccountCode == "131").Rows.Single();
            row.DauKy.Should().Be(8_000_000m); // trước 01/09: 5tr + 3tr
            row.PhatSinh.Should().Be(0m);
            row.DaThuTra.Should().Be(2_000_000m);
            row.CuoiKy.Should().Be(6_000_000m);

            // FIFO: 2tr trả vào khoản 01/07 → khoản 01/07 còn 3tr (>90 ngày tại 30/09: 91 ngày)
            // khoản 15/08 còn 3tr (30-60 ngày tại 30/09: 46 ngày)
            row.Aging.Tren90.Should().Be(3_000_000m);
            row.Aging.Tu30Den60.Should().Be(3_000_000m);
            row.Aging.Duoi30.Should().Be(0m);
            row.Aging.Tu60Den90.Should().Be(0m);
        }

        [Fact]
        public async Task ScenarioD_BaoCaoThang8_KhoanCu_NamODauKy()
        {
            _ = await _service.CreateReceivableAsync(_tenantA, 5_000_000m, "Khách A", null, new DateTime(2026, 7, 1), isPayment: false);
            _ = await _service.CreateReceivableAsync(_tenantA, 3_000_000m, "Khách A", null, new DateTime(2026, 8, 15), isPayment: false);

            CongNoReportDto report = await _service.GetCongNoReportAsync(_tenantA, 2026, 8);

            CongNoRowDto row = report.Groups.Single(g => g.AccountCode == "131").Rows.Single();
            row.DauKy.Should().Be(5_000_000m);
            row.PhatSinh.Should().Be(3_000_000m);
            row.CuoiKy.Should().Be(8_000_000m);
            // 30/08: khoản 01/07 = 60 ngày → 60-90; khoản 15/08 = 15 ngày → <30
            row.Aging.Tu60Den90.Should().Be(5_000_000m);
            row.Aging.Duoi30.Should().Be(3_000_000m);
        }

        // ===================== D2 — LỊCH SỬ THANH TOÁN KHOẢN NỢ =====================

        [Fact]
        public async Task D2_LichSuThanhToanKhoan_FifoDung()
        {
            AccountingEntryDto khoan1 = await _service.CreateReceivableAsync(_tenantA, 5_000_000m, "Khách A", null, new DateTime(2026, 7, 1), isPayment: false);
            _ = await _service.CreateReceivableAsync(_tenantA, 3_000_000m, "Khách A", null, new DateTime(2026, 8, 15), isPayment: false);
            _ = await _service.CreateReceivableAsync(_tenantA, 2_000_000m, "Khách A", null, new DateTime(2026, 9, 20), isPayment: true);

            KhoanNoPaymentsDto? dto = await _service.GetKhoanNoPaymentsAsync(_tenantA, khoan1.Id);

            dto.Should().NotBeNull();
            dto!.SoTien.Should().Be(5_000_000m);
            dto.DaThanhToan.Should().Be(2_000_000m);
            dto.ConLai.Should().Be(3_000_000m);
            dto.Payments.Should().ContainSingle();
            dto.Payments[0].SoTien.Should().Be(2_000_000m);
            dto.Payments[0].SoDuConLai.Should().Be(3_000_000m); // còn nợ 3tr sau lần trả
        }

        [Fact]
        public async Task D2_KhoanKhongTonTai_TraNull()
        {
            KhoanNoPaymentsDto? dto = await _service.GetKhoanNoPaymentsAsync(_tenantA, Guid.NewGuid());
            dto.Should().BeNull();
        }

        [Fact]
        public async Task D2_KhoanDaBiDao_TraNull()
        {
            AccountingEntryDto khoan = await _service.CreateReceivableAsync(_tenantA, 5_000_000m, "Khách A", null, new DateTime(2026, 7, 1), isPayment: false);
            CoreAccountingEntry reversal = CoreAccountingEntry.CreateReversal(
                _entries.Single(e => e.Id == khoan.Id), "Nhập sai");
            await _repo.Object.AddAsync(reversal);

            KhoanNoPaymentsDto? dto = await _service.GetKhoanNoPaymentsAsync(_tenantA, khoan.Id);
            dto.Should().BeNull(); // cặp gốc + đảo net = 0 → không còn trong stream
        }

        // ===================== ISOLATION TENANT =====================

        [Fact]
        public async Task Isolation_TenantB_KhongAnhHuongTenantA()
        {
            _ = await _service.CreateReceivableAsync(_tenantA, 5_000_000m, "Khách A", null, new DateTime(2026, 10, 1), isPayment: false);
            _ = await _service.CreateReceivableAsync(_tenantB, 99_000_000m, "Khách B", null, new DateTime(2026, 10, 2), isPayment: false);

            CongNoReportDto report = await _service.GetCongNoReportAsync(_tenantA, 2026, 10);

            CongNoRowDto row = report.Groups.Single(g => g.AccountCode == "131").Rows.Single();
            row.DoiTuong.Should().Be("Khách A");
            row.CuoiKy.Should().Be(5_000_000m);

            // Repo được gọi với đúng tenant
            _repo.Verify(r => r.GetByTenantAndAccountCodesAsync(_tenantA, It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()), Times.AtLeastOnce);
        }

        // ===================== [G6] — KHÔNG LỌT VÀO DOANH THU / CHI PHÍ =====================

        [Fact]
        public async Task G6_PhieuCongNo_EntryTypeAdjustment_BookTypeCashBank()
        {
            AccountingEntryDto dto = await _service.CreateReceivableAsync(_tenantA, 5_000_000m, "Khách A", null, new DateTime(2026, 10, 1), isPayment: false);

            dto.AccountCode.Should().Be("131");
            dto.Vendor.Should().Be("Khách A");
            dto.Amount.Should().Be(5_000_000m);
            dto.EntryType.Should().Be(AccountingEntryType.Adjustment);   // không tính doanh thu (GetTodayRevenueAsync lọc Revenue)
            dto.AccountingBookType.Should().Be(AccountingBookType.CashBankBook); // không tính revenue/expense totals (lọc theo BookType)

            AccountingEntryDto payment = await _service.CreateReceivableAsync(_tenantA, 2_000_000m, "Khách A", null, new DateTime(2026, 10, 15), isPayment: true);
            payment.Amount.Should().Be(-2_000_000m); // dấu âm = giảm công nợ [G9]
        }

        [Fact]
        public async Task G6_PhieuCongNo_KhongLoTVaoRevenueTotal()
        {
            // Mô phỏng repo HKD (GetByTenantAndBookTypeAsync lọc theo BookType như DB thật):
            // tenant chỉ có phiếu công nợ (CashBankBook) → tổng doanh thu = 0; thêm 1 phiếu thu
            // doanh thu thật (RevenueBook) → tổng = đúng doanh thu thật, không gồm 131.
            CoreAccountingEntry debt = await CreateDebtEntryOnlyAsync(_tenantA, "131", 5_000_000m, new DateTime(2026, 10, 1));
            _ = debt;

            var hkdRepo = new Mock<IAccountingEntryRepository>();
            _ = hkdRepo.Setup(r => r.GetByTenantAndBookTypeAsync(_tenantA, AccountingBookType.RevenueBook, It.IsAny<CancellationToken>()))
                .Returns((TenantId tenant, AccountingBookType _, CancellationToken _) =>
                    Task.FromResult(_entries
                        .Where(e => e.TenantId == tenant && e.AccountingBookType == AccountingBookType.RevenueBook)
                        .ToList() as IEnumerable<CoreAccountingEntry>));

            var hkdService = new HKDBookService(hkdRepo.Object, Mock.Of<IHKDBookRepository>(),
                Mock.Of<IHKDBookGenerationService>(), NullLogger<HKDBookService>.Instance);

            decimal revenue = await hkdService.GetRevenueTotalAsync(_tenantA, new AccountingPeriod(2026, 10));
            revenue.Should().Be(0m); // 131 không lọt — phiếu công nợ không nằm RevenueBook

            CoreAccountingEntry revenueEntry = CoreAccountingEntry.CreateRevenue(
                _tenantA, new AccountingPeriod(2026, 10), new Money(7_000_000m), "Doanh thu thật",
                accountCode: "511", transactionDate: new DateTime(2026, 10, 2));
            await _repo.Object.AddAsync(revenueEntry);

            decimal revenueWithSale = await hkdService.GetRevenueTotalAsync(_tenantA, new AccountingPeriod(2026, 10));
            revenueWithSale.Should().Be(7_000_000m); // chỉ doanh thu thật 511, KHÔNG gồm 131
        }

        // ===================== VALIDATION & P1 DECISIONS =====================

        [Fact]
        public async Task CreatePhiếu_SoTienKhongDuong_Throw()
        {
            Func<Task> act = () => _service.CreateReceivableAsync(_tenantA, 0m, "Khách A", null, new DateTime(2026, 10, 1), isPayment: false);
            (await act.Should().ThrowAsync<ArgumentException>()).Which.ParamName.Should().Be("amount");
        }

        [Fact]
        public async Task CreatePhiếu_DoiTuongTrong_Throw()
        {
            Func<Task> act = () => _service.CreateReceivableAsync(_tenantA, 1_000_000m, "  ", null, new DateTime(2026, 10, 1), isPayment: false);
            (await act.Should().ThrowAsync<ArgumentException>()).Which.ParamName.Should().Be("doiTuong");
        }

        [Fact]
        public async Task CreatePhiếu_KyDaDong_Throw()
        {
            _ = _periodClosing.Setup(p => p.GetPeriodStatusAsync(new AccountingPeriod(2026, 10), _tenantA, It.IsAny<CancellationToken>()))
                .ReturnsAsync(PeriodClosingStatus.Closed);

            Func<Task> act = () => _service.CreateReceivableAsync(_tenantA, 1_000_000m, "Khách A", null, new DateTime(2026, 10, 15), isPayment: false);
            (await act.Should().ThrowAsync<InvalidOperationException>()).Which.Message.Should().Contain("đã đóng sổ");
        }

        [Fact]
        public async Task CreatePhiếu_DienGiaiMacDinh_KemMst_G11()
        {
            AccountingEntryDto dto = await _service.CreateReceivableAsync(_tenantA, 5_000_000m, "Công Ty TNHH X",
                null, new DateTime(2026, 10, 1), isPayment: false, mst: "0312345678");

            dto.Description.Should().Be("Bán chịu — Công Ty TNHH X (MST 0312345678)");

            AccountingEntryDto thuNo = await _service.CreateReceivableAsync(_tenantA, 2_000_000m, "Khách A",
                null, new DateTime(2026, 10, 15), isPayment: true);
            thuNo.Description.Should().Be("Thu tiền khách trả nợ — Khách A");

            AccountingEntryDto traNo = await _service.CreatePayableAsync(_tenantA, 1_000_000m, "Nhà cung cấp Y",
                null, new DateTime(2026, 10, 20), isPayment: true);
            traNo.Description.Should().Be("Trả tiền người bán — Nhà cung cấp Y");
        }

        [Fact]
        public async Task P1Decision2_HaiPhieuCungSoTienTrong5Phut_DeuDuocTao()
        {
            // 2 khách cùng bán chịu 5tr trong 5 phút — KHÔNG bị duplicate-check chặn
            AccountingEntryDto khachA = await _service.CreateReceivableAsync(_tenantA, 5_000_000m, "Khách A", null, new DateTime(2026, 10, 1), isPayment: false);
            AccountingEntryDto khachB = await _service.CreateReceivableAsync(_tenantA, 5_000_000m, "Khách B", null, new DateTime(2026, 10, 1), isPayment: false);

            khachA.Id.Should().NotBe(khachB.Id);
            _entries.Count.Should().Be(2);
        }

        [Fact]
        public async Task P1Decision3_TieBreak_CungNgay_TheoCreatedAt()
        {
            // 2 khoản cùng ngày 01/10: khoản 1 tạo trước phải bị trừ trước (FIFO theo CreatedAt)
            AccountingEntryDto k1 = await _service.CreateReceivableAsync(_tenantA, 5_000_000m, "Khách A", null, new DateTime(2026, 10, 1), isPayment: false);
            _ = await _service.CreateReceivableAsync(_tenantA, 3_000_000m, "Khách A", null, new DateTime(2026, 10, 1), isPayment: false);
            _ = await _service.CreateReceivableAsync(_tenantA, 4_000_000m, "Khách A", null, new DateTime(2026, 10, 20), isPayment: true);

            KhoanNoPaymentsDto? dto = await _service.GetKhoanNoPaymentsAsync(_tenantA, k1.Id);

            dto.Should().NotBeNull();
            dto!.DaThanhToan.Should().Be(4_000_000m); // khoản 01/07 cũ nhất bị trừ trước
            dto.ConLai.Should().Be(1_000_000m);
        }

        // ===================== HELPERS =====================

        private async Task<CoreAccountingEntry> CreateDebtEntryOnlyAsync(TenantId tenant, string accountCode, decimal amount, DateTime date)
        {
            CoreAccountingEntry entry = CoreAccountingEntry.CreateDebt(
                tenant, new AccountingPeriod(date.Year, date.Month), new Money(amount),
                "Test", accountCode: accountCode, vendor: "Khách A", transactionDate: date);
            await _repo.Object.AddAsync(entry);
            return entry;
        }
    }
}
