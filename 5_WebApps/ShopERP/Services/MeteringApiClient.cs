using System.Net.Http.Json;
using System.Text.Json;
using VanAn.Shared.Domain.Common;

namespace VanAn.ShopERP.Services;

/// <summary>
/// Thin, fail-safe HTTP client into the independent vanan-metering service ("Phí nền tảng").
/// Registered as a typed HttpClient (timeout 3s, X-Caller-Id/X-Metering-Key headers in DI).
/// GĐ-1: ShopERP-local copy per task_p4 D2 — Gateway/Directory have their own thin copies;
/// no shared metering client DLL.
///
/// Guarantees (G9): NEVER throws into business code. Transport errors / timeout / circuit-open
/// → LogWarning once per burst + return false/null. Missing Metering:BaseUrl or CallerKey →
/// self-disables silently (single startup warning) so staging can run without metering.
/// </summary>
public sealed class MeteringApiClient : IMeteringClient
{
    private const int CircuitFailureThreshold = 5;
    private static readonly TimeSpan CircuitOpenDuration = TimeSpan.FromSeconds(60);

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _http;
    private readonly ILogger<MeteringApiClient> _logger;
    private readonly bool _enabled;

    private int _consecutiveFailures;
    private DateTime _circuitOpenUntilUtc = DateTime.MinValue;

    public MeteringApiClient(HttpClient http, IConfiguration config, ILogger<MeteringApiClient> logger)
    {
        _http = http;
        // BaseAddress must end with "/" or relative "v1/..." paths drop the last segment.
        if (http.BaseAddress is { } b && !b.OriginalString.EndsWith('/'))
            http.BaseAddress = new Uri(b.OriginalString + "/");
        _logger = logger;
        _enabled = http.BaseAddress is not null
            && !string.IsNullOrWhiteSpace(config["Metering:CallerId"])
            && !string.IsNullOrWhiteSpace(config["Metering:CallerKey"]);
        if (!_enabled)
            logger.LogWarning("Metering client disabled — Metering:BaseUrl/CallerId/CallerKey not configured.");
    }

    public async Task<bool> RecordUsageAsync(MeteringUsageEvent evt, CancellationToken ct = default)
    {
        if (!Enabled()) return false;
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Post, "v1/usage/events");
            req.Headers.TryAddWithoutValidation("Idempotency-Key", evt.IdempotencyKey);
            req.Content = JsonContent.Create(ToBody(evt), options: Json);
            using var resp = await _http.SendAsync(req, ct);
            if (resp.IsSuccessStatusCode) { _consecutiveFailures = 0; return true; }
            return Fail($"HTTP {(int)resp.StatusCode}", evt.ServiceCode);
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            return Fail(ex.Message, evt.ServiceCode);
        }
    }

    public async Task<bool> RecordUsageBatchAsync(IReadOnlyList<MeteringUsageEvent> events, CancellationToken ct = default)
    {
        if (!Enabled() || events.Count == 0) return false;
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Post, "v1/usage/events:batch");
            req.Content = JsonContent.Create(events.Select(e => new
            {
                idempotencyKey = e.IdempotencyKey,
                tenantId = e.TenantId,
                serviceCode = e.ServiceCode,
                quantity = e.Quantity,
                unit = e.Unit,
                occurredAtUtc = e.OccurredAtUtc,
                shopInstanceId = e.ShopInstanceId,
                requestedBy = e.RequestedBy,
                taskRef = e.TaskRef,
                metadataJson = e.MetadataJson
            }), options: Json);
            using var resp = await _http.SendAsync(req, ct);
            if (resp.IsSuccessStatusCode) { _consecutiveFailures = 0; return true; }
            return Fail($"HTTP {(int)resp.StatusCode}", "batch");
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            return Fail(ex.Message, "batch");
        }
    }

    /// <summary>P7 stub — wallet check endpoint does not exist in GĐ-1.</summary>
    public Task<MeteringBalanceCheck?> CheckBalanceAsync(Guid tenantId, string serviceCode, CancellationToken ct = default)
        => Task.FromResult<MeteringBalanceCheck?>(null);

    private bool Enabled()
    {
        if (!_enabled) return false;
        // Circuit breaker: after CircuitFailureThreshold consecutive failures, short-circuit for 60s
        // (no outbound calls at all — metering outage must not add latency to business traffic).
        if (_consecutiveFailures >= CircuitFailureThreshold && DateTime.UtcNow < _circuitOpenUntilUtc)
            return false;
        return true;
    }

    private bool Fail(string reason, string serviceCode)
    {
        _consecutiveFailures++;
        if (_consecutiveFailures >= CircuitFailureThreshold)
            _circuitOpenUntilUtc = DateTime.UtcNow + CircuitOpenDuration;
        _logger.LogWarning("Metering event dropped ({ServiceCode}): {Reason}", serviceCode, reason);
        return false;
    }

    private static object ToBody(MeteringUsageEvent e) => new
    {
        tenantId = e.TenantId,
        serviceCode = e.ServiceCode,
        quantity = e.Quantity,
        unit = e.Unit,
        occurredAtUtc = e.OccurredAtUtc,
        shopInstanceId = e.ShopInstanceId,
        requestedBy = e.RequestedBy,
        taskRef = e.TaskRef,
        metadataJson = e.MetadataJson
    };
}
