using System.Net.Http;
using Microsoft.AspNetCore.Components.Authorization;
using VanAn.CoreHub.Services;

namespace VanAn.ShopERP.Services
{
    /// <summary>Contract for business-info lookup by MST (phiếu thu/phiếu chi).</summary>
    public interface IBusinessInfoApiClient
    {
        /// <summary>Lookup business info by MST. Returns null when not found (404).</summary>
        Task<BusinessInfoDto?> GetByMstAsync(string mst, CancellationToken ct = default);
    }

    /// <summary>
    /// 2026-10-01: ShopERP client for the Gateway business-info lookup API (phiếu thu/phiếu chi).
    /// Calls GET /api/v1/business-info/{mst} with a minted SystemAdmin JWT.
    /// Gateway resolves local-first (PG tenants) → doanhnghiep.vn fallback (key stays server-side).
    /// </summary>
    public sealed class BusinessInfoApiClient : GatewayAdminApiClientBase, IBusinessInfoApiClient
    {
        public BusinessInfoApiClient(
            IHttpClientFactory httpClientFactory,
            IConfiguration configuration,
            IJwtTokenService jwtTokenService,
            AuthenticationStateProvider authStateProvider,
            ILogger<BusinessInfoApiClient> logger)
            : base(httpClientFactory, configuration, jwtTokenService, authStateProvider, logger) { }

        /// <inheritdoc />
        public async Task<BusinessInfoDto?> GetByMstAsync(string mst, CancellationToken ct = default)
        {
            var req = await CreateRequestAsync(HttpMethod.Get, $"api/v1/business-info/{Uri.EscapeDataString(mst)}");
            try
            {
                return await SendAndReadAsync<BusinessInfoDto>(HttpClient, req, ct);
            }
            catch (InvalidOperationException ex) when (ex.Message.Contains("404"))
            {
                return null;
            }
        }
    }

    /// <summary>Mirror of Gateway BusinessLookupResult — kept here (no Gateway→ShopERP DTO dependency).</summary>
    public record BusinessInfoDto
    {
        public string TaxCode { get; init; } = "";
        public string BusinessName { get; init; } = "";
        public string? Address { get; init; }
        public string? Status { get; init; }
        public string? LegalRepName { get; init; }
        public string? IndustryName { get; init; }
        public string? ProvinceName { get; init; }
        public string Source { get; init; } = "";
    }
}
