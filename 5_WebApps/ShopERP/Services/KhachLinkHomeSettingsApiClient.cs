using System.Net.Http.Json;
using Microsoft.AspNetCore.Components.Authorization;
using VanAn.CoreHub.Services;

namespace VanAn.ShopERP.Services
{
    /// <summary>
    /// #100: ShopERP client for Gateway KhachLinkHomeSettings admin API.
    /// Calls /api/platform/khachlink-home-settings with SystemAdmin Bearer JWT.
    /// Backed by PostgreSQL (Gateway DB) — global config, NOT per-tenant.
    /// </summary>
    public sealed class KhachLinkHomeSettingsApiClient : GatewayAdminApiClientBase
    {
        public KhachLinkHomeSettingsApiClient(
            IHttpClientFactory httpClientFactory,
            IConfiguration configuration,
            IJwtTokenService jwtTokenService,
            AuthenticationStateProvider authStateProvider,
            ILogger<KhachLinkHomeSettingsApiClient> logger)
            : base(httpClientFactory, configuration, jwtTokenService, authStateProvider, logger) { }

        public async Task<KhachLinkHomeSettingsDto> GetSettingsAsync(CancellationToken ct = default)
        {
            var req = await CreateRequestAsync(HttpMethod.Get, "api/platform/khachlink-home-settings");
            return await SendAndReadAsync<KhachLinkHomeSettingsDto>(HttpClient, req, ct) ?? new KhachLinkHomeSettingsDto();
        }

        public async Task<KhachLinkHomeSettingsDto> UpdateSettingsAsync(KhachLinkHomeSettingsDto body, CancellationToken ct = default)
        {
            var req = await CreateRequestAsync(HttpMethod.Put, "api/platform/khachlink-home-settings", body);
            return await SendAndReadAsync<KhachLinkHomeSettingsDto>(HttpClient, req, ct)
                ?? throw new InvalidOperationException("Gateway returned an empty KhachLink home settings response.");
        }
    }

    public sealed class KhachLinkHomeSettingsDto
    {
        public bool Home_CampaignSection_Enabled { get; set; } = true;
        public bool Home_StoreSection_Enabled { get; set; } = true;
        public bool Home_FeaturedSection_Enabled { get; set; } = true;
        public bool Home_SocialHub_Enabled { get; set; } = true;
    }
}
