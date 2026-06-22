// File-scoped type for EInvoice Bounded Context
// HKD Electronic Invoice - Create request DTO
namespace VanAn.ShopERP.EInvoice.Dtos;

/// <summary>
/// Request to create HKD Electronic Invoice
/// ACID: Invoice creation + Revenue recognition + Inventory deduction (if applicable)
/// </summary>
public sealed record CreateInvoiceRequest(
    Guid OrderId,
    string InvoiceType,       // "Goods" or "Service"
    decimal Amount,
    decimal VatAmount,
    decimal TotalAmount,
    string CustomerName,
    string CustomerTaxCode,
    string CustomerAddress,
    List<InvoiceItemDto> Items,
    string? PreferredProvider = null   // "viettel" or "misa"
);
