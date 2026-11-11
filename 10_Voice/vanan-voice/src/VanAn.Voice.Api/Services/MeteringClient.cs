using System.Collections.Concurrent;
using System.Net.Http.Json;

namespace VanAn.Voice.Api.Services;

public sealed record MeteringUsageEvent(
    string IdempotencyKey,
    Guid TenantId,
    string ServiceCode,
    decimal Quantity,
    string Unit,
    DateTime OccurredAtUtc,
    Guid? ShopInstanceId = null,
    string? RequestedBy = null,
    string? MetadataJson = null);

public sealed record MeteringCheckResult(bool Deny, decimal Balance);

/// <summary>
/// Thin, fail-safe client into vanan-metering (Phí nền tảng) — mirrors the Gateway copy
/// (SRS v1.1 §14, metering rules §5): timeout 3 s, circuit breaker 5-fail/60 s, check result
/// cached 30 s per (tenant, serviceCode), and FAIL-OPEN — metering death never breaks voice.
/// Caller identity: <c>voice</c> (seeded via Metering:BootstrapCallers).
/// </summary>
public sealed class MeteringClient
{
    private const int CircuitFailureThreshold = 5;
    private static readonly TimeSpan CircuitOpenDuration = TimeSpan.FromSeconds(60);
    private static readonly TimeSpan CheckCacheTtl = TimeSpan.FromSeconds(30);

    private readonly HttpClient _http;
    private readonly ILogger<MeteringClient> _logger;
    private readonly bool _enabled;

    private int _consecutiveFailures;
    private DateTime _circuitOpenUntilUtc = DateTime.MinValue;
    private readonly ConcurrentDictionary<(Guid TenantId, string ServiceCode), (MeteringCheckResult? Result, DateTime ExpiresUtc)> _checkCache = new();

    public MeteringClient(HttpClient http, IConfiguration config, ILogger<MeteringClient> logger)
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

    /// <summary>
    /// Wallet deny check (SRS §14.2). Returns null on failure → caller treats as "allow"
    /// (fail-open, metering convention G9).
    /// </summary>
    public async Task<MeteringCheckResult?> CheckAsync(Guid tenantId, string serviceCode, decimal estimate, CancellationToken ct = default)
    {
        if (!Enabled()) return null;
        var cacheKey = (tenantId, serviceCode);
        if (_checkCache.TryGetValue(cacheKey, out var cached) && cached.ExpiresUtc > DateTime.UtcNow)
            return cached.Result;

        var result = await SendCheckAsync(tenantId, serviceCode, estimate, ct);
        _checkCache[cacheKey] = (result, DateTime.UtcNow + CheckCacheTtl);
        return result;
    }

    /// <summary>Ingests usage events (deduct happens inside vanan-metering). Fail-open.</summary>
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
                metadataJson = e.MetadataJson
            }));
            using var resp = await _http.SendAsync(req, ct);
            if (resp.IsSuccessStatusCode) { _consecutiveFailures = 0; return true; }
            return Fail($"HTTP {(int)resp.StatusCode}", "batch");
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            return Fail(ex.Message, "batch");
        }
    }

    private async Task<MeteringCheckResult?> SendCheckAsync(Guid tenantId, string serviceCode, decimal estimate, CancellationToken ct)
    {
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Get,
                $"v1/wallets/{tenantId}/check?serviceCode={Uri.EscapeDataString(serviceCode)}&estimate={estimate}");
            using var resp = await _http.SendAsync(req, ct);
            if (!resp.IsSuccessStatusCode)
            {
                Fail($"HTTP {(int)resp.StatusCode}", serviceCode);
                return null;
            }

            _consecutiveFailures = 0;
            var doc = await resp.Content.ReadFromJsonAsync<CheckResponse>(ct);
            return new MeteringCheckResult(doc?.Deny ?? false, doc?.Balance ?? 0);
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            Fail(ex.Message, serviceCode);
            return null;
        }
    }

    private bool Enabled() => _enabled && DateTime.UtcNow > _circuitOpenUntilUtc;

    private bool Fail(string reason, string serviceCode)
    {
        _consecutiveFailures++;
        if (_consecutiveFailures >= CircuitFailureThreshold)
            _circuitOpenUntilUtc = DateTime.UtcNow + CircuitOpenDuration;
        _logger.LogWarning("Metering client fail-open ({Reason}) for {ServiceCode}.", reason, serviceCode);
        return false;
    }

    public sealed class CheckResponse
    {
        public bool Deny { get; set; }
        public decimal Balance { get; set; }
    }
}