using System.Net.Http.Json;
using System.Text.Json;

namespace VanAn.Gateway.Services;

/// <summary>Wire DTOs for the gateway → vanan-ai forward (SRS v1.1 §6.1, camelCase on the wire).</summary>
public sealed record TenantAiChatForwardRequest(string SessionId, string MessageId, string Text, string? Locale);

public sealed record TenantAiForwardResult(int StatusCode, string? Body);

/// <summary>
/// Task AI-P4 — thin, fail-safe forwarder into the independent vanan-ai service.
/// Registered as a typed HttpClient (timeout 35s > LLM 30s; X-Caller-Id + X-Ai-Key default headers in DI).
/// Signs a fresh tenant context per request (HMAC-SHA256, TTL 2ph, requestId = caller-supplied id).
///
/// Guarantees: NEVER throws into business code — transport errors / timeout → return null
/// (controller maps to 503 graceful). Missing TenantAi:BaseUrl or CtxSigningSecret → self-disables
/// (IsEnabled=false) so staging/main-sln works without the AI service deployed.
/// </summary>
public sealed class TenantAiForwarder
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _http;
    private readonly ILogger<TenantAiForwarder> _logger;
    private readonly string? _signingSecret;
    private readonly string _callerId;

    public TenantAiForwarder(HttpClient http, IConfiguration config, ILogger<TenantAiForwarder> logger)
    {
        _http = http;
        // BaseAddress must end with "/" or relative "v1/..." paths drop the last segment.
        if (http.BaseAddress is { } b && !b.OriginalString.EndsWith('/'))
            http.BaseAddress = new Uri(b.OriginalString + "/");
        _logger = logger;
        _callerId = config["TenantAi:CallerId"] ?? "gateway";
        _signingSecret = config["TenantAi:CtxSigningSecret"];
        IsEnabled = http.BaseAddress is not null && !string.IsNullOrWhiteSpace(_signingSecret);
        if (!IsEnabled)
            logger.LogWarning("TenantAi forwarder disabled — TenantAi:BaseUrl/CtxSigningSecret not configured.");
    }

    public bool IsEnabled { get; }

    /// <summary>POST /v1/chat with a signed tenant ctx (requestId = messageId — dedupe key end-to-end).</summary>
    public Task<TenantAiForwardResult?> ChatAsync(
        Guid tenantId, string sessionId, string messageId, string text, string? locale, CancellationToken ct)
        => SendAsync(HttpMethod.Post, "v1/chat", tenantId, messageId,
            JsonContent.Create(new TenantAiChatForwardRequest(sessionId, messageId, text, locale), options: Json), ct);

    /// <summary>GET /v1/config/widget — widget enable gate + aiName/greeting/suggestions.</summary>
    public Task<TenantAiForwardResult?> GetWidgetConfigAsync(Guid tenantId, string requestId, CancellationToken ct)
        => SendAsync(HttpMethod.Get, "v1/config/widget", tenantId, requestId, content: null, ct);

    /// <summary>GET /v1/chat/sessions/{sid}/messages — session resume history.</summary>
    public Task<TenantAiForwardResult?> GetSessionMessagesAsync(Guid tenantId, string sessionId, CancellationToken ct)
        => SendAsync(HttpMethod.Get, $"v1/chat/sessions/{Uri.EscapeDataString(sessionId)}/messages",
            tenantId, $"hist-{sessionId}", content: null, ct);

    private async Task<TenantAiForwardResult?> SendAsync(
        HttpMethod method, string path, Guid tenantId, string requestId, HttpContent? content, CancellationToken ct)
    {
        if (!IsEnabled) return null;
        try
        {
            var (ctxHeader, sig) = TenantAiContextSigner.Sign(
                TenantAiContextSigner.CreatePayload(tenantId, _callerId, requestId), _signingSecret!);
            using var req = new HttpRequestMessage(method, path);
            req.Headers.TryAddWithoutValidation("X-Tenant-Ctx", ctxHeader);
            req.Headers.TryAddWithoutValidation("X-Tenant-Ctx-Sig", sig);
            req.Content = content;
            using var resp = await _http.SendAsync(req, ct);
            var body = resp.Content is null ? null : await resp.Content.ReadAsStringAsync(ct);
            return new TenantAiForwardResult((int)resp.StatusCode, body);
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            _logger.LogWarning(ex, "TenantAi forward failed ({Method} {Path})", method, path);
            return null;
        }
    }
}
