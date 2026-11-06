using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace VanAn.TenantAi.Infra;

/// <summary>Rate-card codes vanan-ai reports to vanan-metering (SRS §10, PO-2).
/// `ai.chat.message` = Deny enforcement · `ai.chat.degraded`/`ai.embedding.batch` = MeterOnly.</summary>
public static class MeteringServiceCodes
{
    public const string ChatMessage = "ai.chat.message";
    public const string ChatDegraded = "ai.chat.degraded";
    public const string EmbeddingBatch = "ai.embedding.batch";
}

/// <summary>Wallet check result from GET /v1/wallets/{t}/check (deny = Enforcement Deny + balance&lt;required).</summary>
public sealed record MeteringCheckResult(decimal Balance, decimal Required, bool Deny, string? AlertLevel);

/// <summary>Usage event queued for POST /v1/usage/events — IdempotencyKey dedupes per caller.</summary>
public sealed record MeteringUsageRecord(
    Guid TenantId,
    string ServiceCode,
    decimal Quantity,
    string Unit,
    DateTime OccurredAtUtc,
    string IdempotencyKey,
    string? TaskRef = null,
    string? MetadataJson = null);

/// <summary>
/// Thin metering surface for vanan-ai. Same guarantees as the per-host
/// MeteringApiClient convention (metering.md §5): never throws, returns null/false
/// on transport failure, self-disables when Metering:BaseUrl/CallerId/CallerKey
/// missing. The [G7] degrade-vs-fail-open policy lives in QuotaGate, NOT here —
/// this client only reports facts (IsConfigured / check result / ingest result).
/// </summary>
public interface IMeteringService
{
    /// <summary>False when Metering:* config missing — callers fail-open (staging without metering).</summary>
    bool IsConfigured { get; }
    Task<MeteringCheckResult?> CheckBalanceAsync(Guid tenantId, string serviceCode, CancellationToken ct = default);
    Task<bool> IngestAsync(MeteringUsageRecord evt, CancellationToken ct = default);
}

/// <summary>
/// task_ai_p5 — per-host metering client (vanan-ai copy; D2 no shared DLL).
/// Singleton via named HttpClient "metering" so circuit state persists:
/// timeout 3s, circuit breaker 5-fail/60s, X-Caller-Id/X-Metering-Key default headers.
/// </summary>
public sealed class MeteringClient : IMeteringService
{
    private const int CircuitFailureThreshold = 5;
    private static readonly TimeSpan CircuitOpenDuration = TimeSpan.FromSeconds(60);
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _http;
    private readonly ILogger<MeteringClient> _logger;

    private int _consecutiveFailures;
    private DateTime _circuitOpenUntilUtc = DateTime.MinValue;

    public bool IsConfigured { get; }

    public MeteringClient(HttpClient http, IConfiguration config, ILogger<MeteringClient> logger)
    {
        _http = http;
        // BaseAddress must end with "/" or relative "v1/..." paths drop the last segment.
        if (http.BaseAddress is { } b && !b.OriginalString.EndsWith('/'))
            http.BaseAddress = new Uri(b.OriginalString + "/");
        _logger = logger;
        IsConfigured = http.BaseAddress is not null
            && !string.IsNullOrWhiteSpace(config["Metering:CallerId"])
            && !string.IsNullOrWhiteSpace(config["Metering:CallerKey"]);
        if (!IsConfigured)
            logger.LogWarning("Metering client disabled — Metering:BaseUrl/CallerId/CallerKey not configured.");
    }

    public async Task<MeteringCheckResult?> CheckBalanceAsync(Guid tenantId, string serviceCode, CancellationToken ct = default)
    {
        if (!Enabled()) return null;
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Get,
                $"v1/wallets/{tenantId}/check?serviceCode={Uri.EscapeDataString(serviceCode)}");
            using var resp = await _http.SendAsync(req, ct);
            if (!resp.IsSuccessStatusCode) { Fail($"HTTP {(int)resp.StatusCode}", $"check:{serviceCode}"); return null; }
            var dto = await resp.Content.ReadFromJsonAsync<WalletCheckWire>(Json, ct);
            _consecutiveFailures = 0;
            return dto == null ? null : new MeteringCheckResult(dto.Balance, dto.Required, dto.Deny, dto.AlertLevel);
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            Fail(ex.Message, $"check:{serviceCode}");
            return null;
        }
    }

    public async Task<bool> IngestAsync(MeteringUsageRecord evt, CancellationToken ct = default)
    {
        if (!Enabled()) return false;
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Post, "v1/usage/events");
            req.Headers.TryAddWithoutValidation("Idempotency-Key", evt.IdempotencyKey);
            req.Content = JsonContent.Create(new
            {
                tenantId = evt.TenantId,
                serviceCode = evt.ServiceCode,
                quantity = evt.Quantity,
                unit = evt.Unit,
                occurredAtUtc = evt.OccurredAtUtc,
                taskRef = evt.TaskRef,
                metadataJson = evt.MetadataJson
            }, options: Json);
            using var resp = await _http.SendAsync(req, ct);
            if (resp.IsSuccessStatusCode) { _consecutiveFailures = 0; return true; }
            return Fail($"HTTP {(int)resp.StatusCode}", evt.ServiceCode);
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            return Fail(ex.Message, evt.ServiceCode);
        }
    }

    private bool Enabled()
    {
        if (!IsConfigured) return false;
        // Circuit breaker: after N consecutive failures, short-circuit for 60s —
        // a metering outage must not add latency to chat traffic.
        return _consecutiveFailures < CircuitFailureThreshold || DateTime.UtcNow >= _circuitOpenUntilUtc;
    }

    private bool Fail(string reason, string serviceCode)
    {
        _consecutiveFailures++;
        if (_consecutiveFailures >= CircuitFailureThreshold)
            _circuitOpenUntilUtc = DateTime.UtcNow + CircuitOpenDuration;
        _logger.LogWarning("Metering call dropped ({ServiceCode}): {Reason}", serviceCode, reason);
        return false;
    }

    private sealed record WalletCheckWire(decimal Balance, decimal Required, bool Deny, string? AlertLevel);
}
