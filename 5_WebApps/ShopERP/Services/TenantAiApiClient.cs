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

/// <summary>Thin fail-safe client into the independent vanan-ai service (pattern MeteringApiClient).</summary>
public interface ITenantAiClient
{
    Task<TenantAiBatchResponse?> PushProductsBatchAsync(Guid tenantId, IReadOnlyList<TenantAiProductItem> items, bool fullSync, string sourceVersion, string idempotencyKey, CancellationToken ct = default);
    Task<bool> DeleteProductAsync(Guid tenantId, Guid productId, CancellationToken ct = default);
    Task<bool> IngestDocumentAsync(Guid tenantId, string type, string title, string content, string classification, CancellationToken ct = default);
    Task<bool> ApproveDocumentAsync(Guid tenantId, Guid docId, CancellationToken ct = default);
    Task<bool> RejectDocumentAsync(Guid tenantId, Guid docId, string reason, CancellationToken ct = default);
    Task<List<TenantAiDocSummary>> GetPendingDocumentsAsync(Guid tenantId, CancellationToken ct = default);
    Task<TenantAiStatus?> GetKnowledgeStatusAsync(Guid tenantId, CancellationToken ct = default);
    /// <summary>Task AI-P9 — paged AI conversation sessions (newest last-active first). Null on failure.</summary>
    Task<TenantAiConversationList?> GetAiConversationsAsync(Guid tenantId, int page, int size, CancellationToken ct = default);
    /// <summary>Task AI-P9 — full message drill for one session (read-only). Null on failure/404.</summary>
    Task<TenantAiConversationDetail?> GetAiConversationMessagesAsync(Guid tenantId, string sessionId, CancellationToken ct = default);
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

    public async Task<bool> IngestDocumentAsync(Guid tenantId, string type, string title, string content, string classification, CancellationToken ct = default)
        => await Send<object>(tenantId, HttpMethod.Post, "v1/knowledge/documents",
            new { type, title, content, classification }, null, ct) != null;

    public async Task<bool> ApproveDocumentAsync(Guid tenantId, Guid docId, CancellationToken ct = default)
        => await Send<object>(tenantId, HttpMethod.Post, $"v1/admin/knowledge/{docId}/approve", null, AdminTimeout, ct) != null;

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
