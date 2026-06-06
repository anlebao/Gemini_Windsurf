using VanAn.Shared.Domain;

namespace VanAn.CoreHub.Services.Orchestration;

/// <summary>
/// IComplianceService - TT152-2025 compliance validation
/// Focused service: ONLY compliance validation
/// </summary>
public interface IComplianceService
{
    /// <summary>
    /// Validate invoice compliance with TT152-2025
    /// </summary>
    Task ValidateComplianceAsync(
        ElectronicInvoiceId invoiceId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Check if invoice is compliant
    /// </summary>
    Task<bool> IsCompliantAsync(
        ElectronicInvoiceId invoiceId,
        CancellationToken cancellationToken = default);
}
