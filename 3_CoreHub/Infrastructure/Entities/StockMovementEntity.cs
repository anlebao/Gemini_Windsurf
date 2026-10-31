using VanAn.Shared.Domain;
using VanAn.Shared.Domain.Common;

namespace VanAn.CoreHub.Infrastructure.Entities;

/// <summary>Loại biến động kho — dấu (+/−) suy ra từ loại, Quantity luôn dương.</summary>
public enum StockMovementType
{
    /// <summary>Nhập từ phiếu mua (link PurchaseReceiptLine).</summary>
    PurchaseIn = 1,
    /// <summary>Xuất trả hàng cho NCC (link PurchaseReceiptLine gốc).</summary>
    PurchaseReturn = 2,
    /// <summary>Nhập tay trên trang Nguyên liệu.</summary>
    ManualIn = 3,
    /// <summary>Xuất tay trên trang Nguyên liệu.</summary>
    ManualOut = 4,
    /// <summary>Điều chỉnh tăng (kiểm kê/chênh lệch dư).</summary>
    AdjustmentIn = 5,
    /// <summary>Điều chỉnh giảm (kiểm kê/hao hụt).</summary>
    AdjustmentOut = 6
}

/// <summary>
/// SRS v1.2 W6 tiếp (GĐ2 — user chốt 2026-10-31): StockMovement — nguồn audit chính
/// của tồn kho. Tồn kho = Σ movements (CurrentStock trên Ingredient chỉ là snapshot/cache;
/// báo cáo nhập–xuất–tồn rebuild được từ bảng này).
///
/// Persistence entity — KHÔNG domain aggregate (precedent PurchaseReceiptEntity).
/// CHỈ map trong ShopERPDbContext (SQLite per-tenant — stock là business data, không phải
/// accounting). PurchaseReceipt tham chiếu sống ở PG — link qua Id, không FK vật lý (cross-DB).
///
/// Idempotency: (PurchaseReceiptId, PurchaseReceiptLineId, MovementType) — service check
/// trước khi ghi (retry Post/ApplyStock/Return không tạo movement trùng).
/// </summary>
public class StockMovementEntity : BaseEntity
{
    /// <summary>Nguyên liệu (Ingredients.Id — cùng DB SQLite).</summary>
    public Guid IngredientId { get; private set; }

    public StockMovementType MovementType { get; private set; }

    /// <summary>Số lượng (luôn dương — hướng nhập/xuất suy ra từ MovementType).</summary>
    public decimal Quantity { get; private set; }

    /// <summary>Đơn giá nhập (dòng mua) — dùng cho định giá/đối chiếu; null với nhập tay không giá.</summary>
    public decimal? UnitCost { get; private set; }

    /// <summary>Phiếu nhập mua gốc (PG PurchaseReceipts.Id) — link logic cross-DB, KHÔNG FK.</summary>
    public Guid? PurchaseReceiptId { get; private set; }

    /// <summary>Dòng phiếu mua gốc (PG PurchaseReceiptLines.Id) — khóa idempotency kèm MovementType.</summary>
    public Guid? PurchaseReceiptLineId { get; private set; }

    /// <summary>Ngày nghiệp vụ (ReceiptDate của phiếu / thời điểm nhập tay).</summary>
    public DateTime MovementDate { get; private set; }

    public string? Note { get; private set; }

    /// <summary>TRUE với PurchaseIn/ManualIn/AdjustmentIn (cộng tồn); FALSE với nhóm xuất.</summary>
    public bool IsIn => MovementType is StockMovementType.PurchaseIn
        or StockMovementType.ManualIn or StockMovementType.AdjustmentIn;

    /// <summary>Delta tồn kho có dấu — cộng dồn để rebuild tồn.</summary>
    public decimal SignedQuantity => IsIn ? Quantity : -Quantity;

    private StockMovementEntity() { } // EF Core materialization

    public StockMovementEntity(TenantId tenantId, Guid ingredientId,
        StockMovementType movementType, decimal quantity, decimal? unitCost,
        DateTime movementDate, string? note = null,
        Guid? purchaseReceiptId = null, Guid? purchaseReceiptLineId = null,
        string? createdBy = null)
        : base(tenantId)
    {
        if (ingredientId == Guid.Empty)
            throw new ArgumentException("IngredientId is required.", nameof(ingredientId));
        if (quantity <= 0)
            throw new ArgumentOutOfRangeException(nameof(quantity), "Quantity must be positive");
        if (unitCost < 0)
            throw new ArgumentOutOfRangeException(nameof(unitCost), "UnitCost cannot be negative");

        IngredientId = ingredientId;
        MovementType = movementType;
        Quantity = quantity;
        UnitCost = unitCost;
        MovementDate = movementDate;
        Note = note;
        PurchaseReceiptId = purchaseReceiptId;
        PurchaseReceiptLineId = purchaseReceiptLineId;
        CreatedBy = createdBy; // BaseEntity.CreatedBy (protected set — audit người nhập tay)
    }
}
