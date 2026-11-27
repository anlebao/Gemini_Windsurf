using Microsoft.AspNetCore.Authorization;
using VanAn.Shared.Services;
using Microsoft.AspNetCore.Mvc;
using VanAn.CoreHub.Services;

namespace VanAn.ShopERP.Controllers;

/// <summary>
/// API for shop feature toggle settings.
/// KhachLink calls via Gateway: shoperp/api/shop/settings/features
/// </summary>
[ApiController]
[Route("api/shop/settings")]
[Authorize]
public class ShopSettingsController : ControllerBase
{
    private readonly IShopFeatureSettingsService _settingsService;
    private readonly IInstanceFeatureGate _instanceGate;

    public ShopSettingsController(IShopFeatureSettingsService settingsService, IInstanceFeatureGate instanceGate)
    {
        _settingsService = settingsService;
        _instanceGate = instanceGate;
    }

    /// <summary>Get feature settings for the current tenant.</summary>
    /// <remarks>
    /// ISSUE #4 FIX: AllowAnonymous — KhachLink (Customer PWA) calls this endpoint without
    /// staff auth to determine which shop features are enabled (Kitchen display, QR table
    /// number, voice note). Feature settings are public shop configuration, not sensitive.
    /// Instance Feature Gate (2026-11-27): EFFECTIVE flags — SysAdmin NavFlags kill-switch
    /// (X-KhachLink-Domain) ANDed onto tenant settings. Owner UI dùng in-proc service nên
    /// không bị ảnh hưởng (không mất giá trị gốc khi save).
    /// </remarks>
    [HttpGet("features")]
    [AllowAnonymous]
    public async Task<ActionResult<ShopFeatureSettingsDto>> GetFeatures([FromQuery] Guid? tenantId, CancellationToken ct)
    {
        Guid tid = tenantId ?? GetTenantIdFromContext();
        if (tid == Guid.Empty)
            return BadRequest("TenantId is required.");

        ShopFeatureSettingsDto settings = await _settingsService.GetSettingsAsync(tid, ct);
        var flags = await _instanceGate.GetNavFlagsAsync(ct);
        return Ok(_instanceGate.ApplyOverrides(settings, flags));
    }

    /// <summary>Update feature settings for the current tenant.</summary>
    [HttpPut("features")]
    public async Task<ActionResult<ShopFeatureSettingsDto>> UpdateFeatures(
        [FromBody] ShopFeatureSettingsDto settings,
        [FromQuery] Guid? tenantId,
        CancellationToken ct)
    {
        Guid tid = tenantId ?? GetTenantIdFromContext();
        if (tid == Guid.Empty)
            return BadRequest("TenantId is required.");

        ShopFeatureSettingsDto updated = await _settingsService.UpdateSettingsAsync(tid, settings, ct);
        return Ok(updated);
    }

    private Guid GetTenantIdFromContext()
    {
        // Try to get TenantId from claims (set by auth middleware)
        string? tenantIdStr = User.FindFirst("TenantId")?.Value;
        if (Guid.TryParse(tenantIdStr, out Guid tid))
            return tid;
        return Guid.Empty;
    }
}
