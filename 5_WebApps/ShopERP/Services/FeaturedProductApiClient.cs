using System.Net.Http.Json;
using Microsoft.AspNetCore.Components.Authorization;
using VanAn.CoreHub.Services;

namespace VanAn.ShopERP.Services
{
    /// <summary>
    /// Phase 6: ShopERP client for the Gateway FeaturedProducts admin API.
    /// Calls /api/v1/featured-products with SystemAdmin Bearer JWT.
    /// </summary>
    public sealed class FeaturedProductApiClient : GatewayAdminApiClientBase
    {
        public FeaturedProductApiClient(
            IHttpClientFactory httpClientFactory,
            IConfiguration configuration,
            IJwtTokenService jwtTokenService,
            AuthenticationStateProvider authStateProvider,
            ILogger<FeaturedProductApiClient> logger)
            : base(httpClientFactory, configuration, jwtTokenService, authStateProvider, logger) { }

        public async Task<List<FeaturedProductDto>> ListAsync(Guid? tenantId = null, CancellationToken ct = default)
        {
            string url = "api/v1/featured-products";
            if (tenantId.HasValue && tenantId.Value != Guid.Empty)
                url += $"?tenantId={tenantId.Value}";

            var req = await CreateRequestAsync(HttpMethod.Get, url);
            return await SendAndReadAsync<List<FeaturedProductDto>>(HttpClient, req, ct) ?? new();
        }

        public async Task<FeaturedProductDto> CreateAsync(CreateFeaturedProductRequest request, CancellationToken ct = default)
        {
            var req = await CreateRequestAsync(HttpMethod.Post, "api/v1/featured-products", request);
            return await SendAndReadAsync<FeaturedProductDto>(HttpClient, req, ct)
                ?? throw new InvalidOperationException("Gateway returned an empty FeaturedProduct response.");
        }

        public async Task<FeaturedProductDto?> UpdateAsync(Guid id, UpdateFeaturedProductRequest request, CancellationToken ct = default)
        {
            var req = await CreateRequestAsync(HttpMethod.Put, $"api/v1/featured-products/{id}", request);
            return await SendAndReadAsync<FeaturedProductDto>(HttpClient, req, ct);
        }

        public async Task DeleteAsync(Guid id, CancellationToken ct = default)
        {
            var req = await CreateRequestAsync(HttpMethod.Delete, $"api/v1/featured-products/{id}");
            HttpResponseMessage response = await HttpClient.SendAsync(req, ct);
            response.EnsureSuccessStatusCode();
        }
    }

    // DTOs mirror Gateway FeaturedProductsController DTOs (kept here to avoid Gateway→ShopERP DTO dependency)
    public record FeaturedProductDto
    {
        public Guid Id { get; set; }
        public Guid ProductId { get; set; }
        public Guid TenantId { get; set; }
        public string DisplayName { get; set; } = "";
        public string? DisplayDescription { get; set; }
        public string? ImageUrl { get; set; }
        public decimal DisplayPrice { get; set; }
        public decimal VatRate { get; set; }
        public bool IsActive { get; set; }
        public int SortOrder { get; set; }
        public DateTime FeaturedAt { get; set; }
    }

    public record CreateFeaturedProductRequest
    {
        public Guid ProductId { get; set; }
        public Guid TenantId { get; set; }
        public string DisplayName { get; set; } = "";
        public decimal DisplayPrice { get; set; }
        public decimal VatRate { get; set; } = 0.10m;
        public string? DisplayDescription { get; set; }
        public string? ImageUrl { get; set; }
        public int SortOrder { get; set; }
    }

    public record UpdateFeaturedProductRequest
    {
        public string DisplayName { get; set; } = "";
        public decimal DisplayPrice { get; set; }
        public decimal VatRate { get; set; } = 0.10m;
        public string? DisplayDescription { get; set; }
        public string? ImageUrl { get; set; }
        public int SortOrder { get; set; }
        public bool? IsActive { get; set; }
    }
}
