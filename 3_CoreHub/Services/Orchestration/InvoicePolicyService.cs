using VanAn.Shared.Domain;

namespace VanAn.CoreHub.Services.Orchestration;

/// <summary>
/// InvoicePolicyService - Invoice business rules implementation
/// Focused service: ONLY invoice policy logic
/// </summary>
public class InvoicePolicyService : IInvoicePolicyService
{
    public async Task ValidateInvoiceAsync(
        ElectronicInvoiceId invoiceId,
        CancellationToken cancellationToken = default)
    {
        // Stub: Validate invoice business rules
        // TODO: Implement with actual invoice validation
        await Task.CompletedTask;
    }

    public async Task<bool> CanSubmitAsync(
        ElectronicInvoiceId invoiceId,
        CancellationToken cancellationToken = default)
    {
        // Stub: Check if invoice can be submitted
        // TODO: Implement with actual invoice status check
        return await Task.FromResult(true);
    }
}
