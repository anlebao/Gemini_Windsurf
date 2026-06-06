namespace VanAn.CoreHub.Services.Orchestration;

/// <summary>
/// WebhookService - Webhook processing with idempotency implementation
/// Focused service: ONLY webhook processing
/// </summary>
public class WebhookService : IWebhookService
{
    public async Task ProcessWebhookAsync(
        string providerId,
        string providerInvoiceNumber,
        string callbackData,
        System.Threading.CancellationToken cancellationToken = default)
    {
        // Stub: Process webhook with idempotency check
        // TODO: Implement with actual webhook processing and invoice status update
        await Task.CompletedTask;
    }

    public async Task<bool> HasBeenProcessedAsync(
        string providerId,
        string providerInvoiceNumber,
        System.Threading.CancellationToken cancellationToken = default)
    {
        // Stub: Check if webhook has been processed (idempotency)
        // TODO: Implement with actual idempotency check using OutboxEvent
        return await Task.FromResult(false);
    }
}
