using VanAn.Shared.Domain;

namespace VanAn.CoreHub.Services.Booking;

/// <summary>
/// Fix 2b (2026-10-14, user approve): consumer cho 3 sự kiện tài chính booking
/// (DepositPaid / PaymentCaptured / ServiceCompleted) → sinh AccountingEntry đúng luồng
/// kế toán hiện có (511/3331 — như OrderService.GenerateAccountingEntriesAsync; cọc trả
/// trước xử lý bản chất riêng — TK 3387, KHÔNG tính doanh thu).
///
/// Triển khai IN-PROCESS tại thời điểm phát event (BookingFinancialService gọi trực tiếp) —
/// cùng process Gateway (PG), durable hơn NATS subscriber (không cửa sổ mất event khi
/// subscribe muộn), idempotent theo Reference:
///   - Revenue: "Booking:{bookingId}" (tối đa 1 lần — PaymentCaptured trước ServiceCompleted
///     ghi nhận trước, sau đó ServiceCompleted skip; ngược lại cũng vậy — KHÔNG double count).
///   - Deposit: "Booking:{bookingId}:Deposit:{txId}" (1 entry mỗi transaction cọc).
/// Fail-safe: lỗi kế toán KHÔNG fail luồng booking (log + retry ở lần gọi sau — idempotent).
/// </summary>
public interface IBookingAccountingService
{
    /// <summary>Cọc đã thanh toán (SecurityDeposit/PrepaymentForService) → TK 3387 (nhận trước), KHÔNG doanh thu.</summary>
    Task RecordDepositPaidAsync(TenantId tenantId, Guid bookingId, Guid txId, decimal amount, MoneyNatureType nature, DateTime paidAt, CancellationToken ct = default);

    /// <summary>Thanh toán cuối → ghi nhận doanh thu (511/3331) NẾU booking chưa có revenue (cash-basis).</summary>
    Task RecordFinalPaymentAsync(TenantId tenantId, Guid bookingId, Guid txId, decimal amount, DateTime paidAt, CancellationToken ct = default);

    /// <summary>Dịch vụ hoàn thành → ghi nhận doanh thu (511/3331) — anchor SRS "hoàn tất lịch → doanh thu về kế toán".</summary>
    Task RecordServiceCompletedAsync(TenantId tenantId, Guid bookingId, decimal total, DateTime? completedAt, CancellationToken ct = default);
}
