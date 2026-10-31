using VanAn.CoreHub.Infrastructure.Entities;
using VanAn.Shared.Domain;

namespace VanAn.CoreHub.Services.Stock;

/// <summary>Một dòng báo cáo Nhập–Xuất–Tồn theo nguyên liệu (GĐ2 W6).</summary>
public sealed record StockReportRow(
    Guid IngredientId,
    string Name,
    string Unit,
    decimal OpeningQty,
    decimal InQty,
    decimal OutQty,
    decimal ClosingQty,
    decimal UnitPrice,
    decimal ClosingValue);

/// <summary>
/// SRS v1.2 W6 tiếp (GĐ2): ghi/đọc StockMovement — nguồn audit tồn kho.
///
/// Implement ở ShopERP host (ShopERPDbContext — SQLite per-tenant, stock là business
/// data). CoreHub/Gateway host KHÔNG đăng ký → PurchaseReceiptService nhận optional
/// dependency và skip âm thầm (stock chỉ áp dụng tại host ShopERP nơi phiếu được lập).
///
/// Idempotency: ghi movement theo khóa (PurchaseReceiptId, PurchaseReceiptLineId,
/// MovementType) — retry Post/Return/ApplyStock không tạo movement trùng.
/// </summary>
public interface IStockMovementService
{
    /// <summary>Ghi movement PurchaseIn cho các dòng có IngredientId + tăng CurrentStock +
    /// cập nhật PricePerUnit = giá nhập mới nhất (user chốt). Trả số movement đã ghi
    /// (0 khi không có dòng link hoặc tất cả đã ghi trước đó).</summary>
    Task<int> ApplyReceiptStockAsync(PurchaseReceiptEntity receipt, CancellationToken ct = default);

    /// <summary>Ghi movement PurchaseReturn cho các dòng đã có PurchaseIn + giảm CurrentStock.
    /// Idempotent theo (lineId, PurchaseReturn). Cho phép tồn âm (đã tiêu hao trước trả).</summary>
    Task<int> ApplyReturnStockAsync(PurchaseReceiptEntity receipt, CancellationToken ct = default);

    /// <summary>Nhập/xuất tay (trang Nguyên liệu) — ManualIn/ManualOut + cập nhật CurrentStock.
    /// Xuất vượt tồn → InvalidOperationException.</summary>
    Task<StockMovementEntity> RecordManualAsync(TenantId tenantId, Guid ingredientId, bool isIn,
        decimal quantity, decimal? unitCost, string? note, string? createdBy,
        CancellationToken ct = default);

    /// <summary>Danh sách movement (mới nhất trước) — drill-down báo cáo/thẻ kho.</summary>
    Task<List<StockMovementEntity>> ListMovementsAsync(TenantId tenantId, Guid? ingredientId = null,
        DateTime? from = null, DateTime? to = null, CancellationToken ct = default);

    /// <summary>
    /// Báo cáo Nhập–Xuất–Tồn theo kỳ [from, to): Tồn đầu / Nhập / Xuất / Tồn cuối / Giá trị.
    /// Baseline = CurrentStock − Σ(mọi movement) → opening/closing chính xác kể cả tồn kho
    /// có sẵn trước khi bảng movement ra đời; tồn cuối khớp snapshot khi to = hiện tại.
    /// </summary>
    Task<List<StockReportRow>> GetStockReportAsync(TenantId tenantId, DateTime from, DateTime to,
        CancellationToken ct = default);
}
