using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using VanAn.Metering.Domain;

namespace VanAn.Metering.Infra;

/// <summary>
/// P8 — handles Tingee IPN webhooks (POST /v1/webhooks/tingee).
/// Auth: x-signature = HMAC_SHA512(x-request-timestamp + ":" + rawBody, ClientSecret).
///   - webhook-type: url-validation → return {plainToken, encryptedToken} challenge response.
///   - payment events → success → TopupService.ConfirmPaidByReferenceAsync (idempotent).
/// Non-success statuses and unknown references still return 200/404 so Tingee's
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

    private (int, object) HandleUrlValidation(string rawBody)
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

    /// <summary>Inbound payment event fields (docs /webhook). Unknown fields ignored.</summary>
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
}
