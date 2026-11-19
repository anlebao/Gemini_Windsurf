using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using VanAn.Metering.Domain;

namespace VanAn.Metering.Infra;

/// <summary>
/// P8/P9 — handles Tingee webhooks (POST /v1/webhooks/tingee).
/// Auth: x-signature = HMAC_SHA512(x-request-timestamp + ":" + rawBody, ClientSecret).
///   - webhook-type: url-validation → return {plainToken, encryptedToken} challenge response.
///   - Payment Gateway events (orderId/status) → success → ConfirmPaidByReferenceAsync.
///   - P9 Payment Callback events (transactionCode/type/vaAccountNumber — QR/VA
///     bank transactions) → credit only → match by additionalData[].billId
///     (ConfirmPaidByQrBillIdAsync), fallback TOPUP-XXX inside transfer content.
/// Non-success statuses, debits and unknown references return 200/404 so Tingee's
/// 5-retry policy terminates; only a bad signature returns 401.
/// </summary>
public sealed class TingeeWebhookService(
    TopupService topups,
    IConfiguration config,
    ILogger<TingeeWebhookService> logger)
{
    private readonly TingeeOptions _opt = TingeeOptions.From(config);
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public async Task<(int Status, object Body)> HandleAsync(
        string? webhookType, string rawBody,
        string? timestamp, string? signature, CancellationToken ct = default)
    {
        if (webhookType == "url-validation")
            return HandleUrlValidation(rawBody);

        if (!_opt.IsEnabled || string.IsNullOrEmpty(timestamp)
            || !TingeeSignature.VerifySignature(timestamp, rawBody, signature, _opt.VerifySecret))
        {
            logger.LogWarning("Tingee webhook rejected: missing/invalid signature");
            return (401, new { error = "invalid_signature" });
        }

        // P9 — Payment Callback payloads carry transactionCode/type/vaAccountNumber
        // and NO orderId/status. Detect by probing the raw JSON once.
        if (IsTransactionCallback(rawBody))
            return await HandleTransactionCallbackAsync(rawBody, ct);

        TingeePaymentEvent? evt;
        try { evt = JsonSerializer.Deserialize<TingeePaymentEvent>(rawBody, Json); }
        catch (JsonException) { return (400, new { error = "invalid_json" }); }
        if (evt == null) return (400, new { error = "invalid_json" });

        var isSuccess = string.Equals(evt.Status, "success", StringComparison.OrdinalIgnoreCase)
                        || evt.StatusCode == "00";
        if (!isSuccess)
        {
            // expired/canceled — keep Pending; caller can mint a fresh link via /v1/topups/{id}/pay.
            logger.LogInformation("Tingee webhook non-success ignored: orderId={OrderId} status={Status}",
                evt.OrderId, evt.Status);
            return (200, new { received = true, ignored = evt.Status ?? evt.StatusCode });
        }

        var paid = evt.PaidAmount ?? evt.Amount ?? 0m;
        var r = await topups.ConfirmPaidByReferenceAsync(
            evt.OrderId ?? string.Empty, paid, evt.ProviderTransactionCode, DateTime.UtcNow, ct);
        return ToResponse(r);
    }

    /// <summary>P9 — bank-transaction Payment Callback (dynamic QR / VA credit).</summary>
    private async Task<(int Status, object Body)> HandleTransactionCallbackAsync(
        string rawBody, CancellationToken ct)
    {
        TingeeTransactionEvent? evt;
        try { evt = JsonSerializer.Deserialize<TingeeTransactionEvent>(rawBody, Json); }
        catch (JsonException) { return (400, new { error = "invalid_json" }); }
        if (evt == null) return (400, new { error = "invalid_json" });

        // Only incoming money matters — debits (refunds/adjustments) are ignored.
        if (string.Equals(evt.Type, "debit", StringComparison.OrdinalIgnoreCase))
        {
            logger.LogInformation("Tingee transaction callback debit ignored: txn={Txn}", evt.TransactionCode);
            return (200, new { received = true, ignored = "debit" });
        }

        var paid = evt.Amount ?? 0m;
        var billId = ExtractBillId(evt.AdditionalData);
        TopupOpResult r;
        if (!string.IsNullOrWhiteSpace(billId))
        {
            r = await topups.ConfirmPaidByQrBillIdAsync(
                billId, paid, evt.TransactionCode, DateTime.UtcNow, ct);
            // billId matched nothing — fall through to content reference before 404.
            if (r.ErrorCode == "not_found")
                r = await TryContentReferenceAsync(evt.Content, paid, evt.TransactionCode, ct);
        }
        else
        {
            r = await TryContentReferenceAsync(evt.Content, paid, evt.TransactionCode, ct);
        }
        return ToResponse(r);
    }

    private async Task<TopupOpResult> TryContentReferenceAsync(
        string? content, decimal paid, string? txnId, CancellationToken ct)
    {
        var m = Regex.Match(content ?? string.Empty, @"TOPUP-[A-Z0-9]{8}",
            RegexOptions.IgnoreCase);
        if (!m.Success) return TopupOpResult.Fail("not_found", "Topup request not found.");
        return await topups.ConfirmPaidByReferenceAsync(
            m.Value.ToUpperInvariant(), paid, txnId, DateTime.UtcNow, ct);
    }

    private static (int Status, object Body) ToResponse(TopupOpResult r)
    {
        if (r.Request != null)
        {
            return (200, new
            {
                received = true,
                referenceCode = r.Request.ReferenceCode,
                status = StatusName(r.Request.Status)
            });
        }
        return r.ErrorCode == "not_found"
            ? (404, new { error = r.ErrorCode, message = r.ErrorMessage })
            : (409, new { error = r.ErrorCode, message = r.ErrorMessage });
    }

    /// <summary>True when the payload looks like a Payment Callback transaction
    /// (has transactionCode/type/vaAccountNumber, lacks the gateway orderId/status).</summary>
    private static bool IsTransactionCallback(string rawBody)
    {
        try
        {
            using var doc = JsonDocument.Parse(rawBody);
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object) return false;
            var hasOrder = root.TryGetProperty("orderId", out _)
                           || root.TryGetProperty("status", out _)
                           || root.TryGetProperty("statusCode", out _);
            var hasTxn = root.TryGetProperty("transactionCode", out _)
                         || root.TryGetProperty("vaAccountNumber", out _)
                         || root.TryGetProperty("type", out _);
            return hasTxn && !hasOrder;
        }
        catch (JsonException) { return false; }
    }

    /// <summary>additionalData shape varies (array of objects/strings) — scan its
    /// raw JSON for a billId value defensively.</summary>
    private static string? ExtractBillId(JsonElement? additionalData)
    {
        if (additionalData is not { } el) return null;
        var raw = el.GetRawText();
        var m = Regex.Match(raw, "\"billId\"\\s*:\\s*\"([^\"]+)\"", RegexOptions.IgnoreCase);
        if (m.Success) return m.Groups[1].Value;
        // Fallback: bare billId-looking token inside string entries.
        m = Regex.Match(raw, "\\b(QR[A-Z0-9]{5,})\\b");
        return m.Success ? m.Groups[1].Value : null;
    }

    private (int Status, object Body) HandleUrlValidation(string rawBody)
    {
        string? plainToken = null;
        try
        {
            using var doc = JsonDocument.Parse(rawBody);
            plainToken = doc.RootElement.TryGetProperty("plainToken", out var p) ? p.GetString() : null;
        }
        catch (JsonException) { /* fallthrough → 400 below */ }
        if (string.IsNullOrEmpty(plainToken)) return (400, new { error = "missing_plainToken" });
        return (200, new
        {
            plainToken,
            encryptedToken = TingeeSignature.EncryptedToken(plainToken, _opt.VerifySecret)
        });
    }

    private static string StatusName(TopupStatus s) => s switch
    {
        TopupStatus.Confirmed => "confirmed",
        TopupStatus.Rejected => "rejected",
        TopupStatus.Expired => "expired",
        _ => "pending",
    };

    /// <summary>Inbound payment-gateway event fields (docs /webhook). Unknown fields ignored.</summary>
    private sealed class TingeePaymentEvent
    {
        public string? RequestId { get; set; }
        public string? OrderId { get; set; }
        public decimal? Amount { get; set; }
        public decimal? PaidAmount { get; set; }
        public string? Status { get; set; }
        public string? StatusCode { get; set; }
        public string? BillId { get; set; }
        public string? PaymentMethod { get; set; }
        public string? ProviderTransactionCode { get; set; }
        public DateTime? PaidAt { get; set; }
    }

    /// <summary>P9 — inbound Payment Callback (bank transaction) fields.
    /// additionalData is kept as raw JsonElement — its shape is provider-defined.</summary>
    private sealed class TingeeTransactionEvent
    {
        public string? TransactionCode { get; set; }
        public decimal? Amount { get; set; }
        public string? Content { get; set; }
        public string? Type { get; set; }               // "credit" | "debit"
        public string? VaAccountNumber { get; set; }
        public JsonElement? AdditionalData { get; set; }
    }
}
