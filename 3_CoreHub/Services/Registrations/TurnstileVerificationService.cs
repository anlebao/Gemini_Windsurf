using System.Net.Http.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace VanAn.CoreHub.Services.Registrations
{
    /// <summary>
    /// GTM Drill Machine W2 (2026-09-08): Cloudflare Turnstile server-side verification.
    /// Verifies the Turnstile token returned by the client widget via Cloudflare API.
    /// If Turnstile:SecretKey not configured (dev/test) → skip verification (log warning).
    /// </summary>
    public interface ITurnstileVerificationService
    {
        Task<bool> VerifyAsync(string? token, string? remoteIp, CancellationToken ct = default);
    }

    public class TurnstileVerificationService(
        IConfiguration configuration,
        IHttpClientFactory httpClientFactory,
        ILogger<TurnstileVerificationService> logger) : ITurnstileVerificationService
    {
        private readonly HttpClient _httpClient = httpClientFactory.CreateClient("Turnstile");
        private readonly ILogger<TurnstileVerificationService> _logger = logger;

        public async Task<bool> VerifyAsync(string? token, string? remoteIp, CancellationToken ct = default)
        {
            var secretKey = configuration["Turnstile:SecretKey"];

            // Dev fallback: skip verification if not configured
            if (string.IsNullOrEmpty(secretKey))
            {
                _logger.LogWarning("Turnstile:SecretKey not configured — skipping verification (dev mode). Production MUST have keys.");
                return true;
            }

            if (string.IsNullOrEmpty(token))
            {
                _logger.LogWarning("Turnstile token is empty — verification failed.");
                return false;
            }

            try
            {
                var payload = new Dictionary<string, string?>
                {
                    ["secret"] = secretKey,
                    ["response"] = token
                };
                if (!string.IsNullOrEmpty(remoteIp))
                    payload["remoteip"] = remoteIp;

                var response = await _httpClient.PostAsync(
                    "https://challenges.cloudflare.com/turnstile/v0/siteverify",
                    new FormUrlEncodedContent(payload), ct);

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("Turnstile API returned {Status}", response.StatusCode);
                    return false;
                }

                var result = await response.Content.ReadFromJsonAsync<TurnstileResponse>(cancellationToken: ct);
                if (result is null)
                {
                    _logger.LogWarning("Turnstile API returned null response");
                    return false;
                }

                if (!result.Success)
                {
                    _logger.LogWarning("Turnstile verification failed — errors: {Errors}",
                        result.ErrorCodes is not null ? string.Join(", ", result.ErrorCodes) : "(none)");
                }

                return result.Success;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Turnstile verification exception");
                return false;
            }
        }

        private sealed record TurnstileResponse(bool Success, string[]? ErrorCodes);
    }
}
