// File-scoped type for EInvoice Bounded Context
// HKD Electronic Invoice - Response DTO
namespace VanAn.ShopERP.EInvoice.Dtos;

/// <summary>
/// HKD Electronic Invoice response
/// </summary>
public sealed record InvoiceDto(
    Guid InvoiceId,
    Guid OrderId,
    string InvoiceType,
    decimal Amount,
    decimal VatAmount,
    decimal TotalAmount,
    string CustomerName,
    string CustomerTaxCode,
    string CustomerAddress,
    string Status,
    string? CurrentProvider,
    DateTime? SubmittedAt,
    DateTime? ApprovedAt,
    string? ProviderInvoiceNumber,
    DateTime CreatedAt,
    List<InvoiceItemDto> Items
);
