using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VanAn.CoreHub.Services;

namespace VanAn.Gateway.Controllers;

/// <summary>
/// VALCN v2.0 feature flag toggle API — SystemAdmin only.
/// GET /api/admin/feature-flags — list all feature toggles
/// PUT /api/admin/feature-flags/{featureName} — { isEnabled: true/false }
/// Default: all features OFF (existing behavior preserved until admin enables).
/// </summary>
[ApiController]
[Route("api/admin/feature-flags")]
[Authorize(Policy = "SystemAdmin", AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
public class FeatureFlagsController(
    IFeatureFlagService flagService,
    ILogger<FeatureFlagsController> logger) : ControllerBase
{
    private readonly IFeatureFlagService _flagService = flagService;
    private readonly ILogger<FeatureFlagsController> _logger = logger;

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<FeatureFlagDto>>> GetAll(CancellationToken ct)
    {
        var flags = await _flagService.GetAllAsync(ct);
        return Ok(flags);
    }

    [HttpPut("{featureName}")]
    public async Task<IActionResult> SetEnabled(string featureName, [FromBody] SetEnabledRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(featureName))
            return BadRequest("Feature name is required.");

        // Extract user ID from JWT claims
        var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)
                          ?? User.FindFirst("sub");
        if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var userId))
        {
            return Unauthorized();
        }

        await _flagService.SetEnabledAsync(featureName, request.IsEnabled, userId, ct);
        _logger.LogInformation("Feature flag {FeatureName} set to {IsEnabled} by user {UserId}", featureName, request.IsEnabled, userId);
        return NoContent();
    }

    public record SetEnabledRequest(bool IsEnabled);
}
