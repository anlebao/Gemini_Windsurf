using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VanAn.CoreHub.Services;
using VanAn.Shared.Domain;

namespace VanAn.ShopERP.Controllers;

/// <summary>
/// VAS Wave 5 — Cash Flow Statement API endpoint.
/// GET /api/cash-flow-statements?year=2026&amp;month=6&amp;standard=TT133_2016
///
/// Controller purity: forwards all calls to ICashFlowStatementService (governance VA0003).
/// Multi-tenancy: TenantId extracted from JWT claim, never from request body.
/// </summary>
[ApiController]
[Route("api/cash-flow-statements")]
[Authorize]
[Produces("application/json")]
public class CashFlowStatementsController(
    ICashFlowStatementService cashFlowStatementService,
    IVasFeatureFlagService featureFlagService,
    ILogger<CashFlowStatementsController> logger) : ControllerBase
{
    private readonly ICashFlowStatementService _cashFlowStatementService = cashFlowStatementService;
    private readonly IVasFeatureFlagService _featureFlagService = featureFlagService;
    private readonly ILogger<CashFlowStatementsController> _logger = logger;

    /// <summary>
    /// Generate the Cash Flow Statement (direct method) for the authenticated tenant + period.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetCashFlowStatement(
        [FromQuery] int year,
        [FromQuery] int month,
        [FromQuery] AccountingStandard standard = AccountingStandard.TT133_2016,
        CancellationToken ct = default)
    {
        try
        {
            TenantId tenantId = GetCurrentTenantId();

            // W8 feature flag: HKD tenants cannot access VAS reports (403 Forbidden).
            if (!await _featureFlagService.CanAccessVasReportsAsync(tenantId, ct))
            {
                _logger.LogWarning("VAS access denied for tenant {TenantId} (HKD tenant — feature flag blocked)", tenantId.Value);
                return Forbid("VAS reports are only available for Enterprise tenants. HKD tenants use the HKD Book module.");
            }

            var period = new AccountingPeriod(year, month);

            CashFlowStatement cf = await _cashFlowStatementService.GenerateAsync(tenantId, period, standard, ct);

            _logger.LogInformation("API: Cash Flow Statement generated for tenant {TenantId}, period {Period}", tenantId.Value, period.ToString());
            return Ok(cf);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating Cash Flow Statement for period {Year}-{Month}", year, month);
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
