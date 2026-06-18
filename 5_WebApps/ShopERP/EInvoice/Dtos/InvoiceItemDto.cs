// File-scoped type for EInvoice Bounded Context
// HKD Electronic Invoice - Item line DTO
namespace VanAn.ShopERP.EInvoice.Dtos;

/// <summary>
/// Invoice line item for HKD Electronic Invoice
/// </summary>
public sealed record InvoiceItemDto(
    string ProductName,
    decimal Quantity,
    decimal UnitPrice,
    decimal TaxRate,
    decimal Amount,
    decimal VatAmount
);
