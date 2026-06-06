namespace VanAn.CoreHub.Services.Orchestration;

/// <summary>
/// IWebhookService - Webhook handling with idempotency
/// Focused service: ONLY webhook processing
/// </summary>
public interface IWebhookService
{
    /// <summary>
    /// Process webhook callback from provider
    /// </summary>
    Task ProcessWebhookAsync(
        string providerId,
        string providerInvoiceNumber,
        string callbackData,
        System.Threading.CancellationToken cancellationToken = default);

    /// <summary>
    /// Check if webhook has been processed (idempotency)
    /// </summary>
    Task<bool> HasBeenProcessedAsync(
        string providerId,
        string providerInvoiceNumber,
        System.Threading.CancellationToken cancellationToken = default);
}
