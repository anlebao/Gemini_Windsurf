using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VanAn.CoreHub.Services;
using VanAn.Shared.Domain;

namespace VanAn.ShopERP.Controllers;

/// <summary>
/// VAS Wave 5 — Income Statement API endpoint.
/// GET /api/income-statements?year=2026&amp;month=6&amp;standard=TT133_2016
///
/// Controller purity: forwards all calls to IIncomeStatementService (governance VA0003).
/// Multi-tenancy: TenantId extracted from JWT claim, never from request body.
/// </summary>
[ApiController]
[Route("api/income-statements")]
[Authorize]
[Produces("application/json")]
public class IncomeStatementsController(
    IIncomeStatementService incomeStatementService,
    ILogger<IncomeStatementsController> logger) : ControllerBase
{
    private readonly IIncomeStatementService _incomeStatementService = incomeStatementService;
    private readonly ILogger<IncomeStatementsController> _logger = logger;

    /// <summary>
    /// Generate the Income Statement for the authenticated tenant + period.
    /// 2-column comparative: Ending = current period, Opening = same month prior year.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetIncomeStatement(
        [FromQuery] int year,
        [FromQuery] int month,
        [FromQuery] AccountingStandard standard = AccountingStandard.TT133_2016,
        CancellationToken ct = default)
    {
        try
        {
            TenantId tenantId = GetCurrentTenantId();
            var period = new AccountingPeriod(year, month);

            IncomeStatement is_ = await _incomeStatementService.GenerateAsync(tenantId, period, standard, ct);

            _logger.LogInformation("API: Income Statement generated for tenant {TenantId}, period {Period}", tenantId.Value, period.ToString());
            return Ok(is_);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating Income Statement for period {Year}-{Month}", year, month);
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
