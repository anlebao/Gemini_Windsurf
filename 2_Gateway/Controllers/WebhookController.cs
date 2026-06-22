using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VanAn.CoreHub.Services;
using VanAn.CoreHub.Services.Orchestration;

namespace VanAn.Gateway.Controllers;

/// <summary>
/// WebhookController - REST API for provider webhook callbacks
/// Routes: api/webhooks/{provider}
/// Idempotency enforcement
/// Phase 2: Webhook callbacks from external providers use AllowAnonymous (no JWT)
/// Body: Raw provider payload (Viettel/MISA format) with invoiceNo field
/// Sprint B: Added POST /api/webhooks/payment — VietQR/bank payment confirmation endpoint
/// </summary>
[ApiController]
[Route("api/webhooks")]
[Authorize(Policy = "RequireTenantAccess")]
public class WebhookController : ControllerBase
{
    private readonly IWebhookService _webhookService;
    private readonly IOrderService _orderService;
    private readonly ILogger<WebhookController> _logger;

    // JSON options for parsing raw webhook payloads
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        AllowTrailingCommas = false,
        ReadCommentHandling = JsonCommentHandling.Skip
    };

    public WebhookController(IWebhookService webhookService, IOrderService orderService, ILogger<WebhookController> logger)
    {
        _webhookService = webhookService;
        _orderService = orderService;
        _logger = logger;
    }

    /// <summary>
    /// Receive webhook callback from provider (external - no JWT)
    /// Accepts raw provider payload (Viettel/MISA format) and extracts invoiceNo
    /// </summary>
    [HttpPost("{provider}")]
    [AllowAnonymous]
    public async Task<IActionResult> ReceiveWebhook(
        string provider,
        CancellationToken cancellationToken)
    {
        // Read raw body
        using var reader = new StreamReader(Request.Body);
        var rawBody = await reader.ReadToEndAsync(cancellationToken);

        if (string.IsNullOrWhiteSpace(rawBody))
        {
            _logger.LogWarning("WebhookController: Empty body received from provider={Provider}", provider);
            return BadRequest(new { Error = "Empty webhook body" });
        }

        // Extract providerInvoiceNumber from raw payload
        var providerInvoiceNumber = ExtractInvoiceNumber(provider, rawBody);
        if (string.IsNullOrWhiteSpace(providerInvoiceNumber))
        {
            _logger.LogWarning(
                "WebhookController: Could not extract invoiceNo from provider={Provider} payload. Body preview: {Preview}",
                provider,
                rawBody.Length > 200 ? rawBody[..200] + "..." : rawBody);
            return BadRequest(new { Error = "Missing invoiceNo in payload" });
        }

        _logger.LogInformation(
            "WebhookController: Received webhook from provider={Provider} invoice={Invoice}",
            provider,
            providerInvoiceNumber);

        // Check idempotency
        var hasBeenProcessed = await _webhookService.HasBeenProcessedAsync(
            provider,
            providerInvoiceNumber,
            cancellationToken);

        if (hasBeenProcessed)
        {
            _logger.LogInformation(
                "WebhookController: Duplicate webhook suppressed — provider={Provider} invoice={Invoice}",
                provider,
                providerInvoiceNumber);
            return Ok(new { Message = "Webhook already processed", Idempotent = true });
        }

        // Process webhook with raw callback data
        await _webhookService.ProcessWebhookAsync(
            provider,
            providerInvoiceNumber,
            rawBody,
            cancellationToken);

        _logger.LogInformation(
            "WebhookController: Webhook processed successfully — provider={Provider} invoice={Invoice}",
            provider,
            providerInvoiceNumber);

        return Ok(new { Message = "Webhook processed successfully" });
    }

    /// <summary>
    /// Sprint B: Receive VietQR/bank payment confirmation.
    /// Called by KhachLink (or bank callback) after payment is confirmed.
    /// Triggers accounting entry generation (Revenue + COGS) for the order.
    /// AllowAnonymous: bank callbacks do not carry JWT — validated via transactionId presence.
    /// Idempotent: duplicate calls for same orderId return 200 without creating duplicate entries.
    /// TT 152/2025/TT-BTC: doanh thu chỉ ghi nhận sau khi thanh toán xác nhận.
    /// </summary>
    [HttpPost("payment")]
    [AllowAnonymous]
    public async Task<IActionResult> ConfirmPayment(
        [FromBody] PaymentConfirmRequest request,
        CancellationToken cancellationToken)
    {
        if (request == null || request.OrderId == Guid.Empty)
        {
            _logger.LogWarning("WebhookController.ConfirmPayment: Missing or invalid orderId");
            return BadRequest(new { Error = "orderId is required" });
        }

        if (string.IsNullOrWhiteSpace(request.TransactionId))
        {
            _logger.LogWarning("WebhookController.ConfirmPayment: Missing transactionId for order {OrderId}", request.OrderId);
            return BadRequest(new { Error = "transactionId is required" });
        }

        if (request.TenantId == Guid.Empty)
        {
            _logger.LogWarning("WebhookController.ConfirmPayment: Missing tenantId for order {OrderId}", request.OrderId);
            return BadRequest(new { Error = "tenantId is required" });
        }

        _logger.LogInformation(
            "WebhookController.ConfirmPayment: Received payment confirmation — orderId={OrderId} transactionId={TransactionId}",
            request.OrderId, request.TransactionId);

        try
        {
            await _orderService.ConfirmPaymentAsync(request.OrderId, request.TenantId, request.TransactionId, cancellationToken);

            return Ok(new { Message = "Payment confirmed and accounting entries generated", OrderId = request.OrderId });
        }
        catch (KeyNotFoundException ex)
        {
            _logger.LogWarning("WebhookController.ConfirmPayment: {Message}", ex.Message);
            return NotFound(new { Error = ex.Message });
        }
    }

    /// <summary>
    /// Extract invoice number from raw provider payload
    /// Viettel: invoiceNo field
    /// MISA: invoiceNo field
    /// Fallback: Try common field names
    /// </summary>
    private static string? ExtractInvoiceNumber(string providerId, string rawBody)
    {
        try
        {
            using var doc = JsonDocument.Parse(rawBody);
            var root = doc.RootElement;

            var providerLower = providerId.ToLowerInvariant();

            // Try provider-specific fields first
            if (providerLower.Contains("viettel"))
            {
                if (root.TryGetProperty("invoiceNo", out var invoiceNo))
                    return invoiceNo.GetString();
                if (root.TryGetProperty("InvoiceNo", out var invoiceNoPascal))
                    return invoiceNoPascal.GetString();
            }
            else if (providerLower.Contains("misa"))
            {
                if (root.TryGetProperty("invoiceNo", out var invoiceNo))
                    return invoiceNo.GetString();
                if (root.TryGetProperty("InvoiceNo", out var invoiceNoPascal))
                    return invoiceNoPascal.GetString();
            }

            // Fallback: Try common field names
            string[] possibleFields = { "invoiceNo", "InvoiceNo", "invoiceNumber", "InvoiceNumber", "invoice_id", "invoiceId" };
            foreach (var field in possibleFields)
            {
                if (root.TryGetProperty(field, out var value))
                {
                    var strValue = value.GetString();
                    if (!string.IsNullOrWhiteSpace(strValue))
                        return strValue;
                }
            }

            return null;
        }
        catch (JsonException)
        {
            return null;
        }
    }
}

/// <summary>
/// Sprint B: Request DTO for POST /api/webhooks/payment
/// Payload sent by KhachLink (or bank callback) after VietQR/payment confirmed.
/// </summary>
public sealed record PaymentConfirmRequest
{
    public Guid OrderId { get; init; }
    public Guid TenantId { get; init; }
    public string TransactionId { get; init; } = string.Empty;
    public DateTime ConfirmedAt { get; init; } = DateTime.UtcNow;
}
