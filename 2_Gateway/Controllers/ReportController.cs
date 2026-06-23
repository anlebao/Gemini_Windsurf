using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VanAn.CoreHub.Services;
using VanAn.Shared.Domain;

namespace VanAn.Gateway.Controllers;

/// <summary>
/// Wave 3: Report export controller.
/// Provides Excel export endpoints for Revenue, Inventory, and Customer reports.
/// </summary>
[ApiController]
[Route("api/reports")]
[Authorize(Policy = "RequireTenantAccess", Roles = "Owner,StoreKeeper")]
public class ReportController(IExcelExportService excelExportService) : ControllerBase
{
    private readonly IExcelExportService _excelExportService = excelExportService;

    /// <summary>
    /// Export Excel report by type.
    /// Supported types: revenue, inventory, customer.
    /// </summary>
    [HttpGet("export/excel")]
    [Produces("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet")]
    public async Task<IActionResult> ExportExcel(
        [FromQuery] string type,
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        CancellationToken cancellationToken = default)
    {
        Guid tenantId = GetTenantIdFromClaim();
        if (tenantId == Guid.Empty)
        {
            return Unauthorized(new { error = "Tenant ID required in JWT claim" });
        }

        DateTime rangeFrom = from ?? DateTime.UtcNow.AddMonths(-1);
        DateTime rangeTo = to ?? DateTime.UtcNow;

        if (rangeTo < rangeFrom)
        {
            return BadRequest(new { error = "'to' date must be greater than or equal to 'from' date" });
        }

        byte[] fileBytes;
        string fileName;

        switch (type?.ToUpperInvariant())
        {
            case "REVENUE":
                fileBytes = await _excelExportService.ExportRevenueAsync(tenantId, rangeFrom, rangeTo, cancellationToken);
                fileName = $"revenue-report-{rangeFrom:yyyyMMdd}-{rangeTo:yyyyMMdd}.xlsx";
                break;
            case "INVENTORY":
                fileBytes = await _excelExportService.ExportInventoryAsync(tenantId, cancellationToken);
                fileName = $"inventory-report-{DateTime.UtcNow:yyyyMMdd}.xlsx";
                break;
            case "CUSTOMER":
                fileBytes = await _excelExportService.ExportCustomerAsync(tenantId, rangeFrom, rangeTo, cancellationToken);
                fileName = $"customer-report-{rangeFrom:yyyyMMdd}-{rangeTo:yyyyMMdd}.xlsx";
                break;
            default:
                return BadRequest(new { error = "Unsupported report type. Use: revenue, inventory, customer." });
        }

        return File(fileBytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
    }

    /// <summary>
    /// Wave 0 Phase 2: Get TenantId from JWT claim (standardized claim name).
    /// </summary>
    private Guid GetTenantIdFromClaim()
    {
        string? tenantClaim = User.FindFirst("tenant_id")?.Value
            ?? User.FindFirst("TenantId")?.Value;
        return Guid.TryParse(tenantClaim, out Guid tenantId) ? tenantId : Guid.Empty;
    }
}
