using System.Net;
using System.Net.Http.Json;
using VanAn.UI.Platform.Components.Ai;

namespace VanAn.KhachLink.Services.Http;

/// <summary>
/// Task AI-P4 — IAiChatClient implementation for KhachLink: calls the Gateway
/// public tenant-ai surface (anonymous + rate-limited). Fail-safe: every error
/// path returns null — the widget treats null as "AI tạm nghỉ" (NFR-3).
/// </summary>
public class TenantAiPublicHttpService : IAiChatClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<TenantAiPublicHttpService> _logger;

    public TenantAiPublicHttpService(IHttpClientFactory httpClientFactory, ILogger<TenantAiPublicHttpService> logger)
    {
        _httpClient = httpClientFactory.CreateClient("gateway");
        _logger = logger;
    }

    public async Task<AiWidgetConfig?> GetWidgetConfigAsync(Guid tenantId, CancellationToken ct = default)
    {
        try
        {
            var resp = await _httpClient.GetAsync($"api/public/tenant-ai/widget-config?tenantId={tenantId}", ct);
            if (!resp.IsSuccessStatusCode) return null;
            return await resp.Content.ReadFromJsonAsync<AiWidgetConfig>(cancellationToken: ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogDebug(ex, "TenantAi widget-config failed for {TenantId}", tenantId);
            return null;
        }
    }

    public async Task<AiChatResult?> SendChatAsync(Guid tenantId, string sessionId, string messageId,
        string text, string? locale = null, CancellationToken ct = default)
    {
        try
        {
            var resp = await _httpClient.PostAsJsonAsync("api/public/tenant-ai/chat",
                new PublicAiChatRequestDto(tenantId, sessionId, messageId, text, locale), ct);
            if (resp.StatusCode == HttpStatusCode.TooManyRequests)
                return new AiChatResult(
                    "Bạn đã gửi quá nhiều tin nhắn trong giờ này. Vui lòng thử lại sau.",
                    [], [], 0, Refused: true, "rate_limited", StaleData: false, Degraded: false);
            if (!resp.IsSuccessStatusCode) return null;
            return await resp.Content.ReadFromJsonAsync<AiChatResult>(cancellationToken: ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogDebug(ex, "TenantAi chat failed for {TenantId}", tenantId);
            return null;
        }
    }

    public async Task<IReadOnlyList<AiSessionMessage>?> GetSessionMessagesAsync(
        Guid tenantId, string sessionId, CancellationToken ct = default)
    {
        try
        {
            var url = $"api/public/tenant-ai/sessions/{Uri.EscapeDataString(sessionId)}/messages?tenantId={tenantId}";
            var resp = await _httpClient.GetAsync(url, ct);
            if (!resp.IsSuccessStatusCode) return null;
            var body = await resp.Content.ReadFromJsonAsync<SessionMessagesDto>(cancellationToken: ct);
            return body?.Messages;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogDebug(ex, "TenantAi session history failed for {TenantId}", tenantId);
            return null;
        }
    }

    /// <summary>
    /// SF-W2 (A2): storefront semantic search — GET /api/public/tenant-ai/search.
    /// The gateway forwards to /v1/chat under a synthetic session, so the payload is
    /// the same AiChatResult shape (answer + products + refused).
    /// </summary>
    public async Task<AiChatResult?> SearchAiAsync(Guid tenantId, string query, CancellationToken ct = default)
    {
        try
        {
            var resp = await _httpClient.GetAsync(
                $"api/public/tenant-ai/search?query={Uri.EscapeDataString(query)}&tenantId={tenantId}", ct);
            if (!resp.IsSuccessStatusCode) return null;
            return await resp.Content.ReadFromJsonAsync<AiChatResult>(cancellationToken: ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogDebug(ex, "TenantAi search failed for {TenantId}", tenantId);
            return null;
        }
    }

    private sealed record PublicAiChatRequestDto(
        Guid? TenantId, string SessionId, string MessageId, string Text, string? Locale);

    private sealed record SessionMessagesDto(string SessionId, IReadOnlyList<AiSessionMessage> Messages);
}
