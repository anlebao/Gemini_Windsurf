using VanAn.Shared.Domain;

namespace VanAn.CoreHub.Services.Booking;

/// <summary>
/// IBookingFinancialService — financial facts (SRS §16). Deposit classification §16.2 (theo tax profile —
/// KHÔNG hard-code), PaymentTransaction 7-state §16.3, InvoiceIntegrationRecord + invoice_trigger snapshot §16.4,
/// outbox events §16.6 (DepositPaid/PaymentCaptured/ServiceCompleted/InvoiceRequired/InvoiceFailed/RefundCompleted).
/// Booking = source of booking facts; Accounting/E-Invoice = source of accounting/invoice state (Risk 11).
/// </summary>
public interface IBookingFinancialService
{
    /// <summary>Phân loại bản chất khoản tiền theo tax profile tenant (§16.2 — NĐ 70/2025: hóa đơn tại thời điểm thu tiền trước).</summary>
    MoneyNatureType ResolveDepositNature(BookingTenantConfig config);

    /// <summary>Invoice trigger snapshot theo bản chất khoản tiền + tax profile (§16.4 — KHÔNG cho frontend chọn).</summary>
    InvoiceTrigger ResolveInvoiceTrigger(MoneyNatureType nature, BookingTenantConfig config);

    /// <summary>Tạo PaymentTransaction deposit (PENDING). providerRef != null → confirm ngay (Paid + outbox DepositPaid [+ InvoiceRequired]).</summary>
    Task<PaymentTransaction> CaptureDepositAsync(
        TenantId tenantId, Guid bookingId, decimal amount, string method = "VIETQR",
        string? providerRef = null, CancellationToken ct = default);

    /// <summary>Confirm deposit đã capture (PENDING → PAID) + outbox DepositPaid [+ InvoiceRequired nếu trigger OnPayment].</summary>
    Task<PaymentTransaction> ConfirmDepositAsync(TenantId tenantId, Guid bookingId, Guid paymentTransactionId, string? providerRef = null, CancellationToken ct = default);

    /// <summary>Capture final payment khi hoàn tất (FINAL_PAYMENT §16.2) + outbox PaymentCaptured.</summary>
    Task<PaymentTransaction> CaptureFinalPaymentAsync(
        TenantId tenantId, Guid bookingId, decimal amount, string method = "CASH",
        string? providerRef = null, CancellationToken ct = default);

    /// <summary>Hoàn cọc / refund (REFUNDED) + outbox RefundCompleted. Deposit bị forfeit khi no-show (FORFEITED).</summary>
    Task<PaymentTransaction> RefundDepositAsync(TenantId tenantId, Guid bookingId, string? providerRef = null, CancellationToken ct = default);

    /// <summary>ServiceCompleted facts (gọi tại transition COMPLETED — P3.5 hook) + outbox ServiceCompleted [+ InvoiceRequired nếu trigger OnCompletion].</summary>
    Task RecordServiceCompletedAsync(TenantId tenantId, Guid bookingId, CancellationToken ct = default);

    /// <summary>Get-or-create InvoiceIntegrationRecord với trigger snapshot (SRS §16.4).</summary>
    Task<InvoiceIntegrationRecord> EnsureInvoiceRecordAsync(TenantId tenantId, Guid bookingId, CancellationToken ct = default);

    /// <summary>Cập nhật invoice status từ provider (Accounting/E-Invoice source of state) + outbox InvoiceIssued/InvoiceFailed.</summary>
    Task<InvoiceIntegrationRecord> UpdateInvoiceStatusAsync(
        TenantId tenantId, Guid bookingId, BookingInvoiceStatus status,
        string? provider = null, string? reference = null, string? errorCode = null, CancellationToken ct = default);

    /// <summary>Deposit transaction hiện tại của booking (P5 `/booking/deposits` + status DTO).</summary>
    Task<PaymentTransaction?> GetDepositAsync(TenantId tenantId, Guid bookingId, CancellationToken ct = default);
}
