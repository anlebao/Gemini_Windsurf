using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VanAn.CoreHub.Services;
using VanAn.Gateway.Filters;

namespace VanAn.Gateway.Controllers;

/// <summary>
/// VALCN v2.0 Phase 3 — Internal service-to-service API for ShopERP loyalty budget checks.
/// ShopERP's SQLite ignores LoyaltyTenantConfig (PG-only) → budget checks/updates
/// must go through this Gateway controller which has PG access.
///
/// Auth: [InternalApiKey] (custom IAsyncAuthorizationFilter) validates X-Internal-Api-Key header
/// against InternalLoyalty:ApiKey config. [AllowAnonymous] suppresses the default JWT/Cookie auth
/// pipeline — internal endpoints use API key, not user credentials.
/// </summary>
[ApiController]
[Route("api/internal/loyalty-budget")]
[AllowAnonymous]
[InternalApiKey]
public class LoyaltyBudgetController(
    ILoyaltyBudgetService budgetService,
    ILogger<LoyaltyBudgetController> logger) : ControllerBase
{
    private readonly ILoyaltyBudgetService _budgetService = budgetService;
    private readonly ILogger<LoyaltyBudgetController> _logger = logger;

    /// <summary>
    /// POST /api/internal/loyalty-budget/check-adjust
    /// Check budget caps and return adjusted points.
    /// Called by ShopERP's LoyaltyBudgetServiceHttpProxy before AddPoints.
    /// </summary>
    [HttpPost("check-adjust")]
    public async Task<ActionResult<CheckAdjustResponse>> CheckAndAdjust([FromBody] CheckAdjustRequest req, CancellationToken ct)
    {
        try
        {
            int adjusted = await _budgetService.CheckAndAdjustPointsAsync(
                req.TenantId, req.CustomerId, req.OrderAmount, req.RequestedPoints, ct);
            return Ok(new CheckAdjustResponse { AdjustedPoints = adjusted });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "LoyaltyBudget: CheckAndAdjust failed for tenant {TenantId}", req.TenantId);
            return StatusCode(500, new { error = "Budget check failed" });
        }
    }

    /// <summary>
    /// POST /api/internal/loyalty-budget/record
    /// Record points issuance (atomic counter increment).
    /// Called by ShopERP's LoyaltyBudgetServiceHttpProxy after AddPoints succeeds.
    /// </summary>
    [HttpPost("record")]
    public async Task<IActionResult> RecordIssuance([FromBody] RecordIssuanceRequest req, CancellationToken ct)
    {
        try
        {
            await _budgetService.RecordIssuanceAsync(req.TenantId, req.PointsIssued, ct);
            return Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "LoyaltyBudget: RecordIssuance failed for tenant {TenantId}", req.TenantId);
            return StatusCode(500, new { error = "Record issuance failed" });
        }
    }

    /// <summary>
    /// POST /api/internal/loyalty-budget/decrement
    /// Decrement counters on reversal (Phase 4).
    /// Called by ShopERP's LoyaltyBudgetServiceHttpProxy during refund reversal.
    /// </summary>
    [HttpPost("decrement")]
    public async Task<IActionResult> DecrementIssuance([FromBody] DecrementIssuanceRequest req, CancellationToken ct)
    {
        try
        {
            await _budgetService.DecrementIssuanceAsync(req.TenantId, req.PointsToReverse, ct);
            return Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "LoyaltyBudget: DecrementIssuance failed for tenant {TenantId}", req.TenantId);
            return StatusCode(500, new { error = "Decrement issuance failed" });
        }
    }

    // === Request/Response DTOs ===

    public class CheckAdjustRequest
    {
        public Guid TenantId { get; set; }
        public Guid CustomerId { get; set; }
        public decimal OrderAmount { get; set; }
        public int RequestedPoints { get; set; }
    }

    public class CheckAdjustResponse
    {
        public int AdjustedPoints { get; set; }
    }

    public class RecordIssuanceRequest
    {
        public Guid TenantId { get; set; }
        public int PointsIssued { get; set; }
    }

    public class DecrementIssuanceRequest
    {
        public Guid TenantId { get; set; }
        public int PointsToReverse { get; set; }
    }
}
