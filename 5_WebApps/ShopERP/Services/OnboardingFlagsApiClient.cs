using System.Net.Http.Json;
using Microsoft.AspNetCore.Components.Authorization;
using VanAn.CoreHub.Services;
using VanAn.CoreHub.Services.Onboarding;

namespace VanAn.ShopERP.Services;

/// <summary>
/// Self-Serve Onboarding P2 (2026-11-09, SRS FR-22): ShopERP client cho
/// GET/PUT /api/v1/platform/onboarding-flags (Gateway). GET public nhưng PUT
/// yêu cầu SystemAdmin — dùng GatewayAdminApiClientBase mint JWT cho cả hai.
/// Dùng bởi OnboardingFlagsCard + QrSignupCard trên /admin/onboarding-ocr.
/// </summary>
public sealed class OnboardingFlagsApiClient : GatewayAdminApiClientBase
{
    private readonly ILogger<OnboardingFlagsApiClient> _logger;

    public OnboardingFlagsApiClient(
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        IJwtTokenService jwtTokenService,
        AuthenticationStateProvider authStateProvider,
        ILogger<OnboardingFlagsApiClient> logger)
        : base(httpClientFactory, configuration, jwtTokenService, authStateProvider, logger)
    {
        _logger = logger;
    }

    /// <summary>GET flags — trả null khi Gateway lỗi (UI fail-safe: ẩn card).</summary>
    public async Task<OnboardingFlagsDto?> GetFlagsAsync(CancellationToken ct = default)
    {
        try
        {
            var req = await CreateRequestAsync(HttpMethod.Get, "api/v1/platform/onboarding-flags");
            var resp = await HttpClient.SendAsync(req, ct);
            if (!resp.IsSuccessStatusCode) return null;
            return await resp.Content.ReadFromJsonAsync<OnboardingFlagsDto>(GatewayJsonOptions, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "GetFlagsAsync failed — onboarding flags unavailable");
            return null;
        }
    }

    /// <summary>PUT flags (full state) — SystemAdmin only.</summary>
    public async Task<OnboardingFlagsDto> SetFlagsAsync(UpdateOnboardingFlagsRequest flags, CancellationToken ct = default)
    {
        var req = await CreateRequestAsync(HttpMethod.Put, "api/v1/platform/onboarding-flags", flags);
        return await SendAndReadAsync<OnboardingFlagsDto>(HttpClient, req, ct)
            ?? throw new InvalidOperationException("Onboarding flags PUT returned empty response.");
    }
}
