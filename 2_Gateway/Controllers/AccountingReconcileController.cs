using System.Globalization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VanAn.CoreHub.Infrastructure;
using VanAn.CoreHub.Services;
using VanAn.CoreHub.Services.Reconcile;
using VanAn.Shared.Domain;
using VanAn.Shared.Domain.Audit;
using TenantEntity = VanAn.Shared.Domain.Aggregates.TenantAggregate.Tenant;

namespace VanAn.Gateway.Controllers;

/// <summary>
/// Tra soát số liệu kế toán P3 (SRS v1.1 — G9 SystemAdmin only):
/// GET /api/admin/accounting/reconcile?tenantId={guid}&period=YYYY-MM
/// → chạy R1-R12 read-only trên PG (AccountingReconcileService) → trả summary + checks.
/// period hỏng → 400 · tenant không tồn tại → 404 · audit log (tenant, kỳ, user, summary).
/// </summary>
[ApiController]
[Route("api/admin/accounting/reconcile")]
[Authorize(Policy = "SystemAdmin", AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
public class AccountingReconcileController(
    IAccountingReconcileService reconcileService,
    IVanAnDbContext db,
    IAuditTrailService auditTrailService,
    ILogger<AccountingReconcileController> logger) : ControllerBase
{
    private readonly IAccountingReconcileService _reconcileService = reconcileService;
    private readonly IVanAnDbContext _db = db;
    private readonly IAuditTrailService _auditTrailService = auditTrailService;
    private readonly ILogger<AccountingReconcileController> _logger = logger;

    [HttpGet]
    [ProducesResponseType(typeof(ReconcileResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Run([FromQuery] Guid tenantId, [FromQuery] string? period, CancellationToken ct)
    {
        if (tenantId == Guid.Empty)
            return BadRequest(new { error = "tenantId là bắt buộc." });
        if (string.IsNullOrWhiteSpace(period)
            || !DateTime.TryParseExact(period, "yyyy-MM", CultureInfo.InvariantCulture,
                DateTimeStyles.None, out DateTime start))
        {
            return BadRequest(new { error = "period là bắt buộc, dạng YYYY-MM (vd: 2026-10)." });
        }

        bool tenantExists = await _db.Tenants.IgnoreQueryFilters().AsNoTracking()
            .AnyAsync(t => t.Id == new TenantId(tenantId), ct);
        if (!tenantExists)
            return NotFound(new { error = $"Tenant {tenantId} không tồn tại." });

        ReconcileResult result = await _reconcileService.RunAsync(
            new TenantId(tenantId), new AccountingPeriod(start.Year, start.Month), ct);

        // Audit log — G12: không lưu lịch sử reconcile, chỉ audit (tenant, kỳ, user, summary).
        try
        {
            string actor = User.Identity?.Name ?? "sysadmin";
            await _auditTrailService.LogCreateAsync(AuditableEntityType.Tenant, tenantId,
                System.Text.Json.JsonSerializer.Serialize(new
                {
                    period = result.Period,
                    actor,
                    checks = result.Summary.Total,
                    error = result.Summary.Error,
                    warning = result.Summary.Warning
                }),
                correlationId: $"reconcile-api:{tenantId}:{result.Period}", cancellationToken: ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Reconcile audit log failed (non-blocking): tenant={Tenant}", tenantId);
        }

        return Ok(result);
    }
}
