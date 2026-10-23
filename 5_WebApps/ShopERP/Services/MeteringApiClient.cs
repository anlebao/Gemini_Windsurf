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

    // P7 — wallet-check cache (30s per card G20) so deny checks stay cheap.
    private static readonly TimeSpan CheckCacheTtl = TimeSpan.FromSeconds(30);
    private readonly Dictionary<(Guid Tenant, string Code), (MeteringBalanceCheck? Value, DateTime ExpiresAtUtc)> _checkCache = new();

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

    /// <summary>
    /// P7 D3 — GET /v1/wallets/{t}/check. Cached 30s per (tenant, code) per card G20.
    /// Fail-open: null on transport error — business code treats null as "allow".
    /// </summary>
    public async Task<MeteringBalanceCheck?> CheckBalanceAsync(Guid tenantId, string serviceCode, CancellationToken ct = default)
    {
        if (!Enabled()) return null;
        var key = (tenantId, serviceCode);
        lock (_checkCache)
        {
            if (_checkCache.TryGetValue(key, out var hit) && hit.ExpiresAtUtc > DateTime.UtcNow)
                return hit.Value;
        }
        try
        {
            using var resp = await _http.GetAsync(
                $"v1/wallets/{tenantId}/check?serviceCode={Uri.EscapeDataString(serviceCode)}", ct);
            if (!resp.IsSuccessStatusCode) { Fail($"HTTP {(int)resp.StatusCode}", $"check:{serviceCode}"); return null; }
            var dto = await resp.Content.ReadFromJsonAsync<WalletCheckWire>(Json, ct);
            _consecutiveFailures = 0;
            var result = dto == null ? null : new MeteringBalanceCheck(
                dto.Balance, dto.Required, dto.Deny, dto.AlertLevel);
            lock (_checkCache) _checkCache[key] = (result, DateTime.UtcNow + CheckCacheTtl);
            return result;
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            Fail(ex.Message, $"check:{serviceCode}");
            return null;
        }
    }

    /// <summary>P7 — wallet view for the "Phí nền tảng" page + banner. Null = unavailable.</summary>
    public async Task<MeteringWalletDto?> GetWalletAsync(Guid tenantId, CancellationToken ct = default)
    {
        if (!Enabled()) return null;
        try
        {
            using var resp = await _http.GetAsync($"v1/wallets/{tenantId}", ct);
            if (!resp.IsSuccessStatusCode) { Fail($"HTTP {(int)resp.StatusCode}", "wallet"); return null; }
            _consecutiveFailures = 0;
            return await resp.Content.ReadFromJsonAsync<MeteringWalletDto>(Json, ct);
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            Fail(ex.Message, "wallet");
            return null;
        }
    }

    /// <summary>P7 — paged append-only ledger for the wallet page.</summary>
    public async Task<MeteringLedgerPage?> GetLedgerAsync(Guid tenantId, int page, int pageSize, CancellationToken ct = default)
    {
        if (!Enabled()) return null;
        try
        {
            using var resp = await _http.GetAsync(
                $"v1/wallets/{tenantId}/ledger?page={page}&pageSize={pageSize}", ct);
            if (!resp.IsSuccessStatusCode) { Fail($"HTTP {(int)resp.StatusCode}", "ledger"); return null; }
            _consecutiveFailures = 0;
            return await resp.Content.ReadFromJsonAsync<MeteringLedgerPage>(Json, ct);
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            Fail(ex.Message, "ledger");
            return null;
        }
    }

    /// <summary>P7 D6 — create manual top-up request; returns dto with referenceCode + QR payload.</summary>
    public async Task<MeteringTopupDto?> CreateTopupAsync(Guid tenantId, decimal? amountVnd, CancellationToken ct = default)
    {
        if (!Enabled()) return null;
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Post, "v1/topups");
            // Stable per-minute key — double-click / F5 replays the same request server-side.
            req.Headers.TryAddWithoutValidation("Idempotency-Key",
                $"topup:{tenantId}:{DateTime.UtcNow:yyyyMMddHHmm}");
            req.Content = JsonContent.Create(new { tenantId, amountVnd }, options: Json);
            using var resp = await _http.SendAsync(req, ct);
            if (!resp.IsSuccessStatusCode) { Fail($"HTTP {(int)resp.StatusCode}", "topup-create"); return null; }
            _consecutiveFailures = 0;
            return await resp.Content.ReadFromJsonAsync<MeteringTopupDto>(Json, ct);
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            Fail(ex.Message, "topup-create");
            return null;
        }
    }

    /// <summary>P7 — tenant's own top-up requests (list page / status refresh).</summary>
    public async Task<List<MeteringTopupDto>> GetTopupsAsync(Guid tenantId, CancellationToken ct = default)
    {
        if (!Enabled()) return new List<MeteringTopupDto>();
        try
        {
            using var resp = await _http.GetAsync($"v1/topups?tenantId={tenantId}", ct);
            if (!resp.IsSuccessStatusCode) { Fail($"HTTP {(int)resp.StatusCode}", "topups"); return new List<MeteringTopupDto>(); }
            _consecutiveFailures = 0;
            var list = await resp.Content.ReadFromJsonAsync<MeteringTopupList>(Json, ct);
            return list?.Items ?? new List<MeteringTopupDto>();
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            Fail(ex.Message, "topups");
            return new List<MeteringTopupDto>();
        }
    }

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

    /// <summary>Wire shape of GET /v1/wallets/{t}/check.</summary>
    private sealed class WalletCheckWire
    {
        public decimal Balance { get; set; }
        public decimal Required { get; set; }
        public bool Deny { get; set; }
        public string? AlertLevel { get; set; }
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
