using VanAn.Shared.Domain;

namespace VanAn.CoreHub.Services.Orchestration;

/// <summary>
/// IEInvoiceOrchestrator - ONLY coordination, NO business logic
/// Anti-God Service pattern: Orchestrator delegates to focused services
/// </summary>
public interface IEInvoiceOrchestrator
{
    /// <summary>
    /// Submit invoice to provider with orchestrator coordination
    /// </summary>
    Task SubmitInvoiceAsync(
        ElectronicInvoiceId invoiceId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Process webhook callback from provider
    /// </summary>
    Task ProcessWebhookAsync(
        string providerId,
        string providerInvoiceNumber,
        string callbackData,
        CancellationToken cancellationToken = default);
}
