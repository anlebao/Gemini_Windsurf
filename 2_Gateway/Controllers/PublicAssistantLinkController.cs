using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using VanAn.CoreHub.Infrastructure;
using VanAn.Gateway.Services;
using VanAn.Shared.Domain;
using VanAn.Shared.Domain.Aggregates.TenantAggregate;
using VanAn.Shared.Services;

namespace VanAn.Gateway.Controllers;

/// <summary>
/// UNIVERSAL AI ASSISTANT W1 (2026-11-22): public surface cho hosted AI Link.
/// Route public: GET /a/{slug} trên KhachLink → page gọi endpoint này.
///
/// Tenant resolution là SERVER-SIDE duy nhất: slug → AssistantLink (active) →
/// OwnerTenantId → tenant Active + AIChat_Enabled. Client KHÔNG gửi tenantId.
///
/// Endpoint (anonymous + rate-limit "tenant-ai-public" — tái dùng policy sẵn có):
///   GET /api/public/assistant-link/{slug}/resolve → {tenantId, slug, aiEnabled}
///
/// Sau resolve, page dùng IAiChatClient → /api/public/tenant-ai/* (PublicTenantAiController)
/// cho widget-config/chat/sessions — KHÔNG duplicate forwarding ở đây (audit D1).
/// Chat/widget-config/sessions theo slug đã bị xóa 2026-11-22 (dead code).
///
/// Graceful degradation giống PublicTenantAiController: resolve fail → 404 —
/// storefront không bao giờ phụ thuộc AI.
/// </summary>
[ApiController]
[Route("api/public/assistant-link")]
[EnableRateLimiting("tenant-ai-public")]
public class PublicAssistantLinkController(
    VanAnDbContext dbContext,
    IShopFeatureSettingsService featureSettingsService,
    TenantAiForwarder forwarder,
    ILogger<PublicAssistantLinkController> logger) : ControllerBase
{
    private readonly VanAnDbContext _dbContext = dbContext;
    private readonly IShopFeatureSettingsService _featureSettingsService = featureSettingsService;
    private readonly TenantAiForwarder _forwarder = forwarder;
    private readonly ILogger<PublicAssistantLinkController> _logger = logger;

    /// <summary>
    /// Resolve slug → tenant context cho hosted page. Chỉ trả {tenantId, slug, aiEnabled}
    /// — page sau đó dùng IAiChatClient hiện có (widget-config/chat/sessions qua
    /// /api/public/tenant-ai/* với tenantId đã resolve). Không forward widget-config ở đây
    /// để page có đúng 1 code path chat như storefront.
    /// </summary>
    [HttpGet("{slug}/resolve")]
    [AllowAnonymous]
    public async Task<IActionResult> Resolve(string slug, CancellationToken ct = default)
    {
        var tenantId = await ResolveTenantBySlugAsync(slug, ct);
        if (tenantId is null)
            return NotFound(new { aiEnabled = false, message = "Trợ lý không tồn tại hoặc đã ngừng hoạt động." });

        var aiEnabled = _forwarder.IsEnabled && await IsAiChatEnabledAsync(tenantId.Value, ct);
        return Ok(new { tenantId = tenantId.Value, slug, aiEnabled });
    }

    // ── Helpers ────────────────────────────────────────────────────────────

    /// <summary>
    /// slug → active AssistantLink → OwnerTenantId → tenant Active. Slug không tồn tại
    /// hoặc link revoked → null (404). Tenant inactive → null.
    /// </summary>
    private async Task<Guid?> ResolveTenantBySlugAsync(string slug, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(slug) || slug.Length > 32)
            return null;

        var link = await _dbContext.AssistantLinks
            .IgnoreQueryFilters()
            .AsNoTracking()
            .FirstOrDefaultAsync(l => l.Slug == slug && l.IsActive, ct);
        if (link is null)
            return null;

        var tenantId = link.TenantId.Value;
        var active = await _dbContext.Tenants.IgnoreQueryFilters().AsNoTracking()
            .AnyAsync(t => t.Id == new TenantId(tenantId) && t.Status == TenantStatus.Active, ct);
        return active ? tenantId : (Guid?)null;
    }

    private async Task<bool> IsAiChatEnabledAsync(Guid tenantId, CancellationToken ct)
    {
        try
        {
            var settings = await _featureSettingsService.GetSettingsAsync(tenantId, ct);
            return settings.AIChat_Enabled;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "AssistantLink feature-settings lookup failed tenant={Tenant}", tenantId);
            return false;
        }
    }
}
