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
    // UnsafeRelaxedJsonEscaping: Tingee verifies signature over the DECODED JSON
    // (unicode literal), not raw escaped bytes — default encoder's \uXXXX escaping
    // breaks it (code 97 Invalid signature, verified against prod 2026-11-19).
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
            _logger.LogWarning("Tingee create-link rejected: http={Http} code={Code} msg={Msg}",
                (int)resp.StatusCode, code, msg ?? text);
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

    private static string? Truncate(string? s, int max)
        => s == null ? null : s.Length <= max ? s : s[..max];

    private sealed record CreateLinkRequest(
        [property: JsonPropertyName("requestId")] string RequestId,
        [property: JsonPropertyName("orderId")] string OrderId,
        [property: JsonPropertyName("amount")] decimal Amount,
        [property: JsonPropertyName("currency")] string Currency,
        [property: JsonPropertyName("expireInMinute")] int ExpireInMinute,
        [property: JsonPropertyName("description")] string? Description,
        [property: JsonPropertyName("returnUrl")] string? ReturnUrl);
}

public sealed record TingeeLinkResult(string? CheckoutUrl, string? ErrorCode, string? ErrorMessage)
{
    public static TingeeLinkResult Ok(string url) => new(url, null, null);
    public static TingeeLinkResult Fail(string code, string msg) => new(null, code, msg);
}
