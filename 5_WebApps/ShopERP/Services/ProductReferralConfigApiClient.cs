using System.Net.Http.Json;
using Microsoft.AspNetCore.Components.Authorization;
using VanAn.CoreHub.Services;

namespace VanAn.ShopERP.Services
{
    /// <summary>
    /// CC-S4 (Sprint 4): ShopERP client for the Gateway ProductReferralConfig admin API.
    /// Calls /api/admin/products/{productId}/referral-config with SystemAdmin Bearer JWT.
    /// </summary>
    public sealed class ProductReferralConfigApiClient : GatewayAdminApiClientBase
    {
        public ProductReferralConfigApiClient(
            IHttpClientFactory httpClientFactory,
            IConfiguration configuration,
            IJwtTokenService jwtTokenService,
            AuthenticationStateProvider authStateProvider,
            ILogger<ProductReferralConfigApiClient> logger)
            : base(httpClientFactory, configuration, jwtTokenService, authStateProvider, logger) { }

        public async Task<List<ProductReferralConfigDto>> ListAllAsync(CancellationToken ct = default)
        {
            var req = await CreateRequestAsync(HttpMethod.Get, "api/admin/products/referral-configs");
            return await SendAndReadAsync<List<ProductReferralConfigDto>>(HttpClient, req, ct) ?? new();
        }

        public async Task<ProductReferralConfigDto?> GetByProductIdAsync(Guid productId, CancellationToken ct = default)
        {
            var req = await CreateRequestAsync(HttpMethod.Get, $"api/admin/products/{productId}/referral-config");
            return await SendAndReadAsync<ProductReferralConfigDto>(HttpClient, req, ct);
        }

        public async Task<ProductReferralConfigDto> CreateAsync(Guid productId, CreateProductReferralConfigRequest request, CancellationToken ct = default)
        {
            var req = await CreateRequestAsync(HttpMethod.Post, $"api/admin/products/{productId}/referral-config", request);
            return await SendAndReadAsync<ProductReferralConfigDto>(HttpClient, req, ct)
                ?? throw new InvalidOperationException("Gateway returned an empty ProductReferralConfig response.");
        }

        public async Task<ProductReferralConfigDto?> UpdateAsync(Guid productId, UpdateProductReferralConfigRequest request, CancellationToken ct = default)
        {
            var req = await CreateRequestAsync(HttpMethod.Put, $"api/admin/products/{productId}/referral-config", request);
            return await SendAndReadAsync<ProductReferralConfigDto>(HttpClient, req, ct);
        }

        public async Task DeleteAsync(Guid productId, CancellationToken ct = default)
        {
            var req = await CreateRequestAsync(HttpMethod.Delete, $"api/admin/products/{productId}/referral-config");
            var resp = await HttpClient.SendAsync(req, ct);
            resp.EnsureSuccessStatusCode();
        }
    }

    public class ProductReferralConfigDto
    {
        public Guid Id { get; set; }
        public Guid ProductId { get; set; }
        public string? ProductShortCode { get; set; }
        public decimal CommissionRate { get; set; }
        public decimal AppInstallBonus { get; set; }
        public bool IsActive { get; set; }
    }

    public class CreateProductReferralConfigRequest
    {
        public decimal CommissionRate { get; set; }
        public decimal AppInstallBonus { get; set; }
        public string? ProductShortCode { get; set; }
    }

    public class UpdateProductReferralConfigRequest
    {
        public decimal CommissionRate { get; set; }
        public decimal AppInstallBonus { get; set; }
        public string? ProductShortCode { get; set; }
        public bool IsActive { get; set; } = true;
    }
}
