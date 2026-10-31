using VanAn.Shared.Domain;
using VanAn.Shared.Domain.Common;

namespace VanAn.CoreHub.Infrastructure.Entities;

/// <summary>
/// SRS v1.2 W4/C2 (M8): mỗi lần thu tiền của 1 order = 1 payment event riêng
/// (partial/multi-method/multi-lần — FR-SAL-001/004/006, ACC12-003).
/// SourceEventId unique per tenant → idempotency theo PAYMENT, không theo order (ACC12-004).
/// Data-only Infrastructure entity (precedent: InternalTransferEntity — W3/D).
/// </summary>
public class OrderPaymentEntity : BaseEntity
{
    /// <summary>Order được thu tiền.</summary>
    public Guid OrderId { get; private set; }

    /// <summary>Số tiền thu lần này (bao gồm VAT).</summary>
    public decimal Amount { get; private set; }

    /// <summary>Hình thức thanh toán (CASH/VIETQR/TRANSFER/legacy...).</summary>
    public string? Method { get; private set; }

    /// <summary>Thời điểm thu.</summary>
    public DateTime PaidAt { get; private set; }

    /// <summary>Khóa idempotency — unique per tenant (transactionId / legacy-backfill key).</summary>
    public string SourceEventId { get; private set; } = string.Empty;

    /// <summary>Tham chiếu ngoài (số UNC / mã giao dịch ngân hàng).</summary>
    public string? Reference { get; private set; }

    /// <summary>Row synthetic từ backfill cho order đã Paid trước C2 (giữ tương thích).</summary>
    public bool IsSynthetic { get; private set; }

    private OrderPaymentEntity() { } // EF Core materialization

    public OrderPaymentEntity(TenantId tenantId, Guid orderId, decimal amount, string? method,
        string sourceEventId, DateTime? paidAt = null, string? reference = null, bool isSynthetic = false)
        : base(tenantId)
    {
        if (amount < 0) throw new ArgumentOutOfRangeException(nameof(amount), "Amount cannot be negative");
        OrderId = orderId;
        Amount = amount;
        Method = method;
        SourceEventId = sourceEventId;
        PaidAt = paidAt ?? DateTime.UtcNow;
        Reference = reference;
        IsSynthetic = isSynthetic;
    }
}
