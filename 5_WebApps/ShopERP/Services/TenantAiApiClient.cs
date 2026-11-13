using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace VanAn.ShopERP.Services;

/// <summary>Wire DTOs for vanan-ai knowledge ingest (SRS v1.1 §6.2).</summary>
public sealed record TenantAiProductItem(
    Guid ProductId, string Name, string? Description, decimal Price,
    int? Calories, string[]? Ingredients, string[]? Allergens,
    bool Available, string? CategoryName);

public sealed record TenantAiBatchResponse(int Upserted, int Removed, string IdempotencyKey, bool Replayed);

public sealed record TenantAiDocSummary(
    Guid Id, string Type, string Title, string Classification, string ApprovalStatus,
    string? ApprovedBy, DateTime? ApprovedAtUtc, string? RejectionReason, DateTime CreatedAtUtc);

public sealed record TenantAiStatus(
    int PendingDocs, int ApprovedDocs, int RejectedDocs, int Chunks, int ChunksEmbedded,
    int ChunksPendingEmbedding, int Products, string? LastProductSourceVersion,
    DateTime? LastProductSyncedAtUtc, bool ProductsStale, int StaleHours);

/// <summary>Task AI-P9 — admin conversation review DTOs (mirror vanan-ai Contracts/AdminDtos, scope=admin).</summary>
public sealed record TenantAiConversationSummary(
    string SessionId, string Channel, DateTime LastActiveAtUtc,
    int MessageCount, int RefusedCount, int DegradedCount);

public sealed record TenantAiConversationList(
    int Page, int Size, int Total, IReadOnlyList<TenantAiConversationSummary> Items);

public sealed record TenantAiConversationMessage(
    string Role, string Content, string? Intent, double? Confidence,
    bool Refused, bool Degraded, DateTime CreatedAtUtc);

public sealed record TenantAiConversationDetail(
    string SessionId, IReadOnlyList<TenantAiConversationMessage> Messages);

/// <summary>Task AI-P10 — admin profile/policy/usage/preview/docs DTOs (mirror vanan-ai Contracts).</summary>
public sealed record TenantAiProfileVersion(
    int Version, string Status, string AiName, string PersonaJson, string BusinessProfileJson,
    DateTime? PublishedAtUtc, string? PublishedBy);

public sealed record TenantAiProfileResponse(
    TenantAiProfileVersion? Draft, TenantAiProfileVersion? Published,
    IReadOnlyList<TenantAiProfileVersion> RecentVersions);

public sealed record TenantAiPutProfileResult(int Version, string Status);

public sealed record TenantAiPublishResult(
    int Version, string Status, DateTime PublishedAtUtc, string PublishedBy);

public sealed record TenantAiPolicyResponse(
    int? ConfigVersion, string IntentScopesJson, string ExternalAllowJson, string RefusedTopicsJson);

public sealed record TenantAiChatProduct(Guid ProductId, string Name, decimal Price, string? ImageUrl);

public sealed record TenantAiChatResult(
    string Answer, IReadOnlyList<TenantAiChatProduct> Products, IReadOnlyList<string> SourceIds,
    double Confidence, bool Refused, string? RefuseReason, bool StaleData, bool Degraded);

public sealed record TenantAiDocItem(
    Guid Id, string Type, string Title, string Classification, string ApprovalStatus,
    string? ApprovedBy, DateTime? ApprovedAtUtc, string? RejectionReason,
    DateTime CreatedAtUtc, bool Sanitized, string Content);

public sealed record TenantAiDocList(
    int Page, int Size, int Total, IReadOnlyList<TenantAiDocItem> Items);

public sealed record TenantAiUsageDay(
    string Date, int Messages, int Refused, int Degraded, long PromptTokens, long CompletionTokens);

public sealed record TenantAiUsageResponse(
    int Days, IReadOnlyList<TenantAiUsageDay> PerDay, TenantAiUsageDay Total);

/// <summary>Thin fail-safe client into the independent vanan-ai service (pattern MeteringApiClient).</summary>
public interface ITenantAiClient
{
    Task<TenantAiBatchResponse?> PushProductsBatchAsync(Guid tenantId, IReadOnlyList<TenantAiProductItem> items, bool fullSync, string sourceVersion, string idempotencyKey, CancellationToken ct = default);
    Task<bool> DeleteProductAsync(Guid tenantId, Guid productId, CancellationToken ct = default);
    Task<bool> IngestDocumentAsync(Guid tenantId, string type, string title, string content, string classification, string? sourceRef = null, CancellationToken ct = default);
    /// <summary>AI-P11 — classification optional: owner chọn visibility lúc approve (null = giữ nguyên).</summary>
    Task<bool> ApproveDocumentAsync(Guid tenantId, Guid docId, string? classification = null, CancellationToken ct = default);
    Task<bool> RejectDocumentAsync(Guid tenantId, Guid docId, string reason, CancellationToken ct = default);
    Task<List<TenantAiDocSummary>> GetPendingDocumentsAsync(Guid tenantId, CancellationToken ct = default);
    Task<TenantAiStatus?> GetKnowledgeStatusAsync(Guid tenantId, CancellationToken ct = default);
    /// <summary>Task AI-P9 — paged AI conversation sessions (newest last-active first). Null on failure.</summary>
    Task<TenantAiConversationList?> GetAiConversationsAsync(Guid tenantId, int page, int size, CancellationToken ct = default);
    /// <summary>Task AI-P9 — full message drill for one session (read-only). Null on failure/404.</summary>
    Task<TenantAiConversationDetail?> GetAiConversationMessagesAsync(Guid tenantId, string sessionId, CancellationToken ct = default);
    /// <summary>Task AI-P10 — draft + published + recent version history. Null on failure.</summary>
    Task<TenantAiProfileResponse?> GetProfileAsync(Guid tenantId, CancellationToken ct = default);
    /// <summary>PUT profile — append-only: creates a new Draft version. Null on failure.</summary>
    Task<TenantAiPutProfileResult?> PutProfileAsync(Guid tenantId, string aiName, string? personaJson, string? businessProfileJson, CancellationToken ct = default);
    /// <summary>Publish current Draft → published; previous published archived. Null on failure/409.</summary>
    Task<TenantAiPublishResult?> PublishConfigAsync(Guid tenantId, CancellationToken ct = default);
    /// <summary>Rollback = copy target version into a new draft + publish (append-only). Null on failure/404.</summary>
    Task<TenantAiPublishResult?> RollbackConfigAsync(Guid tenantId, int version, CancellationToken ct = default);
    /// <summary>Policy bound to the current config version (draft preferred). Null on failure.</summary>
    Task<TenantAiPolicyResponse?> GetPolicyAsync(Guid tenantId, CancellationToken ct = default);
    Task<TenantAiPolicyResponse?> PutPolicyAsync(Guid tenantId, string? intentScopesJson, string? externalAllowJson, string? refusedTopicsJson, CancellationToken ct = default);
    /// <summary>Draft-config preview chat (30s LLM timeout) — never persists/meters. Null on failure.</summary>
    Task<TenantAiChatResult?> PreviewChatAsync(Guid tenantId, string sessionId, string messageId, string text, CancellationToken ct = default);
    /// <summary>Paged knowledge documents; status = pending_review|approved|rejected (null = all active).</summary>
    Task<TenantAiDocList?> GetDocumentsAsync(Guid tenantId, string? status, int page, int size, CancellationToken ct = default);
    Task<bool> DeleteDocumentAsync(Guid tenantId, Guid docId, CancellationToken ct = default);
    /// <summary>Per-day usage aggregate (messages/refused/degraded/tokens). Null on failure.</summary>
    Task<TenantAiUsageResponse?> GetUsageAsync(Guid tenantId, int days, CancellationToken ct = default);
}

/// <summary>
/// task_ai_p2 — HTTP client for vanan-ai ingest/admin APIs. Self-disables when
/// TenantAi:BaseUrl/CallerId/CallerKey/CtxSigningSecret missing. Signs X-Tenant-Ctx
/// per request (HMAC-SHA256, 120s TTL). Circuit breaker 5-fail/60s; transport
/// errors return false/null — knowledge sync never blocks selling.
/// </summary>
public sealed class TenantAiApiClient : ITenantAiClient
{
    private const int CircuitFailureThreshold = 5;
    private static readonly TimeSpan CircuitOpenDuration = TimeSpan.FromSeconds(60);
    private static readonly TimeSpan AdminTimeout = TimeSpan.FromSeconds(5);
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _http;
    private readonly ILogger<TenantAiApiClient> _logger;
    private readonly string? _callerId;
    private readonly string? _secret;
    private readonly bool _enabled;

    private int _consecutiveFailures;
    private DateTime _circuitOpenUntilUtc = DateTime.MinValue;

    public TenantAiApiClient(HttpClient http, IConfiguration config, ILogger<TenantAiApiClient> logger)
    {
        _http = http;
        if (http.BaseAddress is { } b && !b.OriginalString.EndsWith('/'))
            http.BaseAddress = new Uri(b.OriginalString + "/");
        _logger = logger;
        _callerId = config["TenantAi:CallerId"];
        _secret = config["TenantAi:CtxSigningSecret"];
        _enabled = http.BaseAddress is not null
            && !string.IsNullOrWhiteSpace(_callerId)
            && !string.IsNullOrWhiteSpace(config["TenantAi:CallerKey"])
            && !string.IsNullOrWhiteSpace(_secret);
        if (!_enabled)
            logger.LogWarning("TenantAi client disabled — TenantAi:BaseUrl/CallerId/CallerKey/CtxSigningSecret not configured.");
    }

    public Task<TenantAiBatchResponse?> PushProductsBatchAsync(Guid tenantId, IReadOnlyList<TenantAiProductItem> items,
        bool fullSync, string sourceVersion, string idempotencyKey, CancellationToken ct = default)
        => Send<TenantAiBatchResponse>(tenantId, HttpMethod.Post, "v1/knowledge/products:batch",
            new { fullSync, sourceVersion, items }, TimeSpan.FromSeconds(30), ct, idempotencyKey);

    public Task<bool> DeleteProductAsync(Guid tenantId, Guid productId, CancellationToken ct = default)
        => SendVoid(tenantId, HttpMethod.Delete, $"v1/knowledge/products/{productId}", null, null, ct);

    public async Task<bool> IngestDocumentAsync(Guid tenantId, string type, string title, string content, string classification, string? sourceRef = null, CancellationToken ct = default)
        => await Send<object>(tenantId, HttpMethod.Post, "v1/knowledge/documents",
            new { type, title, content, classification, sourceRef }, null, ct) != null;

    public async Task<bool> ApproveDocumentAsync(Guid tenantId, Guid docId, string? classification = null, CancellationToken ct = default)
        => await Send<object>(tenantId, HttpMethod.Post, $"v1/admin/knowledge/{docId}/approve",
            classification == null ? null : new { classification }, AdminTimeout, ct) != null;

    public async Task<bool> RejectDocumentAsync(Guid tenantId, Guid docId, string reason, CancellationToken ct = default)
        => await Send<object>(tenantId, HttpMethod.Post, $"v1/admin/knowledge/{docId}/reject", new { reason }, AdminTimeout, ct) != null;

    public async Task<List<TenantAiDocSummary>> GetPendingDocumentsAsync(Guid tenantId, CancellationToken ct = default)
        => await Send<List<TenantAiDocSummary>>(tenantId, HttpMethod.Get, "v1/admin/knowledge/pending", null, AdminTimeout, ct) ?? [];

    public Task<TenantAiStatus?> GetKnowledgeStatusAsync(Guid tenantId, CancellationToken ct = default)
        => Send<TenantAiStatus>(tenantId, HttpMethod.Get, "v1/knowledge/status", null, AdminTimeout, ct);

    public Task<TenantAiConversationList?> GetAiConversationsAsync(Guid tenantId, int page, int size, CancellationToken ct = default)
        => Send<TenantAiConversationList>(tenantId, HttpMethod.Get,
            $"v1/admin/tenants/{tenantId}/conversations?page={page}&size={size}", null, AdminTimeout, ct);

    public Task<TenantAiConversationDetail?> GetAiConversationMessagesAsync(Guid tenantId, string sessionId, CancellationToken ct = default)
        => Send<TenantAiConversationDetail>(tenantId, HttpMethod.Get,
            $"v1/admin/tenants/{tenantId}/conversations?sessionId={Uri.EscapeDataString(sessionId)}", null, AdminTimeout, ct);

    // ---------- task_ai_p10: admin API (SRS §6.3) ----------

    public Task<TenantAiProfileResponse?> GetProfileAsync(Guid tenantId, CancellationToken ct = default)
        => Send<TenantAiProfileResponse>(tenantId, HttpMethod.Get, $"v1/admin/tenants/{tenantId}/profile", null, AdminTimeout, ct);

    public Task<TenantAiPutProfileResult?> PutProfileAsync(Guid tenantId, string aiName,
        string? personaJson, string? businessProfileJson, CancellationToken ct = default)
        => Send<TenantAiPutProfileResult>(tenantId, HttpMethod.Put, $"v1/admin/tenants/{tenantId}/profile",
            new { aiName, personaJson, businessProfileJson }, AdminTimeout, ct);

    public Task<TenantAiPublishResult?> PublishConfigAsync(Guid tenantId, CancellationToken ct = default)
        => Send<TenantAiPublishResult>(tenantId, HttpMethod.Post, $"v1/admin/tenants/{tenantId}/publish", null, AdminTimeout, ct);

    public Task<TenantAiPublishResult?> RollbackConfigAsync(Guid tenantId, int version, CancellationToken ct = default)
        => Send<TenantAiPublishResult>(tenantId, HttpMethod.Post, $"v1/admin/tenants/{tenantId}/rollback",
            new { version }, AdminTimeout, ct);

    public Task<TenantAiPolicyResponse?> GetPolicyAsync(Guid tenantId, CancellationToken ct = default)
        => Send<TenantAiPolicyResponse>(tenantId, HttpMethod.Get, $"v1/admin/tenants/{tenantId}/policy", null, AdminTimeout, ct);

    public Task<TenantAiPolicyResponse?> PutPolicyAsync(Guid tenantId, string? intentScopesJson,
        string? externalAllowJson, string? refusedTopicsJson, CancellationToken ct = default)
        => Send<TenantAiPolicyResponse>(tenantId, HttpMethod.Put, $"v1/admin/tenants/{tenantId}/policy",
            new { intentScopesJson, externalAllowJson, refusedTopicsJson }, AdminTimeout, ct);

    public Task<TenantAiChatResult?> PreviewChatAsync(Guid tenantId, string sessionId,
        string messageId, string text, CancellationToken ct = default)
        => Send<TenantAiChatResult>(tenantId, HttpMethod.Post, $"v1/admin/tenants/{tenantId}/preview-chat",
            new { sessionId, messageId, text }, TimeSpan.FromSeconds(30), ct);

    public Task<TenantAiDocList?> GetDocumentsAsync(Guid tenantId, string? status, int page, int size, CancellationToken ct = default)
        => Send<TenantAiDocList>(tenantId, HttpMethod.Get,
            $"v1/admin/knowledge/documents?page={page}&size={size}{(string.IsNullOrWhiteSpace(status) ? "" : $"&status={Uri.EscapeDataString(status)}")}",
            null, AdminTimeout, ct);

    public Task<bool> DeleteDocumentAsync(Guid tenantId, Guid docId, CancellationToken ct = default)
        => SendVoid(tenantId, HttpMethod.Delete, $"v1/knowledge/documents/{docId}", null, AdminTimeout, ct);

    public Task<TenantAiUsageResponse?> GetUsageAsync(Guid tenantId, int days, CancellationToken ct = default)
        => Send<TenantAiUsageResponse>(tenantId, HttpMethod.Get, $"v1/admin/tenants/{tenantId}/usage?days={days}", null, AdminTimeout, ct);

    // ---------- plumbing ----------

    private async Task<T?> Send<T>(Guid tenantId, HttpMethod method, string path,
        object? body, TimeSpan? timeout, CancellationToken ct, string? idemKey = null)
    {
        if (!Enabled()) return default;
        try
        {
            using var cts = timeout.HasValue ? CancellationTokenSource.CreateLinkedTokenSource(ct) : null;
            cts?.CancelAfter(timeout!.Value);
            using var req = new HttpRequestMessage(method, path);
            SignTenantCtx(req, tenantId);
            if (idemKey != null) req.Headers.TryAddWithoutValidation("Idempotency-Key", idemKey);
            if (body != null) req.Content = JsonContent.Create(body, options: Json);
            using var resp = await _http.SendAsync(req, cts?.Token ?? ct);
            if (!resp.IsSuccessStatusCode) { Fail($"HTTP {(int)resp.StatusCode}", path); return default; }
            _consecutiveFailures = 0;
            // Bool-ish callers treat any 2xx as success even with empty body.
            if (typeof(T) == typeof(object)) return (T)(object)new object();
            return await resp.Content.ReadFromJsonAsync<T>(Json, ct);
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            Fail(ex.Message, path);
            return default;
        }
    }

    private async Task<bool> SendVoid(Guid tenantId, HttpMethod method, string path,
        object? body, TimeSpan? timeout, CancellationToken ct)
    {
        if (!Enabled()) return false;
        try
        {
            using var cts = timeout.HasValue ? CancellationTokenSource.CreateLinkedTokenSource(ct) : null;
            cts?.CancelAfter(timeout!.Value);
            using var req = new HttpRequestMessage(method, path);
            SignTenantCtx(req, tenantId);
            if (body != null) req.Content = JsonContent.Create(body, options: Json);
            using var resp = await _http.SendAsync(req, cts?.Token ?? ct);
            if (!resp.IsSuccessStatusCode) { Fail($"HTTP {(int)resp.StatusCode}", path); return false; }
            _consecutiveFailures = 0;
            return true;
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            Fail(ex.Message, path);
            return false;
        }
    }

    /// <summary>SRS [D7] — signed tenant ctx: base64url(payload) + base64url(HMAC-SHA256).</summary>
    private void SignTenantCtx(HttpRequestMessage req, Guid tenantId)
    {
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var payload = JsonSerializer.Serialize(new
        {
            tenantId, callerId = _callerId, iat = now, exp = now + 120,
            requestId = Guid.NewGuid().ToString("N")
        }, Json);
        var ctx = Base64Url(Encoding.UTF8.GetBytes(payload));
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(_secret!));
        req.Headers.TryAddWithoutValidation("X-Tenant-Ctx", ctx);
        req.Headers.TryAddWithoutValidation("X-Tenant-Ctx-Sig", Base64Url(hmac.ComputeHash(Encoding.UTF8.GetBytes(ctx))));
    }

    private static string Base64Url(byte[] b)
        => Convert.ToBase64String(b).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private bool Enabled()
        => _enabled && (_consecutiveFailures < CircuitFailureThreshold || DateTime.UtcNow >= _circuitOpenUntilUtc);

    private void Fail(string reason, string path)
    {
        _consecutiveFailures++;
        if (_consecutiveFailures >= CircuitFailureThreshold)
            _circuitOpenUntilUtc = DateTime.UtcNow + CircuitOpenDuration;
        _logger.LogWarning("TenantAi call dropped ({Path}): {Reason}", path, reason);
    }
}
