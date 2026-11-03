using VanAn.CoreHub.Services;

namespace VanAn.ShopERP.Components.Accounting;

/// <summary>
/// Task G4 — giữ kết quả parse multi qua các trang (scoped per circuit): user parse
/// "thu 5tr, chi 3tr" ở form Doanh Thu → sang form Chi Phí (navigation) → block trích
/// xuất vẫn hiện 2 phiếu. Clear khi parse câu mới / hết phiếu chờ.
/// Task PH3 — thêm ProcessedIndices (phiếu đã lưu) để không áp lại khi sang trang khác.
/// </summary>
public sealed class SmartEntrySessionState
{
    public List<ParsedVoucher>? Multi { get; private set; }
    public string? Text { get; private set; }
    /// <summary>PH3: index phiếu đã lưu trong Multi (đánh dấu "✓ Đã lưu" khi khôi phục trang khác).</summary>
    public List<int>? ProcessedIndices { get; private set; }

    public void Set(List<ParsedVoucher> multi, string text, List<int>? processedIndices = null)
    {
        Multi = multi;
        Text = text;
        ProcessedIndices = processedIndices;
    }

    public void Clear()
    {
        Multi = null;
        Text = null;
        ProcessedIndices = null;
    }
}

/// <summary>
/// Task PH3 — codec multi JSON cho sessionStorage (khôi phục DETERMINISTIC sau full reload,
/// không phụ thuộc LLM re-parse). ParsedVoucher là record — serialize qua DTO để deserialize
/// an toàn (System.Text.Json không deserialize được IReadOnlyList&lt;string&gt;).
/// </summary>
public static class SmartEntrySessionCodec
{
    private sealed class ParsedVoucherDto
    {
        public VoucherIntent Intent { get; set; }
        public decimal? Amount { get; set; }
        public DateTime Date { get; set; }
        public string? PaymentMethod { get; set; }
        public string? FromAccountCode { get; set; }
        public string? ToAccountCode { get; set; }
        public string? DoiTuong { get; set; }
        public string? Category { get; set; }
        public decimal? FeeAmount { get; set; }
        public string Description { get; set; } = string.Empty;
        public string Confidence { get; set; } = string.Empty;
        public List<string> MissingFields { get; set; } = new();
        public List<string> Warnings { get; set; } = new();
    }

    public static string Serialize(IReadOnlyList<ParsedVoucher> list)
    {
        try
        {
            var dtos = list.Select(v => new ParsedVoucherDto
            {
                Intent = v.Intent,
                Amount = v.Amount,
                Date = v.Date,
                PaymentMethod = v.PaymentMethod,
                FromAccountCode = v.FromAccountCode,
                ToAccountCode = v.ToAccountCode,
                DoiTuong = v.DoiTuong,
                Category = v.Category,
                FeeAmount = v.FeeAmount,
                Description = v.Description,
                Confidence = v.Confidence,
                MissingFields = v.MissingFields.ToList(),
                Warnings = v.Warnings.ToList(),
            }).ToList();
            return System.Text.Json.JsonSerializer.Serialize(dtos);
        }
        catch
        {
            return string.Empty;
        }
    }

    public static List<ParsedVoucher>? Deserialize(string json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try
        {
            var dtos = System.Text.Json.JsonSerializer.Deserialize<List<ParsedVoucherDto>>(json);
            if (dtos == null || dtos.Count == 0) return null;
            return dtos.Select(d => new ParsedVoucher(
                d.Intent, d.Amount, d.Date, d.PaymentMethod, d.FromAccountCode, d.ToAccountCode,
                d.DoiTuong, d.Category, d.FeeAmount, d.Description, d.Confidence,
                d.MissingFields, d.Warnings)).ToList();
        }
        catch
        {
            return null;
        }
    }
}