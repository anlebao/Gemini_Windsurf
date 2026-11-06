using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using VanAn.CoreHub.Infrastructure;
using VanAn.Gateway.Services;
using VanAn.Shared.Domain;
using VanAn.Shared.Domain.Aggregates.TenantAggregate;
using VanAn.Shared.Domain.Common;
using VanAn.Shared.Services;

namespace VanAn.Gateway.Controllers;

/// <summary>
/// Task AI-P4 (SRS v1.1 §6.1 — public storefront AI chat surface).
/// Anonymous + rate-limited ("tenant-ai-public" policy) — pattern PublicBookingController.
///
/// Endpoints:
///   POST /api/public/tenant-ai/chat                     → resolve tenant → sign ctx → forward /v1/chat
///   GET  /api/public/tenant-ai/widget-config?tenantId=  → resolve tenant → flag gate → forward /v1/config/widget
///   GET  /api/public/tenant-ai/sessions/{sid}/messages?tenantId= → session resume history
///
/// Tenant resolution is ALWAYS server-side (T6):
///   1. KhachLinkInstance bound to Request.Host with OwnerTenantId → that tenant wins,
///      client-sent tenantId is IGNORED.
///   2. Otherwise the client tenantId is validated against an existing Active tenant —
///      never signed into a ctx raw.
///
/// Graceful degradation (NFR-3): vanan-ai down / not configured → 503 {aiEnabled:false}
/// (widget-config returns 200 {aiEnabled:false} when the feature is simply off — the
/// storefront/cart/checkout must never depend on AI availability).
/// </summary>
[ApiController]
[Route("api/public/tenant-ai")]
[EnableRateLimiting("tenant-ai-public")]
public class PublicTenantAiController(
    VanAnDbContext dbContext,
    IShopFeatureSettingsService featureSettingsService,
    TenantAiForwarder forwarder,
    IMemoryCache memoryCache,
    IConfiguration configuration,
    ILogger<PublicTenantAiController> logger) : ControllerBase
{
    private readonly VanAnDbContext _dbContext = dbContext;
    private readonly IShopFeatureSettingsService _featureSettingsService = featureSettingsService;
    private readonly TenantAiForwarder _forwarder = forwarder;
    private readonly IMemoryCache _memoryCache = memoryCache;
    private readonly IConfiguration _configuration = configuration;
    private readonly ILogger<PublicTenantAiController> _logger = logger;

    /// <summary>Session cap [G8 PO-3]: 20 messages/session/hour — config TenantAi:SessionCap.</summary>
    private int SessionCap => _configuration.GetValue("TenantAi:SessionCap", 20);

    /// <summary>
    /// Public chat — anonymous customer on a tenant storefront.
    /// SessionId/MessageId are client-generated (widget): messageId is the end-to-end
    /// idempotency key (vanan-ai dedupes conversation_messages on (tenant_id, message_id)).
    /// </summary>
    [HttpPost("chat")]
    [AllowAnonymous]
    public async Task<IActionResult> Chat([FromBody] PublicAiChatRequest request, CancellationToken ct = default)
    {
        if (request is null || string.IsNullOrWhiteSpace(request.SessionId) || request.SessionId.Length > 64
            || string.IsNullOrWhiteSpace(request.MessageId) || request.MessageId.Length > 56
            || string.IsNullOrWhiteSpace(request.Text) || request.Text.Length > 2000)
            return BadRequest(new { aiEnabled = false, message = "Tin nhắn không hợp lệ." });

        var tenantId = await ResolveStoreTenantAsync(request.TenantId, ct);
        if (tenantId is null)
            return NotFound(new { aiEnabled = false, message = "Cửa hàng không tồn tại." });

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

        // Upstream rejected (4xx = bad input/ctx) or failed (5xx) — never leak internals.
        _logger.LogWarning("TenantAi chat upstream {Status} tenant={Tenant}", result.StatusCode, tenantId);
        return result.StatusCode is >= 400 and < 500
            ? StatusCode(result.StatusCode, new { aiEnabled = false, message = "Tin nhắn không hợp lệ." })
            : AiUnavailable();
    }

    /// <summary>
    /// Widget config — aiEnabled gate. 200 {aiEnabled:false} when the feature is off
    /// (flag off / service not configured / tenant not resolvable) so the widget just
    /// stays hidden; 503 only when the upstream call itself fails.
    /// </summary>
    [HttpGet("widget-config")]
    [AllowAnonymous]
    public async Task<IActionResult> GetWidgetConfig([FromQuery] Guid? tenantId, CancellationToken ct = default)
    {
        if (!_forwarder.IsEnabled)
            return Ok(new { aiEnabled = false });

        var tenant = await ResolveStoreTenantAsync(tenantId, ct);
        if (tenant is null || !await IsAiChatEnabledAsync(tenant.Value, ct))
            return Ok(new { aiEnabled = false });

        var result = await _forwarder.GetWidgetConfigAsync(
            tenant.Value, $"wc-{Guid.NewGuid():N}", ct);
        if (result is null)
            return AiUnavailable();

        if (result.StatusCode is >= 200 and < 300 && result.Body is not null)
            return Content(result.Body, "application/json");

        // Upstream has no widget config yet (501 stub lands in P5) → widget hidden (200, not an error).
        // Other 5xx = genuine outage → 503 graceful; 4xx → treat as "off" too (feature gate upstream).
        _logger.LogInformation("TenantAi widget-config upstream {Status} tenant={Tenant}", result.StatusCode, tenant);
        return result.StatusCode == StatusCodes.Status501NotImplemented || result.StatusCode < 500
            ? Ok(new { aiEnabled = false })
            : AiUnavailable();
    }

    /// <summary>Session resume history — ctx tenant must own the session (enforced upstream too).</summary>
    [HttpGet("sessions/{sessionId}/messages")]
    [AllowAnonymous]
    public async Task<IActionResult> GetSessionMessages(
        string sessionId, [FromQuery] Guid? tenantId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(sessionId) || sessionId.Length > 64)
            return BadRequest(new { message = "Session không hợp lệ." });

        var tenant = await ResolveStoreTenantAsync(tenantId, ct);
        if (tenant is null)
            return NotFound(new { message = "Cửa hàng không tồn tại." });

        if (!_forwarder.IsEnabled)
            return AiUnavailable();

        var result = await _forwarder.GetSessionMessagesAsync(tenant.Value, sessionId, ct);
        if (result is null)
            return AiUnavailable();

        if (result.StatusCode is >= 200 and < 300 && result.Body is not null)
            return Content(result.Body, "application/json");

        _logger.LogInformation("TenantAi session-messages upstream {Status} tenant={Tenant}", result.StatusCode, tenant);
        return AiUnavailable();
    }

    // ── Helpers ────────────────────────────────────────────────────────────

    private IActionResult AiUnavailable()
        => StatusCode(StatusCodes.Status503ServiceUnavailable,
            new { aiEnabled = false, message = "Trợ lý AI tạm nghỉ, bạn thử lại sau nhé." });

    /// <summary>Per-tenant feature flag gate (AIChat_Enabled — Store page already gates on it).</summary>
    private async Task<bool> IsAiChatEnabledAsync(Guid tenantId, CancellationToken ct)
    {
        try
        {
            var settings = await _featureSettingsService.GetSettingsAsync(tenantId, ct);
            return settings.AIChat_Enabled;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "TenantAi feature-settings lookup failed tenant={Tenant}", tenantId);
            return false;
        }
    }

    /// <summary>
    /// Server-side tenant resolution (T6): a tenant-owned KhachLinkInstance bound to the
    /// request Host always wins and the client-sent tenantId is dropped entirely. On
    /// platform/marketplace instances the client tenantId is validated against an
    /// existing Active tenant — it is never signed into a context unchecked.
    /// </summary>
    private async Task<Guid?> ResolveStoreTenantAsync(Guid? clientTenantId, CancellationToken ct)
    {
        var host = Request.Host.Host;
        if (!string.IsNullOrEmpty(host))
        {
            var instance = await _dbContext.KhachLinkInstances
                .IgnoreQueryFilters()
                .AsNoTracking()
                .FirstOrDefaultAsync(i => i.IsActive && i.CustomDomain == host.ToLowerInvariant(), ct);
            if (instance?.OwnerTenantId is Guid bound && bound != Guid.Empty)
                return await TenantActiveAsync(bound, ct) ? bound : (Guid?)null;
        }

        if (clientTenantId is Guid tid && tid != Guid.Empty && await TenantActiveAsync(tid, ct))
            return tid;
        return null;
    }

    private async Task<bool> TenantActiveAsync(Guid tenantId, CancellationToken ct)
        => await _dbContext.Tenants.IgnoreQueryFilters().AsNoTracking()
            .AnyAsync(t => t.Id == new TenantId(tenantId) && t.Status == TenantStatus.Active, ct);

    /// <summary>Session cap: fixed window per (tenant, session), 1h, config TenantAi:SessionCap (default 20).</summary>
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

/// <summary>Public chat request — tenantId is a hint only; the server re-resolves/validates (T6).</summary>
public class PublicAiChatRequest
{
    public Guid? TenantId { get; set; }
    public string? SessionId { get; set; }
    public string? MessageId { get; set; }
    public string? Text { get; set; }
    public string? Locale { get; set; }
}
