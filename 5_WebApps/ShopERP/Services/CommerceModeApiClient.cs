using System.Net.Http.Json;
using Microsoft.AspNetCore.Components.Authorization;
using VanAn.CoreHub.Services;

namespace VanAn.ShopERP.Services
{
    /// <summary>
    /// Sprint 7: ShopERP client for Gateway Commerce Mode admin APIs.
    /// Calls /api/admin/commerce-mode/* with SystemAdmin Bearer JWT.
    /// </summary>
    public sealed class CommerceModeApiClient : GatewayAdminApiClientBase
    {
        public CommerceModeApiClient(
            IHttpClientFactory httpClientFactory,
            IConfiguration configuration,
            IJwtTokenService jwtTokenService,
            AuthenticationStateProvider authStateProvider,
            ILogger<CommerceModeApiClient> logger)
            : base(httpClientFactory, configuration, jwtTokenService, authStateProvider, logger) { }

        public async Task<CommerceModeSettingsDto> GetSettingsAsync(CancellationToken ct = default)
        {
            var req = await CreateRequestAsync(HttpMethod.Get, "api/admin/commerce-mode");
            return await SendAndReadAsync<CommerceModeSettingsDto>(HttpClient, req, ct) ?? new();
        }

        public async Task SetGlobalModeAsync(string mode, decimal platformFeeRate, decimal communityFundRate, decimal deliveryFee, CancellationToken ct = default)
        {
            var req = await CreateRequestAsync(HttpMethod.Post, "api/admin/commerce-mode/global", new
            {
                Mode = mode,
                PlatformFeeRate = platformFeeRate,
                CommunityFundRate = communityFundRate,
                DeliveryFee = deliveryFee
            });
            var resp = await HttpClient.SendAsync(req, ct);
            resp.EnsureSuccessStatusCode();
        }

        public async Task SetTenantOverrideAsync(Guid tenantId, string overrideMode, CancellationToken ct = default)
        {
            var req = await CreateRequestAsync(HttpMethod.Post, $"api/admin/commerce-mode/tenant/{tenantId}", new { OverrideMode = overrideMode });
            var resp = await HttpClient.SendAsync(req, ct);
            resp.EnsureSuccessStatusCode();
        }

        public async Task<string> ResolveModeAsync(Guid tenantId, CancellationToken ct = default)
        {
            var req = await CreateRequestAsync(HttpMethod.Get, $"api/admin/commerce-mode/resolve/{tenantId}");
            var result = await SendAndReadAsync<ResolveModeResult>(HttpClient, req, ct);
            return result?.ResolvedMode ?? "Marketplace";
        }

        /// <summary>
        /// Sprint 7 Q5: Confirm external payment (non-COD Reseller — VietQR/card).
        /// Creates 5-split: ExternalPayment + Settlement + DeliveryFee + Commission + PlatformFee + CommunityFund.
        /// </summary>
        public async Task<ConfirmExternalPaymentResult> ConfirmExternalPaymentAsync(
            Guid orderId, decimal amount, string paymentRef, CancellationToken ct = default)
        {
            var req = await CreateRequestAsync(HttpMethod.Post, "api/admin/commerce-mode/confirm-external-payment",
                new { OrderId = orderId, Amount = amount, PaymentRef = paymentRef });
            var resp = await HttpClient.SendAsync(req, ct);

            if (!resp.IsSuccessStatusCode)
            {
                var errorBody = await resp.Content.ReadAsStringAsync(ct);
                throw new HttpRequestException($"Confirm external payment failed ({resp.StatusCode}): {errorBody}");
            }

            return await resp.Content.ReadFromJsonAsync<ConfirmExternalPaymentResult>(GatewayJsonOptions, ct)
                   ?? new ConfirmExternalPaymentResult();
        }
    }

    // DTOs matching Gateway response shapes
    public class CommerceModeSettingsDto
    {
        public string GlobalMode { get; set; } = "Marketplace";
        public decimal DefaultPlatformFeeRate { get; set; }
        public decimal DefaultCommunityFundRate { get; set; }
        public decimal DefaultDeliveryFee { get; set; }
        public List<TenantOverrideItem> TenantOverrides { get; set; } = new();
    }

    public class TenantOverrideItem
    {
        public Guid TenantId { get; set; }
        public string TenantName { get; set; } = string.Empty;
        public string Override { get; set; } = "Inherit";
        public string ResolvedMode { get; set; } = "Marketplace";
    }

    public class ResolveModeResult
    {
        public Guid TenantId { get; set; }
        public string ResolvedMode { get; set; } = "Marketplace";
    }

    public class ConfirmExternalPaymentResult
    {
        public Guid TransactionId { get; set; }
        public decimal BalanceAfter { get; set; }
    }
}
