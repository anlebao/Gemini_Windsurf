using Microsoft.EntityFrameworkCore;
using VanAn.CoreHub.Infrastructure.Entities;
using VanAn.CoreHub.Services.Stock;
using VanAn.Shared.Domain;
using VanAn.ShopERP.Infrastructure;

namespace VanAn.ShopERP.Services;

/// <summary>
/// SRS v1.2 W6 tiếp (GĐ2): ghi StockMovement + rebuild báo cáo NXT — ShopERP SQLite
/// (stock là business data per-tenant). Chỉ đăng ký ở ShopERP host — CoreHub/Gateway
/// không có → PurchaseReceiptService skip âm thầm (optional dependency).
///
/// Idempotency: movement kiểm tra theo (PurchaseReceiptId, PurchaseReceiptLineId,
/// MovementType) trước khi ghi — retry Post/Return không trùng.
/// Cost: PurchaseIn cập nhật Ingredient.PricePerUnit = giá nhập mới nhất (user chốt).
/// </summary>
public sealed class StockMovementService(
    ShopERPDbContext dbContext,
    ILogger<StockMovementService> logger) : IStockMovementService
{
    public async Task<int> ApplyReceiptStockAsync(PurchaseReceiptEntity receipt, CancellationToken ct = default)
    {
        var linked = receipt.Lines.Where(l => l.IngredientId.HasValue).ToList();
        if (linked.Count == 0)
        {
            return 0;
        }

        var ingredientIds = linked.Select(l => l.IngredientId!.Value).ToList();
        List<Ingredient> ingredients = await dbContext.Ingredients.IgnoreQueryFilters()
            .Where(i => i.TenantId == receipt.TenantId && ingredientIds.Contains(i.Id))
            .ToListAsync(ct);

        // Idempotent: bỏ qua dòng đã có movement PurchaseIn.
        List<Guid> alreadyIn = await dbContext.StockMovements.IgnoreQueryFilters()
            .Where(m => m.TenantId == receipt.TenantId && m.PurchaseReceiptId == receipt.Id
                && m.MovementType == StockMovementType.PurchaseIn && m.PurchaseReceiptLineId != null)
            .Select(m => m.PurchaseReceiptLineId!.Value)
            .ToListAsync(ct);

        int written = 0;
        foreach (PurchaseReceiptLineEntity line in linked)
        {
            if (alreadyIn.Contains(line.Id))
            {
                continue;
            }
            Ingredient? ingredient = ingredients.FirstOrDefault(i => i.Id == line.IngredientId!.Value);
            if (ingredient == null)
            {
                logger.LogWarning("PurchaseReceipt {ReceiptId}: Ingredient {IngredientId} không tồn tại — skip line {LineId} (drift → R13)",
                    receipt.Id, line.IngredientId, line.Id);
                continue;
            }

            ingredient.CurrentStock += line.Quantity;
            ingredient.PricePerUnit = line.UnitPrice; // giá nhập mới nhất — nguồn cost cho R13/NXT
            dbContext.StockMovements.Add(new StockMovementEntity(receipt.TenantId, ingredient.Id,
                StockMovementType.PurchaseIn, line.Quantity, line.UnitPrice,
                receipt.ReceiptDate, $"Nhập mua {receipt.VoucherNumber} — {receipt.SupplierName}",
                receipt.Id, line.Id));
            written++;
        }

        await dbContext.SaveChangesAsync(ct);
        return written;
    }

    public async Task<int> ApplyReturnStockAsync(PurchaseReceiptEntity receipt, CancellationToken ct = default)
    {
        var linked = receipt.Lines.Where(l => l.IngredientId.HasValue).ToList();
        if (linked.Count == 0)
        {
            return 0;
        }

        var lineIds = linked.Select(l => l.Id).ToList();
        List<StockMovementEntity> existing = await dbContext.StockMovements.IgnoreQueryFilters()
            .Where(m => m.TenantId == receipt.TenantId && m.PurchaseReceiptId == receipt.Id
                && m.PurchaseReceiptLineId != null && lineIds.Contains(m.PurchaseReceiptLineId.Value))
            .ToListAsync(ct);

        var ingredientIds = linked.Select(l => l.IngredientId!.Value).ToList();
        List<Ingredient> ingredients = await dbContext.Ingredients.IgnoreQueryFilters()
            .Where(i => i.TenantId == receipt.TenantId && ingredientIds.Contains(i.Id))
            .ToListAsync(ct);

        int written = 0;
        foreach (PurchaseReceiptLineEntity line in linked)
        {
            // Chỉ xuất trả cho dòng ĐÃ có PurchaseIn và CHƯA có PurchaseReturn (idempotent).
            bool hasIn = existing.Any(m => m.PurchaseReceiptLineId == line.Id && m.MovementType == StockMovementType.PurchaseIn);
            bool hasReturn = existing.Any(m => m.PurchaseReceiptLineId == line.Id && m.MovementType == StockMovementType.PurchaseReturn);
            if (!hasIn || hasReturn)
            {
                continue;
            }
            Ingredient? ingredient = ingredients.FirstOrDefault(i => i.Id == line.IngredientId!.Value);
            if (ingredient == null)
            {
                logger.LogWarning("PurchaseReceipt {ReceiptId} return: Ingredient {IngredientId} không tồn tại — skip line {LineId}",
                    receipt.Id, line.IngredientId, line.Id);
                continue;
            }

            // Cho phép tồn âm — hàng có thể đã tiêu hao trước khi trả NCC.
            ingredient.CurrentStock -= line.Quantity;
            dbContext.StockMovements.Add(new StockMovementEntity(receipt.TenantId, ingredient.Id,
                StockMovementType.PurchaseReturn, line.Quantity, line.UnitPrice,
                DateTime.UtcNow, $"Trả hàng {receipt.VoucherNumber} — {receipt.ReturnReason}",
                receipt.Id, line.Id));
            written++;
        }

        await dbContext.SaveChangesAsync(ct);
        return written;
    }

    public async Task<StockMovementEntity> RecordManualAsync(TenantId tenantId, Guid ingredientId, bool isIn,
        decimal quantity, decimal? unitCost, string? note, string? createdBy, CancellationToken ct = default)
    {
        if (quantity <= 0)
            throw new ArgumentOutOfRangeException(nameof(quantity), "Số lượng phải lớn hơn 0.");

        Ingredient? ingredient = await dbContext.Ingredients.IgnoreQueryFilters()
            .FirstOrDefaultAsync(i => i.TenantId == tenantId && i.Id == ingredientId, ct)
            ?? throw new KeyNotFoundException($"Ingredient {ingredientId} not found");

        decimal delta = isIn ? quantity : -quantity;
        decimal newStock = ingredient.CurrentStock + delta;
        if (newStock < 0)
            throw new InvalidOperationException($"Không đủ tồn kho để xuất (còn {ingredient.CurrentStock:N0} {ingredient.Unit}).");

        ingredient.CurrentStock = newStock;
        var movement = new StockMovementEntity(tenantId, ingredient.Id,
            isIn ? StockMovementType.ManualIn : StockMovementType.ManualOut,
            quantity, unitCost, DateTime.UtcNow, note, createdBy: createdBy);
        dbContext.StockMovements.Add(movement);
        await dbContext.SaveChangesAsync(ct);
        return movement;
    }

    public async Task<List<StockMovementEntity>> ListMovementsAsync(TenantId tenantId, Guid? ingredientId = null,
        DateTime? from = null, DateTime? to = null, CancellationToken ct = default)
    {
        return await dbContext.StockMovements.IgnoreQueryFilters()
            .AsNoTracking()
            .Where(m => m.TenantId == tenantId
                && (ingredientId == null || m.IngredientId == ingredientId)
                && (from == null || m.MovementDate >= from) && (to == null || m.MovementDate < to))
            .OrderByDescending(m => m.MovementDate)
            .ThenByDescending(m => m.CreatedAt)
            .Take(500)
            .ToListAsync(ct);
    }

    public async Task<List<StockReportRow>> GetStockReportAsync(TenantId tenantId, DateTime from, DateTime to,
        CancellationToken ct = default)
    {
        List<Ingredient> ingredients = await dbContext.Ingredients.IgnoreQueryFilters()
            .AsNoTracking()
            .Where(i => i.TenantId == tenantId)
            .OrderBy(i => i.Name)
            .ToListAsync(ct);
        // Toàn bộ movement của tenant — baseline = CurrentStock − Σ(mọi movement) đảm bảo
        // tồn cuối khớp snapshot kể cả tồn kho có sẵn trước khi bảng movement ra đời.
        List<StockMovementEntity> movements = await dbContext.StockMovements.IgnoreQueryFilters()
            .AsNoTracking()
            .Where(m => m.TenantId == tenantId)
            .ToListAsync(ct);

        var rows = new List<StockReportRow>(ingredients.Count);
        var movementIngredientIds = movements.Select(m => m.IngredientId).ToHashSet();

        foreach (Ingredient ingredient in ingredients)
        {
            rows.Add(BuildRow(ingredient.Id, ingredient.Name, ingredient.Unit,
                ingredient.CurrentStock, ingredient.PricePerUnit,
                movements.Where(m => m.IngredientId == ingredient.Id).ToList(), from, to));
        }

        // Movement của nguyên liệu đã xóa — vẫn hiển thị để audit đầy đủ.
        foreach (Guid orphanId in movementIngredientIds.Where(id => ingredients.All(i => i.Id != id)))
        {
            rows.Add(BuildRow(orphanId, "(đã xóa)", "", 0m, 0m,
                movements.Where(m => m.IngredientId == orphanId).ToList(), from, to));
        }

        return rows;
    }

    private static StockReportRow BuildRow(Guid ingredientId, string name, string unit,
        decimal currentStock, decimal unitPrice, List<StockMovementEntity> movements,
        DateTime from, DateTime to)
    {
        decimal totalDelta = movements.Sum(m => m.SignedQuantity);
        decimal baseline = currentStock - totalDelta;
        decimal opening = baseline + movements.Where(m => m.MovementDate < from).Sum(m => m.SignedQuantity);
        decimal inQty = movements.Where(m => m.MovementDate >= from && m.MovementDate < to && m.IsIn).Sum(m => m.Quantity);
        decimal outQty = movements.Where(m => m.MovementDate >= from && m.MovementDate < to && !m.IsIn).Sum(m => m.Quantity);
        decimal closing = opening + inQty - outQty;
        return new StockReportRow(ingredientId, name, unit, opening, inQty, outQty, closing,
            unitPrice, closing * unitPrice);
    }
}
