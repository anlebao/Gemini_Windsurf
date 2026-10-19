using System.Net.Http.Json;
using Microsoft.AspNetCore.Components.Authorization;
using VanAn.CoreHub.Services;
using VanAn.Shared.Services;

namespace VanAn.ShopERP.Services
{
    /// <summary>
    /// Issue #195 (P3a): SystemAdmin CRUD client cho Gateway /api/v1/admin/orderflow.
    /// Mints SystemAdmin JWT cho user hiện tại (pattern GatewayAdminApiClientBase).
    /// </summary>
    public sealed class OrderFlowAdminApiClient : GatewayAdminApiClientBase
    {
        public OrderFlowAdminApiClient(
            IHttpClientFactory httpClientFactory,
            IConfiguration configuration,
            IJwtTokenService jwtTokenService,
            AuthenticationStateProvider authStateProvider,
            ILogger<OrderFlowAdminApiClient> logger)
            : base(httpClientFactory, configuration, jwtTokenService, authStateProvider, logger)
        {
        }

        /// <summary>GET /api/v1/admin/orderflow — tất cả profiles (hoặc theo tenant).</summary>
        public async Task<List<OrderFlowProfileDto>> GetProfilesAsync(Guid? tenantId = null, CancellationToken ct = default)
        {
            string uri = "api/v1/admin/orderflow" + (tenantId.HasValue ? $"?tenantId={tenantId.Value}" : "");
            using HttpRequestMessage request = await CreateRequestAsync(HttpMethod.Get, uri);
            return await SendAndReadAsync<List<OrderFlowProfileDto>>(HttpClient, request, ct) ?? new List<OrderFlowProfileDto>();
        }

        /// <summary>POST /api/v1/admin/orderflow — upsert profile + steps.</summary>
        public async Task<OrderFlowProfileDto> UpsertProfileAsync(UpsertOrderFlowRequestBody request, CancellationToken ct = default)
        {
            using HttpRequestMessage httpRequest = await CreateRequestAsync(HttpMethod.Post, "api/v1/admin/orderflow", request);
            OrderFlowProfileDto? dto = await SendAndReadAsync<OrderFlowProfileDto>(HttpClient, httpRequest, ct);
            return dto ?? throw new InvalidOperationException("Gateway returned empty response for upsert order flow");
        }

        /// <summary>DELETE /api/v1/admin/orderflow/{id} — deactivate profile (204 NoContent).</summary>
        public async Task DeactivateAsync(Guid profileId, CancellationToken ct = default)
        {
            using HttpRequestMessage request = await CreateRequestAsync(HttpMethod.Delete, $"api/v1/admin/orderflow/{profileId}");
            HttpResponseMessage response = await HttpClient.SendAsync(request, ct);
            if (!response.IsSuccessStatusCode)
            {
                string body = await response.Content.ReadAsStringAsync(ct);
                throw new InvalidOperationException(
                    $"Gateway API DELETE orderflow/{profileId} failed ({(int)response.StatusCode}): {body}");
            }
        }
    }

    /// <summary>Request body khớp Gateway UpsertOrderFlowRequest (camelCase serialization).</summary>
    public sealed class UpsertOrderFlowRequestBody
    {
        public Guid? TenantId { get; set; }
        public string? SourceType { get; set; }
        public string? CategoryKey { get; set; }
        public string Name { get; set; } = string.Empty;
        public List<UpsertOrderFlowStepBody> Steps { get; set; } = new();
    }

    public sealed class UpsertOrderFlowStepBody
    {
        public string Code { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public int SortOrder { get; set; }
        public bool IsTerminal { get; set; }
        public List<string> AllowedNextCodes { get; set; } = new();
    }
}
