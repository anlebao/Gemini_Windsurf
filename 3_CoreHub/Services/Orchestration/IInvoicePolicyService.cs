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

    /// <summary>
    /// Determine recipient type based on MST and phone number
    /// B2B: Has valid MST (10 digits)
    /// RetailMember: Has phone number but no MST
    /// RetailAnonymous: No MST, no phone
    /// </summary>
    InvoiceRecipientType DetermineRecipientType(string? mst, string? phoneNumber);

    /// <summary>
    /// Check if e-invoice is required based on business type and annual revenue
    /// HKD: Required if revenue >= 1 billion VND (per TT152-2025)
    /// Company: Always required
    /// </summary>
    bool IsEInvoiceRequired(BusinessType businessType, decimal annualRevenue);
}
