using System.Net.Http.Json;
using System.Text.Json.Serialization;
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

        public async Task<GoogleAuthResponse> ExchangeCodeForUserInfoAsync(string code, string redirectUri)
        {
            var response = new GoogleAuthResponse();

            if (string.IsNullOrEmpty(_clientId) || string.IsNullOrEmpty(_clientSecret))
            {
                _logger.LogError("[GoogleAuth] Cannot exchange code — ClientId/ClientSecret not configured.");
                response.Error = new GoogleAuthError("config_missing", "ClientId or ClientSecret not configured");
                return response;
            }

            try
            {
                // Exchange authorization code for tokens
                var tokenRequest = new Dictionary<string, string>
                {
                    ["code"] = code,
                    ["client_id"] = _clientId,
                    ["client_secret"] = _clientSecret,
                    ["redirect_uri"] = redirectUri,
                    ["grant_type"] = "authorization_code"
                };

                _logger.LogInformation("[GoogleAuth] Exchanging code for tokens. RedirectUri={RedirectUri}", redirectUri);

                var tokenResponse = await _httpClient.PostAsync(GoogleTokenUrl, new FormUrlEncodedContent(tokenRequest));
                if (!tokenResponse.IsSuccessStatusCode)
                {
                    var errorBody = await tokenResponse.Content.ReadAsStringAsync();
                    _logger.LogError("[GoogleAuth] Token exchange failed: {Status} {Error}", tokenResponse.StatusCode, errorBody);
                    response.Error = new GoogleAuthError("token_exchange_failed", $"Status: {tokenResponse.StatusCode}, Body: {errorBody}");
                    return response;
                }

                var tokenData = await tokenResponse.Content.ReadFromJsonAsync<GoogleTokenResponse>();
                if (tokenData?.IdToken == null)
                {
                    _logger.LogError("[GoogleAuth] Token response missing id_token. Keys: {Keys}",
                        tokenData == null ? "null" : string.Join(",", GetTypeKeys(tokenData)));
                    response.Error = new GoogleAuthError("missing_id_token", "Token response did not contain id_token");
                    return response;
                }

                _logger.LogInformation("[GoogleAuth] Token exchange successful. Validating ID token...");

                // Verify the ID token with explicit audience validation
                var settings = new GoogleJsonWebSignature.ValidationSettings
                {
                    Audience = new[] { _clientId }
                };
                var payload = await GoogleJsonWebSignature.ValidateAsync(tokenData.IdToken, settings);

                _logger.LogInformation("[GoogleAuth] ID token valid. Email={Email} Name={Name} Issuer={Issuer}",
                    payload.Email, payload.Name, payload.Issuer);

                // Verify issuer
                if (payload.Issuer != GoogleIssuer)
                {
                    _logger.LogError("[GoogleAuth] Issuer mismatch: expected {Expected}, got {Actual}", GoogleIssuer, payload.Issuer);
                    response.Error = new GoogleAuthError("issuer_mismatch", $"Expected: {GoogleIssuer}, Got: {payload.Issuer}");
                    return response;
                }

                response.UserInfo = new SocialUserInfo(
                    payload.Email,
                    payload.Name ?? payload.Email,
                    payload.Picture,
                    "Google"
                );
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[GoogleAuth] Exception during ExchangeCodeForUserInfoAsync: {Message}", ex.Message);
                response.Error = new GoogleAuthError("exception", $"{ex.GetType().Name}: {ex.Message}");
                return response;
            }
        }

        private static List<string> GetTypeKeys(object obj)
        {
            return obj.GetType().GetProperties().Select(p => p.Name).ToList();
        }

        private class GoogleTokenResponse
        {
            [JsonPropertyName("access_token")]
            public string? AccessToken { get; set; }

            [JsonPropertyName("id_token")]
            public string? IdToken { get; set; }

            [JsonPropertyName("token_type")]
            public string? TokenType { get; set; }

            [JsonPropertyName("expires_in")]
            public int? ExpiresIn { get; set; }

            [JsonPropertyName("refresh_token")]
            public string? RefreshToken { get; set; }

            [JsonPropertyName("error")]
            public string? Error { get; set; }

            [JsonPropertyName("error_description")]
            public string? ErrorDescription { get; set; }
        }
    }
}
