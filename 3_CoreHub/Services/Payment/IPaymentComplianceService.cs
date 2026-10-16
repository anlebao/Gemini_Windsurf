using VanAn.Shared.Domain;

namespace VanAn.CoreHub.Services.Payment;

/// <summary>
/// P0.1 (2026-10-16 — master plan accounting-legal-compliance): Kiểm soát thanh toán ≥ 20 triệu
/// không dùng tiền mặt — TT 219/2013/TT-BTC Điều 15.2.b (GTGT) · TT 96/2015/TT-BTC Điều 4 (TNDN)
/// · hướng dẫn TCT gộp hóa đơn cùng ngày cùng nhà cung cấp.
/// Quyết định user Q1 (2026-10-16): CẢNH BÁO + bắt buộc ghi lý do — KHÔNG chặn cứng.
/// </summary>
public interface IPaymentComplianceService
{
    /// <summary>Ngưỡng kiểm soát: 20.000.000 VNĐ.</summary>
    decimal Threshold { get; }

    /// <summary>
    /// Đánh giá 1 giao dịch: khi chọn hình thức thanh toán tiền mặt (CASH/COD) với tổng ≥ ngưỡng
    /// → cần xác nhận lý do (NeedsReview = true). Thuần tính toán — không đọc DB.
    /// </summary>
    Task<PaymentComplianceResult> EvaluateAsync(
        TenantId tenantId,
        decimal amount,
        string? paymentMethod,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Báo cáo rà soát cuối kỳ phục vụ thanh tra: (1) đơn hàng ≥ ngưỡng thanh toán tiền mặt (Paid);
    /// (2) gộp phiếu cùng ngày cùng nhà cung cấp (AccountingEntry.Vendor) tổng ≥ ngưỡng.
    /// </summary>
    Task<CashPaymentReviewDto> GetCashPaymentReviewAsync(
        TenantId tenantId,
        int year,
        int month,
        CancellationToken cancellationToken = default);
}

/// <summary>Kết quả đánh giá 1 giao dịch thanh toán.</summary>
public sealed record PaymentComplianceResult(
    decimal Amount,
    decimal Threshold,
    bool IsCashLike,
    bool NeedsReview);

/// <summary>Đơn hàng ≥ ngưỡng thanh toán tiền mặt trong kỳ.</summary>
public sealed record CashPaymentReviewItemDto(
    Guid OrderId,
    string TrackingCode,
    DateTime OrderDate,
    decimal Amount,
    string PaymentMethod);

/// <summary>Gộp phiếu cùng ngày cùng nhà cung cấp có tổng ≥ ngưỡng.</summary>
public sealed record SameDayAggregateDto(
    string Vendor,
    DateTime Date,
    decimal TotalAmount,
    int EntryCount);

/// <summary>Báo cáo rà soát thanh toán tiền mặt cuối kỳ.</summary>
public sealed record CashPaymentReviewDto(
    int Year,
    int Month,
    decimal Threshold,
    IReadOnlyList<CashPaymentReviewItemDto> Items,
    IReadOnlyList<SameDayAggregateDto> SameDayAggregates);