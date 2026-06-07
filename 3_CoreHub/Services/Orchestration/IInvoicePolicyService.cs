using VanAn.Shared.Domain;

namespace VanAn.CoreHub.Services.Orchestration;

/// <summary>
/// IInvoicePolicyService - Invoice business rules and validation
/// Focused service: ONLY invoice policy logic
/// </summary>
public interface IInvoicePolicyService
{
    /// <summary>
    /// Validate invoice business rules
    /// </summary>
    Task ValidateInvoiceAsync(
        ElectronicInvoiceId invoiceId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Check if invoice can be submitted
    /// </summary>
    Task<bool> CanSubmitAsync(
        ElectronicInvoiceId invoiceId,
        CancellationToken cancellationToken = default);
}
