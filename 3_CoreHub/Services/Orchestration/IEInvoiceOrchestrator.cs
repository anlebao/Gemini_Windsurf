using VanAn.Shared.Domain;

namespace VanAn.CoreHub.Services.Orchestration;

/// <summary>
/// IEInvoiceOrchestrator - ONLY coordination, NO business logic
/// Anti-God Service pattern: Orchestrator delegates to focused services
/// </summary>
public interface IEInvoiceOrchestrator
{
    /// <summary>
    /// Create new invoice and enqueue in outbox for async processing
    /// </summary>
    Task<ElectronicInvoiceId> CreateInvoiceAsync(
        TenantId tenantId,
        OrderId orderId,
        InvoiceIdempotencyKey idempotencyKey,
        InvoiceType invoiceType,
        decimal amount,
        decimal vatAmount,
        decimal totalAmount,
        string customerName,
        string customerTaxCode,
        string customerAddress,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get invoice by ID — returns null if not found (no IInvoiceRepository yet)
    /// </summary>
    Task<ElectronicInvoice?> GetInvoiceAsync(
        ElectronicInvoiceId invoiceId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get invoice status — returns null if not found
    /// </summary>
    Task<InvoiceStatus?> GetInvoiceStatusAsync(
        ElectronicInvoiceId invoiceId,
        CancellationToken cancellationToken = default);

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
