using System.Net.Http.Json;
using Google.Apis.Auth;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace VanAn.CoreHub.Services
{
    public class GoogleAuthService : IGoogleAuthService
    {
        private readonly HttpClient _httpClient;
        private readonly string _clientId;
        private readonly string _clientSecret;
        private readonly ILogger<GoogleAuthService> _logger;

        private const string GoogleAuthUrl = "https://accounts.google.com/o/oauth2/v2/auth";
        private const string GoogleTokenUrl = "https://oauth2.googleapis.com/token";
        private const string GoogleIssuer = "https://accounts.google.com";

        public GoogleAuthService(HttpClient httpClient, IConfiguration configuration, ILogger<GoogleAuthService> logger)
        {
            _httpClient = httpClient;
            _clientId = configuration["Google:ClientId"] ?? string.Empty;
            _clientSecret = configuration["Google:ClientSecret"] ?? string.Empty;
            _logger = logger;

            if (string.IsNullOrEmpty(_clientId) || string.IsNullOrEmpty(_clientSecret))
                _logger.LogWarning("[GoogleAuth] ClientId or ClientSecret not configured. Google OAuth will not work.");
        }

        public string GetAuthorizationUrl(string redirectUri, string? state = null)
        {
            var scopes = "openid email profile";
            var url = $"{GoogleAuthUrl}?client_id={Uri.EscapeDataString(_clientId)}" +
                      $"&redirect_uri={Uri.EscapeDataString(redirectUri)}" +
                      $"&response_type=code" +
                      $"&scope={Uri.EscapeDataString(scopes)}" +
                      $"&prompt=consent";
            if (!string.IsNullOrEmpty(state))
                url += $"&state={Uri.EscapeDataString(state)}";
            return url;
        }

        public async Task<SocialUserInfo?> ExchangeCodeForUserInfoAsync(string code, string redirectUri)
        {
            if (string.IsNullOrEmpty(_clientId) || string.IsNullOrEmpty(_clientSecret))
            {
                _logger.LogError("[GoogleAuth] Cannot exchange code — ClientId/ClientSecret not configured.");
                return null;
            }

            // Exchange authorization code for tokens
            var tokenRequest = new Dictionary<string, string>
            {
                ["code"] = code,
                ["client_id"] = _clientId,
                ["client_secret"] = _clientSecret,
                ["redirect_uri"] = redirectUri,
                ["grant_type"] = "authorization_code"
            };

            var tokenResponse = await _httpClient.PostAsync(GoogleTokenUrl, new FormUrlEncodedContent(tokenRequest));
            if (!tokenResponse.IsSuccessStatusCode)
            {
                var errorBody = await tokenResponse.Content.ReadAsStringAsync();
                _logger.LogError("[GoogleAuth] Token exchange failed: {Status} {Error}", tokenResponse.StatusCode, errorBody);
                return null;
            }

            var tokenData = await tokenResponse.Content.ReadFromJsonAsync<GoogleTokenResponse>();
            if (tokenData?.IdToken == null)
            {
                _logger.LogError("[GoogleAuth] Token response missing id_token.");
                return null;
            }

            // Verify the ID token
            var payload = await GoogleJsonWebSignature.ValidateAsync(tokenData.IdToken);
            if (payload == null)
            {
                _logger.LogError("[GoogleAuth] ID token validation failed.");
                return null;
            }

            // Verify audience
            if (payload.Audience != _clientId)
            {
                _logger.LogError("[GoogleAuth] Audience mismatch: expected {Expected}, got {Actual}", _clientId, payload.Audience);
                return null;
            }

            // Verify issuer
            if (payload.Issuer != GoogleIssuer)
            {
                _logger.LogError("[GoogleAuth] Issuer mismatch: expected {Expected}, got {Actual}", GoogleIssuer, payload.Issuer);
                return null;
            }

            return new SocialUserInfo(
                payload.Email,
                payload.Name ?? payload.Email,
                payload.Picture,
                "Google"
            );
        }

        private class GoogleTokenResponse
        {
            public string? AccessToken { get; set; }
            public string? IdToken { get; set; }
            public string? TokenType { get; set; }
            public int? ExpiresIn { get; set; }
            public string? RefreshToken { get; set; }
        }
    }
}
