using VanAn.Shared.Domain;
using VanAn.Shared.Domain.Common;

namespace VanAn.CoreHub.Infrastructure.Entities;

/// <summary>Hình thức thanh toán phiếu nhập mua (SRS v1.2 W6/E — card §2.1).</summary>
public enum PurchasePayMethod
{
    /// <summary>Mua chịu — JE Nợ {debit}/Có 331 + phiếu công nợ 331+ (CongNo ledger).</summary>
    Credit = 1,
    /// <summary>Trả ngay — JE Nợ {debit}/Có {cash} (111/112x).</summary>
    Immediate = 2
}

/// <summary>Trạng thái phiếu nhập mua — Posted → Returned (đảo toàn bộ, link gốc).</summary>
public enum PurchaseReceiptStatus
{
    Posted = 1,
    Returned = 2
}

/// <summary>
/// SRS v1.2 W6/E (FR-PUR-001/002/005 — task card E): phiếu nhập mua tối thiểu.
/// Persistence entity — KHÔNG domain aggregate (pattern InternalTransferEntity).
///
/// Posting (PurchaseReceiptService — qua account-map purchase/purchase-cash):
///   Credit    → JE Nợ {DebitAccountCode} net (+Nợ 133 VAT) / Có 331 total, purpose `purchase`
///   Immediate → JE Nợ {DebitAccountCode} net (+Nợ 133 VAT) / Có {CashAccountCode} total, purpose `purchase-cash`
///   Credit    → phiếu công nợ 331+ qua CongNoService.CreatePayableAsync (PayableEntryId link)
///   Return    → JE đảo purpose `purchase-return` (ReversedJournalId) + reversal phiếu công nợ.
///
/// Idempotency (FR-PUR-007): SourceEventId — sync/retry lặp cùng event → trả phiếu đã có.
/// JE ref (ReferenceType "PurchaseReceipt", ReferenceId=Id, purpose) — unique index Phase A cover.
/// </summary>
public class PurchaseReceiptEntity : BaseEntity
{
    /// <summary>Số chứng từ NM-YYYY-NNNNN (VoucherNumberService TypePurchaseReceipt).</summary>
    public string VoucherNumber { get; private set; } = string.Empty;

    public string SupplierName { get; private set; } = string.Empty;

    /// <summary>MST / mã NCC — ghi kèm diễn giải công nợ để truy vết (G11 precedent).</summary>
    public string? SupplierRef { get; private set; }

    /// <summary>Ngày nhập mua (ngày nghiệp vụ của JE + phiếu công nợ).</summary>
    public DateTime ReceiptDate { get; private set; }

    /// <summary>Tiền hàng chưa VAT = Σ(qty × price) — cộng dồn qua AddLine.</summary>
    public decimal NetAmount { get; private set; }

    /// <summary>VAT đầu vào (TK 133 khi đủ điều kiện khấu trừ — user nhập, 0 = không kê).</summary>
    public decimal VatAmount { get; private set; }

    /// <summary>NetAmount + VatAmount — Có 331/{cash} trên JE.</summary>
    public decimal TotalAmount { get; private set; }

    public PurchasePayMethod PayMethod { get; private set; }

    /// <summary>TK tiền khi trả ngay (111/112x). Null khi mua chịu.</summary>
    public string? CashAccountCode { get; private set; }

    /// <summary>TK ghi Nợ trên JE (156 hàng hóa DN · 611 mua hàng HTX · 6xx chi phí HKD) — tenant chọn.</summary>
    public string DebitAccountCode { get; private set; } = string.Empty;

    public PurchaseReceiptStatus Status { get; private set; } = PurchaseReceiptStatus.Posted;

    /// <summary>Số chứng từ/hóa đơn NCC (text tự do).</summary>
    public string? Reference { get; private set; }

    /// <summary>FR-PUR-007: khóa idempotency nghiệp vụ (ví dụ id chứng từ nguồn). Unique per tenant khi set.</summary>
    public string? SourceEventId { get; private set; }

    /// <summary>AccountingEntry công nợ 331+ tạo khi mua chịu (link receipt ↔ phiếu nợ ↔ thanh toán).</summary>
    public Guid? PayableEntryId { get; private set; }

    /// <summary>FR-RPT-006 (optional): hạn thanh toán NCC.</summary>
    public DateTime? DueDate { get; private set; }

    /// <summary>Lý do trả hàng (khi Status=Returned).</summary>
    public string? ReturnReason { get; private set; }

    public List<PurchaseReceiptLineEntity> Lines { get; private set; } = [];

    private PurchaseReceiptEntity() { } // EF Core materialization

    public PurchaseReceiptEntity(TenantId tenantId, string voucherNumber,
        string supplierName, string? supplierRef, DateTime receiptDate,
        PurchasePayMethod payMethod, string? cashAccountCode, string debitAccountCode,
        decimal vatAmount = 0m, string? reference = null, string? sourceEventId = null,
        DateTime? dueDate = null)
        : base(tenantId)
    {
        if (string.IsNullOrWhiteSpace(supplierName))
            throw new ArgumentException("SupplierName is required.", nameof(supplierName));
        if (string.IsNullOrWhiteSpace(debitAccountCode))
            throw new ArgumentException("DebitAccountCode is required.", nameof(debitAccountCode));
        if (vatAmount < 0)
            throw new ArgumentOutOfRangeException(nameof(vatAmount), "VAT amount cannot be negative");

        VoucherNumber = voucherNumber;
        SupplierName = supplierName;
        SupplierRef = supplierRef;
        ReceiptDate = receiptDate;
        PayMethod = payMethod;
        CashAccountCode = cashAccountCode;
        DebitAccountCode = debitAccountCode;
        VatAmount = vatAmount;
        Reference = reference;
        SourceEventId = sourceEventId;
        DueDate = dueDate;
    }

    /// <summary>Thêm dòng hàng — cộng dồn NetAmount/TotalAmount.</summary>
    public PurchaseReceiptLineEntity AddLine(string itemName, decimal quantity, decimal unitPrice)
    {
        var line = new PurchaseReceiptLineEntity(TenantId, Id, itemName, quantity, unitPrice);
        Lines.Add(line);
        NetAmount += line.LineTotal;
        TotalAmount = NetAmount + VatAmount;
        UpdateAudit();
        return line;
    }

    /// <summary>Gắn phiếu công nợ 331+ đã tạo (mua chịu).</summary>
    public void SetPayableEntry(Guid payableEntryId)
    {
        PayableEntryId = payableEntryId;
        UpdateAudit();
    }

    /// <summary>Trả hàng/điều chỉnh — chỉ từ Posted; JE đảo + reversal công nợ do service xử lý.</summary>
    public void MarkReturned(string reason)
    {
        if (Status != PurchaseReceiptStatus.Posted)
            throw new InvalidOperationException($"Cannot return receipt in status {Status}");
        Status = PurchaseReceiptStatus.Returned;
        ReturnReason = reason;
        UpdateAudit();
    }
}

/// <summary>Dòng hàng của phiếu nhập mua — text tự do (tối thiểu, không link Ingredient).</summary>
public class PurchaseReceiptLineEntity : BaseEntity
{
    public Guid PurchaseReceiptId { get; private set; }

    public string ItemName { get; private set; } = string.Empty;

    public decimal Quantity { get; private set; }

    public decimal UnitPrice { get; private set; }

    public decimal LineTotal => Quantity * UnitPrice;

    public PurchaseReceiptEntity Receipt { get; private set; } = null!;

    private PurchaseReceiptLineEntity() { } // EF Core materialization

    public PurchaseReceiptLineEntity(TenantId tenantId, Guid purchaseReceiptId,
        string itemName, decimal quantity, decimal unitPrice)
        : base(tenantId)
    {
        if (string.IsNullOrWhiteSpace(itemName))
            throw new ArgumentException("ItemName is required.", nameof(itemName));
        if (quantity <= 0)
            throw new ArgumentOutOfRangeException(nameof(quantity), "Quantity must be positive");
        if (unitPrice < 0)
            throw new ArgumentOutOfRangeException(nameof(unitPrice), "UnitPrice cannot be negative");

        PurchaseReceiptId = purchaseReceiptId;
        ItemName = itemName;
        Quantity = quantity;
        UnitPrice = unitPrice;
    }
}
