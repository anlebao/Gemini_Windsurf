using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using VanAn.CoreHub.Infrastructure;
using VanAn.Gateway.Services;
using VanAn.Shared.Domain;
using VanAn.Shared.Domain.Aggregates.TenantAggregate;
using VanAn.Shared.Services;

namespace VanAn.Gateway.Controllers;

/// <summary>
/// UNIVERSAL AI ASSISTANT W1 (2026-11-22): public surface cho hosted AI Link.
/// Route public: GET /a/{slug} trên KhachLink → page gọi các endpoint này.
///
/// Tenant resolution là SERVER-SIDE duy nhất: slug → AssistantLink (active) →
/// OwnerTenantId → tenant Active + AIChat_Enabled. Client KHÔNG gửi tenantId.
///
/// Endpoints (anonymous + rate-limit "tenant-ai-public" — tái dùng policy sẵn có):
///   GET  /api/public/assistant-link/{slug}/resolve          → {tenantId, slug, aiEnabled, widgetConfig?}
///   POST /api/public/assistant-link/{slug}/chat             → forward TenantAI /v1/chat
///   GET  /api/public/assistant-link/{slug}/widget-config    → forward TenantAI /v1/config/widget
///   GET  /api/public/assistant-link/{slug}/sessions/{sid}/messages → session resume
///
/// Graceful degradation giống PublicTenantAiController: AI down → 503 {aiEnabled:false},
/// resolve fail → 404 — storefront không bao giờ phụ thuộc AI.
/// </summary>
[ApiController]
[Route("api/public/assistant-link")]
[EnableRateLimiting("tenant-ai-public")]
public class PublicAssistantLinkController(
    VanAnDbContext dbContext,
    IShopFeatureSettingsService featureSettingsService,
    TenantAiForwarder forwarder,
    IMemoryCache memoryCache,
    IConfiguration configuration,
    ILogger<PublicAssistantLinkController> logger) : ControllerBase
{
    private readonly VanAnDbContext _dbContext = dbContext;
    private readonly IShopFeatureSettingsService _featureSettingsService = featureSettingsService;
    private readonly TenantAiForwarder _forwarder = forwarder;
    private readonly IMemoryCache _memoryCache = memoryCache;
    private readonly IConfiguration _configuration = configuration;
    private readonly ILogger<PublicAssistantLinkController> _logger = logger;

    private int SessionCap => _configuration.GetValue("TenantAi:SessionCap", 20);

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

    /// <summary>Public chat qua AI Link — cùng contract PublicAiChatRequest trừ tenantId.</summary>
    [HttpPost("{slug}/chat")]
    [AllowAnonymous]
    public async Task<IActionResult> Chat(string slug, [FromBody] PublicAssistantChatRequest request, CancellationToken ct = default)
    {
        if (request is null || string.IsNullOrWhiteSpace(request.SessionId) || request.SessionId.Length > 64
            || string.IsNullOrWhiteSpace(request.MessageId) || request.MessageId.Length > 56
            || string.IsNullOrWhiteSpace(request.Text) || request.Text.Length > 2000)
            return BadRequest(new { aiEnabled = false, message = "Tin nhắn không hợp lệ." });

        var tenantId = await ResolveTenantBySlugAsync(slug, ct);
        if (tenantId is null)
            return NotFound(new { aiEnabled = false, message = "Trợ lý không tồn tại hoặc đã ngừng hoạt động." });

        if (!_forwarder.IsEnabled || !await IsAiChatEnabledAsync(tenantId.Value, ct))
            return AiUnavailable();

        if (!TryAcquireSessionSlot(tenantId.Value, request.SessionId))
            return StatusCode(StatusCodes.Status429TooManyRequests,
                new { aiEnabled = false, message = "Bạn đã gửi quá nhiều tin nhắn trong giờ này. Vui lòng thử lại sau." });

        var result = await _forwarder.ChatAsync(
            tenantId.Value, request.SessionId.Trim(), request.MessageId.Trim(),
            request.Text, request.Locale, ct);

        if (result is null)
            return AiUnavailable();

        if (result.StatusCode is >= 200 and < 300 && result.Body is not null)
            return Content(result.Body, "application/json");

        _logger.LogWarning("AssistantLink chat upstream {Status} tenant={Tenant}", result.StatusCode, tenantId);
        return result.StatusCode is >= 400 and < 500
            ? StatusCode(result.StatusCode, new { aiEnabled = false, message = "Tin nhắn không hợp lệ." })
            : AiUnavailable();
    }

    /// <summary>Widget config trực tiếp (page đã resolve có thể gọi riêng nếu cần refresh).</summary>
    [HttpGet("{slug}/widget-config")]
    [AllowAnonymous]
    public async Task<IActionResult> GetWidgetConfig(string slug, CancellationToken ct = default)
    {
        var tenantId = await ResolveTenantBySlugAsync(slug, ct);
        if (tenantId is null || !_forwarder.IsEnabled || !await IsAiChatEnabledAsync(tenantId.Value, ct))
            return Ok(new { aiEnabled = false });

        var result = await _forwarder.GetWidgetConfigAsync(tenantId.Value, $"wc-{Guid.NewGuid():N}", ct);
        if (result is null)
            return AiUnavailable();

        if (result.StatusCode is >= 200 and < 300 && result.Body is not null)
            return Content(result.Body, "application/json");

        _logger.LogInformation("AssistantLink widget-config upstream {Status} tenant={Tenant}", result.StatusCode, tenantId);
        return result.StatusCode == StatusCodes.Status501NotImplemented || result.StatusCode < 500
            ? Ok(new { aiEnabled = false })
            : AiUnavailable();
    }

    /// <summary>Session resume history — tenant do slug quyết định, client không chọn được.</summary>
    [HttpGet("{slug}/sessions/{sessionId}/messages")]
    [AllowAnonymous]
    public async Task<IActionResult> GetSessionMessages(string slug, string sessionId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(sessionId) || sessionId.Length > 64)
            return BadRequest(new { message = "Session không hợp lệ." });

        var tenantId = await ResolveTenantBySlugAsync(slug, ct);
        if (tenantId is null)
            return NotFound(new { message = "Trợ lý không tồn tại hoặc đã ngừng hoạt động." });

        if (!_forwarder.IsEnabled)
            return AiUnavailable();

        var result = await _forwarder.GetSessionMessagesAsync(tenantId.Value, sessionId, ct);
        if (result is null)
            return AiUnavailable();

        if (result.StatusCode is >= 200 and < 300 && result.Body is not null)
            return Content(result.Body, "application/json");

        _logger.LogInformation("AssistantLink session-messages upstream {Status} tenant={Tenant}", result.StatusCode, tenantId);
        return AiUnavailable();
    }

    // ── Helpers ────────────────────────────────────────────────────────────

    private IActionResult AiUnavailable()
        => StatusCode(StatusCodes.Status503ServiceUnavailable,
            new { aiEnabled = false, message = "Trợ lý AI tạm nghỉ, bạn thử lại sau nhé." });

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

    /// <summary>Session cap fixed-window 1h per (tenant, session) — giống PublicTenantAiController.</summary>
    private bool TryAcquireSessionSlot(Guid tenantId, string sessionId)
    {
        var key = $"tenant-ai:cap:{tenantId:N}:{sessionId}";
        var count = _memoryCache.GetOrCreate(key, entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(1);
            return 0;
        });
        if (count >= SessionCap) return false;
        _memoryCache.Set(key, count + 1, TimeSpan.FromHours(1));
        return true;
    }
}

/// <summary>Chat request qua AI Link — không có tenantId (slug quyết định tenant).</summary>
public class PublicAssistantChatRequest
{
    public string? SessionId { get; set; }
    public string? MessageId { get; set; }
    public string? Text { get; set; }
    public string? Locale { get; set; }
}
