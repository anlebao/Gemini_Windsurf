using VanAn.Shared.Domain;

namespace VanAn.Shared.DTOs
{
    /// <summary>
    /// THU CHI & CÔNG NỢ MVP (2026-10-06, SRS v1.1 §5.3 FR-7):
    /// Báo cáo công nợ tổng hợp theo tháng — 2 khối (Phải thu 131 / Phải trả 331),
    /// mỗi đối tượng: Đầu kỳ · PS tăng · Đã thu/trả · Cuối kỳ · Tuổi nợ (FIFO động [G12]).
    /// </summary>
    public class CongNoReportDto
    {
        public int Year { get; set; }
        public int Month { get; set; }
        public DateTime ReportDate { get; set; }

        /// <summary>2 khối: AccountCode "131" (Phải thu) + "331" (Phải trả).</summary>
        public List<CongNoGroupDto> Groups { get; set; } = [];
    }

    public class CongNoGroupDto
    {
        public string AccountCode { get; set; } = string.Empty;
        public string AccountName { get; set; } = string.Empty;

        /// <summary>Chỉ các đối tượng có số dư cuối kỳ khác 0 (hoặc lọc "Tất cả").</summary>
        public List<CongNoRowDto> Rows { get; set; } = [];

        public decimal TongDauKy { get; set; }
        public decimal TongPhatSinh { get; set; }
        public decimal TongDaThuTra { get; set; }
        public decimal TongCuoiKy { get; set; }
        public CongNoAgingDto TongAging { get; set; } = new();
    }

    public class CongNoRowDto
    {
        public string DoiTuong { get; set; } = string.Empty;
        public decimal DauKy { get; set; }
        public decimal PhatSinh { get; set; }
        public decimal DaThuTra { get; set; }
        public decimal CuoiKy { get; set; }
        public CongNoAgingDto Aging { get; set; } = new();
    }

    /// <summary>Phân loại tuổi nợ [G12] — nhóm <30 · 30-60 · 60-90 · &gt;90 ngày (FIFO động).</summary>
    public class CongNoAgingDto
    {
        public decimal Duoi30 { get; set; }
        public decimal Tu30Den60 { get; set; }
        public decimal Tu60Den90 { get; set; }
        public decimal Tren90 { get; set; }

        public decimal Tong => Duoi30 + Tu30Den60 + Tu60Den90 + Tren90;
    }

    /// <summary>
    /// Sổ theo dõi phải thu / phải trả theo đối tượng (SRS §5.4 FR-8):
    /// Ngày · Diễn giải · Tăng · Giảm · Số dư cộng dồn — kèm đầu kỳ/cuối kỳ.
    /// </summary>
    public class DoiTuongLedgerDto
    {
        public string AccountCode { get; set; } = string.Empty;
        public string DoiTuong { get; set; } = string.Empty;
        public int Year { get; set; }
        public int Month { get; set; }
        public decimal DauKy { get; set; }
        public decimal CuoiKy { get; set; }
        public List<DoiTuongLedgerLineDto> Lines { get; set; } = [];
    }

    public class DoiTuongLedgerLineDto
    {
        public Guid EntryId { get; set; }
        public DateTime TransactionDate { get; set; }
        public string Description { get; set; } = string.Empty;
        public decimal Tang { get; set; }
        public decimal Giam { get; set; }
        public decimal SoDu { get; set; }
        public bool IsReversal { get; set; }
    }

    /// <summary>
    /// Truy ngược lịch sử thanh toán của 1 khoản nợ (SRS §5.4 FR-8.1 [G12] FIFO động):
    /// các lần thu/trả đã trừ vào khoản + số dư còn lại sau mỗi lần.
    /// </summary>
    public class KhoanNoPaymentsDto
    {
        public Guid KhoanNoEntryId { get; set; }
        public string AccountCode { get; set; } = string.Empty;
        public string DoiTuong { get; set; } = string.Empty;
        public DateTime TransactionDate { get; set; }
        public string Description { get; set; } = string.Empty;
        public decimal SoTien { get; set; }
        public decimal DaThanhToan { get; set; }
        public decimal ConLai { get; set; }
        public List<KhoanNoPaymentLineDto> Payments { get; set; } = [];
    }

    public class KhoanNoPaymentLineDto
    {
        public Guid EntryId { get; set; }
        public DateTime TransactionDate { get; set; }
        public string Description { get; set; } = string.Empty;
        public decimal SoTien { get; set; }
        public decimal SoDuConLai { get; set; }
    }
}
