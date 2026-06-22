// File-scoped type for EInvoice Bounded Context
// HKD Electronic Invoice - Submit response DTO
namespace VanAn.ShopERP.EInvoice.Dtos;

/// <summary>
/// Response from submitting invoice to tax provider
/// </summary>
public sealed record SubmitInvoiceResponse(
    bool Success,
    string Message,
    string? ProviderSubmissionId = null,
    DateTime? SubmittedAt = null
);
