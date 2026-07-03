using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VanAn.CoreHub.Services.Template;
using VanAn.Shared.Domain;
using VanAn.Shared.DTOs;

namespace VanAn.Gateway.Controllers
{
    /// <summary>
    /// API Controller for HKD Book generation — Wave 7.
    /// Exposes HKD book generation for UI consumption (Wave 8).
    ///
    /// Controller purity: forwards all calls to IHKDBookGenerationService.
    /// No business logic in controller (governance VA0003).
    /// Multi-tenancy: TenantId extracted from JWT claim, never from request body.
    /// </summary>
    [ApiController]
    [Route("api/hkd-books")]
    [Authorize(Policy = "RequireTenantAccess", AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    [Produces("application/json")]
    public class HKDBooksController(
        IHKDBookGenerationService hkdBookGenerationService,
        ILogger<HKDBooksController> logger) : ControllerBase
    {
        private readonly IHKDBookGenerationService _hkdBookGenerationService = hkdBookGenerationService;
        private readonly ILogger<HKDBooksController> _logger = logger;

        /// <summary>
        /// List available HKD book templates for the authenticated tenant's HKD group.
        /// GET /api/hkd-books
        /// </summary>
        [HttpGet]
        public async Task<ActionResult<IEnumerable<HKDBookTemplateDto>>> GetAvailableTemplates()
        {
            try
            {
                Guid tenantGuid = GetTenantIdFromClaim();
                if (tenantGuid == Guid.Empty)
                {
                    _logger.LogWarning("GetAvailableTemplates rejected: missing TenantId claim");
                    return Unauthorized(new { error = "Tenant ID required in JWT claim" });
                }

                TenantId tenantId = new(tenantGuid);
                List<HKDBookTemplate> templates = await _hkdBookGenerationService.GetAvailableTemplatesAsync(tenantId);

                List<HKDBookTemplateDto> dtos = templates.Select(HKDBookTemplateDto.FromDomain).ToList();
                return Ok(dtos);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error listing available HKD book templates");
                return StatusCode(500, "Internal server error");
            }
        }

        /// <summary>
        /// Generate a specific HKD book for the given period.
        /// GET /api/hkd-books/{templateCode}?year=2024&amp;month=1
        /// </summary>
        [HttpGet("{templateCode}")]
        public async Task<ActionResult<HKDBookDto>> GenerateBook(string templateCode, [FromQuery] int year, [FromQuery] int month)
        {
            try
            {
                Guid tenantGuid = GetTenantIdFromClaim();
                if (tenantGuid == Guid.Empty)
                {
                    _logger.LogWarning("GenerateBook rejected: missing TenantId claim");
                    return Unauthorized(new { error = "Tenant ID required in JWT claim" });
                }

                if (year < 2000 || year > 2100 || month < 1 || month > 12)
                {
                    return BadRequest(new { error = "Invalid period — year must be 2000-2100, month must be 1-12" });
                }

                TenantId tenantId = new(tenantGuid);
                AccountingPeriod period = new(year, month);

                GenericHKDBook book = await _hkdBookGenerationService.GenerateBookAsync(tenantId, period, templateCode);
                HKDBookDto dto = HKDBookDto.FromDomain(book);

                _logger.LogInformation("HKD book generated: {TemplateCode} for tenant {TenantId}, period {Year}/{Month}",
                    templateCode, tenantGuid, year, month);

                return Ok(dto);
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning(ex, "Invalid template code or tenant: {TemplateCode}", templateCode);
                return BadRequest(new { error = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating HKD book {TemplateCode}", templateCode);
                return StatusCode(500, new { error = ex.Message, inner = ex.InnerException?.Message, stack = ex.StackTrace?.Substring(0, Math.Min(800, ex.StackTrace.Length)) });
            }
        }

        private Guid GetTenantIdFromClaim()
        {
            // Support dual-read during migration: "tenant_id" first, then legacy "TenantId"
            string? tenantClaim = User.FindFirst("tenant_id")?.Value
                ?? User.FindFirst("TenantId")?.Value;

            return Guid.TryParse(tenantClaim, out Guid tenantId) ? tenantId : Guid.Empty;
        }
    }
}
