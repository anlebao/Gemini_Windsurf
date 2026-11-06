using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VanAn.Shared.Services;
using VanAn.ShopERP.Services;

namespace VanAn.ShopERP.Controllers
{
    /// <summary>
    /// task_ai_p6b — manual "Đồng bộ AI" trigger (SRS [G9]): owner/staff của tenant
    /// bấm đồng bộ menu sang vanan-ai ngay, không chờ nightly reconcile.
    /// Flag TenantAiEnabled OFF → no-op {disabled:true} (zero HTTP calls downstream).
    /// </summary>
    [ApiController]
    [Route("api/tenant-ai")]
    [Authorize]
    public class TenantAiController(
        ITenantProvider tenantProvider,
        IShopFeatureSettingsService featureSettings,
        ITenantAiKnowledgeSync? tenantAiSync = null) : ControllerBase
    {
        [HttpPost("sync")]
        public async Task<ActionResult> SyncNow(CancellationToken ct)
        {
            if (!tenantProvider.HasTenant)
                return BadRequest(new { error = "No tenant context." });
            if (tenantAiSync == null)
                return Ok(new { synced = 0, disabled = true });

            var tenantId = tenantProvider.TenantId;
            var enabled = await featureSettings.IsEnabledAsync(
                tenantId, TenantAiKnowledgeSyncService.FlagName, ct);
            if (!enabled)
                return Ok(new { synced = 0, disabled = true });

            var synced = await tenantAiSync.SyncNowAsync(tenantId, ct);
            return Ok(new { synced });
        }
    }
}
