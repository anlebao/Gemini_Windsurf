using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using VanAn.Shared.Domain;

namespace VanAn.CoreHub.Services;

public enum DialogState { Asking, Ready, Abandoned }

/// <summary>Task G5: gợi ý trả lời 1-chạm — Value điền thẳng vào slot, bỏ qua NLP (deterministic).</summary>
public sealed record QuickReply(string Label, string Value);

/// <summary>Task G5: câu hỏi làm rõ cho 1 slot còn thiếu/mơ hồ của 1 phiếu trong session.</summary>
public sealed record ClarifyingQuestion(
    int VoucherIndex,
    string Slot,
    string QuestionText,
    IReadOnlyList<QuickReply> QuickReplies,
    bool Required);

/// <summary>Task G5: snapshot serializable của session — persist qua sessionStorage (full reload).</summary>
public sealed record SmartEntryDialogSnapshot(
    Guid TenantId,
    string OriginalText,
    List<ParsedVoucher> Vouchers,
    int AskedCount,
    List<string> AskedSlotKeys,
    List<string> SkippedSlotKeys,
    ClarifyingQuestion? Pending,
    DialogState State,
    List<string> Transcript);

/// <summary>Task G5: session vấn đáp — phiếu + câu hỏi đang chờ + transcript + đếm chống loop.</summary>
public sealed class SmartEntryDialogSession
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public required TenantId TenantId { get; init; }
    public required string OriginalText { get; init; }
    public required List<ParsedVoucher> Vouchers { get; init; }
    public ClarifyingQuestion? Pending { get; set; }
    public int AskedCount { get; set; }
    public DialogState State { get; set; } = DialogState.Asking;
    /// <summary>"voucherIndex:slot" → số lần đã hỏi mà câu trả lời chưa điền được slot.</summary>
    public Dictionary<string, int> AskedSlotCounts { get; } = new();
    /// <summary>Slot đã bỏ qua (user skip hoặc hết lượt hỏi) — không hỏi lại.</summary>
    public HashSet<string> SkippedSlots { get; } = new();
    /// <summary>Dòng transcript "U: ..."/"B: ..." — hiển thị thread hội thoại.</summary>
    public List<string> Transcript { get; } = new();

    public SmartEntryDialogSnapshot ToSnapshot() => new(
        TenantId.Value, OriginalText, Vouchers.ToList(), AskedCount,
        AskedSlotCounts.Keys.ToList(), SkippedSlots.ToList(),
        Pending, State, Transcript.ToList());
}

public interface ISmartEntryDialogService
{
    /// <summary>Parse câu gốc → session. Nếu mọi phiếu đủ field → State=Ready, Pending=null
    /// (hành vi y hệt parse cũ — UI không đổi). Còn thiếu → Pending = câu hỏi đầu tiên.
    /// <param name="forceLlm">G2: truyền tiếp tới parser decorator (nút "✨ AI phân tích").</param></summary>
    Task<SmartEntryDialogSession> StartAsync(TenantId tenantId, string text, CancellationToken ct = default, bool forceLlm = false);

    /// <summary>Nhận đáp án (chip Value hoặc free text) → merge → câu hỏi tiếp hoặc Ready.</summary>
    Task<SmartEntryDialogSession> AnswerAsync(Guid sessionId, string answer, CancellationToken ct = default);

    /// <summary>"Bỏ qua câu này" — đánh dấu slot skipped, sang câu hỏi tiếp.</summary>
    SmartEntryDialogSession? Skip(Guid sessionId);

    /// <summary>"Bỏ qua — tự nhập form" — kết thúc dialog, giữ voucher prefill phần có.</summary>
    SmartEntryDialogSession? Abandon(Guid sessionId);

    SmartEntryDialogSession? Get(Guid sessionId);

    /// <summary>Khôi phục session từ snapshot sau navigation/full-reload (id mới, state giữ).</summary>
    SmartEntryDialogSession Restore(SmartEntryDialogSnapshot snapshot);
}

/// <summary>
/// Task G5 (Smart Entry Vấn Đáp — P1 thuần rule-based): vòng lặp hỏi–theo–slot. Parse câu gốc
/// → voucher nào còn slot bắt buộc trống → sinh ClarifyingQuestion (text + quick-reply chips)
/// → đáp án merge qua SlotExtraction (điền slot đang hỏi trước, rồi opportunistic các slot còn
/// thiếu) → re-validate → hỏi tiếp hoặc Ready. Chống loop: ≤2 lần/slot, ≤6 câu/session.
/// KHÔNG auto-save, KHÔNG đoán — hỏi thay vì đoán (SRS §18.4.1.5). P3 cắm LLM interpret sau.
/// </summary>
public sealed class SmartEntryDialogService(
    IVoucherTextParseService parser,
    VoucherTextParseService ruleParser,
    IBankAccountService? bankAccounts = null,
    ILogger<SmartEntryDialogService>? logger = null) : ISmartEntryDialogService
{
    internal const int MaxQuestions = 6;
    internal const int MaxPerSlot = 2;

    private readonly Dictionary<Guid, SmartEntryDialogSession> _sessions = new();

    /// <summary>Intent cần đối tượng — khớp rule MissingFields của parser (công nợ + advance).</summary>
    private static bool NeedsDoiTuong(VoucherIntent i) => i is
        VoucherIntent.Receivable or VoucherIntent.ReceivablePayment or VoucherIntent.Advance
        or VoucherIntent.Payable or VoucherIntent.PayablePayment;

    private static bool IsTransfer(VoucherIntent i) => i is VoucherIntent.TransferOut or VoucherIntent.TransferIn;

    /// <summary>Thứ tự hỏi trong 1 phiếu: intent → direction → amount → đối tượng → category (soft).</summary>
    private static IEnumerable<string> MissingSlots(ParsedVoucher v)
    {
        if (v.Intent == VoucherIntent.Unknown) yield return "intent";
        if (IsTransfer(v.Intent) && DirectionUnclear(v)) yield return "direction";
        if (v.Amount == null) yield return "amount";
        if (NeedsDoiTuong(v.Intent) && string.IsNullOrWhiteSpace(v.DoiTuong)) yield return "doiTuong";
        if (v.Intent == VoucherIntent.Expense && v.Category == null) yield return "category";
    }

    /// <summary>Transfer nhưng hướng chưa chắc: thiếu from/to, hoặc parser chỉ ĐOÁN theo marker-order
    /// (warning "hướng chưa rõ") mà from/to không khớp semantics của intent. Hỏi xác nhận thay vì
    /// tin guess (SRS §18.4.1.5).</summary>
    private static bool DirectionUnclear(ParsedVoucher v)
        => v.MissingFields.Any(m => m.Contains("nguồn/đích", StringComparison.Ordinal))
           || v.FromAccountCode == null || v.ToAccountCode == null
           || (v.Warnings.Any(w => w.Contains("hướng chưa rõ", StringComparison.Ordinal))
               && !DirectionConsistent(v));

    /// <summary>from/to khớp semantics intent: Out = 112x→111 · In = 111→112x. Parser fallback
    /// gán TransferIn + from=112/to=111 (không consistent) → vẫn phải hỏi.</summary>
    private static bool DirectionConsistent(ParsedVoucher v) => v.Intent switch
    {
        VoucherIntent.TransferOut => v.FromAccountCode != null
            && v.FromAccountCode.StartsWith("112", StringComparison.Ordinal) && v.ToAccountCode == "111",
        VoucherIntent.TransferIn => v.FromAccountCode == "111" && v.ToAccountCode != null
            && v.ToAccountCode.StartsWith("112", StringComparison.Ordinal),
        _ => false,
    };

    public async Task<SmartEntryDialogSession> StartAsync(TenantId tenantId, string text, CancellationToken ct = default, bool forceLlm = false)
    {
        var vouchers = (await parser.ParseMultipleAsync(tenantId, text, ct, forceLlm)).ToList();
        var session = new SmartEntryDialogSession
        {
            TenantId = tenantId,
            OriginalText = text ?? string.Empty,
            Vouchers = vouchers,
        };
        session.Transcript.Add("U: " + session.OriginalText);
        _sessions[session.Id] = session;
        Advance(session);
        return session;
    }

    public async Task<SmartEntryDialogSession> AnswerAsync(Guid sessionId, string answer, CancellationToken ct = default)
    {
        var s = Get(sessionId) ?? throw new InvalidOperationException($"Dialog session {sessionId} không tồn tại.");
        if (s.State != DialogState.Asking || s.Pending == null) return s;

        var q = s.Pending;
        answer = (answer ?? string.Empty).Trim();
        s.Transcript.Add("U: " + answer);

        var chip = q.QuickReplies.FirstOrDefault(r => string.Equals(r.Value, answer, StringComparison.OrdinalIgnoreCase));
        bool filled;
        if (chip != null)
        {
            filled = await ApplyChipAsync(s, q, chip.Value, ct);
        }
        else if (IsSkipText(answer))
        {
            s.SkippedSlots.Add(SlotKey(q.VoucherIndex, q.Slot));
            filled = true; // coi như xử lý xong slot (bỏ qua)
        }
        else
        {
            var ex = ruleParser.ExtractSlots(answer);
            filled = await FillPendingSlotAsync(s, q, answer, ex, ct);
            // Opportunistic: đáp án gộp nhiều slot ("5 triệu anh Hùng ck") → điền các chỗ còn trống.
            s.Vouchers[q.VoucherIndex] = MergeOpportunistic(s.Vouchers[q.VoucherIndex], ex);
        }

        if (!filled)
        {
            var key = SlotKey(q.VoucherIndex, q.Slot);
            int n = s.AskedSlotCounts.GetValueOrDefault(key) + 1;
            s.AskedSlotCounts[key] = n;
            if (n >= MaxPerSlot) s.SkippedSlots.Add(key);
        }

        s.Vouchers[q.VoucherIndex] = Revalidate(s.Vouchers[q.VoucherIndex]);
        s.Pending = null;
        Advance(s);
        return s;
    }

    public SmartEntryDialogSession? Skip(Guid sessionId)
    {
        var s = Get(sessionId);
        if (s?.Pending == null) return s;
        s.SkippedSlots.Add(SlotKey(s.Pending.VoucherIndex, s.Pending.Slot));
        s.Pending = null;
        Advance(s);
        return s;
    }

    public SmartEntryDialogSession? Abandon(Guid sessionId)
    {
        var s = Get(sessionId);
        if (s == null) return null;
        s.State = DialogState.Abandoned;
        s.Pending = null;
        return s;
    }

    public SmartEntryDialogSession? Get(Guid sessionId) => _sessions.GetValueOrDefault(sessionId);

    public SmartEntryDialogSession Restore(SmartEntryDialogSnapshot snapshot)
    {
        var s = new SmartEntryDialogSession
        {
            TenantId = new TenantId(snapshot.TenantId),
            OriginalText = snapshot.OriginalText,
            Vouchers = snapshot.Vouchers.ToList(),
            AskedCount = snapshot.AskedCount,
            Pending = snapshot.Pending,
            State = snapshot.State,
        };
        s.Transcript.AddRange(snapshot.Transcript);
        foreach (var k in snapshot.AskedSlotKeys) s.AskedSlotCounts[k] = MaxPerSlot;
        foreach (var k in snapshot.SkippedSlotKeys) s.SkippedSlots.Add(k);
        _sessions[s.Id] = s;
        return s;
    }

    // ---------- question generation ----------

    private void Advance(SmartEntryDialogSession s)
    {
        if (s.State == DialogState.Abandoned) return;
        if (s.AskedCount >= MaxQuestions) { s.State = DialogState.Ready; s.Pending = null; return; }
        for (int i = 0; i < s.Vouchers.Count; i++)
        {
            var q = BuildQuestion(s, i, s.Vouchers[i]);
            if (q != null)
            {
                s.Pending = q;
                s.State = DialogState.Asking;
                s.AskedCount++;
                s.Transcript.Add("B: " + q.QuestionText);
                return;
            }
        }
        s.State = DialogState.Ready;
        s.Pending = null;
    }

    private ClarifyingQuestion? BuildQuestion(SmartEntryDialogSession s, int idx, ParsedVoucher v)
    {
        string prefix = s.Vouchers.Count > 1 ? $"Phiếu {idx + 1}/{s.Vouchers.Count} — " : string.Empty;
        foreach (var slot in MissingSlots(v))
        {
            var key = SlotKey(idx, slot);
            if (s.SkippedSlots.Contains(key)) continue;
            if (s.AskedSlotCounts.GetValueOrDefault(key) >= MaxPerSlot) continue;
            bool retry = s.AskedSlotCounts.GetValueOrDefault(key) > 0;
            return slot switch
            {
                "intent" => new ClarifyingQuestion(idx, slot,
                    prefix + "Nghiệp vụ này là loại phiếu gì?",
                    [new("Thu doanh thu", "Revenue"), new("Chi phí", "Expense"),
                     new("Nộp quỹ vào NH", "TransferIn"), new("Rút NH về quỹ", "TransferOut"),
                     new("Thu nợ khách", "ReceivablePayment"), new("Trả người bán", "PayablePayment"),
                     new("Bán chịu (phải thu)", "Receivable"), new("Mua chịu (phải trả)", "Payable"),
                     new("Góp vốn", "Capital"), new("Vay nhận tiền", "Loan"),
                     new("Trả nợ vay", "LoanRepayment"), new("CTGS/hạch toán", "JournalAdjustment")],
                    Required: true),
                "direction" => new ClarifyingQuestion(idx, slot,
                    prefix + (retry ? "Chuyển tiền theo hướng nào? (chọn 1 trong 2)" : "Chuyển tiền theo hướng nào?"),
                    [new("Quỹ → Ngân hàng", "in"), new("Ngân hàng → Quỹ", "out")],
                    Required: true),
                "amount" => new ClarifyingQuestion(idx, slot,
                    prefix + (retry ? "Chưa đọc được số tiền — nhập dạng \"5 triệu\" hoặc \"5000000\":" : "Số tiền bao nhiêu?"),
                    [], Required: true),
                "doiTuong" => new ClarifyingQuestion(idx, slot,
                    prefix + (retry ? "Chưa đọc được tên — gõ tên đối tượng (VD: anh Hùng / công ty ABC):" : "Đối tượng (khách/người bán) là ai?"),
                    [], Required: true),
                "category" => new ClarifyingQuestion(idx, slot,
                    prefix + "Khoản chi này thuộc nhóm nào? (giúp gợi ý tài khoản)",
                    [new("Thuê/mặt bằng", "rent"), new("Điện nước", "utilities"), new("Lương", "labor"),
                     new("Vật tư", "materials"), new("Marketing", "marketing"), new("Sửa chữa/vận chuyển", "maintenance"),
                     new("Thuế", "tax"), new("Lãi vay", "interest")],
                    Required: false),
                _ => null,
            };
        }
        return null;
    }

    // ---------- answer merge ----------

    private async Task<bool> ApplyChipAsync(SmartEntryDialogSession s, ClarifyingQuestion q, string value, CancellationToken ct)
    {
        var v = s.Vouchers[q.VoucherIndex];
        switch (q.Slot)
        {
            case "intent":
                if (!Enum.TryParse<VoucherIntent>(value, out var intent)) return false;
                v = v with { Intent = intent };
                if (IsTransfer(intent)) v = await ResolveTransferAccountsAsync(s.TenantId, v, ct);
                s.Vouchers[q.VoucherIndex] = v;
                return true;
            case "direction":
                if (value is not ("in" or "out")) return false;
                v = v with { Intent = value == "out" ? VoucherIntent.TransferOut : VoucherIntent.TransferIn };
                s.Vouchers[q.VoucherIndex] = await ResolveTransferAccountsAsync(s.TenantId, v, ct);
                return true;
            case "category":
                s.Vouchers[q.VoucherIndex] = v with { Category = value };
                return true;
            case "amount":
                if (decimal.TryParse(value, System.Globalization.NumberStyles.Number,
                        System.Globalization.CultureInfo.InvariantCulture, out var a) && a > 0)
                {
                    s.Vouchers[q.VoucherIndex] = v with { Amount = a };
                    return true;
                }
                return false;
            default:
                s.Vouchers[q.VoucherIndex] = q.Slot switch
                {
                    "doiTuong" => v with { DoiTuong = value },
                    "paymentMethod" => v with { PaymentMethod = value },
                    _ => v,
                };
                return q.Slot is "doiTuong" or "paymentMethod";
        }
    }

    private async Task<bool> FillPendingSlotAsync(SmartEntryDialogSession s, ClarifyingQuestion q, string answer, SlotExtraction ex, CancellationToken ct)
    {
        var v = s.Vouchers[q.VoucherIndex];
        switch (q.Slot)
        {
            case "amount" when ex.Amount > 0:
                s.Vouchers[q.VoucherIndex] = v with { Amount = ex.Amount };
                return true;
            case "doiTuong":
                var name = ex.DoiTuong ?? PlausibleName(answer, ex);
                if (name == null) return false;
                s.Vouchers[q.VoucherIndex] = v with { DoiTuong = name };
                return true;
            case "direction" when ex.TransferDirection != null:
                v = v with { Intent = ex.TransferDirection == "out" ? VoucherIntent.TransferOut : VoucherIntent.TransferIn };
                s.Vouchers[q.VoucherIndex] = await ResolveTransferAccountsAsync(s.TenantId, v, ct);
                return true;
            case "intent" when ex.IntentHint != VoucherIntent.Unknown:
                v = v with { Intent = ex.IntentHint };
                if (IsTransfer(v.Intent)) v = await ResolveTransferAccountsAsync(s.TenantId, v, ct);
                s.Vouchers[q.VoucherIndex] = v;
                return true;
            case "category" when ex.Category != null:
                s.Vouchers[q.VoucherIndex] = v with { Category = ex.Category };
                return true;
            case "paymentMethod" when ex.PaymentMethod != null:
                s.Vouchers[q.VoucherIndex] = v with { PaymentMethod = ex.PaymentMethod };
                return true;
            default:
                return false;
        }
    }

    /// <summary>Đáp án gộp: điền opportunistic các slot còn trống (KHÔNG đè field đã có,
    /// KHÔNG đổi intent đã xác định, transfer không nhận PTTT). Ngoại lệ PTTT: parser default
    /// "cash" ngay cả khi user không nhắc → PTTT trích được từ CÂU TRẢ LỜI là chủ đích → đè.</summary>
    private static ParsedVoucher MergeOpportunistic(ParsedVoucher v, SlotExtraction ex) => v with
    {
        Amount = v.Amount ?? ex.Amount,
        DoiTuong = v.DoiTuong ?? ex.DoiTuong,
        PaymentMethod = IsTransfer(v.Intent) ? v.PaymentMethod : (ex.PaymentMethod ?? v.PaymentMethod),
        Category = v.Category ?? ex.Category,
        Date = ex.Date ?? v.Date,
        Intent = v.Intent == VoucherIntent.Unknown && ex.IntentHint != VoucherIntent.Unknown
            ? ex.IntentHint : v.Intent,
    };

    /// <summary>Cụm "không biết/chưa rõ" — KHÔNG được nuốt thành tên đối tượng.</summary>
    private static readonly HashSet<string> UnknownFillers = new(StringComparer.OrdinalIgnoreCase)
        { "không", "không biết", "không rõ", "chưa rõ", "không có", "không nhớ", "chưa biết",
          "tôi không biết", "ko biết", "k biết", "không chắc", "để sau", "không nhập" };

    /// <summary>Tên đối tượng từ câu trả lời trần ("Nguyễn Văn A") — chỉ khi câu không chứa
    /// số tiền/intent khác (tránh nuốt nhầm "5 triệu" thành tên).</summary>
    private static string? PlausibleName(string answer, SlotExtraction ex)
    {
        var t = answer.Trim().Trim('.', ',', ';', ':');
        if (t.Length is < 1 or > 60 || UnknownFillers.Contains(t)) return null;
        if (ex.Amount != null || ex.IntentHint != VoucherIntent.Unknown) return null;
        return Regex.IsMatch(t, @"\p{L}") ? t : null;
    }

    private async Task<ParsedVoucher> ResolveTransferAccountsAsync(TenantId tenantId, ParsedVoucher v, CancellationToken ct)
    {
        string bank = "112";
        if (bankAccounts != null)
        {
            try { bank = await bankAccounts.ResolveCashAccountCodeAsync(tenantId, "bank", ct); }
            catch (Exception ex) { logger?.LogWarning(ex, "G5 ResolveCashAccountCodeAsync failed — fallback 112"); }
        }
        return v.Intent == VoucherIntent.TransferOut
            ? v with { FromAccountCode = bank, ToAccountCode = "111" }
            : v with { FromAccountCode = "111", ToAccountCode = bank };
    }

    /// <summary>Tính lại MissingFields/Confidence sau merge — cùng rule với ParseAsync.</summary>
    private static ParsedVoucher Revalidate(ParsedVoucher v)
    {
        var missing = new List<string>();
        if (v.Amount == null) missing.Add("amount");
        if (v.Intent == VoucherIntent.Unknown) missing.Add("intent");
        if (NeedsDoiTuong(v.Intent) && string.IsNullOrWhiteSpace(v.DoiTuong)) missing.Add("đối tượng");
        if (IsTransfer(v.Intent) && (v.FromAccountCode == null || v.ToAccountCode == null)) missing.Add("tài khoản nguồn/đích");
        string confidence = missing.Count == 0 && v.Intent != VoucherIntent.Unknown ? "High"
            : v.Amount != null && v.Intent != VoucherIntent.Unknown ? "Medium" : "Low";
        // "hướng chưa rõ" chỉ giữ khi from/to vẫn không consistent — đáp án direction xóa nó.
        var warnings = v.Warnings.Where(w => !w.Contains("hướng chưa rõ", StringComparison.Ordinal)
                                             || !DirectionConsistent(v)).ToList();
        return v with { MissingFields = missing, Confidence = confidence, Warnings = warnings };
    }

    private static string SlotKey(int voucherIndex, string slot) => $"{voucherIndex}:{slot}";

    private static bool IsSkipText(string answer)
        => answer.Equals("bỏ qua", StringComparison.OrdinalIgnoreCase)
           || answer.Equals("skip", StringComparison.OrdinalIgnoreCase)
           || answer.Contains("tự nhập", StringComparison.OrdinalIgnoreCase);
}
