using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using VanAn.Shared.Services;

namespace VanAn.Gateway.Controllers;

/// <summary>
/// VOICE SERVICE C3 (2026-11-11, quyết định user — Gateway PG, SRS-compliant):
/// SystemAdmin CRUD cấu hình voice per-tenant (kill-switch VoiceFeatureEnabled +
/// VoiceOpenCalling, SRS v1.1 §14.4).
///   GET /api/admin/voice-config                    — mọi tenant + config hiệu lực
///   PUT /api/admin/voice-config/{tenantId}         — upsert 1 row / tenant (404 nếu tenant không tồn tại)
/// Auth: SystemAdmin Bearer JWT (platform-level, cross-tenant — pattern AdminOrderFlowController).
/// </summary>
[ApiController]
[Route("api/admin/voice-config")]
[Authorize(Policy = "SystemAdmin", AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
public class VoiceAdminController(
    IVoiceTenantConfigService voiceConfigService,
    ILogger<VoiceAdminController> logger) : ControllerBase
{
    private readonly IVoiceTenantConfigService _voiceConfigService = voiceConfigService;
    private readonly ILogger<VoiceAdminController> _logger = logger;

    /// <summary>GET /api/admin/voice-config — mọi tenant (Active) + config hiệu lực.</summary>
    [HttpGet]
    public async Task<ActionResult<List<VoiceTenantConfigDto>>> GetAll(CancellationToken ct)
    {
        List<VoiceTenantConfigDto> configs = await _voiceConfigService.GetAllAsync(ct);
        return Ok(configs);
    }

    /// <summary>PUT /api/admin/voice-config/{tenantId} — upsert kill-switch + open calling.</summary>
    [HttpPut("{tenantId:guid}")]
    public async Task<ActionResult<VoiceTenantConfigDto>> Upsert(
        Guid tenantId, [FromBody] UpdateVoiceTenantConfigRequest request, CancellationToken ct)
    {
        try
        {
            string actor = User.Identity?.Name ?? "sysadmin";
            VoiceTenantConfigDto dto = await _voiceConfigService.UpsertAsync(tenantId, request, actor, ct);
            return Ok(dto);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Upsert voice tenant config failed for tenant {TenantId}", tenantId);
            return StatusCode(500, new { error = "Internal server error" });
        }
    }
}