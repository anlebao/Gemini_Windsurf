using Microsoft.AspNetCore.Components.Authorization;
using VanAn.CoreHub.Services;
using VanAn.CoreHub.Services.Reconcile;

namespace VanAn.ShopERP.Services
{
    /// <summary>
    /// Tra soát số liệu kế toán P4 (SRS v1.1): SystemAdmin client cho Gateway
    /// GET /api/admin/accounting/reconcile?tenantId=&period= — SystemAdmin JWT
    /// (pattern GatewayAdminApiClientBase).
    /// </summary>
    public sealed class ReconcileAdminApiClient : GatewayAdminApiClientBase
    {
        public ReconcileAdminApiClient(
            IHttpClientFactory httpClientFactory,
            IConfiguration configuration,
            IJwtTokenService jwtTokenService,
            AuthenticationStateProvider authStateProvider,
            ILogger<ReconcileAdminApiClient> logger)
            : base(httpClientFactory, configuration, jwtTokenService, authStateProvider, logger)
        {
        }

        /// <summary>GET /api/admin/accounting/reconcile — chạy R1-R12 cho tenant+kỳ.</summary>
        public async Task<ReconcileResult> RunAsync(Guid tenantId, string period, CancellationToken ct = default)
        {
            using HttpRequestMessage request = await CreateRequestAsync(
                HttpMethod.Get, $"api/admin/accounting/reconcile?tenantId={tenantId}&period={Uri.EscapeDataString(period)}");
            ReconcileResult? result = await SendAndReadAsync<ReconcileResult>(HttpClient, request, ct);
            return result ?? throw new InvalidOperationException("Gateway returned empty reconcile result");
        }
    }
}
