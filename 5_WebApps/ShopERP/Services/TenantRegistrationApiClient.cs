using System.Net.Http.Json;
using Microsoft.AspNetCore.Components.Authorization;
using VanAn.CoreHub.Services;

namespace VanAn.ShopERP.Services
{
    /// <summary>
    /// GTM Drill Machine W2: ShopERP client for Gateway Tenant Registration admin APIs.
    /// Calls /api/v1/tenant-registrations/* with SystemAdmin Bearer JWT.
    /// </summary>
    public sealed class TenantRegistrationApiClient : GatewayAdminApiClientBase
    {
        public TenantRegistrationApiClient(
            IHttpClientFactory httpClientFactory,
            IConfiguration configuration,
            IJwtTokenService jwtTokenService,
            AuthenticationStateProvider authStateProvider,
            ILogger<TenantRegistrationApiClient> logger)
            : base(httpClientFactory, configuration, jwtTokenService, authStateProvider, logger) { }

        public async Task<List<TenantRegistrationItem>> ListAsync(CancellationToken ct = default)
        {
            var req = await CreateRequestAsync(HttpMethod.Get, "api/v1/tenant-registrations");
            return await SendAndReadAsync<List<TenantRegistrationItem>>(HttpClient, req, ct) ?? new();
        }

        public async Task<TenantRegistrationItem?> GetAsync(Guid id, CancellationToken ct = default)
        {
            var req = await CreateRequestAsync(HttpMethod.Get, $"api/v1/tenant-registrations/{id}");
            return await SendAndReadAsync<TenantRegistrationItem>(HttpClient, req, ct);
        }

        public async Task MarkContactedAsync(Guid id, CancellationToken ct = default)
        {
            var req = await CreateRequestAsync(HttpMethod.Post, $"api/v1/tenant-registrations/{id}/contact");
            var resp = await HttpClient.SendAsync(req, ct);
            resp.EnsureSuccessStatusCode();
        }

        public async Task MarkOnboardedAsync(Guid id, Guid onboardedTenantId, CancellationToken ct = default)
        {
            var req = await CreateRequestAsync(HttpMethod.Post, $"api/v1/tenant-registrations/{id}/onboard",
                new { OnboardedTenantId = onboardedTenantId });
            var resp = await HttpClient.SendAsync(req, ct);
            resp.EnsureSuccessStatusCode();
        }

        public async Task RejectAsync(Guid id, string reason, CancellationToken ct = default)
        {
            var req = await CreateRequestAsync(HttpMethod.Post, $"api/v1/tenant-registrations/{id}/reject",
                new { Reason = reason });
            var resp = await HttpClient.SendAsync(req, ct);
            resp.EnsureSuccessStatusCode();
        }
    }

    /// <summary>DTO matching Gateway RegistrationDto (camelCase JSON).</summary>
    public class TenantRegistrationItem
    {
        public Guid Id { get; set; }
        public string ShopName { get; set; } = string.Empty;
        public string? Industry { get; set; }
        public string? LogoUrl { get; set; }
        public string ContactName { get; set; } = string.Empty;
        public string ContactPhone { get; set; } = string.Empty;
        public string? ContactEmail { get; set; }
        public string Source { get; set; } = string.Empty;
        public bool TurnstileVerified { get; set; }
        public string Status { get; set; } = string.Empty;
        public DateTime SubmittedAt { get; set; }
        public Guid? ReviewedByUserId { get; set; }
        public DateTime? ReviewedAt { get; set; }
        public string? RejectionReason { get; set; }
        public Guid? OnboardedTenantId { get; set; }
    }
}
