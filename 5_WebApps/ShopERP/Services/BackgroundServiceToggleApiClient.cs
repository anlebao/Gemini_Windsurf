using System.Net.Http.Json;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.Caching.Memory;
using VanAn.CoreHub.Services;

namespace VanAn.ShopERP.Services
{
    /// <summary>
    /// REQ-1.2: ShopERP client for Gateway Background Services toggle API.
    /// Calls /api/admin/background-services with SystemAdmin Bearer JWT.
    /// Used by /admin/background-services Blazor page.
    /// Also used by ShopERP background services to check toggle state (cached 30s).
    /// </summary>
    public sealed class BackgroundServiceToggleApiClient : GatewayAdminApiClientBase, IBackgroundServiceToggleService
    {
        private static readonly TimeSpan CacheTtl = TimeSpan.FromSeconds(30);
        private readonly IMemoryCache _cache;

        public BackgroundServiceToggleApiClient(
            IHttpClientFactory httpClientFactory,
            IConfiguration configuration,
            IJwtTokenService jwtTokenService,
            AuthenticationStateProvider authStateProvider,
            IMemoryCache cache,
            ILogger<BackgroundServiceToggleApiClient> logger)
            : base(httpClientFactory, configuration, jwtTokenService, authStateProvider, logger)
        {
            _cache = cache;
        }

        public async Task<bool> IsEnabledAsync(string serviceName, CancellationToken ct = default)
        {
            string cacheKey = $"bg_toggle_{serviceName}";
            if (_cache.TryGetValue(cacheKey, out bool cached))
                return cached;

            var toggles = await GetAllAsync(ct);
            bool enabled = toggles.FirstOrDefault(t => t.ServiceName == serviceName)?.IsEnabled ?? true;
            _cache.Set(cacheKey, enabled, CacheTtl);
            return enabled;
        }

        public async Task<IReadOnlyList<BackgroundServiceToggleDto>> GetAllAsync(CancellationToken ct = default)
        {
            var req = await CreateRequestAsync(HttpMethod.Get, "api/admin/background-services");
            return await SendAndReadAsync<List<BackgroundServiceToggleDto>>(HttpClient, req, ct) ?? new();
        }

        public async Task SetEnabledAsync(string serviceName, bool enabled, Guid updatedBy, CancellationToken ct = default)
        {
            var body = new { IsEnabled = enabled };
            var req = await CreateRequestAsync(HttpMethod.Put, $"api/admin/background-services/{serviceName}", body);
            var resp = await HttpClient.SendAsync(req, ct);
            resp.EnsureSuccessStatusCode();

            _cache.Remove($"bg_toggle_{serviceName}");
        }
    }
}
