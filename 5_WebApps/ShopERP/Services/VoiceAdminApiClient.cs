using System.Net.Http.Json;
using Microsoft.AspNetCore.Components.Authorization;
using VanAn.CoreHub.Services;
using VanAn.Shared.Services;

namespace VanAn.ShopERP.Services
{
    /// <summary>
    /// VOICE SERVICE C3 (2026-11-11): SystemAdmin client cho Gateway /api/admin/voice-config
    /// (kill-switch VoiceFeatureEnabled + VoiceOpenCalling per tenant — PG source of truth).
    /// Mints SystemAdmin JWT cho user hiện tại (pattern GatewayAdminApiClientBase).
    /// </summary>
    public sealed class VoiceAdminApiClient : GatewayAdminApiClientBase
    {
        public VoiceAdminApiClient(
            IHttpClientFactory httpClientFactory,
            IConfiguration configuration,
            IJwtTokenService jwtTokenService,
            AuthenticationStateProvider authStateProvider,
            ILogger<VoiceAdminApiClient> logger)
            : base(httpClientFactory, configuration, jwtTokenService, authStateProvider, logger)
        {
        }

        /// <summary>GET /api/admin/voice-config — mọi tenant (Active) + config hiệu lực.</summary>
        public async Task<List<VoiceTenantConfigDto>> GetConfigsAsync(CancellationToken ct = default)
        {
            using HttpRequestMessage request = await CreateRequestAsync(HttpMethod.Get, "api/admin/voice-config");
            return await SendAndReadAsync<List<VoiceTenantConfigDto>>(HttpClient, request, ct) ?? new List<VoiceTenantConfigDto>();
        }

        /// <summary>PUT /api/admin/voice-config/{tenantId} — upsert kill-switch + open calling.</summary>
        public async Task<VoiceTenantConfigDto> UpsertAsync(Guid tenantId, UpdateVoiceTenantConfigRequest request, CancellationToken ct = default)
        {
            using HttpRequestMessage httpRequest = await CreateRequestAsync(HttpMethod.Put, $"api/admin/voice-config/{tenantId}", request);
            VoiceTenantConfigDto? dto = await SendAndReadAsync<VoiceTenantConfigDto>(HttpClient, httpRequest, ct);
            return dto ?? throw new InvalidOperationException("Gateway returned empty response for upsert voice config");
        }
    }
}