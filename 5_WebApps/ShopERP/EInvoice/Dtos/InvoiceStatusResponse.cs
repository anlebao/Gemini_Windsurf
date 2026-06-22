// File-scoped type for EInvoice Bounded Context
// HKD Electronic Invoice - Status response DTO
namespace VanAn.ShopERP.EInvoice.Dtos;

/// <summary>
/// HKD Electronic Invoice status response
/// </summary>
public sealed record InvoiceStatusResponse(
    Guid InvoiceId,
    string Status,
    string? CurrentProvider,
    DateTime? SubmittedAt,
    DateTime? ApprovedAt,
    string? ProviderInvoiceNumber,
    string? FailureReason
);
