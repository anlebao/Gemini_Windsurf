using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using VanAn.CoreHub.Services;

namespace VanAn.Gateway.Controllers
{
    /// <summary>
    /// R2 Storage Admin API — per-tenant storage stats + manual cleanup trigger.
    /// SystemAdmin can access any tenant; TenantAdmin can only access own tenant.
    /// </summary>
    [ApiController]
    [Authorize]
    [Route("api/r2storage")]
    public class R2StorageController(
        IR2CleanupService cleanupService,
        ILogger<R2StorageController> logger) : ControllerBase
    {
        private readonly IR2CleanupService _cleanupService = cleanupService;
        private readonly ILogger<R2StorageController> _logger = logger;

        private Guid GetTenantId()
        {
            string? tenantClaim = User.FindFirst("tenant_id")?.Value
                ?? User.FindFirst("TenantId")?.Value;
            return Guid.TryParse(tenantClaim, out Guid tenantId) ? tenantId : Guid.Empty;
        }

        private bool IsSystemAdmin =>
            User.IsInRole("SystemAdmin");

        /// <summary>
        /// Get storage stats for a tenant (photo count + total size).
        /// SystemAdmin: any tenant. Other roles: own tenant only.
        /// </summary>
        [HttpGet("stats/{tenantId}")]
        public async Task<IActionResult> GetStats(Guid tenantId, CancellationToken ct)
        {
            if (!IsSystemAdmin && tenantId != GetTenantId())
            {
                return Forbid("Cannot view stats for another tenant.");
            }

            _logger.LogInformation("R2 storage stats requested for tenant {TenantId} by user {User}",
                tenantId, User.Identity?.Name);
            var stats = await _cleanupService.GetTenantStatsAsync(tenantId, ct);
            return Ok(stats);
        }

        /// <summary>
        /// Trigger immediate cleanup for a specific tenant.
        /// SystemAdmin only. Returns cleanup result (sessions processed, photos deleted, bytes freed).
        /// </summary>
        [HttpPost("cleanup/{tenantId}")]
        [Authorize(Policy = "SystemAdmin")]
        public async Task<IActionResult> TriggerCleanup(Guid tenantId, [FromQuery] int? retentionDays, CancellationToken ct)
        {
            var retention = retentionDays.HasValue && retentionDays > 0
                ? TimeSpan.FromDays(retentionDays.Value)
                : TimeSpan.FromDays(30);

            _logger.LogInformation("R2 manual cleanup triggered for tenant {TenantId} (retention {Days}d) by {User}",
                tenantId, retention.Days, User.Identity?.Name);

            var result = await _cleanupService.CleanupTenantAsync(tenantId, retention, ct);
            return Ok(result);
        }

        /// <summary>
        /// Trigger immediate cleanup for ALL tenants.
        /// SystemAdmin only. Returns aggregate cleanup result.
        /// </summary>
        [HttpPost("cleanup-all")]
        [Authorize(Policy = "SystemAdmin")]
        public async Task<IActionResult> TriggerCleanupAll([FromQuery] int? retentionDays, CancellationToken ct)
        {
            var retention = retentionDays.HasValue && retentionDays > 0
                ? TimeSpan.FromDays(retentionDays.Value)
                : TimeSpan.FromDays(30);

            _logger.LogInformation("R2 manual cleanup-all triggered (retention {Days}d) by {User}",
                retention.Days, User.Identity?.Name);

            var result = await _cleanupService.CleanupAllTenantsAsync(retention, ct);
            return Ok(result);
        }
    }
}
