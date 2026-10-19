using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Caching.Memory;
using VanAn.CoreHub.Services;
using VanAn.Shared.Services;

namespace VanAn.ShopERP.Services
{
    /// <summary>
    /// Issue #195 (2026-10-19, approved Q4): ShopERP implementation của IOrderFlowService —
    /// resolve order flow qua Gateway API (PG source of truth — pattern NotificationApiClient).
    /// OrderWorkflowService (SQLite scope) dùng nó để validate transitions động; khi Gateway
    /// unreachable → built-in fallback (không chặn workflow offline).
    /// Cache 60s per (tenant, sourceType) — profile đổi hiếm, bounded staleness.
    /// </summary>
    public sealed class OrderFlowApiClient : IOrderFlowService
    {
        private readonly HttpClient _httpClient;
        private readonly IJwtTokenService _jwtTokenService;
        private readonly IMemoryCache _cache;
        private readonly ILogger<OrderFlowApiClient> _logger;

        private static readonly TimeSpan CacheTtl = TimeSpan.FromSeconds(60);
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true
        };

        public OrderFlowApiClient(
            IHttpClientFactory httpClientFactory,
            IConfiguration configuration,
            IJwtTokenService jwtTokenService,
            IMemoryCache cache,
            ILogger<OrderFlowApiClient> logger)
        {
            _httpClient = httpClientFactory.CreateClient("GatewayClient");
            _jwtTokenService = jwtTokenService;
            _cache = cache;
            _logger = logger;

            string baseUrl = configuration["Gateway:BaseUrl"] ?? "http://localhost:5001";
            _httpClient.BaseAddress = new Uri(baseUrl);
        }

        public async Task<OrderFlowResolution> ResolveFlowAsync(Guid tenantId, string? sourceType, CancellationToken ct = default)
        {
            string? src = string.IsNullOrWhiteSpace(sourceType) ? null : sourceType.Trim().ToLowerInvariant();
            string cacheKey = $"orderflow:http:{tenantId}:{src ?? "*"}";
            if (_cache.TryGetValue(cacheKey, out OrderFlowResolution? cached) && cached != null)
                return cached;

            OrderFlowResolution resolution;
            try
            {
                string uri = "api/v1/orderflow/resolve" + (src != null ? $"?sourceType={Uri.EscapeDataString(src)}" : "");
                var request = new HttpRequestMessage(HttpMethod.Get, uri);
                // Service-to-service JWT — ShopERP mints short-lived token với tenant claim
                // (không cần user context: chạy được trong background subscribers).
                request.Headers.Authorization = new AuthenticationHeaderValue(
                    JwtBearerDefaults.AuthenticationScheme,
                    _jwtTokenService.GenerateToken(Guid.NewGuid(), "orderflow@shoperp.local", "Staff", tenantId));

                HttpResponseMessage response = await _httpClient.SendAsync(request, ct);
                response.EnsureSuccessStatusCode();
                resolution = await response.Content.ReadFromJsonAsync<OrderFlowResolution>(JsonOptions, ct)
                    ?? OrderFlowService.BuildBuiltin(src);
            }
            catch (Exception ex)
            {
                // Fail-open: Gateway unreachable → built-in flow — transitions không bị chặn offline.
                _logger.LogWarning(ex, "OrderFlowApiClient: resolve failed (tenant={TenantId}, source={Source}) — builtin fallback",
                    tenantId, src ?? "*");
                resolution = OrderFlowService.BuildBuiltin(src);
            }

            _cache.Set(cacheKey, resolution, CacheTtl);
            return resolution;
        }

        public async Task<bool> IsTransitionAllowedAsync(Guid tenantId, string? sourceType, string fromStatus, string toStatus, CancellationToken ct = default)
        {
            OrderFlowResolution flow = await ResolveFlowAsync(tenantId, sourceType, ct);
            return flow.IsAllowed(fromStatus.Trim().ToLowerInvariant(), toStatus.Trim().ToLowerInvariant());
        }

        public async Task<IReadOnlyList<string>> GetNextStatusesAsync(Guid tenantId, string? sourceType, string currentStatus, CancellationToken ct = default)
        {
            OrderFlowResolution flow = await ResolveFlowAsync(tenantId, sourceType, ct);
            return flow.NextOf(currentStatus.Trim().ToLowerInvariant());
        }
    }
}
