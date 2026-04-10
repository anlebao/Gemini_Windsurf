using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Configuration;

namespace VanAn.HRApp.Application
{
    public class LoginResponse
    {
        public string Token { get; set; } = string.Empty;
        public string TenantId { get; set; } = string.Empty;
        public string UserId { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty;
    }

    public class AuthService
    {
        private readonly HttpClient _http;
        private readonly IConfiguration _config;
        private readonly string _baseUrl;
        private readonly string _loginEndpoint;

        public AuthService(HttpClient http, IConfiguration config)
        {
            _http = http;
            _config = config;
            _baseUrl = _config["CoreHub:BaseUrl"] ?? "http://localhost:5010";
            _loginEndpoint = _config["CoreHub:Endpoints:Auth"] ?? "/api/auth/login";
            
            _http.BaseAddress = new Uri(_baseUrl);
            _http.Timeout = TimeSpan.FromSeconds(_config.GetValue<int>("AppSettings:TimeoutSeconds", 30));
        }

        public async Task<LoginResponse?> Login(string username, string password)
        {
            try
            {
                var response = await _http.PostAsJsonAsync(_loginEndpoint, new
                {
                    username,
                    password
                });

                if (!response.IsSuccessStatusCode)
                {
                    return null;
                }

                var json = await response.Content.ReadAsStringAsync();
                var data = JsonSerializer.Deserialize<LoginResponse>(json);
                return data;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Login error: {ex.Message}");
                return null;
            }
        }

        public void SetAuthToken(string token)
        {
            _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        public void ClearAuth()
        {
            _http.DefaultRequestHeaders.Authorization = null;
        }

        public async Task<bool> TestConnectionAsync()
        {
            try
            {
                var healthEndpoint = _config["CoreHub:Endpoints:Health"] ?? "/health";
                var response = await _http.GetAsync(healthEndpoint);
                return response.IsSuccessStatusCode;
            }
            catch
            {
                return false;
            }
        }
    }
}
