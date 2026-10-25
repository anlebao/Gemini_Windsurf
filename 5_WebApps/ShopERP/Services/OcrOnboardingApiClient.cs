using System.Net.Http.Json;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Components.Authorization;
using VanAn.CoreHub.Services;
using VanAn.CoreHub.Services.Onboarding;

namespace VanAn.ShopERP.Services;

/// <summary>
/// OCR Onboarding S2/S4 (2026-10-25): ShopERP client cho Gateway OCR-onboarding API.
/// Mint SystemAdmin JWT (pattern TenantOnboardingApiClient) → POST /api/v1/ocr-onboarding
/// (Gateway PG — source of truth). Credentials trả về HIỆN 1 LẦN.
/// </summary>
public sealed class OcrOnboardingApiClient
{
    private readonly HttpClient _httpClient;
    private readonly IJwtTokenService _jwtTokenService;
    private readonly AuthenticationStateProvider _authStateProvider;
    private readonly ILogger<OcrOnboardingApiClient> _logger;

    public OcrOnboardingApiClient(
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        IJwtTokenService jwtTokenService,
        AuthenticationStateProvider authStateProvider,
        ILogger<OcrOnboardingApiClient> logger)
    {
        _httpClient = httpClientFactory.CreateClient("GatewayClient");
        _jwtTokenService = jwtTokenService;
        _authStateProvider = authStateProvider;
        _logger = logger;

        string baseUrl = configuration["Gateway:BaseUrl"] ?? "http://localhost:5001";
        _httpClient.BaseAddress = new Uri(baseUrl);
    }

    /// <summary>Gửi request OCR onboarding (field đã review trên wizard) → Gateway tạo tenant Pending→Verify.</summary>
    public async Task<OcrOnboardingResult> ExecuteAsync(
        OcrOnboardingRequest request,
        CancellationToken ct = default)
    {
        string token = await MintSystemAdminTokenAsync();
        using var requestMessage = new HttpRequestMessage(HttpMethod.Post, "api/v1/ocr-onboarding")
        {
            Content = JsonContent.Create(request, options: new System.Text.Json.JsonSerializerOptions
            {
                PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase
            })
        };
        requestMessage.Headers.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue(JwtBearerDefaults.AuthenticationScheme, token);

        _logger.LogInformation("Gọi Gateway OCR Onboarding: {Case} — {Name}", request.Case, request.Name);

        HttpResponseMessage response = await _httpClient.SendAsync(requestMessage, ct);
        if (response.IsSuccessStatusCode)
        {
            OcrOnboardingResult? result = await response.Content.ReadFromJsonAsync<OcrOnboardingResult>(ct);
            return result ?? throw new InvalidOperationException("Gateway trả response rỗng.");
        }

        string errorBody = await response.Content.ReadAsStringAsync(ct);
        if (response.StatusCode == System.Net.HttpStatusCode.BadRequest)
            throw new InvalidOperationException($"Dữ liệu không hợp lệ: {errorBody}");
        throw new InvalidOperationException($"Gateway lỗi {(int)response.StatusCode}: {errorBody}");
    }

    /// <summary>Mint SystemAdmin JWT — dùng chung cho cả Gateway API lẫn ShopERP local API (product import).</summary>
    public async Task<string> MintSystemAdminTokenAsync()
    {
        AuthenticationState authState = await _authStateProvider.GetAuthenticationStateAsync();
        ClaimsPrincipal user = authState.User;

        string userId = user.FindFirst("sub")?.Value
            ?? user.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? Guid.NewGuid().ToString();

        string email = user.FindFirst("email")?.Value
            ?? user.FindFirst(ClaimTypes.Email)?.Value
            ?? "systemadmin@vanan.vn";

        return _jwtTokenService.GenerateToken(
            Guid.TryParse(userId, out Guid id) ? id : Guid.NewGuid(),
            email,
            "SystemAdmin",
            Guid.Empty);
    }
}