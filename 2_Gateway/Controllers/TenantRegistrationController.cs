using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using VanAn.CoreHub.Services.Registrations;

namespace VanAn.Gateway.Controllers
{
    /// <summary>
    /// GTM Drill Machine W2 (2026-09-08): Tenant registration endpoints.
    /// Split auth:
    /// - POST /api/v1/tenant-registrations — [AllowAnonymous] + rate-limited (merchant submits registration)
    /// - GET /api/v1/tenant-registrations + GET /{id} + POST /{id}/contact + POST /{id}/onboard + POST /{id}/reject — [Authorize(Policy="SystemAdmin")]
    /// </summary>
    [ApiController]
    [Authorize(Policy = "SystemAdmin")]
    public class TenantRegistrationController(
        ITenantRegistrationService registrationService,
        ITurnstileVerificationService turnstileService,
        ILogger<TenantRegistrationController> logger) : ControllerBase
    {
        // ── Merchant submits registration (AllowAnonymous + rate-limited) ─────

        /// <summary>
        /// Merchant submits a registration (from demo or direct form).
        /// Rate-limited: 5 requests per IP per day (policy "registration-submit").
        /// Turnstile token verified server-side. Honeypot field silently rejected.
        /// </summary>
        [HttpPost("api/v1/tenant-registrations")]
        [AllowAnonymous]
        [EnableRateLimiting("registration-submit")]
        public async Task<ActionResult<RegistrationSubmitResult>> SubmitRegistration(
            [FromBody] SubmitRegistrationRequest request,
            CancellationToken ct = default)
        {
            try
            {
                // Honeypot check: if bot filled the hidden field → silent reject (200 fake success)
                if (!string.IsNullOrEmpty(request.HoneypotWebsite))
                {
                    logger.LogInformation("Honeypot triggered — silent reject (no record saved)");
                    return Ok(new RegistrationSubmitResult(Guid.Empty, "Cảm ơn! Yêu cầu đã gửi."));
                }

                // Turnstile server-side verification
                var clientIp = HttpContext.Connection.RemoteIpAddress?.ToString();
                var turnstileVerified = await turnstileService.VerifyAsync(request.TurnstileToken, clientIp, ct);

                if (!turnstileVerified)
                {
                    return BadRequest(new { error = "Xác thực chống bot thất bại. Vui lòng thử lại." });
                }

                var registrationId = await registrationService.SubmitRegistrationAsync(request, turnstileVerified, ct);
                logger.LogInformation("Registration submitted — registration {RegistrationId}", registrationId);

                return Ok(new RegistrationSubmitResult(
                    registrationId,
                    "Cảm ơn! Yêu cầu đăng ký đã gửi. Chúng tôi sẽ liên hệ trong 1-2 ngày làm việc."));
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { error = ex.Message });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Registration submit exception");
                return StatusCode(StatusCodes.Status500InternalServerError,
                    new { error = "Lỗi không xác định khi gửi yêu cầu. Vui lòng thử lại." });
            }
        }

        // ── SysAdmin endpoints (SystemAdmin policy) ──────────────────────────

        /// <summary>List all Submitted/Contacted registrations (SysAdmin queue).</summary>
        [HttpGet("api/v1/tenant-registrations")]
        [Authorize(Policy = "SystemAdmin")]
        public async Task<ActionResult<List<RegistrationDto>>> ListPendingRegistrations(CancellationToken ct = default)
            => Ok(await registrationService.ListPendingRegistrationsAsync(ct));

        /// <summary>Get a single registration by ID (SysAdmin detail view).</summary>
        [HttpGet("api/v1/tenant-registrations/{registrationId:guid}")]
        [Authorize(Policy = "SystemAdmin")]
        public async Task<ActionResult<RegistrationDto>> GetRegistration(Guid registrationId, CancellationToken ct = default)
        {
            var registration = await registrationService.GetRegistrationAsync(registrationId, ct);
            return registration is null ? NotFound() : Ok(registration);
        }

        /// <summary>SysAdmin marks registration as Contacted (follow-up initiated).</summary>
        [HttpPost("api/v1/tenant-registrations/{registrationId:guid}/contact")]
        [Authorize(Policy = "SystemAdmin")]
        public async Task<IActionResult> MarkContacted(Guid registrationId, CancellationToken ct = default)
        {
            var sysAdminUserId = GetSysAdminUserId();
            if (sysAdminUserId == Guid.Empty)
                return Unauthorized(new { error = "SysAdmin user ID not found in JWT claims." });

            try
            {
                await registrationService.MarkContactedAsync(registrationId, sysAdminUserId, ct);
                return NoContent();
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new { error = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                return Conflict(new { error = ex.Message });
            }
        }

        /// <summary>SysAdmin marks registration as Onboarded — tenant created from this registration.</summary>
        [HttpPost("api/v1/tenant-registrations/{registrationId:guid}/onboard")]
        [Authorize(Policy = "SystemAdmin")]
        public async Task<IActionResult> MarkOnboarded(
            Guid registrationId,
            [FromBody] OnboardRequest request,
            CancellationToken ct = default)
        {
            var sysAdminUserId = GetSysAdminUserId();
            if (sysAdminUserId == Guid.Empty)
                return Unauthorized(new { error = "SysAdmin user ID not found in JWT claims." });

            try
            {
                await registrationService.MarkOnboardedAsync(registrationId, request.OnboardedTenantId, sysAdminUserId, ct);
                return NoContent();
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new { error = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                return Conflict(new { error = ex.Message });
            }
        }

        /// <summary>SysAdmin rejects the registration with reason.</summary>
        [HttpPost("api/v1/tenant-registrations/{registrationId:guid}/reject")]
        [Authorize(Policy = "SystemAdmin")]
        public async Task<IActionResult> RejectRegistration(
            Guid registrationId,
            [FromBody] RejectRegistrationRequest request,
            CancellationToken ct = default)
        {
            var sysAdminUserId = GetSysAdminUserId();
            if (sysAdminUserId == Guid.Empty)
                return Unauthorized(new { error = "SysAdmin user ID not found in JWT claims." });

            try
            {
                await registrationService.RejectRegistrationAsync(registrationId, request.Reason, sysAdminUserId, ct);
                return NoContent();
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new { error = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                return Conflict(new { error = ex.Message });
            }
        }

        // ── Helpers ──────────────────────────────────────────────────────────

        private Guid GetSysAdminUserId()
        {
            var userIdClaim = User.FindFirst("sub")?.Value
                ?? User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            return Guid.TryParse(userIdClaim, out var userId) ? userId : Guid.Empty;
        }
    }

    // ── DTOs ────────────────────────────────────────────────────────────────

    public record OnboardRequest(Guid OnboardedTenantId);
    public record RejectRegistrationRequest(string Reason);
}
