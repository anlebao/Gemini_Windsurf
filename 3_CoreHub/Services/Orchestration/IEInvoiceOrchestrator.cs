using VanAn.Shared.Domain;

namespace VanAn.CoreHub.Services.Orchestration;

/// <summary>
/// W3 (P0.3): dữ liệu mới khi điều chỉnh/thay thế hóa đơn — field null = giữ nguyên giá trị gốc.
/// Theo Điều 10 TT 91/2026: điều chỉnh thông tin (buyer) hoặc điều chỉnh tiền (amounts).
/// </summary>
public record InvoiceAdjustmentData(
    decimal? Amount = null,
    decimal? VatAmount = null,
    decimal? TotalAmount = null,
    string? CustomerName = null,
    string? CustomerTaxCode = null,
    string? CustomerAddress = null);

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
    /// W3 (P0.3): list invoices of a tenant (UI InvoiceManagement — thay placeholder stub).
    /// </summary>
    Task<IReadOnlyList<ElectronicInvoice>> ListInvoicesAsync(
        TenantId tenantId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// W3 (P0.3 — Điều 10 TT 91/2026): tạo hóa đơn điều chỉnh/thay thế cho hóa đơn gốc
    /// đã phát hành (TaxApproved). Tạo ElectronicInvoice mới linked → outbox → pipeline
    /// submit như hóa đơn thường (provider nhận adjustmentType/ReferenceType).
    /// Đồng thời mark hóa đơn gốc Adjusted/Replaced + audit Correction.
    /// </summary>
    /// <returns>ElectronicInvoiceId của hóa đơn điều chỉnh/thay thế mới.</returns>
    Task<ElectronicInvoiceId> AdjustInvoiceAsync(
        ElectronicInvoiceId originalInvoiceId,
        InvoiceAdjustmentType adjustmentType,
        string reason,
        InvoiceAdjustmentData? newData = null,
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

    /// <summary>
    /// P0.4 (W2, 2026-10-17 — Điều 8 TT 91/2026): bật/tắt cờ ngừng/tạm ngừng sử dụng HĐĐT
    /// theo thông báo của cơ quan thuế. Lưu SystemSetting theo tenant — không cần migration.
    /// </summary>
    Task SetEInvoiceUsageSuspendedAsync(
        TenantId tenantId,
        bool suspended,
        string? reason = null,
        Guid? updatedBy = null,
        CancellationToken cancellationToken = default);
}
