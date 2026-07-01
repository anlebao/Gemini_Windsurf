using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VanAn.CoreHub.Services.Onboarding;

namespace VanAn.Gateway.Controllers
{
    /// <summary>
    /// Wave 4: Platform-level tenant onboarding API.
    ///
    /// Intentionally separate from OnboardingController (which carries class-level
    /// [Authorize(Policy="RequireTenantAccess")] for tenant-scoped Cookie auth).
    /// This controller uses Bearer JWT with SystemAdmin role only — no tenant context required.
    ///
    /// Auth flow:
    ///   - No class-level [Authorize] → ASP.NET Core evaluates method-level policies only.
    ///   - Method-level [Authorize(Policy="SystemAdmin", AuthenticationSchemes=Bearer)] works correctly:
    ///     unauthenticated → 401, wrong role → 403, SystemAdmin → proceeds.
    /// </summary>
    [ApiController]
    [Route("api/v1/onboarding")]
    public class TenantOnboardingController(
        ITenantOnboardingService tenantOnboardingService,
        ILogger<TenantOnboardingController> logger) : ControllerBase
    {
        private readonly ITenantOnboardingService _tenantOnboardingService = tenantOnboardingService;
        private readonly ILogger<TenantOnboardingController> _logger = logger;

        /// <summary>
        /// Create a new tenant with full onboarding (industry seed + owner user + permission groups).
        /// Requires SystemAdmin Bearer JWT — platform-level, cross-tenant operation.
        /// </summary>
        [HttpPost("tenants")]
        [Authorize(Policy = "SystemAdmin", AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        public async Task<ActionResult<TenantOnboardingResult>> CreateTenantOnboarding(
            [FromBody] OnboardTenantRequest request,
            CancellationToken ct = default)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            try
            {
                TenantOnboardingResult result = await _tenantOnboardingService.OnboardAsync(request, ct);
                return CreatedAtAction(
                    nameof(GetTenantOnboarding),
                    new { tenantId = result.TenantId },
                    result);
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning(ex, "Invalid onboarding request for tenant '{Name}'", request.Name);
                return BadRequest(new { error = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogWarning(ex, "Onboarding conflict for tenant '{Name}'", request.Name);
                return UnprocessableEntity(new { error = ex.Message });
            }
        }

        /// <summary>
        /// Stub GET for CreatedAtAction routing in POST /tenants response.
        /// Returns 404 — full retrieval endpoint is out of scope for Wave 4.
        /// </summary>
        [HttpGet("tenants/{tenantId:guid}")]
        [Authorize(Policy = "SystemAdmin", AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        public ActionResult GetTenantOnboarding(Guid tenantId)
        {
            return NotFound(new { message = "Tenant retrieval not implemented in this wave." });
        }
    }
}
