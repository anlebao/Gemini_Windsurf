using VanAn.Shared.Domain;

namespace VanAn.CoreHub.Services.Orchestration;

/// <summary>
/// EInvoiceOrchestrator - Orchestrator implementation (ONLY coordination)
/// Delegates to focused services: Policy, Retry, Fallback, Compliance, Webhook
/// </summary>
public class EInvoiceOrchestrator : IEInvoiceOrchestrator
{
    private readonly IInvoicePolicyService _policyService;
    private readonly IRetryPolicyService _retryService;
    private readonly IFallbackService _fallbackService;
    private readonly IComplianceService _complianceService;
    private readonly IWebhookService _webhookService;

    public EInvoiceOrchestrator(
        IInvoicePolicyService policyService,
        IRetryPolicyService retryService,
        IFallbackService fallbackService,
        IComplianceService complianceService,
        IWebhookService webhookService)
    {
        _policyService = policyService;
        _retryService = retryService;
        _fallbackService = fallbackService;
        _complianceService = complianceService;
        _webhookService = webhookService;
    }

    public async Task SubmitInvoiceAsync(
        ElectronicInvoiceId invoiceId,
        CancellationToken cancellationToken = default)
    {
        // Orchestrator ONLY coordinates - delegates to focused services
        await _policyService.ValidateInvoiceAsync(invoiceId, cancellationToken);
        await _complianceService.ValidateComplianceAsync(invoiceId, cancellationToken);
        
        // Retry and fallback logic delegated to respective services
        await _retryService.SubmitWithRetryAsync(invoiceId, cancellationToken);
    }

    public async Task ProcessWebhookAsync(
        string providerId,
        string providerInvoiceNumber,
        string callbackData,
        CancellationToken cancellationToken = default)
    {
        // Orchestrator ONLY coordinates - delegates to webhook service
        await _webhookService.ProcessWebhookAsync(providerId, providerInvoiceNumber, callbackData, cancellationToken);
    }
}
