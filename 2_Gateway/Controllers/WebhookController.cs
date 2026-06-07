using Microsoft.AspNetCore.Mvc;
using VanAn.CoreHub.Services.Orchestration;

namespace VanAn.Gateway.Controllers;

/// <summary>
/// WebhookController - REST API for provider webhook callbacks
/// Idempotency enforcement
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class WebhookController : ControllerBase
{
    private readonly IWebhookService _webhookService;

    public WebhookController(IWebhookService webhookService)
    {
        _webhookService = webhookService;
    }

    /// <summary>
    /// Receive webhook callback from provider
    /// </summary>
    [HttpPost("{provider}")]
    public async Task<IActionResult> ReceiveWebhook(
        string provider,
        [FromBody] WebhookRequest request,
        CancellationToken cancellationToken)
    {
        // Check idempotency
        var hasBeenProcessed = await _webhookService.HasBeenProcessedAsync(
            provider,
            request.ProviderInvoiceNumber,
            cancellationToken);

        if (hasBeenProcessed)
        {
            // Return 200 OK for idempotency (duplicate callback)
            return Ok(new { Message = "Webhook already processed", Idempotent = true });
        }

        // Process webhook
        await _webhookService.ProcessWebhookAsync(
            provider,
            request.ProviderInvoiceNumber,
            request.CallbackData,
            cancellationToken);

        return Ok(new { Message = "Webhook processed successfully" });
    }
}

/// <summary>
/// Webhook Request DTO
/// </summary>
public record WebhookRequest(
    string ProviderInvoiceNumber,
    string CallbackData
);
