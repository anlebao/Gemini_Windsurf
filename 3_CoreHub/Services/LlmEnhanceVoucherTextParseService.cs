using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using VanAn.Shared.Domain;
using VanAn.Shared.Services;

namespace VanAn.CoreHub.Services;

/// <summary>
/// Task G2 (Smart Entry LLM Hybrid) — cấu hình tầng LLM fallback.
/// Config section "SmartEntryLlm" (env: SmartEntryLlm__ApiKey, SmartEntryLlm__Model, ...).
/// Model mặc định: Gemini 3.8 Flash — free tier, structured output, tiếng Việt tốt.
/// </summary>
public sealed class SmartEntryLlmOptions
{
    public const string Section = "SmartEntryLlm";

    /// <summary>Google AI Studio API key (KHÔNG commit, không echo). Rỗng = LLM tắt hoàn toàn.</summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>Model mặc định gemini-3.5-flash-lite (free tier, ~1s, không thinking — đủ cho trích xuất 1 câu).
    /// 3.8-flash từng mất 26s + 503 trên VPS (RV 2026-11-01) → không dùng làm mặc định.</summary>
    public string Model { get; set; } = "gemini-3.5-flash-lite";

    public string Endpoint { get; set; } = "https://generativelanguage.googleapis.com/v1beta";

    /// <summary>Flash thinking model tạo JSON có thể mất 15-40s — mặc định 30s (8s từng gây timeout RV).</summary>
    public int TimeoutSeconds { get; set; } = 30;
}

/// <summary>
/// Task G2 — decorator IVoucherTextParseService: rule-based LUÔN chạy trước (deterministic, tested,
/// offline, privacy 100%). Chỉ gọi Gemini API khi kết quả Unknown / Low / thiếu field (đuôi dài:
/// "thanh toán ... cho X", "tuần trước", tên trùng số, tiếng lóng shop...). Kết quả LLM qua
/// schema validation rồi MERGE — rule-based High KHÔNG bị đè. Không auto-save: user vẫn Lưu tay.
/// Gate: flag per-tenant SmartEntryLlmEnabled + ApiKey rỗng = tắt (hành vi y hệt rule-based).
/// </summary>
public sealed class LlmEnhanceVoucherTextParseService(
    VoucherTextParseService ruleBased,
    IHttpClientFactory httpClientFactory,
    IOptions<SmartEntryLlmOptions> options,
    IShopFeatureSettingsService featureSettings,
    ILogger<LlmEnhanceVoucherTextParseService> logger) : IVoucherTextParseService
{
    private const string LlmEnabledFlag = "SmartEntryLlmEnabled";
    private const decimal MaxAmount = 1_000_000_000_000m; // 1.000 tỷ — chặn LLM bịa số phi lý

    private static readonly Dictionary<string, VoucherIntent> IntentMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["revenue"] = VoucherIntent.Revenue,
        ["receivable"] = VoucherIntent.Receivable,
        ["receivablepayment"] = VoucherIntent.ReceivablePayment,
        ["receivable-payment"] = VoucherIntent.ReceivablePayment,
        ["advance"] = VoucherIntent.Advance,
        ["capital"] = VoucherIntent.Capital,
        ["expense"] = VoucherIntent.Expense,
        ["payable"] = VoucherIntent.Payable,
        ["payablepayment"] = VoucherIntent.PayablePayment,
        ["payable-payment"] = VoucherIntent.PayablePayment,
        ["transferout"] = VoucherIntent.TransferOut,
        ["transfer-out"] = VoucherIntent.TransferOut,
        ["transferin"] = VoucherIntent.TransferIn,
        ["transfer-in"] = VoucherIntent.TransferIn,
        ["purchase"] = VoucherIntent.Purchase,
        // G4: alias tiếng Việt — LLM thật hay trả nhãn tiếng Việt thay vì enum
        ["doanh thu"] = VoucherIntent.Revenue,
        ["thu tiền"] = VoucherIntent.Revenue,
        ["thu"] = VoucherIntent.Revenue,
        ["bán hàng"] = VoucherIntent.Revenue,
        ["bán chịu"] = VoucherIntent.Receivable,
        ["thu nợ"] = VoucherIntent.ReceivablePayment,
        ["khách trả nợ"] = VoucherIntent.ReceivablePayment,
        ["đặt cọc"] = VoucherIntent.Advance,
        ["ứng trước"] = VoucherIntent.Advance,
        ["góp vốn"] = VoucherIntent.Capital,
        ["chi phí"] = VoucherIntent.Expense,
        ["chi"] = VoucherIntent.Expense,
        ["trả tiền"] = VoucherIntent.Expense,
        ["mua chịu"] = VoucherIntent.Payable,
        ["trả nợ nhà cung cấp"] = VoucherIntent.PayablePayment,
        ["trả người bán"] = VoucherIntent.PayablePayment,
        ["chuyển tiền quỹ sang ngân hàng"] = VoucherIntent.TransferIn,
        ["gửi ngân hàng"] = VoucherIntent.TransferIn,
        ["nộp vào ngân hàng"] = VoucherIntent.TransferIn,
        ["chuyển tiền ngân hàng về quỹ"] = VoucherIntent.TransferOut,
        ["rút về quỹ"] = VoucherIntent.TransferOut,
        ["nhập hàng"] = VoucherIntent.Purchase,
        ["mua hàng hóa"] = VoucherIntent.Purchase,
        // PH2: vay / trả nợ vay
        ["vay"] = VoucherIntent.Loan,
        ["vay nhận tiền"] = VoucherIntent.Loan,
        ["vay vốn"] = VoucherIntent.Loan,
        ["nhận vay"] = VoucherIntent.Loan,
        ["trả nợ vay"] = VoucherIntent.LoanRepayment,
        ["trả tiền vay"] = VoucherIntent.LoanRepayment,
        ["hoàn trả vay"] = VoucherIntent.LoanRepayment,
        // PH3: CTGS + khấu hao + ký quỹ
        ["kết chuyển"] = VoucherIntent.JournalAdjustment,
        ["phân bổ"] = VoucherIntent.JournalAdjustment,
        ["điều chỉnh"] = VoucherIntent.JournalAdjustment,
        ["chứng từ ghi sổ"] = VoucherIntent.JournalAdjustment,
        ["ctgs"] = VoucherIntent.JournalAdjustment,
        ["khấu hao"] = VoucherIntent.Depreciation,
        ["trích khấu hao"] = VoucherIntent.Depreciation,
        ["ký quỹ"] = VoucherIntent.Deposit,
        ["ký cược"] = VoucherIntent.Deposit,
        [""] = VoucherIntent.Unknown,
        ["null"] = VoucherIntent.Unknown,
    };

    private static readonly HashSet<string> PaymentMethods = ["cash", "bank", "qr", "other"];
    private static readonly HashSet<string> Categories = ["rent", "utilities", "labor", "materials", "marketing", "maintenance"];
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };

    public async Task<ParsedVoucher> ParseAsync(TenantId tenantId, string text, CancellationToken ct = default, bool forceLlm = false)
    {
        var baseResult = await ruleBased.ParseAsync(tenantId, text, ct);

        // Trigger hẹp: chỉ đuôi dài mà rule-based không chắc chắn — trừ khi user bấm "AI phân tích" (force).
        bool needsLlm = forceLlm
            || baseResult.Intent == VoucherIntent.Unknown
            || string.Equals(baseResult.Confidence, "Low", StringComparison.Ordinal)
            || baseResult.MissingFields.Count > 0;
        if (!needsLlm) return baseResult;
        if (string.IsNullOrWhiteSpace(options.Value.ApiKey)) return baseResult;
        if (!await featureSettings.IsEnabledAsync(tenantId.Value, LlmEnabledFlag, ct)) return baseResult;

        try
        {
            var llm = await CallGeminiAsync(text, ct);
            return llm == null ? baseResult : Merge(baseResult, llm, forceLlm);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "SmartEntry LLM call failed — fallback rule-based");
            return baseResult with
            {
                Warnings = baseResult.Warnings.Append("LLM tạm lỗi — đang dùng kết quả phân tích thường.").ToList()
            };
        }
    }

    // ---------- Gemini API ----------

    private async Task<LlmResponse?> CallGeminiAsync(string text, CancellationToken ct)
    {
        var opt = options.Value;
        string url = $"{opt.Endpoint.TrimEnd('/')}/models/{opt.Model}:generateContent?key={opt.ApiKey}";
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(Math.Clamp(opt.TimeoutSeconds, 3, 30)));

        var payload = new
        {
            contents = new[] { new { parts = new[] { new { text = BuildPrompt(text) } } } },
            generationConfig = new
            {
                temperature = 0.1,
                responseMimeType = "application/json",
            },
        };

        using var client = httpClientFactory.CreateClient("smart-entry-llm");
        using var resp = await client.PostAsJsonAsync(url, payload, timeout.Token);
        resp.EnsureSuccessStatusCode();
        await using var stream = await resp.Content.ReadAsStreamAsync(timeout.Token);
        using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: timeout.Token);

        string? textPart = null;
        if (doc.RootElement.TryGetProperty("candidates", out var cands) && cands.GetArrayLength() > 0
            && cands[0].TryGetProperty("content", out var content)
            && content.TryGetProperty("parts", out var parts) && parts.GetArrayLength() > 0
            && parts[0].TryGetProperty("text", out var t))
        {
            textPart = t.GetString();
        }
        if (string.IsNullOrWhiteSpace(textPart)) return null;

        string json = textPart.Trim();
        if (json.StartsWith("```", StringComparison.Ordinal))
        {
            int nl = json.IndexOf('\n');
            json = nl >= 0 ? json[(nl + 1)..] : json[3..];
        }
        json = json.Trim().TrimEnd('`').Trim();
        if (json.Length == 0) return null;
        try { return JsonSerializer.Deserialize<LlmResponse>(json, JsonOpts); }
        catch (JsonException) { return null; }
    }

    private static string BuildPrompt(string text) =>
        "Bạn là trợ lý nhập liệu kế toán tiếng Việt. Từ câu mô tả nghiệp vụ sau, trả về JSON ĐÚNG schema, KHÔNG thêm chú thích, KHÔNG bịa dữ kiện.\n" +
        "Schema: {\"intent\": \"Revenue|Receivable|ReceivablePayment|Advance|Capital|Expense|Payable|PayablePayment|TransferOut|TransferIn|Purchase|Loan|LoanRepayment|JournalAdjustment|Depreciation|Deposit|null\", " +
        "\"amount\": <số nguyên đồng hoặc null nếu không chắc>, \"date\": \"yyyy-MM-dd hoặc null\", " +
        "\"paymentMethod\": \"cash|bank|qr|null\", \"fromAccountCode\": \"111|112|null\", \"toAccountCode\": \"111|112|null\", " +
        "\"doiTuong\": \"tên khách/công ty hoặc null\", \"category\": \"rent|utilities|labor|materials|marketing|maintenance|null\", " +
        "\"feeAmount\": <số nguyên hoặc null>}.\n" +
        "Lưu ý: \"khách trả tiền\" = thu nợ (ReceivablePayment); \"trả nợ nhà cung cấp\" = PayablePayment; " +
        "\"rút tiền ngân hàng về quỹ\" = TransferOut (112→111); \"nộp tiền mặt vào ngân hàng\" = TransferIn (111→112); " +
        "góp vốn = Capital; đặt cọc = Advance; \"mua hàng hóa/nhập kho\" = Purchase; doanh thu bán hàng = Revenue.\n" +
        $"Câu mô tả: \"{text}\"";

    // ---------- G4: multi-voucher ----------

    private static readonly string[] MultiMarkers = [",", "và", "sau đó", "rồi", "còn lại", "đồng thời", ";", " mang ", " đem ", "mang ", "đem "];

    /// <summary>G4: 1 câu → 1-N phiếu. Chỉ gọi LLM multi khi câu có marker nhiều nghiệp vụ
    /// + flag ON + key; ngược lại trả [ParseAsync] (hành vi cũ). Max 3 phiếu, validate từng phiếu.</summary>
    public async Task<IReadOnlyList<ParsedVoucher>> ParseMultipleAsync(TenantId tenantId, string text, CancellationToken ct = default, bool forceLlm = false)
    {
        var single = await ParseAsync(tenantId, text, ct, forceLlm);
        if (string.IsNullOrWhiteSpace(options.Value.ApiKey)
            || !await featureSettings.IsEnabledAsync(tenantId.Value, LlmEnabledFlag, ct)
            || !LooksMulti(text))
        {
            return [single];
        }

        try
        {
            var llm = await CallGeminiMultiAsync(text, ct);
            if (llm is null or { Count: 0 })
            {
                logger.LogWarning("SmartEntry LLM multi trả rỗng — fallback single. Text: {Text}", Truncate(text, 120));
                return [single];
            }
            var vouchers = llm
                .Where(ValidateMultiVoucher)
                .Select(v => SingleToVoucher(single, v))
                .Take(3)
                .ToList();
            if (vouchers.Count == 0)
            {
                logger.LogWarning("SmartEntry LLM multi: {Count} phiếu nhưng 0 hợp lệ — fallback single. Intents: {Intents}",
                    llm.Count, string.Join(",", llm.Select(v => v.Intent ?? "?")));
                return [single];
            }
            return vouchers;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "SmartEntry LLM multi failed — fallback single");
            return [single];
        }
    }

    private static string Truncate(string s, int max)
        => s.Length <= max ? s : s[..max] + "...";

    private static bool LooksMulti(string text)
        => MultiMarkers.Any(m => text.Contains(m, StringComparison.OrdinalIgnoreCase))
           && Regex.Matches(text.ToLowerInvariant(), @"\d[\d.,]*\s*(nghìn|ngàn|triệu|tỷ|tỉ|củ|tr|k)?", RegexOptions.None).Count >= 2;

    private static bool ValidateMultiVoucher(MultiVoucherDto v)
        => ResolveIntent(v.Intent) != VoucherIntent.Unknown
           && v.Amount is > 0m and <= MaxAmount
           && (v.Date == null || (TryParseLlmDate(v.Date, out var d) && (d - DateTime.Today).TotalDays <= 0));

    /// <summary>G4: resolve intent — exact key trước, rồi Contains theo alias (LLM thật hay trả
    /// nhãn tiếng Việt "thu bán hàng"/"chi phí điện"). Alias ngắn (<4 ký tự) bỏ qua trong Contains
    /// để tránh "thu" khớp nhầm "thuế", "chi" khớp nhầm "chiết khấu".</summary>
    private static VoucherIntent ResolveIntent(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return VoucherIntent.Unknown;
        var key = raw.Trim();
        if (IntentMap.TryGetValue(key, out var exact)) return exact;
        foreach (var (alias, intent) in IntentMap.OrderByDescending(kv => kv.Key.Length))
        {
            if (alias.Length < 4) continue;
            if (key.Contains(alias, StringComparison.OrdinalIgnoreCase)) return intent;
        }
        return VoucherIntent.Unknown;
    }

    private ParsedVoucher SingleToVoucher(ParsedVoucher baseResult, MultiVoucherDto v)
    {
        var intent = ResolveIntent(v.Intent);
        if (intent == VoucherIntent.Unknown) intent = baseResult.Intent;
        var missing = new List<string>();
        if (v.Amount == null) missing.Add("amount");
        if (intent == VoucherIntent.Unknown) missing.Add("intent");
        if (intent is VoucherIntent.Receivable or VoucherIntent.ReceivablePayment or VoucherIntent.Advance
                or VoucherIntent.Payable or VoucherIntent.PayablePayment && string.IsNullOrWhiteSpace(v.DoiTuong))
            missing.Add("đối tượng");
        var date = v.Date != null && TryParseLlmDate(v.Date, out var d) && (d - DateTime.Today).TotalDays <= 0
            ? d : baseResult.Date;
        string confidence = missing.Count == 0 ? "High" : v.Amount != null ? "Medium" : "Low";
        var warnings = new List<string> { "Đề xuất từ AI — kiểm tra lại trước khi lưu." };
        return new ParsedVoucher(intent, v.Amount, date, NormalizePayment(v.PaymentMethod), NormalizeAccount(v.FromAccountCode),
            NormalizeAccount(v.ToAccountCode), string.IsNullOrWhiteSpace(v.DoiTuong) ? null : v.DoiTuong.Trim(),
            v.Category != null && Categories.Contains(v.Category) ? v.Category : null,
            v.FeeAmount is > 0m ? v.FeeAmount : null, v.Description ?? baseResult.Description, confidence, missing, warnings);
    }

    private static string? NormalizePayment(string? pm)
        => pm != null && PaymentMethods.Contains(pm) ? pm : null;

    private async Task<List<MultiVoucherDto>?> CallGeminiMultiAsync(string text, CancellationToken ct)
    {
        var opt = options.Value;
        string url = $"{opt.Endpoint.TrimEnd('/')}/models/{opt.Model}:generateContent?key={opt.ApiKey}";
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(Math.Clamp(opt.TimeoutSeconds, 3, 30)));
        var payload = new
        {
            contents = new[] { new { parts = new[] { new { text = BuildMultiPrompt(text) } } } },
            generationConfig = new { temperature = 0.1, responseMimeType = "application/json" },
        };
        using var client = httpClientFactory.CreateClient("smart-entry-llm");
        using var resp = await client.PostAsJsonAsync(url, payload, timeout.Token);
        resp.EnsureSuccessStatusCode();
        await using var stream = await resp.Content.ReadAsStreamAsync(timeout.Token);
        using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: timeout.Token);
        string? textPart = null;
        if (doc.RootElement.TryGetProperty("candidates", out var cands) && cands.GetArrayLength() > 0
            && cands[0].TryGetProperty("content", out var content)
            && content.TryGetProperty("parts", out var parts) && parts.GetArrayLength() > 0
            && parts[0].TryGetProperty("text", out var t))
        {
            textPart = t.GetString();
        }
        if (string.IsNullOrWhiteSpace(textPart)) return null;
        string json = textPart.Trim();
        if (json.StartsWith("```", StringComparison.Ordinal))
        {
            int nl = json.IndexOf('\n');
            json = nl >= 0 ? json[(nl + 1)..] : json[3..];
        }
        json = json.Trim().TrimEnd('`').Trim();
        if (json.Length == 0) return null;
        try
        {
            var wrapper = JsonSerializer.Deserialize<MultiWrapper>(json, JsonOpts);
            return wrapper?.Vouchers ?? null;
        }
        catch (JsonException) { return null; }
    }

    private static string BuildMultiPrompt(string text) =>
        "Bạn là trợ lý nhập liệu kế toán tiếng Việt. Câu sau có thể chứa NHIỀU nghiệp vụ kế toán. " +
        "Hãy tách thành các nghiệp vụ RIÊNG BIỆT, mỗi nghiệp vụ 1 phiếu chứng từ. Trả JSON ĐÚNG schema, KHÔNG bịa dữ kiện.\n" +
        "Schema: {\"vouchers\": [{\"intent\": \"Revenue|Receivable|ReceivablePayment|Advance|Capital|Expense|Payable|PayablePayment|TransferOut|TransferIn|Purchase|Loan|LoanRepayment|JournalAdjustment|Depreciation|Deposit|null\", " +
        "\"amount\": <số nguyên đồng hoặc null>, \"date\": \"yyyy-MM-dd hoặc null\", " +
        "\"paymentMethod\": \"cash|bank|qr|null\", \"fromAccountCode\": \"111|112|null\", \"toAccountCode\": \"111|112|null\", " +
        "\"doiTuong\": \"tên hoặc null\", \"category\": \"rent|utilities|labor|materials|marketing|maintenance|null\", " +
        "\"feeAmount\": <số nguyên hoặc null>, \"description\": \"mô tả nghiệp vụ này\"}]}.\n" +
        "Lưu ý: chuyển tiền nội bộ là 1 phiếu riêng (TransferIn 111→112 / TransferOut 112→111); " +
        "\"mang X gửi ngân hàng\"/\"gửi X vào tài khoản\"/\"nộp X vào ngân hàng\" = TransferIn (quỹ→NH), KHÔNG phải doanh thu; " +
        "\"rút X về quỹ\" = TransferOut; " +
        "\"thanh lý tài sản cố định\" = thu tiền (Revenue hoặc nếu có bán chịu thì Receivable) — ghi giảm TSCĐ là bút toán CTGS, không tách ở đây; " +
        "\"còn lại\"/\"phần còn lại\" không phải phiếu riêng nếu chỉ là mô tả cách nhận tiền. Tối đa 3 phiếu.\n" +
        $"Câu mô tả: \"{text}\"";

    private sealed class MultiWrapper
    {
        public List<MultiVoucherDto>? Vouchers { get; set; }
    }

    private sealed class MultiVoucherDto
    {
        public string? Intent { get; set; }
        public decimal? Amount { get; set; }
        public string? Date { get; set; }
        public string? PaymentMethod { get; set; }
        public string? FromAccountCode { get; set; }
        public string? ToAccountCode { get; set; }
        public string? DoiTuong { get; set; }
        public string? Category { get; set; }
        public decimal? FeeAmount { get; set; }
        public string? Description { get; set; }
    }

    // ---------- merge (rule-based High thắng) ----------

    private ParsedVoucher Merge(ParsedVoucher b, LlmResponse llm, bool forceLlm = false)
    {
        var warnings = b.Warnings.ToList();
        var missing = b.MissingFields.ToList();
        bool merged = false;

        VoucherIntent intent = b.Intent;
        if (intent == VoucherIntent.Unknown && IntentMap.TryGetValue(llm.Intent ?? string.Empty, out var li))
        {
            intent = li;
            merged = true;
        }

        decimal? amount = b.Amount;
        if (amount == null && llm.Amount is > 0m and <= MaxAmount)
        {
            amount = llm.Amount;
            merged = true;
        }

        DateTime date = b.Date;
        if (llm.Date != null && TryParseLlmDate(llm.Date, out var d))
        {
            // LLM chỉ được lùi ngày trong 3 ngày gần nhất (hôm qua/kia...), không nhận ngày tương lai/xa.
            double diff = (d - b.Date).TotalDays;
            if (diff is <= 0 and >= -3)
            {
                date = d;
                merged = true;
            }
        }

        string? doiTuong = b.DoiTuong;
        if (doiTuong == null && !string.IsNullOrWhiteSpace(llm.DoiTuong))
        {
            doiTuong = llm.DoiTuong.Trim();
            if (doiTuong.Length > 60) doiTuong = doiTuong[..60];
            merged = true;
        }

        string? category = b.Category;
        if (category == null && llm.Category != null && Categories.Contains(llm.Category))
        {
            category = llm.Category;
            merged = true;
        }

        string? from = b.FromAccountCode, to = b.ToAccountCode;
        if (intent is VoucherIntent.TransferOut or VoucherIntent.TransferIn)
        {
            var lf = NormalizeAccount(llm.FromAccountCode);
            var lt = NormalizeAccount(llm.ToAccountCode);
            bool llmOut = lf is not null && lt is not null && lf.StartsWith("112") && lt == "111";
            bool llmIn = lf is not null && lt is not null && lf == "111" && lt.StartsWith("112");
            if (intent == VoucherIntent.TransferOut && llmOut)
            {
                if (from == null) { from = lf; merged = true; }
                if (to == null) { to = lt; merged = true; }
            }
            else if (intent == VoucherIntent.TransferIn && llmIn)
            {
                if (from == null) { from = lf; merged = true; }
                if (to == null) { to = lt; merged = true; }
            }
        }

        // forceLlm: user bấm "AI phân tích" — cảnh báo luôn hiển thị kể cả khi rule-based High và không merge gì.
        if (merged || forceLlm) warnings.Add("Đề xuất từ AI — kiểm tra lại trước khi lưu.");

        // Rebuild missing + confidence theo dữ liệu sau merge.
        missing.Remove("intent");
        if (intent != VoucherIntent.Unknown) missing.Remove("intent");
        if (amount != null) missing.Remove("amount");
        if (doiTuong != null || intent is not (VoucherIntent.Receivable or VoucherIntent.ReceivablePayment
            or VoucherIntent.Advance or VoucherIntent.Payable or VoucherIntent.PayablePayment))
        {
            missing.Remove("đối tượng");
        }
        if (from != null && to != null) missing.Remove("tài khoản nguồn/đích");
        string confidence = missing.Count == 0 && intent != VoucherIntent.Unknown ? "High"
            : amount != null && intent != VoucherIntent.Unknown ? "Medium" : "Low";

        return new ParsedVoucher(intent, amount, date, b.PaymentMethod, from, to, doiTuong, category,
            b.FeeAmount, b.Description, confidence, missing, warnings);
    }

    private static string? NormalizeAccount(string? code)
        => code == null ? null : code.Trim().StartsWith("112", StringComparison.Ordinal) ? "112" : code.Trim();

    private static bool TryParseLlmDate(string raw, out DateTime date)
    {
        date = default;
        return DateTime.TryParseExact(raw, ["yyyy-MM-dd", "dd/MM/yyyy", "d/M/yyyy"],
            CultureInfo.InvariantCulture, DateTimeStyles.None, out date);
    }

    /// <summary>DTO mirror ParsedVoucher — LLM trả về, được validate trước khi merge.</summary>
    private sealed class LlmResponse
    {
        public string? Intent { get; set; }
        public decimal? Amount { get; set; }
        public string? Date { get; set; }
        public string? PaymentMethod { get; set; }
        public string? FromAccountCode { get; set; }
        public string? ToAccountCode { get; set; }
        public string? DoiTuong { get; set; }
        public string? Category { get; set; }
        public decimal? FeeAmount { get; set; }
    }
}