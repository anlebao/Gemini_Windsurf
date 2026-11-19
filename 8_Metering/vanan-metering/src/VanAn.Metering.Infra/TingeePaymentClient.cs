using System.Globalization;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace VanAn.Metering.Infra;

/// <summary>
/// P8 — typed client for Tingee Payment Gateway.
/// POST {BaseUrl}/v1/payment-gateway/create-link
/// Signature: x-signature = HMAC_SHA512(x-request-timestamp + ":" + rawBody, ClientSecret).
/// The body is serialized ONCE and the exact same string is signed and sent —
/// re-serializing would break the signature.
/// Fail-safe: never throws for API/transport errors — returns TingeeLinkResult.
/// </summary>
public sealed class TingeePaymentClient
{
    // Tingee canonicalizes the JSON body before verifying x-signature (verified
    // against prod 2026-11-19):
    //  - unicode must be literal UTF-8, not \uXXXX escapes → UnsafeRelaxedJsonEscaping
    //  - numbers must be canonical: decimal 10000.0m serializes "10000.0" but
    //    Tingee's canonical form is "10000" → code 97 Invalid signature.
    //    CanonicalDecimalJsonConverter strips trailing zeros / writes integral
    //    decimals as integers so our body == their canonical form.
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    private readonly HttpClient _http;
    private readonly TingeeOptions _opt;
    private readonly ILogger<TingeePaymentClient> _logger;

    public TingeePaymentClient(HttpClient http, IConfiguration config, ILogger<TingeePaymentClient> logger)
    {
        _http = http;
        _opt = TingeeOptions.From(config);
        _logger = logger;
        if (_http.BaseAddress is { } b && !b.OriginalString.EndsWith('/'))
            _http.BaseAddress = new Uri(b.OriginalString + "/");
    }

    public bool IsEnabled => _opt.IsEnabled;
    public bool IsQrEnabled => _opt.IsQrEnabled;

    public async Task<TingeeLinkResult> CreatePaymentLinkAsync(
        string orderId, decimal amountVnd, string description, CancellationToken ct = default)
    {
        if (!IsEnabled)
            return TingeeLinkResult.Fail("tingee_disabled", "Tingee payment is not configured.");
        if (amountVnd < 2000)
            return TingeeLinkResult.Fail("amount_too_low", "Minimum payment amount is 2,000 VND.");

        var payload = new CreateLinkRequest(
            Guid.NewGuid().ToString(),
            orderId,
            amountVnd,
            "VND",
            Math.Clamp(_opt.ExpireInMinute, 5, 30),
            Truncate(description, 200),
            _opt.ReturnUrl);
        var body = JsonSerializer.Serialize(payload, Json);
        var timestamp = TingeeSignature.TimestampUtc7(DateTime.UtcNow);

        using var req = new HttpRequestMessage(HttpMethod.Post, "v1/payment-gateway/create-link")
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json")
        };
        req.Headers.TryAddWithoutValidation("x-client-id", _opt.ClientId);
        req.Headers.TryAddWithoutValidation("x-request-timestamp", timestamp);
        req.Headers.TryAddWithoutValidation("x-signature",
            TingeeSignature.SignRequest(timestamp, body, _opt.ClientSecret!));

        try
        {
            using var resp = await _http.SendAsync(req, ct);
            var text = await resp.Content.ReadAsStringAsync(ct);
            using var doc = JsonDocument.Parse(text);
            var root = doc.RootElement;
            var code = root.TryGetProperty("code", out var c) ? c.GetString() : null;
            if (code == "00" && root.TryGetProperty("data", out var d) && d.ValueKind == JsonValueKind.String)
                return TingeeLinkResult.Ok(d.GetString()!);
            var msg = root.TryGetProperty("message", out var m) ? m.GetString() : null;
            _logger.LogWarning("Tingee create-link rejected: http={Http} code={Code} msg={Msg} ts={Ts} sig={Sig} body={Body}",
                (int)resp.StatusCode, code, msg ?? text, timestamp,
                req.Headers.GetValues("x-signature").FirstOrDefault(), body);
            return TingeeLinkResult.Fail("tingee_error", msg ?? $"Tingee API error (HTTP {(int)resp.StatusCode})");
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Tingee create-link returned non-JSON response");
            return TingeeLinkResult.Fail("tingee_bad_response", "Tingee returned a non-JSON response.");
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            _logger.LogWarning(ex, "Tingee create-link transport error");
            return TingeeLinkResult.Fail("tingee_unreachable", "Cannot reach Tingee API.");
        }
    }

    /// <summary>
    /// P9 — POST v1/generate-dynamic-qr: mint a one-time dynamic VietQR for the
    /// configured VA. Returns the EMV QR payload + Tingee billId (webhook match key).
    /// Same signing rules as create-link — body serialized ONCE, exact string signed.
    /// </summary>
    public async Task<TingeeQrResult> GenerateDynamicQrAsync(
        decimal amountVnd, string referenceCode, CancellationToken ct = default)
    {
        if (!IsEnabled)
            return TingeeQrResult.Fail("tingee_disabled", "Tingee payment is not configured.");
        if (!_opt.IsQrEnabled)
            return TingeeQrResult.Fail("qr_disabled", "Tingee VA/bankBin is not configured.");
        if (amountVnd < 2000)
            return TingeeQrResult.Fail("amount_too_low", "Minimum payment amount is 2,000 VND.");

        var payload = new GenerateQrRequest(
            _opt.VaAccountNumber!,
            "dynamic-one-time-payment",
            _opt.VaBankBin!,
            amountVnd,
            // P9 fix: generate-dynamic-qr validates purpose format — rejects non-ASCII
            // AND reference-like tokens ("TOPUP-XXX" → code=400, verified on prod).
            // Purpose is display-only; correlation stays in extraInfo=referenceCode
            // and billId. Keep a plain ASCII description.
            "Nap phi nen tang",
            Truncate(referenceCode, 100));
        var body = JsonSerializer.Serialize(payload, Json);
        var timestamp = TingeeSignature.TimestampUtc7(DateTime.UtcNow);

        using var req = new HttpRequestMessage(HttpMethod.Post, "v1/generate-dynamic-qr")
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json")
        };
        req.Headers.TryAddWithoutValidation("x-client-id", _opt.ClientId);
        req.Headers.TryAddWithoutValidation("x-request-timestamp", timestamp);
        req.Headers.TryAddWithoutValidation("x-signature",
            TingeeSignature.SignRequest(timestamp, body, _opt.ClientSecret!));

        try
        {
            using var resp = await _http.SendAsync(req, ct);
            var text = await resp.Content.ReadAsStringAsync(ct);
            using var doc = JsonDocument.Parse(text);
            var root = doc.RootElement;
            var code = root.TryGetProperty("code", out var c) ? c.GetString() : null;
            if (code == "00" && root.TryGetProperty("data", out var d) && d.ValueKind == JsonValueKind.Object)
            {
                var qr = d.TryGetProperty("qrCode", out var q) ? q.GetString() : null;
                var account = d.TryGetProperty("qrAccount", out var a) ? a.GetString() : null;
                var billId = d.TryGetProperty("billId", out var b) ? b.GetString() : null;
                if (!string.IsNullOrEmpty(qr) && !string.IsNullOrEmpty(billId))
                    return TingeeQrResult.Ok(qr, account, billId);
                _logger.LogWarning("Tingee generate-dynamic-qr: code 00 but missing qrCode/billId: {Body}", text);
                return TingeeQrResult.Fail("tingee_bad_response", "Tingee QR response missing qrCode/billId.");
            }
            var msg = root.TryGetProperty("message", out var m) ? m.GetString() : null;
            _logger.LogWarning("Tingee generate-dynamic-qr rejected: http={Http} code={Code} msg={Msg} ts={Ts} sig={Sig} body={Body}",
                (int)resp.StatusCode, code, msg ?? text, timestamp,
                req.Headers.GetValues("x-signature").FirstOrDefault(), body);
            return TingeeQrResult.Fail("tingee_error", msg ?? $"Tingee API error (HTTP {(int)resp.StatusCode})");
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Tingee generate-dynamic-qr returned non-JSON response");
            return TingeeQrResult.Fail("tingee_bad_response", "Tingee returned a non-JSON response.");
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            _logger.LogWarning(ex, "Tingee generate-dynamic-qr transport error");
            return TingeeQrResult.Fail("tingee_unreachable", "Cannot reach Tingee API.");
        }
    }


    /// <summary>
    /// P12 — POST v1/transaction/get-paging: VA transaction history for polling
    /// reconciliation (webhook fallback). Filters by vaAccountNumbers, returns
    /// credit items; caller matches billId → top-up.
    /// </summary>
    public async Task<List<TingeeVaTransaction>> GetVaTransactionsAsync(
        int maxResultCount, CancellationToken ct = default)
    {
        var result = new List<TingeeVaTransaction>();
        if (!IsEnabled || !_opt.IsQrEnabled)
            return result;

        var payload = new { skipCount = 0, maxResultCount, vaAccountNumbers = new[] { _opt.VaAccountNumber } };
        var body = JsonSerializer.Serialize(payload, Json);
        var timestamp = TingeeSignature.TimestampUtc7(DateTime.UtcNow);

        using var req = new HttpRequestMessage(HttpMethod.Post, "v1/transaction/get-paging")
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json")
        };
        req.Headers.TryAddWithoutValidation("x-client-id", _opt.ClientId);
        req.Headers.TryAddWithoutValidation("x-request-timestamp", timestamp);
        req.Headers.TryAddWithoutValidation("x-signature",
            TingeeSignature.SignRequest(timestamp, body, _opt.ClientSecret!));

        try
        {
            using var resp = await _http.SendAsync(req, ct);
            var text = await resp.Content.ReadAsStringAsync(ct);
            using var doc = JsonDocument.Parse(text);
            var root = doc.RootElement;
            var code = root.TryGetProperty("code", out var c) ? c.GetString() : null;
            if (code != "00")
            {
                _logger.LogWarning("Tingee transaction/get-paging rejected: http={Http} code={Code} body={Body}",
                    (int)resp.StatusCode, code, text);
                return result;
            }
            if (!root.TryGetProperty("data", out var d) || !d.TryGetProperty("items", out var items)
                || items.ValueKind != JsonValueKind.Array)
                return result;
            foreach (var it in items.EnumerateArray())
            {
                result.Add(new TingeeVaTransaction(
                    Get(it, "transactionId"), Get(it, "code"),
                    it.TryGetProperty("amount", out var a) && a.TryGetDecimal(out var dec) ? dec : 0m,
                    Get(it, "type"), Get(it, "content"),
                    Get(it, "vaAccountNumber"), Get(it, "transactionDate"), Get(it, "billId")));
            }
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            _logger.LogWarning(ex, "Tingee transaction/get-paging failed");
        }
        return result;

        static string? Get(JsonElement e, string name)
            => e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;
    }

    private static string? Truncate(string? s, int max)
        => s == null ? null : s.Length <= max ? s : s[..max];

    /// <summary>
    /// Writes decimals in Tingee's canonical numeric form: strips trailing zeros
    /// and writes integral values without a decimal point (10000.0m → 10000).
    /// Non-integral values keep up to 28 fractional digits, trailing zeros trimmed.
    /// </summary>
    private sealed class CanonicalDecimalJsonConverter : JsonConverter<decimal>
    {
        public override decimal Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
            => reader.GetDecimal();

        public override void Write(Utf8JsonWriter writer, decimal value, JsonSerializerOptions options)
        {
            var s = value.ToString("0.############################", CultureInfo.InvariantCulture);
            if (s.Contains('.'))
                s = s.TrimEnd('0').TrimEnd('.');
            writer.WriteRawValue(s);
        }
    }

    private sealed record CreateLinkRequest(
        [property: JsonPropertyName("requestId")] string RequestId,
        [property: JsonPropertyName("orderId")] string OrderId,
        [property: JsonPropertyName("amount")]
        [property: JsonConverter(typeof(CanonicalDecimalJsonConverter))] decimal Amount,
        [property: JsonPropertyName("currency")] string Currency,
        [property: JsonPropertyName("expireInMinute")] int ExpireInMinute,
        [property: JsonPropertyName("description")] string? Description,
        [property: JsonPropertyName("returnUrl")] string? ReturnUrl);

    private sealed record GenerateQrRequest(
        [property: JsonPropertyName("vaAccountNumber")] string VaAccountNumber,
        [property: JsonPropertyName("qrCodeType")] string QrCodeType,
        [property: JsonPropertyName("bankBin")] string BankBin,
        [property: JsonPropertyName("amount")]
        [property: JsonConverter(typeof(CanonicalDecimalJsonConverter))] decimal Amount,
        [property: JsonPropertyName("purpose")] string? Purpose,
        [property: JsonPropertyName("extraInfo")] string? ExtraInfo);
}

public sealed record TingeeLinkResult(string? CheckoutUrl, string? ErrorCode, string? ErrorMessage)
{
    public static TingeeLinkResult Ok(string url) => new(url, null, null);
    public static TingeeLinkResult Fail(string code, string msg) => new(null, code, msg);
}

/// <summary>P9 — result of generate-dynamic-qr: EMV payload + Tingee billId.</summary>
public sealed record TingeeQrResult(string? QrPayload, string? QrAccount, string? BillId,
    string? ErrorCode, string? ErrorMessage)
{
    public static TingeeQrResult Ok(string payload, string? account, string billId)
        => new(payload, account, billId, null, null);
    public static TingeeQrResult Fail(string code, string msg)
        => new(null, null, null, code, msg);
}

/// <summary>P12 — one row of Tingee transaction/get-paging (VA history).</summary>
public sealed record TingeeVaTransaction(
    string? TransactionId, string? TransactionCode, decimal Amount,
    string? Type, string? Content, string? VaAccountNumber,
    string? TransactionDate, string? BillId);
