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

    // ---------- Task G5: dialog snapshot ----------

    private sealed class QuestionDto
    {
        public int VoucherIndex { get; set; }
        public string Slot { get; set; } = string.Empty;
        public string QuestionText { get; set; } = string.Empty;
        public List<QuickReplyDto> QuickReplies { get; set; } = new();
        public bool Required { get; set; }
    }

    private sealed class QuickReplyDto
    {
        public string Label { get; set; } = string.Empty;
        public string Value { get; set; } = string.Empty;
    }

    private sealed class DialogSnapshotDto
    {
        public Guid TenantId { get; set; }
        public string OriginalText { get; set; } = string.Empty;
        public List<ParsedVoucherDto> Vouchers { get; set; } = new();
        public int AskedCount { get; set; }
        public List<string> AskedSlotKeys { get; set; } = new();
        public List<string> SkippedSlotKeys { get; set; } = new();
        public QuestionDto? Pending { get; set; }
        public DialogState State { get; set; }
        public List<string> Transcript { get; set; } = new();
    }

    /// <summary>G5: serialize dialog session đang Asking — khôi phục deterministic sau reload.</summary>
    public static string SerializeDialog(SmartEntryDialogSnapshot snap)
    {
        try
        {
            var dto = new DialogSnapshotDto
            {
                TenantId = snap.TenantId,
                OriginalText = snap.OriginalText,
                Vouchers = snap.Vouchers.Select(ToDto).ToList(),
                AskedCount = snap.AskedCount,
                AskedSlotKeys = snap.AskedSlotKeys,
                SkippedSlotKeys = snap.SkippedSlotKeys,
                Pending = snap.Pending == null ? null : new QuestionDto
                {
                    VoucherIndex = snap.Pending.VoucherIndex,
                    Slot = snap.Pending.Slot,
                    QuestionText = snap.Pending.QuestionText,
                    QuickReplies = snap.Pending.QuickReplies
                        .Select(r => new QuickReplyDto { Label = r.Label, Value = r.Value }).ToList(),
                    Required = snap.Pending.Required,
                },
                State = snap.State,
                Transcript = snap.Transcript,
            };
            return System.Text.Json.JsonSerializer.Serialize(dto);
        }
        catch
        {
            return string.Empty;
        }
    }

    public static SmartEntryDialogSnapshot? DeserializeDialog(string json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try
        {
            var d = System.Text.Json.JsonSerializer.Deserialize<DialogSnapshotDto>(json);
            if (d == null) return null;
            return new SmartEntryDialogSnapshot(
                d.TenantId, d.OriginalText, d.Vouchers.Select(FromDto).ToList(), d.AskedCount,
                d.AskedSlotKeys, d.SkippedSlotKeys,
                d.Pending == null ? null : new ClarifyingQuestion(
                    d.Pending.VoucherIndex, d.Pending.Slot, d.Pending.QuestionText,
                    d.Pending.QuickReplies.Select(r => new QuickReply(r.Label, r.Value)).ToList(),
                    d.Pending.Required),
                d.State, d.Transcript);
        }
        catch
        {
            return null;
        }
    }

    private static ParsedVoucherDto ToDto(ParsedVoucher v) => new()
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
    };

    private static ParsedVoucher FromDto(ParsedVoucherDto d) => new(
        d.Intent, d.Amount, d.Date, d.PaymentMethod, d.FromAccountCode, d.ToAccountCode,
        d.DoiTuong, d.Category, d.FeeAmount, d.Description, d.Confidence,
        d.MissingFields, d.Warnings);
}