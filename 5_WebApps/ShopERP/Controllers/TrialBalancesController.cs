using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VanAn.CoreHub.Services;
using VanAn.Shared.Domain;

namespace VanAn.ShopERP.Controllers;

/// <summary>
/// VAS Wave 5 — Trial Balance API endpoint.
/// GET /api/trial-balances?year=2026&amp;month=6&amp;standard=TT133_2016
///
/// Controller purity: forwards all calls to ITrialBalanceService (governance VA0003).
/// Multi-tenancy: TenantId extracted from JWT claim, never from request body.
/// Replaces the obsolete HKDBookService.GenerateTrialBalanceAsync (W4).
/// </summary>
[ApiController]
[Route("api/trial-balances")]
[Authorize]
[Produces("application/json")]
public class TrialBalancesController(
    ITrialBalanceService trialBalanceService,
    ILogger<TrialBalancesController> logger) : ControllerBase
{
    private readonly ITrialBalanceService _trialBalanceService = trialBalanceService;
    private readonly ILogger<TrialBalancesController> _logger = logger;

    /// <summary>
    /// Generate the Trial Balance for the authenticated tenant + period.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetTrialBalance(
        [FromQuery] int year,
        [FromQuery] int month,
        [FromQuery] AccountingStandard standard = AccountingStandard.TT133_2016,
        CancellationToken ct = default)
    {
        try
        {
            TenantId tenantId = GetCurrentTenantId();
            var period = new AccountingPeriod(year, month);

            TrialBalance tb = await _trialBalanceService.GenerateAsync(tenantId, period, standard, ct);

            _logger.LogInformation("API: Trial Balance generated for tenant {TenantId}, period {Period}", tenantId.Value, period.ToString());
            return Ok(tb);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating Trial Balance for period {Year}-{Month}", year, month);
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
