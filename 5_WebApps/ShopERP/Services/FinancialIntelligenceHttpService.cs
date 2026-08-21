using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.Logging;
using VanAn.CoreHub.Services;
using VanAn.CoreHub.Services.FinancialIntelligence.Dtos;
using VanAn.Shared.Domain;

namespace VanAn.ShopERP.Services
{
    /// <summary>
    /// VA-FI-MVP2 Phase 3 (2026-08-21): ShopERP HTTP proxy for Financial Intelligence Gateway API.
    /// Architecture rule: ShopERP does NOT inject IVanAnDbContext for MVP-2 (accounting source of truth
    /// is Gateway PG). All Financial Intelligence calls route through this proxy.
    /// Precedent: NetworkDashboardHttpService (VALCN v2.0 Phase 7), OcrConfigApiClient (OCR Hub S2).
    ///
    /// Auth: mints short-lived SystemAdmin JWT for the current ShopERP user (same pattern as
    /// GatewayAdminApiClientBase). Gateway validates JWT + extracts tenant_id claim.
    ///
    /// Graceful degradation: Gateway unreachable / 5xx → returns null (UI shows "Chưa có dữ liệu").
    /// </summary>
    public sealed class FinancialIntelligenceHttpService : GatewayAdminApiClientBase
    {
        private readonly ILogger<FinancialIntelligenceHttpService> _logger;

        public FinancialIntelligenceHttpService(
            IHttpClientFactory httpClientFactory,
            IConfiguration configuration,
            IJwtTokenService jwtTokenService,
            AuthenticationStateProvider authStateProvider,
            ILogger<FinancialIntelligenceHttpService> logger)
            : base(httpClientFactory, configuration, jwtTokenService, authStateProvider, logger)
        {
            _logger = logger;
        }

        // ── BusinessProfile ─────────────────────────────────────────────────────

        /// <summary>GET /api/financial/business-profile — returns null if 404 (profile not yet declared).</summary>
        public async Task<BusinessProfileDto?> GetBusinessProfileAsync(CancellationToken ct = default)
        {
            return await GetAsync<BusinessProfileDto>("api/financial/business-profile", ct).ConfigureAwait(false);
        }

        /// <summary>PUT /api/financial/business-profile (upsert).</summary>
        public async Task<BusinessProfileDto?> UpdateBusinessProfileAsync(UpdateBusinessProfileDto dto, CancellationToken ct = default)
        {
            try
            {
                HttpRequestMessage request = await CreateRequestAsync(HttpMethod.Put, "api/financial/business-profile", dto).ConfigureAwait(false);
                HttpResponseMessage response = await HttpClient.SendAsync(request, ct).ConfigureAwait(false);
                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError("FinancialIntelligence: UpdateBusinessProfile returned {Status}", response.StatusCode);
                    return null;
                }
                return await response.Content.ReadFromJsonAsync<BusinessProfileDto>(GatewayJsonOptions, ct).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "FinancialIntelligence: UpdateBusinessProfile failed");
                return null;
            }
        }

        // ── Calculation endpoints ───────────────────────────────────────────────

        public async Task<ProfitSummaryDto?> GetProfitSummaryAsync(AccountingPeriod period, AccountingStandard standard = AccountingStandard.TT99_2025, CancellationToken ct = default)
            => await GetAsync<ProfitSummaryDto>($"api/financial/profit-summary?period={PeriodQuery(period)}&standard={standard}", ct).ConfigureAwait(false);

        public async Task<BreakEvenAnalysisDto?> GetBreakEvenAsync(AccountingPeriod period, AccountingStandard standard = AccountingStandard.TT99_2025, CancellationToken ct = default)
            => await GetAsync<BreakEvenAnalysisDto>($"api/financial/break-even?period={PeriodQuery(period)}&standard={standard}", ct).ConfigureAwait(false);

        public async Task<MultiProductBreakEvenDto?> GetMultiProductBreakEvenAsync(AccountingPeriod period, AccountingStandard standard = AccountingStandard.TT99_2025, CancellationToken ct = default)
            => await GetAsync<MultiProductBreakEvenDto>($"api/financial/break-even/multi-product?period={PeriodQuery(period)}&standard={standard}", ct).ConfigureAwait(false);

        public async Task<UnitEconomicsReportDto?> GetUnitEconomicsAsync(AccountingPeriod period, CancellationToken ct = default)
            => await GetAsync<UnitEconomicsReportDto>($"api/financial/unit-economics?period={PeriodQuery(period)}", ct).ConfigureAwait(false);

        public async Task<TargetProfitAnalysisDto?> AnalyzeTargetProfitAsync(AccountingPeriod period, AccountingStandard standard, decimal targetProfit, CancellationToken ct = default)
        {
            try
            {
                var body = new TargetProfitRequestDto(period.Year, period.Month, standard, targetProfit);
                HttpRequestMessage request = await CreateRequestAsync(HttpMethod.Post, "api/financial/target-profit", body).ConfigureAwait(false);
                HttpResponseMessage response = await HttpClient.SendAsync(request, ct).ConfigureAwait(false);
                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError("FinancialIntelligence: AnalyzeTargetProfit returned {Status}", response.StatusCode);
                    return null;
                }
                return await response.Content.ReadFromJsonAsync<TargetProfitAnalysisDto>(GatewayJsonOptions, ct).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "FinancialIntelligence: AnalyzeTargetProfit failed");
                return null;
            }
        }

        // ── Helpers ─────────────────────────────────────────────────────────────

        /// <summary>GET helper — mints SystemAdmin JWT, sends request, returns null on any failure (graceful degradation).</summary>
        private async Task<T?> GetAsync<T>(string relativeUri, CancellationToken ct) where T : class
        {
            try
            {
                HttpRequestMessage request = await CreateRequestAsync(HttpMethod.Get, relativeUri).ConfigureAwait(false);
                HttpResponseMessage response = await HttpClient.SendAsync(request, ct).ConfigureAwait(false);
                if (!response.IsSuccessStatusCode)
                {
                    // 404 is expected for missing BusinessProfile — log only at debug to avoid noise.
                    if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                        _logger.LogDebug("FinancialIntelligence: {Uri} returned 404 (resource not declared)", relativeUri);
                    else
                        _logger.LogWarning("FinancialIntelligence: {Uri} returned {Status}", relativeUri, response.StatusCode);
                    return null;
                }
                return await response.Content.ReadFromJsonAsync<T>(GatewayJsonOptions, ct).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "FinancialIntelligence: GET {Uri} failed", relativeUri);
                return null;
            }
        }

        private static string PeriodQuery(AccountingPeriod period) => $"{period.Year:D4}-{period.Month:D2}";
    }
}
