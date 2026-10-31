using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VanAn.CoreHub.Infrastructure;
using VanAn.CoreHub.Services.Reconcile;
using VanAn.Shared.Domain;
using VanAn.ShopERP.Filters;

namespace VanAn.ShopERP.Controllers;

/// <summary>
/// SRS v1.2 W6/E (FR-SUB-003/FR-RECNC-004 — task card E §2.2): nội bộ API cho Gateway
/// đọc GIÁ TRỊ TỒN KHO từ SQLite per-tenant (replica — KHÔNG nguồn kế toán) phục vụ
/// check R13 inventory-vs-GL (Σ tồn kho ↔ TK 15x trên journal).
///
/// Valuation = Σ(Ingredient.CurrentStock × PricePerUnit) — cost nguồn duy nhất hiện có
/// (KHÔNG có StockMovements/AverageCost table — card UNKNOWN đã resolve: snapshot
/// point-in-time, movement tổng kỳ không track được → không trả).
///
/// Auth: [InternalApiKey] — X-Internal-Api-Key (Gateway → ShopERP).
/// tenantId BẮT BUỘC — multi-tenancy filter tường minh (Ingredients là per-tenant data).
///
/// GET /api/shoperp/inventory/valuation?tenantId=&period=
/// </summary>
[ApiController]
[Route("api/shoperp/inventory/valuation")]
[AllowAnonymous]
[InternalApiKey]
public class InventoryValuationController(
    IVanAnDbContext dbContext,
    ILogger<InventoryValuationController> logger) : ControllerBase
{
    private readonly IVanAnDbContext _dbContext = dbContext;
    private readonly ILogger<InventoryValuationController> _logger = logger;

    [HttpGet]
    [ProducesResponseType(typeof(InventoryValuationDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetValuation([FromQuery] Guid? tenantId, [FromQuery] string? period, CancellationToken ct)
    {
        if (tenantId == null || tenantId == Guid.Empty)
        {
            return BadRequest(new { error = "tenantId là bắt buộc." });
        }

        var ingredients = await _dbContext.Ingredients
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(i => i.TenantId == new TenantId(tenantId.Value))
            .Select(i => new { i.CurrentStock, i.PricePerUnit })
            .ToListAsync(ct);

        decimal total = ingredients.Sum(i => i.CurrentStock * i.PricePerUnit);

        _logger.LogInformation("inventory-valuation tenant={TenantId} period={Period} → {Count} items, value {Total}",
            tenantId, period, ingredients.Count, total);
        return Ok(new InventoryValuationDto(tenantId.Value, ingredients.Count, total));
    }
}
