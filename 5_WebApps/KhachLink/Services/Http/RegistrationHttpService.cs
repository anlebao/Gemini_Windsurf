using System.Net.Http.Json;
using System.Net;

namespace VanAn.KhachLink.Services.Http;

/// <summary>
/// GTM Drill Machine W2 (2026-09-08): HTTP client for merchant registration submission.
/// KhachLink Register.razor form calls Gateway POST /api/v1/tenant-registrations.
/// Gateway endpoint is [AllowAnonymous] + rate-limited (5/24h per IP — policy "registration-submit").
/// </summary>
public class RegistrationHttpService(IHttpClientFactory httpClientFactory, ILogger<RegistrationHttpService> logger)
{
    private readonly HttpClient _httpClient = httpClientFactory.CreateClient("gateway");
    private readonly ILogger<RegistrationHttpService> _logger = logger;

    /// <summary>
    /// Submit a merchant registration. Returns success message, or error on failure.
    /// Handles 429 (rate limit) + 400 (Turnstile fail / validation) + 500 (server error).
    /// </summary>
    public async Task<RegistrationSubmitOutcome> SubmitRegistrationAsync(
        SubmitRegistrationRequestDto request,
        CancellationToken ct = default)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync(
                "api/v1/tenant-registrations", request, ct);

            if (response.StatusCode == HttpStatusCode.TooManyRequests)
            {
                _logger.LogWarning("Registration submit rate-limited for shop {ShopName}", request.ShopName);
                return RegistrationSubmitOutcome.Failed(
                    "Bạn đã gửi quá nhiều yêu cầu. Vui lòng thử lại sau 24 giờ.");
            }

            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadFromJsonAsync<ErrorBody>(cancellationToken: ct);
                _logger.LogWarning("Registration submit failed: {Status} {Error}", response.StatusCode, body?.Error);
                return RegistrationSubmitOutcome.Failed(body?.Error ?? $"Gửi yêu cầu thất bại (HTTP {response.StatusCode}).");
            }

            var result = await response.Content.ReadFromJsonAsync<RegistrationResultBody>(cancellationToken: ct);
            _logger.LogInformation("Registration submitted for shop {ShopName} — registration {RegistrationId}",
                request.ShopName, result?.RegistrationId);
            return RegistrationSubmitOutcome.Ok(result?.Message ?? "Cảm ơn! Yêu cầu đăng ký đã gửi.");
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Registration submit exception for shop {ShopName}", request.ShopName);
            return RegistrationSubmitOutcome.Failed("Lỗi kết nối khi gửi yêu cầu. Vui lòng thử lại.");
        }
    }

    // ── Local DTOs matching Gateway request/response bodies ──────────────────
    private sealed record RegistrationResultBody(Guid RegistrationId, string Message);
    private sealed record ErrorBody(string? Error);
}

/// <summary>Request DTO sent to Gateway — mirrors CoreHub SubmitRegistrationRequest.</summary>
public sealed record SubmitRegistrationRequestDto(
    string ShopName,
    string ContactName,
    string ContactPhone,
    string Source,
    string? TurnstileToken,
    string? Industry = null,
    string? LogoUrl = null,
    string? ContactEmail = null,
    string? HoneypotWebsite = null);

/// <summary>Outcome of a registration submission — Ok (with message) or Failed (with error).</summary>
public sealed record RegistrationSubmitOutcome(bool Success, string? Message, string? Error)
{
    public static RegistrationSubmitOutcome Ok(string message) => new(true, message, null);
    public static RegistrationSubmitOutcome Failed(string error) => new(false, null, error);
}
