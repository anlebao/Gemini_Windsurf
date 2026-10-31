using VanAn.Shared.Domain;
using VanAn.Shared.Domain.Common;

namespace VanAn.CoreHub.Infrastructure.Entities;

/// <summary>Trạng thái phiếu chuyển tiền nội bộ (AT-011 — Received mới chốt số dư).</summary>
public enum InternalTransferStatus
{
    Draft = 0,
    InTransit = 1,
    Received = 2,
    Discrepancy = 3,
    Cancelled = 4
}

/// <summary>
/// SRS v1.2 W3/D (M9): phiếu chuyển tiền nội bộ giữa các TK tiền (111↔112x).
/// KHÔNG tạo AccountingEntry (không phải doanh thu/chi phí) — chỉ JournalEntry qua
/// account-map: transfer-out → Nợ {đích|113}/Có {nguồn} · transfer-in → Nợ {đích}/Có 113 ·
/// transfer-fee → Nợ {642}/Có {nguồn} (phí riêng — FR-TRF-004).
/// Sửa/đảo: không sửa khi đã Received — tạo đảo (reversal pattern) + ReversedById.
/// </summary>
public class InternalTransferEntity : BaseEntity
{
    /// <summary>Số chứng từ CT-YYYY-NNNNN (VoucherNumberService).</summary>
    public string VoucherNumber { get; private set; } = string.Empty;

    /// <summary>TK nguồn (111/112x) — Có trên JE out.</summary>
    public string FromAccountCode { get; private set; } = string.Empty;

    /// <summary>TK đích (111/112x) — Nợ trên JE in (hoặc ngay JE out khi không qua 113).</summary>
    public string ToAccountCode { get; private set; } = string.Empty;

    public decimal Amount { get; private set; }

    /// <summary>Phí chuyển tiền — JE riêng Nợ FeeAccountCode/Có FromAccountCode.</summary>
    public decimal? FeeAmount { get; private set; }

    /// <summary>TK phí (mặc định 642 — chi phí quản lý). Null khi không có phí.</summary>
    public string? FeeAccountCode { get; private set; }

    public InternalTransferStatus Status { get; private set; } = InternalTransferStatus.Draft;

    /// <summary>Ngày lập/chuyển (ngày nghiệp vụ của JE out).</summary>
    public DateTime TransferDate { get; private set; }

    /// <summary>Ngày nhận tiền tại đích (ngày nghiệp vụ của JE in — chỉ có khi đã qua InTransit).</summary>
    public DateTime? ReceivedDate { get; private set; }

    /// <summary>Số tham chiếu ngân hàng / UNC.</summary>
    public string? BankReference { get; private set; }

    public string? Description { get; private set; }

    /// <summary>Id phiếu đảo — đảo transfer đã Received tạo phiếu ngược và link về đây.</summary>
    public Guid? ReversedById { get; private set; }

    private InternalTransferEntity() { } // EF Core materialization

    /// <summary>FR-TRF-002/003: inTransit=true → JE out qua 113 Tiền đang chuyển, status InTransit chờ nhận.</summary>
    public InternalTransferEntity(TenantId tenantId, string voucherNumber,
        string fromAccountCode, string toAccountCode, decimal amount,
        decimal? feeAmount = null, string? feeAccountCode = null,
        bool inTransit = false, DateTime? transferDate = null,
        string? bankReference = null, string? description = null)
        : base(tenantId)
    {
        VoucherNumber = voucherNumber;
        FromAccountCode = fromAccountCode;
        ToAccountCode = toAccountCode;
        Amount = amount;
        FeeAmount = feeAmount;
        FeeAccountCode = feeAccountCode;
        Status = inTransit ? InternalTransferStatus.InTransit : InternalTransferStatus.Draft;
        TransferDate = transferDate ?? DateTime.UtcNow;
        BankReference = bankReference;
        Description = description;
    }

    /// <summary>Immediate path: Draft → Received ngay sau khi post JE Nợ đích/Có nguồn (CK01/02).</summary>
    public void ForceReceived(DateTime? receivedDate = null)
    {
        if (Status != InternalTransferStatus.Draft)
            throw new InvalidOperationException($"Cannot force-receive transfer in status {Status}");
        Status = InternalTransferStatus.Received;
        ReceivedDate = receivedDate ?? DateTime.UtcNow;
        UpdateAudit();
    }

    /// <summary>InTransit → Cancelled — phiếu out đã post JE, hủy kèm JE đảo về nguồn.</summary>
    public void MarkCancelledFromTransit()
    {
        if (Status != InternalTransferStatus.InTransit)
            throw new InvalidOperationException($"Cannot cancel in-transit transfer in status {Status}");
        Status = InternalTransferStatus.Cancelled;
        UpdateAudit();
    }

    /// <summary>Xác nhận tiền đã về đích (InTransit → Received).</summary>
    public void MarkReceived(DateTime? receivedDate = null)
    {
        if (Status != InternalTransferStatus.InTransit)
            throw new InvalidOperationException($"Cannot receive transfer in status {Status}");
        Status = InternalTransferStatus.Received;
        ReceivedDate = receivedDate ?? DateTime.UtcNow;
        UpdateAudit();
    }

    /// <summary>Đánh dấu lệch số — xử lý manual + audit (AT-011).</summary>
    public void MarkDiscrepancy()
    {
        if (Status != InternalTransferStatus.InTransit)
            throw new InvalidOperationException($"Cannot mark discrepancy in status {Status}");
        Status = InternalTransferStatus.Discrepancy;
        UpdateAudit();
    }

    /// <summary>Hủy phiếu Draft (chưa có JE nào).</summary>
    public void Cancel()
    {
        if (Status != InternalTransferStatus.Draft)
            throw new InvalidOperationException($"Cannot cancel transfer in status {Status} — dùng phiếu đảo");
        Status = InternalTransferStatus.Cancelled;
        UpdateAudit();
    }

    /// <summary>Đánh dấu đã bị đảo bởi phiếu reversalId (Received → đảo ngược lại).</summary>
    public void MarkReversed(Guid reversalId)
    {
        if (Status != InternalTransferStatus.Received)
            throw new InvalidOperationException($"Cannot reverse transfer in status {Status}");
        ReversedById = reversalId;
        UpdateAudit();
    }
}
