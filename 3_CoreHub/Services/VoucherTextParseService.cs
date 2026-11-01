using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using VanAn.Shared.Domain;

namespace VanAn.CoreHub.Services;

/// <summary>Loại nghiệp vụ suy ra từ câu mô tả (task PG — Smart Entry AI).</summary>
public enum VoucherIntent
{
    Unknown = 0,
    Revenue,            // thu doanh thu
    Receivable,         // ghi nhận phải thu (bán chịu)
    ReceivablePayment,  // thu tiền khách trả nợ
    Advance,            // khách ứng trước/đặt cọc → trả trước
    Capital,            // góp vốn chủ sở hữu (411)
    Expense,            // chi phí
    Payable,            // ghi nhận phải trả (mua chịu phi-hàng)
    PayablePayment,     // trả tiền người bán
    TransferOut,        // ngân hàng → quỹ (rút NH về quỹ — CK01)
    TransferIn,         // quỹ → ngân hàng (nộp quỹ vào NH — CK02)
    Purchase            // nhập hàng/mua hàng hóa → hint PurchaseReceipt
}

/// <summary>Kết quả trích xuất từ câu mô tả tự nhiên — prefill form, KHÔNG auto-save.</summary>
public sealed record ParsedVoucher(
    VoucherIntent Intent,
    decimal? Amount,
    DateTime Date,
    string? PaymentMethod,      // cash|bank|qr|other
    string? FromAccountCode,    // transfer: TK nguồn ("111"/"112")
    string? ToAccountCode,      // transfer: TK đích
    string? DoiTuong,           // đối tượng công nợ
    string? Category,           // expense category key (rent/utilities/...)
    decimal? FeeAmount,         // phí chuyển tiền
    string Description,         // câu gốc — dùng làm diễn giải
    string Confidence,          // High | Medium | Low
    IReadOnlyList<string> MissingFields,
    IReadOnlyList<string> Warnings);

public interface IVoucherTextParseService
{
    Task<ParsedVoucher> ParseAsync(TenantId tenantId, string text, CancellationToken ct = default);
}

/// <summary>
/// Task PG (Smart Entry AI): parser tiếng Việt rule-based deterministic — trích xuất
/// intent + số tiền + ngày + PTTT + đối tượng từ câu mô tả; route về đúng form.
/// KHÔNG đoán khi thiếu dữ liệu (SRS §18.4.1.5) → MissingFields để user bổ sung.
/// LLM fallback cắm sau qua interface này (flag) — v1 không external dependency.
/// </summary>
public sealed class VoucherTextParseService(
    IBankAccountService? bankAccounts = null,
    ILogger<VoucherTextParseService>? logger = null) : IVoucherTextParseService
{
    private static readonly Dictionary<string, decimal> NumWords = new(StringComparer.Ordinal)
    {
        ["không"] = 0, ["một"] = 1, ["mốt"] = 1, ["hai"] = 2, ["ba"] = 3,
        ["bốn"] = 4, ["tư"] = 4, ["năm"] = 5, ["lăm"] = 5, ["nhăm"] = 5,
        ["sáu"] = 6, ["bảy"] = 7, ["bẩy"] = 7, ["tám"] = 8, ["chín"] = 9,
    };

    private static readonly string[] CashMarkers = ["quỹ", "tiền mặt", "két"];
    private static readonly string[] BankMarkers = ["ngân hàng", "tài khoản", "tgnh", "tk ngân hàng"];
    private static readonly string[] ObjectMarkers =
        ["khách hàng", "khách", "anh", "chị", "cô", "chú", "ông", "bà", "em",
         "công ty", "cty", "nhà cung cấp", "ncc", "người bán", "bên", "đối tác", "nhân viên"];
    private static readonly string[] VendorMarkers = ["nhà cung cấp", "ncc", "người bán", "bên bán", "nhà cung"];

    private static readonly HashSet<string> DoiTuongStopWords = new(StringComparer.Ordinal)
    {
        "trả", "nợ", "thanh", "toán", "bằng", "vào", "về", "ra", "ngày", "hôm", "số",
        "tiền", "mặt", "chuyển", "khoản", "đồng", "vnđ", "vnd", "đ", "là", "cho", "của",
        "một", "hai", "ba", "bốn", "tư", "năm", "lăm", "nhăm", "sáu", "bảy", "bẩy",
        "tám", "chín", "mười", "mươi", "trăm", "nghìn", "ngàn", "triệu", "tr", "củ",
        "tỷ", "tỉ", "k", "lẻ", "lẽ", "linh", "rưỡi", "không", "mốt", "chục"
    };

    private static readonly (string[] Keys, string Category)[] ExpenseCategories =
    [
        (["thuê", "mặt bằng"], "rent"),
        (["điện", "nước", "internet", "điện thoại", "wifi"], "utilities"),
        (["lương", "nhân công", "thưởng", "tạm ứng"], "labor"),
        (["nguyên liệu", "nguyên vật liệu", "vật tư", "nvl"], "materials"),
        (["quảng cáo", "marketing", "ads", "khuyến mãi"], "marketing"),
        (["sửa", "bảo trì", "bảo dưỡng", "xăng", "vận chuyển", "ship"], "maintenance"),
    ];

    public async Task<ParsedVoucher> ParseAsync(TenantId tenantId, string text, CancellationToken ct = default)
    {
        var missing = new List<string>();
        var warnings = new List<string>();
        string original = (text ?? string.Empty).Trim();
        string norm = Normalize(original);
        if (norm.Length == 0)
        {
            return new ParsedVoucher(VoucherIntent.Unknown, null, DateTime.Today, null, null, null,
                null, null, null, original, "Low", ["text"], warnings);
        }

        // 1) Ngày — trích trước (tránh "ngày 25/10" bị đọc nhầm thành số tiền).
        DateTime date = ExtractDate(norm, out string dateConsumed);
        string work = dateConsumed;

        // 2) Số tiền — amount đầu tiên; số sau "phí" → FeeAmount.
        var amounts = ExtractAmounts(work);
        decimal? amount = amounts.Count > 0 ? amounts[0].Value : null;
        decimal? fee = null;
        for (int k = 1; k < amounts.Count; k++)
        {
            int pos = work.IndexOf(amounts[k].Raw, StringComparison.Ordinal);
            string before = pos > 12 ? work[..pos] : work;
            if (before.Contains("phí"))
            {
                fee = amounts[k].Value;
                break;
            }
        }
        if (amounts.Count > 1 && fee == null && amount < 10_000 && amounts[0].Bare)
        {
            warnings.Add("Số tiền nhỏ/bare — kiểm tra lại đơn vị (đồng?).");
        }

        // 3) Intent — thứ tự: transfer > công nợ > purchase > vốn/ứng > chi > thu.
        VoucherIntent intent = ClassifyIntent(norm, out string? fromAcc, out string? toAcc, warnings);

        // 4) PTTT — chỉ với non-transfer.
        string? paymentMethod = intent is VoucherIntent.TransferOut or VoucherIntent.TransferIn
            ? null : ExtractPaymentMethod(norm);

        // 5) Đối tượng (công nợ / advance).
        string? doiTuong = intent is VoucherIntent.Receivable or VoucherIntent.ReceivablePayment
            or VoucherIntent.Advance or VoucherIntent.Payable or VoucherIntent.PayablePayment
            ? ExtractDoiTuong(original, norm) : null;

        // 6) Category (expense).
        string? category = intent == VoucherIntent.Expense ? ExtractCategory(norm) : null;

        // 7) Bank account resolve cho transfer (TK 112x thật của tenant).
        if (intent is VoucherIntent.TransferOut or VoucherIntent.TransferIn && bankAccounts != null)
        {
            try
            {
                string bank112 = await bankAccounts.ResolveCashAccountCodeAsync(tenantId, "bank", ct);
                if (intent == VoucherIntent.TransferOut) { fromAcc ??= bank112; toAcc ??= "111"; }
                else { fromAcc ??= "111"; toAcc ??= bank112; }
            }
            catch (Exception ex)
            {
                logger?.LogWarning(ex, "ResolveCashAccountCodeAsync failed — fallback generic 112");
                if (intent == VoucherIntent.TransferOut) { fromAcc ??= "112"; toAcc ??= "111"; }
                else { fromAcc ??= "111"; toAcc ??= "112"; }
            }
        }

        // 8) Missing fields — không đoán (§18.4.1.5).
        if (amount == null) missing.Add("amount");
        if (intent == VoucherIntent.Unknown) missing.Add("intent");
        if (intent is VoucherIntent.Receivable or VoucherIntent.ReceivablePayment
            or VoucherIntent.Advance or VoucherIntent.Payable or VoucherIntent.PayablePayment
            && string.IsNullOrWhiteSpace(doiTuong))
        {
            missing.Add("đối tượng");
        }
        if (intent is VoucherIntent.TransferOut or VoucherIntent.TransferIn
            && (fromAcc == null || toAcc == null))
        {
            missing.Add("tài khoản nguồn/đích");
        }

        string confidence = missing.Count == 0 && intent != VoucherIntent.Unknown ? "High"
            : amount != null && intent != VoucherIntent.Unknown ? "Medium" : "Low";

        return new ParsedVoucher(intent, amount, date, paymentMethod, fromAcc, toAcc,
            doiTuong, category, fee, original, confidence, missing, warnings);
    }

    // ---------- intent ----------

    private static VoucherIntent ClassifyIntent(
        string norm, out string? fromAcc, out string? toAcc, List<string> warnings)
    {
        fromAcc = null; toAcc = null;
        bool hasCash = ContainsAny(norm, CashMarkers);
        bool hasBank = ContainsAny(norm, BankMarkers);
        bool isTransfer = (hasCash && hasBank)
            || norm.Contains("chuyển tiền nội bộ")
            || Regex.IsMatch(norm, @"\bchuyển\s+(từ\s+)?(quỹ|111|tk|tài khoản|ngân hàng)\b");

        if (isTransfer)
        {
            // Hướng: "về/nhập/vào quỹ" → bank→cash (TransferOut); "vào/nộp ... ngân hàng" → cash→bank (TransferIn).
            bool toCash = Regex.IsMatch(norm, @"(về|nhập|vào|bỏ vào|cho vào)\s+(quỹ|tiền mặt|két)");
            bool toBank = Regex.IsMatch(norm, @"(vào|nộp|gửi|sang)\s+(ngân hàng|tài khoản|tgnh)");
            bool withdraw = norm.Contains("rút") || norm.Contains("lấy tiền") || norm.Contains("rút tiền");

            if (toCash && !toBank) { fromAcc = "112"; toAcc = "111"; return VoucherIntent.TransferOut; }
            if (toBank && !toCash) { fromAcc = "111"; toAcc = "112"; return VoucherIntent.TransferIn; }
            if (withdraw && hasBank && hasCash) { fromAcc = "112"; toAcc = "111"; return VoucherIntent.TransferOut; }
            if ((norm.Contains("nộp") || norm.Contains("gửi")) && hasBank) { fromAcc = "111"; toAcc = "112"; return VoucherIntent.TransferIn; }
            // Hai bên đều nhắc nhưng hướng mơ hồ — chọn theo marker nào đứng trước "sang/về".
            warnings.Add("Chuyển tiền nhưng hướng chưa rõ — kiểm tra lại TK nguồn/đích.");
            fromAcc = hasCash && norm.IndexOf("quỹ", StringComparison.Ordinal) < norm.IndexOf("ngân hàng", StringComparison.Ordinal) ? "111" : "112";
            toAcc = fromAcc == "111" ? "112" : "111";
            return VoucherIntent.TransferIn;
        }

        // Công nợ — phân biệt receivable (khách) vs payable (NCC) theo object marker.
        bool vendor = ContainsAny(norm, VendorMarkers);
        bool khach = norm.Contains("khách") || norm.Contains("khách hàng");
        bool traNo = norm.Contains("trả nợ") || norm.Contains("thanh toán nợ") || norm.Contains("trả góp");
        bool thuNo = norm.Contains("thu nợ") || norm.Contains("đòi nợ") || norm.Contains("trả tiền nợ");
        bool banChiu = norm.Contains("bán chịu") || norm.Contains("cho nợ") || norm.Contains("ghi nợ")
            || norm.Contains("khách nợ");
        bool muaChiu = norm.Contains("mua chịu") || norm.Contains("nợ người bán") || norm.Contains("nợ ncc")
            || norm.Contains("nợ nhà cung") || norm.Contains("chưa thanh toán") || norm.Contains("chưa trả tiền");

        // Payment trước: "trả nợ NCC" chứa "nợ nhà cung" nhưng là trả nợ, không phải mua chịu.
        if (vendor && (traNo || norm.Contains("trả tiền") || norm.Contains("thanh toán")))
            return VoucherIntent.PayablePayment;
        if (muaChiu || (norm.Contains("nợ") && vendor)) return VoucherIntent.Payable;
        if (khach && (traNo || thuNo || norm.Contains("trả tiền"))) return VoucherIntent.ReceivablePayment;
        if (banChiu) return VoucherIntent.Receivable;
        if (traNo || thuNo)
        {
            warnings.Add("Trả nợ nhưng chưa rõ khách hàng hay nhà cung cấp — kiểm tra lại loại phiếu.");
            return khach ? VoucherIntent.ReceivablePayment : VoucherIntent.PayablePayment;
        }

        // Purchase hint — nhập hàng hóa → PurchaseReceipt, không fill phiếu chi.
        if (norm.Contains("nhập hàng") || norm.Contains("nhập kho") || norm.Contains("mua hàng hóa"))
            return VoucherIntent.Purchase;

        // Vốn / ứng trước / vay.
        if (norm.Contains("góp vốn") || norm.Contains("vốn chủ") || norm.Contains("bổ sung vốn")
            || norm.Contains("góp thêm vốn")) return VoucherIntent.Capital;
        if (norm.Contains("ứng trước") || norm.Contains("đặt cọc") || norm.Contains("nhận cọc")
            || norm.Contains("cọc trước"))
        {
            warnings.Add("Khách ứng trước/cọc → ghi nhận trả trước (không phải doanh thu).");
            return VoucherIntent.Advance;
        }

        // Chi — keyword trước thu để "chi trả" không rơi vào thu.
        if (norm.Contains("chi ") || norm.StartsWith("chi") || norm.Contains("trả tiền")
            || norm.Contains("nộp tiền") || norm.Contains("nộp thuế") || norm.Contains("tạm ứng")
            || norm.Contains("hoàn tiền") || norm.Contains("mua văn phòng")
            || norm.Contains("thanh toán tiền") || norm.Contains("trả lương")
            || norm.Contains("tiền thuê") || norm.Contains("tiền điện") || norm.Contains("tiền nước"))
            return VoucherIntent.Expense;

        // Thu — default khi có động từ thu/bán/nhận.
        if (norm.Contains("thu ") || norm.StartsWith("thu") || norm.Contains("bán")
            || norm.Contains("doanh thu") || norm.Contains("nhận tiền") || norm.Contains("thu tiền"))
            return VoucherIntent.Revenue;

        return VoucherIntent.Unknown;
    }

    // ---------- extraction helpers ----------

    private static string ExtractPaymentMethod(string norm)
    {
        if (norm.Contains("qr") || norm.Contains("momo") || norm.Contains("zalopay") || norm.Contains("ví "))
            return "qr";
        if (norm.Contains("chuyển khoản") || norm.Contains("ngân hàng") || norm.Contains("tài khoản")
            || norm.Contains("ck ") || norm.EndsWith(" ck"))
            return "bank";
        return "cash"; // "tiền mặt/quỹ" hoặc không nhắc → tiền mặt (safe default của form)
    }

    private static string? ExtractCategory(string norm)
    {
        foreach (var (keys, cat) in ExpenseCategories)
        {
            if (ContainsAny(norm, keys)) return cat;
        }
        return null;
    }

    /// <summary>Đối tượng: cụm danh từ sau marker ("khách anh Hùng", "công ty TNHH ABC") — stop tại stop-word/động từ/số.</summary>
    private static string? ExtractDoiTuong(string original, string norm)
    {
        int best = -1; string? bestMarker = null;
        foreach (var m in ObjectMarkers.OrderByDescending(x => x.Length))
        {
            int idx = norm.IndexOf(m + " ", StringComparison.Ordinal);
            if (idx >= 0 && (best < 0 || idx < best)) { best = idx + m.Length + 1; bestMarker = m; }
        }
        // "công ty"/"nhà cung cấp" đứng đầu câu
        if (best < 0)
        {
            foreach (var m in ObjectMarkers.OrderByDescending(x => x.Length))
            {
                if (norm.StartsWith(m + " ")) { best = m.Length + 1; bestMarker = m; break; }
            }
        }
        if (best < 0) return null;

        // Lấy tối đa 6 token sau marker trong chuỗi GỐC (giữ hoa/thường).
        var normTokens = norm.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var origTokens = original.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        // Tìm vị trí token bắt đầu cụm đối tượng trong norm → map sang original.
        int startNormIdx = norm[..Math.Min(best, norm.Length)].Split(' ', StringSplitOptions.RemoveEmptyEntries).Length;
        var collected = new List<string>();
        for (int i = startNormIdx; i < normTokens.Length && collected.Count < 6; i++)
        {
            string w = normTokens[i].Trim(',', '.', ';', ':');
            if (w.Length == 0) continue;
            if (DoiTuongStopWords.Contains(w) || char.IsDigit(w[0])) break;
            collected.Add(i < origTokens.Length ? origTokens[i].Trim(',', '.', ';', ':') : w);
        }
        if (collected.Count == 0) return null;
        // Entity-marker (công ty/NCC/người bán) giữ prefix cho rõ nghĩa; person-marker (khách/anh/chị) bỏ.
        string result = string.Join(' ', collected).Trim();
        if (bestMarker is "công ty" or "cty" or "nhà cung cấp" or "ncc" or "người bán" or "đối tác")
        {
            result = bestMarker + " " + result;
        }
        return result.Length > 60 ? result[..60] : result;
    }

    private static DateTime ExtractDate(string norm, out string consumed)
    {
        DateTime today = DateTime.Today;
        var m = Regex.Match(norm, @"ngày\s+(\d{1,2})[/-](\d{1,2})(?:[/-](\d{2,4}))?");
        if (m.Success)
        {
            int d = int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture);
            int mo = int.Parse(m.Groups[2].Value, CultureInfo.InvariantCulture);
            int y = m.Groups[3].Success
                ? (m.Groups[3].Value.Length == 2 ? 2000 + int.Parse(m.Groups[3].Value) : int.Parse(m.Groups[3].Value))
                : today.Year;
            consumed = norm.Remove(m.Index, m.Length);
            try { return new DateTime(y, mo, d); } catch { return today; }
        }
        if (norm.Contains("hôm kia") || norm.Contains("hôm kía"))
        {
            consumed = norm.Replace("hôm kia", "").Replace("hôm kía", "");
            return today.AddDays(-2);
        }
        if (norm.Contains("hôm qua") || norm.Contains("tối qua"))
        {
            consumed = norm.Replace("hôm qua", "").Replace("tối qua", "");
            return today.AddDays(-1);
        }
        consumed = norm; // hôm nay / không nhắc → hôm nay
        return today;
    }

    private sealed record AmountHit(decimal Value, string Raw, bool Bare);

    /// <summary>Tìm mọi cụm số tiền (digits có unit, word-number chain) theo thứ tự xuất hiện.</summary>
    private static List<AmountHit> ExtractAmounts(string text)
    {
        // Pre-pass: "1.234.567"/"1,234,567" → "1234567" ; "1,5" → "1.5" ; "2tr5"/"100k" → "2 tr 5"/"100 k".
        string work = Regex.Replace(text, @"\b(\d{1,3}([.,]\d{3})+)\b", m => m.Groups[1].Value.Replace(".", "").Replace(",", ""));
        work = Regex.Replace(work, @"\b(\d+),(\d{1,2})\b", "$1.$2");
        work = Regex.Replace(work, @"(\d+(?:\.\d+)?)\s*(triệu|trieu|tỷ|tỉ|nghìn|ngàn|củ|tr|k)\s*(\d+)?", "$1 $2 $3", RegexOptions.IgnoreCase);

        var tokens = work.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var hits = new List<AmountHit>();
        int i = 0;
        while (i < tokens.Length)
        {
            if (IsNumberStart(tokens[i]))
            {
                var (value, consumed, bare) = ParseAmountAt(tokens, i);
                if (consumed > 0 && value > 0)
                {
                    hits.Add(new AmountHit(value, string.Join(' ', tokens.Skip(i).Take(consumed)), bare));
                    i += consumed;
                    continue;
                }
            }
            i++;
        }
        return hits;
    }

    private static bool IsNumberStart(string w)
        => NumWords.ContainsKey(w) || decimal.TryParse(w, NumberStyles.Number, CultureInfo.InvariantCulture, out _)
           || w is "mười" or "mươi" or "chục" or "trăm" or "nghìn" or "ngàn" or "triệu" or "tỷ" or "tỉ" or "củ";

    private static (decimal Value, int Consumed, bool Bare) ParseAmountAt(string[] t, int start)
    {
        decimal total = 0, group = 0, factor = 1, lastUnit = 1;
        bool hasVal = false, bare = true;
        int i = start;
        while (i < t.Length)
        {
            string w = t[i].ToLowerInvariant().Trim(',', '.', ';', ':', ')');
            if (w.Length == 0) { i++; continue; }
            string? next = i + 1 < t.Length ? t[i + 1].ToLowerInvariant().Trim(',', '.', ';', ':') : null;

            if (NumWords.TryGetValue(w, out decimal nw) || decimal.TryParse(w, NumberStyles.Number, CultureInfo.InvariantCulture, out nw))
            {
                if (next is "mươi" or "mười" or "chục") { group += nw * 10; lastUnit = 10; i += 2; }
                else if (next == "trăm") { group += nw * 100; lastUnit = 100; i += 2; factor = 10; }
                else { group += nw * factor; i += 1; }
                factor = next is "mươi" or "mười" or "chục" ? 1 : factor;
                hasVal = true;
                continue;
            }
            if (w is "mươi" or "mười" or "chục") { group = group == 0 ? 10 : group * 10; lastUnit = 10; factor = 1; i++; hasVal = true; bare = false; continue; }
            if (w == "trăm") { group = group == 0 ? 100 : group * 100; lastUnit = 100; factor = 10; i++; hasVal = true; bare = false; continue; }
            if (w is "lẻ" or "lẽ" or "linh") { factor = 1; i++; continue; }
            if (w == "rưỡi") { group += 0.5m * lastUnit; i++; hasVal = true; continue; }
            if (w is "nghìn" or "ngàn" or "nghin" or "k") { total += (group == 0 ? 1 : group) * 1_000m; group = 0; lastUnit = 1_000m; factor = 100; i++; hasVal = true; bare = false; continue; }
            if (w is "triệu" or "trieu" or "củ" or "tr") { total += (group == 0 ? 1 : group) * 1_000_000m; group = 0; lastUnit = 1_000_000m; factor = 100_000m; i++; hasVal = true; bare = false; continue; }
            if (w is "tỷ" or "tỉ") { total += (group == 0 ? 1 : group) * 1_000_000_000m; group = 0; lastUnit = 1_000_000_000m; factor = 100_000_000m; i++; hasVal = true; bare = false; continue; }
            break;
        }
        return (total + group, hasVal ? i - start : 0, bare);
    }

    private static bool ContainsAny(string norm, IEnumerable<string> markers)
        => markers.Any(m => norm.Contains(m, StringComparison.Ordinal));

    private static string Normalize(string s)
    {
        s = s.Trim().ToLowerInvariant();
        s = Regex.Replace(s, @"\s+", " ");
        return s;
    }
}
