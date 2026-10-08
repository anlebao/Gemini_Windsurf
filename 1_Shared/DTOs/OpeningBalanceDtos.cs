namespace VanAn.Shared.DTOs
{
    /// <summary>
    /// NHẬP LIỆU & SỔ SÁCH P3 (#2): khai báo số dư đầu kỳ — 2 phần:
    /// (a) số dư các tài khoản tổng (111/112/156/211/311/333/421...) — vào BalanceSheet/B01 qua JournalEntry;
    /// (b) công nợ cũ theo đối tượng (khách A 5tr, NCC Y 3tr — nguồn Excel) — tạo phiếu 131/331 per đối tượng.
    /// </summary>
    public class OpeningBalanceLineDto
    {
        public string AccountCode { get; set; } = string.Empty;
        public string AccountName { get; set; } = string.Empty;
        public decimal Debit { get; set; }
        public decimal Credit { get; set; }
    }

    public class OpeningBalanceCongNoLineDto
    {
        /// <summary>"131" (khách còn nợ) hoặc "331" (mình còn nợ người bán).</summary>
        public string AccountCode { get; set; } = "131";
        public string DoiTuong { get; set; } = string.Empty;
        public decimal Amount { get; set; }
    }

    public class OpeningBalanceRequestDto
    {
        public int Year { get; set; }
        public int Month { get; set; }

        /// <summary>Số dư các tài khoản (a) — ΣDebit phải == ΣCredit (chênh → nhập 421 để bù).</summary>
        public List<OpeningBalanceLineDto> Lines { get; set; } = [];

        /// <summary>Công nợ cũ theo đối tượng (b) — tùy chọn.</summary>
        public List<OpeningBalanceCongNoLineDto> CongNoLines { get; set; } = [];
    }

    public class OpeningBalanceSaveResultDto
    {
        public int Year { get; set; }
        public int Month { get; set; }
        public int JournalEntryCount { get; set; }
        public int CongNoEntryCount { get; set; }
        public string Message { get; set; } = string.Empty;
    }

    public class OpeningBalanceDto
    {
        public int Year { get; set; }
        public int Month { get; set; }
        public bool Exists { get; set; }
        public List<OpeningBalanceLineDto> Lines { get; set; } = [];
        public List<OpeningBalanceCongNoLineDto> CongNoLines { get; set; } = [];
    }
}
