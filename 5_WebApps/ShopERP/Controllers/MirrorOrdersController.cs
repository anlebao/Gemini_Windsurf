using System.Globalization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VanAn.CoreHub.Infrastructure;
using VanAn.CoreHub.Services.Reconcile;
using VanAn.Shared.Domain;
using VanAn.ShopERP.Filters;

namespace VanAn.ShopERP.Controllers;

/// <summary>
/// Tra soát số liệu kế toán P2 (G8 — SRS v1.1): nội bộ API cho Gateway đọc
/// SQLite mirror Orders (replica PG→SQLite qua NATS routed delivery) phục vụ
/// check R11 cross-store (PG ↔ mirror). READ-ONLY tuyệt đối.
///
/// Auth: [InternalApiKey] — X-Internal-Api-Key (Gateway → ShopERP, cùng shared
/// secret InternalLoyalty:ApiKey). [AllowAnonymous] vì caller là service, không
/// phải user — filter xử lý auth riêng.
///
/// GET /api/shoperp/accounting/mirror-orders?period=YYYY-MM
/// Filter kỳ theo CreatedAt (chốt P2 — khớp paidOrders filter của reconcile service).
/// Trả MirrorOrderDto[] (không tenant-scoped — endpoint chạy trên instance của shop,
/// R11 lookup theo OrderId nên cross-tenant noise không ảnh hưởng).
/// </summary>
[ApiController]
[Route("api/shoperp/accounting/mirror-orders")]
[AllowAnonymous]
[InternalApiKey]
public class MirrorOrdersController(
    IVanAnDbContext dbContext,
    ILogger<MirrorOrdersController> logger) : ControllerBase
{
    private readonly IVanAnDbContext _dbContext = dbContext;
    private readonly ILogger<MirrorOrdersController> _logger = logger;

    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<MirrorOrderDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetMirrorOrders([FromQuery] string? period, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(period)
            || !DateTime.TryParseExact(period, "yyyy-MM", CultureInfo.InvariantCulture,
                DateTimeStyles.None, out DateTime start))
        {
            return BadRequest(new { error = "period là bắt buộc, dạng YYYY-MM (vd: 2026-10)." });
        }

        DateTime end = start.AddMonths(1);

        List<Order> orders = await _dbContext.Orders
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(o => o.CreatedAt >= start && o.CreatedAt < end)
            .OrderBy(o => o.CreatedAt)
            .ToListAsync(ct);

        var list = orders
            .Select(o => new MirrorOrderDto(
                o.Id, o.SourceType, o.Status?.Value, o.PaymentStatus, o.DepositPaidAmount, o.TotalAmount))
            .ToList();

        _logger.LogInformation("mirror-orders period={Period} → {Count} orders", period, list.Count);
        return Ok(list);
    }
}
