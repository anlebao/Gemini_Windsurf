using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VanAn.CoreHub.Services;
using VanAn.Shared.Domain;

namespace VanAn.ShopERP.Controllers;

/// <summary>
/// VAS Wave 5 — Balance Sheet API endpoint.
/// GET /api/balance-sheets?year=2026&amp;month=6&amp;standard=TT133_2016
///
/// Controller purity: forwards all calls to IBalanceSheetService (governance VA0003).
/// Multi-tenancy: TenantId extracted from JWT claim, never from request body.
/// </summary>
[ApiController]
[Route("api/balance-sheets")]
[Authorize]
[Produces("application/json")]
public class BalanceSheetsController(
    IBalanceSheetService balanceSheetService,
    ILogger<BalanceSheetsController> logger) : ControllerBase
{
    private readonly IBalanceSheetService _balanceSheetService = balanceSheetService;
    private readonly ILogger<BalanceSheetsController> _logger = logger;

    /// <summary>
    /// Generate the Balance Sheet for the authenticated tenant + period.
    /// </summary>
    /// <param name="year">Period year (e.g. 2026)</param>
    /// <param name="month">Period month (1-12)</param>
    /// <param name="standard">Accounting standard (TT133_2016 default; TT99_2025 for large enterprises). W8 will auto-detect from Tenant.</param>
    [HttpGet]
    public async Task<IActionResult> GetBalanceSheet(
        [FromQuery] int year,
        [FromQuery] int month,
        [FromQuery] AccountingStandard standard = AccountingStandard.TT133_2016,
        CancellationToken ct = default)
    {
        try
        {
            TenantId tenantId = GetCurrentTenantId();
            var period = new AccountingPeriod(year, month);

            BalanceSheet bs = await _balanceSheetService.GenerateAsync(tenantId, period, standard, ct);

            _logger.LogInformation("API: Balance Sheet generated for tenant {TenantId}, period {Period}", tenantId.Value, period.ToString());
            return Ok(bs);
        }
        catch (InvalidOperationException ex)
        {
            // W2 invariant violation (unbalanced BS) — 422 Unprocessable Entity.
            _logger.LogWarning(ex, "Balance Sheet invariant violated for tenant, period {Year}-{Month}", year, month);
            return UnprocessableEntity(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating Balance Sheet for period {Year}-{Month}", year, month);
            return StatusCode(500, "Internal server error");
        }
    }

    private TenantId GetCurrentTenantId()
    {
        var tenantClaim = User.FindFirst("tenant_id")?.Value ?? User.FindFirst("TenantId")?.Value;
        if (string.IsNullOrWhiteSpace(tenantClaim) || !Guid.TryParse(tenantClaim, out var tenantId))
            throw new UnauthorizedAccessException("Tenant ID is missing or invalid.");
        return new TenantId(tenantId);
    }
}
