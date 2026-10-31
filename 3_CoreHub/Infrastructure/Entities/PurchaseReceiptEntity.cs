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

/// <summary>
/// Trạng thái phiếu nhập mua — luồng 1 cấp duyệt (W6 tiếp, user chốt 2026-10-31):
/// Draft → Approved → Posted → Returned (đảo toàn bộ, link gốc).
/// Draft|Approved → Cancelled. JE/công nợ/kho CHỈ ghi khi Post (không phải lúc tạo).
/// Giá trị int giữ nguyên Posted=1/Returned=2 cho data đã deploy (W6/E).
/// </summary>
public enum PurchaseReceiptStatus
{
    Draft = 0,
    Posted = 1,
    Returned = 2,
    Approved = 3,
    Cancelled = 4
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

    public PurchaseReceiptStatus Status { get; private set; } = PurchaseReceiptStatus.Draft;

    /// <summary>Thời điểm + người duyệt (Draft → Approved).</summary>
    public DateTime? ApprovedAt { get; private set; }
    public string? ApprovedBy { get; private set; }

    /// <summary>Thời điểm ghi sổ (Approved → Posted — JE + công nợ + kho).</summary>
    public DateTime? PostedAt { get; private set; }

    /// <summary>Đã ghi StockMovement cho các dòng có IngredientId (null → chưa/drift — retry qua ApplyStock).</summary>
    public DateTime? StockAppliedAt { get; private set; }

    /// <summary>Lý do hủy (Draft|Approved → Cancelled).</summary>
    public string? CancelReason { get; private set; }

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

    /// <summary>Thêm dòng hàng — cộng dồn NetAmount/TotalAmount. Chỉ khi còn Draft.</summary>
    public PurchaseReceiptLineEntity AddLine(string itemName, decimal quantity, decimal unitPrice,
        Guid? ingredientId = null)
    {
        if (Status != PurchaseReceiptStatus.Draft)
            throw new InvalidOperationException($"Cannot add lines to receipt in status {Status}");
        var line = new PurchaseReceiptLineEntity(TenantId, Id, itemName, quantity, unitPrice, ingredientId);
        Lines.Add(line);
        NetAmount += line.LineTotal;
        TotalAmount = NetAmount + VatAmount;
        UpdateAudit();
        return line;
    }

    /// <summary>Cập nhật header — chỉ khi còn Draft (phiếu đã duyệt/post khóa sửa).</summary>
    public void UpdateDraft(string supplierName, string? supplierRef, DateTime receiptDate,
        PurchasePayMethod payMethod, string? cashAccountCode, string debitAccountCode,
        decimal vatAmount, string? reference, DateTime? dueDate)
    {
        if (Status != PurchaseReceiptStatus.Draft)
            throw new InvalidOperationException($"Cannot edit receipt in status {Status}");
        if (string.IsNullOrWhiteSpace(supplierName))
            throw new ArgumentException("SupplierName is required.", nameof(supplierName));
        if (string.IsNullOrWhiteSpace(debitAccountCode))
            throw new ArgumentException("DebitAccountCode is required.", nameof(debitAccountCode));
        if (vatAmount < 0)
            throw new ArgumentOutOfRangeException(nameof(vatAmount), "VAT amount cannot be negative");

        SupplierName = supplierName;
        SupplierRef = supplierRef;
        ReceiptDate = receiptDate;
        PayMethod = payMethod;
        CashAccountCode = payMethod == PurchasePayMethod.Immediate ? cashAccountCode : null;
        DebitAccountCode = debitAccountCode;
        VatAmount = vatAmount;
        Reference = reference;
        DueDate = dueDate;
        TotalAmount = NetAmount + VatAmount;
        UpdateAudit();
    }

    /// <summary>Set NetAmount/TotalAmount trực tiếp — dùng khi service thay dòng qua DbSet
    /// (không qua nav AddLine — tránh EF fixup trên collection đã track).</summary>
    public void SetTotals(decimal netAmount)
    {
        NetAmount = netAmount;
        TotalAmount = NetAmount + VatAmount;
        UpdateAudit();
    }

    /// <summary>Xóa toàn bộ dòng để nhập lại — chỉ khi còn Draft.</summary>
    public void ClearLines()
    {
        if (Status != PurchaseReceiptStatus.Draft)
            throw new InvalidOperationException($"Cannot clear lines in status {Status}");
        Lines.Clear();
        NetAmount = 0;
        TotalAmount = VatAmount;
        UpdateAudit();
    }

    /// <summary>Duyệt phiếu (1 cấp) — Draft → Approved.</summary>
    public void Approve(string? approvedBy)
    {
        if (Status != PurchaseReceiptStatus.Draft)
            throw new InvalidOperationException($"Cannot approve receipt in status {Status}");
        Status = PurchaseReceiptStatus.Approved;
        ApprovedAt = DateTime.UtcNow;
        ApprovedBy = approvedBy;
        UpdateAudit();
    }

    /// <summary>Ghi sổ — Approved → Posted. JE/công nợ/kho do service xử lý trước khi gọi.</summary>
    public void MarkPosted()
    {
        if (Status != PurchaseReceiptStatus.Approved)
            throw new InvalidOperationException($"Cannot post receipt in status {Status}");
        Status = PurchaseReceiptStatus.Posted;
        PostedAt = DateTime.UtcNow;
        UpdateAudit();
    }

    /// <summary>Hủy phiếu — chỉ từ Draft|Approved (Posted phải trả hàng, không hủy).</summary>
    public void Cancel(string reason)
    {
        if (Status is not (PurchaseReceiptStatus.Draft or PurchaseReceiptStatus.Approved))
            throw new InvalidOperationException($"Cannot cancel receipt in status {Status}");
        Status = PurchaseReceiptStatus.Cancelled;
        CancelReason = reason;
        UpdateAudit();
    }

    /// <summary>Đánh dấu đã ghi StockMovement (idempotency — retry ApplyStock skip khi đã set).</summary>
    public void MarkStockApplied()
    {
        StockAppliedAt = DateTime.UtcNow;
        UpdateAudit();
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

/// <summary>
/// Dòng hàng của phiếu nhập mua. IngredientId (nullable) link sang kho SQLite —
/// khi Post, dòng có IngredientId sinh StockMovement PurchaseIn + cập nhật tồn kho;
/// dòng null = chi phí/hàng không tồn kho (text tự do, giữ nguyên hành vi W6/E).
/// </summary>
public class PurchaseReceiptLineEntity : BaseEntity
{
    public Guid PurchaseReceiptId { get; private set; }

    public string ItemName { get; private set; } = string.Empty;

    /// <summary>Nguyên liệu kho (ShopERP SQLite Ingredients.Id) — null = dòng tự do không ghi kho.</summary>
    public Guid? IngredientId { get; private set; }

    public decimal Quantity { get; private set; }

    public decimal UnitPrice { get; private set; }

    public decimal LineTotal => Quantity * UnitPrice;

    public PurchaseReceiptEntity Receipt { get; private set; } = null!;

    private PurchaseReceiptLineEntity() { } // EF Core materialization

    public PurchaseReceiptLineEntity(TenantId tenantId, Guid purchaseReceiptId,
        string itemName, decimal quantity, decimal unitPrice, Guid? ingredientId = null)
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
        IngredientId = ingredientId;
        Quantity = quantity;
        UnitPrice = unitPrice;
    }
}
