using System.Net.Http.Json;
using Microsoft.AspNetCore.Components.Authorization;
using VanAn.CoreHub.Services;

namespace VanAn.ShopERP.Services
{
    /// <summary>
    /// CC-S6-T5: ShopERP client for Gateway Collaborator Verification admin APIs.
    /// Calls /api/admin/collaborator-verification/* with SystemAdmin Bearer JWT.
    /// </summary>
    public sealed class CollaboratorVerificationApiClient : GatewayAdminApiClientBase
    {
        public CollaboratorVerificationApiClient(
            IHttpClientFactory httpClientFactory,
            IConfiguration configuration,
            IJwtTokenService jwtTokenService,
            AuthenticationStateProvider authStateProvider,
            ILogger<CollaboratorVerificationApiClient> logger)
            : base(httpClientFactory, configuration, jwtTokenService, authStateProvider, logger) { }

        public async Task<CollaboratorVerificationSettingsDto> GetSettingsAsync(CancellationToken ct = default)
        {
            var req = await CreateRequestAsync(HttpMethod.Get, "api/admin/collaborator-verification/settings");
            return await SendAndReadAsync<CollaboratorVerificationSettingsDto>(HttpClient, req, ct) ?? new();
        }

        public async Task UpdateSettingsAsync(bool enabled, decimal feePerVerification, decimal minDeposit, CancellationToken ct = default)
        {
            var req = await CreateRequestAsync(HttpMethod.Post, "api/admin/collaborator-verification/settings", new
            {
                Enabled = enabled,
                FeePerVerification = feePerVerification,
                MinDeposit = minDeposit
            });
            var resp = await HttpClient.SendAsync(req, ct);
            resp.EnsureSuccessStatusCode();
        }
    }

    public class CollaboratorVerificationSettingsDto
    {
        public bool Enabled { get; set; }
        public decimal FeePerVerification { get; set; }
        public decimal MinDeposit { get; set; }
    }
}
