using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Components.Authorization;
using VanAn.CoreHub.Services;
using UserRole = VanAn.Shared.Domain.Aggregates.UserAggregate.UserRole;

namespace VanAn.ShopERP.Services
{
    /// <summary>
    /// #126: ShopERP client for Gateway Guard API (/api/guard/*).
    /// Mints a short-lived JWT with the current user's ACTUAL role (Guard) + tenant_id.
    /// Unlike GatewayAdminApiClientBase (which mints SystemAdmin), this preserves the user's real role
    /// so [Authorize(Roles="Guard")] on GuardController accepts the token.
    /// </summary>
    public sealed class GuardApiClient
    {
        private readonly HttpClient _httpClient;
        private readonly IJwtTokenService _jwtTokenService;
        private readonly AuthenticationStateProvider _authStateProvider;
        private readonly ILogger<GuardApiClient> _logger;

        private static readonly JsonSerializerOptions GatewayJsonOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
        };

        public GuardApiClient(
            IHttpClientFactory httpClientFactory,
            IConfiguration configuration,
            IJwtTokenService jwtTokenService,
            AuthenticationStateProvider authStateProvider,
            ILogger<GuardApiClient> logger)
        {
            _httpClient = httpClientFactory.CreateClient("GatewayClient");
            _jwtTokenService = jwtTokenService;
            _authStateProvider = authStateProvider;
            _logger = logger;
            _httpClient.BaseAddress = new Uri(configuration["Gateway:BaseUrl"] ?? "http://localhost:5001");
        }

        // === Token minting (preserves user's actual role + tenant_id) ===

        private async Task<string> MintUserTokenAsync()
        {
            var authState = await _authStateProvider.GetAuthenticationStateAsync();
            var user = authState.User;

            var userIdStr = user.FindFirst("sub")?.Value
                ?? user.FindFirst(ClaimTypes.NameIdentifier)?.Value
                ?? Guid.NewGuid().ToString();
            var userId = Guid.TryParse(userIdStr, out var id) ? id : Guid.NewGuid();

            var email = user.FindFirst("email")?.Value
                ?? user.FindFirst(ClaimTypes.Email)?.Value
                ?? "guard@vanan.vn";

            var roleStr = user.FindFirst(ClaimTypes.Role)?.Value
                ?? user.FindFirst("role")?.Value
                ?? "Guard";

            var tenantStr = user.FindFirst("tenant_id")?.Value
                ?? user.FindFirst("TenantId")?.Value
                ?? Guid.Empty.ToString();
            var tenantId = Guid.TryParse(tenantStr, out var tid) ? tid : Guid.Empty;

            // Parse role string to UserRole enum (for typed overload) or use string overload
            if (Enum.TryParse<UserRole>(roleStr, out var roleEnum))
            {
                return _jwtTokenService.GenerateToken(userId, email, roleEnum, tenantId);
            }
            return _jwtTokenService.GenerateToken(userId, email, roleStr, tenantId);
        }

        private async Task<HttpRequestMessage> CreateRequestAsync(HttpMethod method, string relativeUri, object? body = null)
        {
            var token = await MintUserTokenAsync();
            var request = new HttpRequestMessage(method, relativeUri);
            request.Headers.Authorization = new AuthenticationHeaderValue(JwtBearerDefaults.AuthenticationScheme, token);
            if (body != null)
            {
                request.Content = JsonContent.Create(body, options: GatewayJsonOptions);
            }
            return request;
        }

        private static async Task<T?> SendAndReadAsync<T>(HttpClient client, HttpRequestMessage request, CancellationToken ct = default)
        {
            var response = await client.SendAsync(request, ct);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<T>(GatewayJsonOptions, ct);
        }

        // === API methods ===

        /// <summary>#130: Get JWT token for direct browser→Gateway fetch (JS upload-photo endpoint).</summary>
        public async Task<string> GetJwtTokenAsync()
        {
            return await MintUserTokenAsync();
        }

        /// <summary>#130: Get Gateway base URL for direct browser→Gateway fetch.</summary>
        public string GatewayBaseUrl => _httpClient.BaseAddress?.ToString().TrimEnd('/') ?? string.Empty;

        public async Task<PresignUploadResultDto> PresignUploadAsync(string contentType = "image/jpeg", CancellationToken ct = default)
        {
            var req = await CreateRequestAsync(HttpMethod.Post, "api/guard/presign-upload", new { contentType });
            return await SendAndReadAsync<PresignUploadResultDto>(_httpClient, req, ct) ?? new();
        }

        public async Task<IssueResultDto> IssueAsync(IssueRequestDto req, CancellationToken ct = default)
        {
            var request = await CreateRequestAsync(HttpMethod.Post, "api/guard/issue", req);
            return await SendAndReadAsync<IssueResultDto>(_httpClient, request, ct) ?? new();
        }

        public async Task<VerifyResultDto> VerifyAsync(string qrPayload, CancellationToken ct = default)
        {
            var req = await CreateRequestAsync(HttpMethod.Post, "api/guard/verify", new { qrPayload });
            return await SendAndReadAsync<VerifyResultDto>(_httpClient, req, ct) ?? new();
        }

        public async Task<CheckoutResultDto> CheckoutAsync(Guid sessionId, CancellationToken ct = default)
        {
            var req = await CreateRequestAsync(HttpMethod.Post, $"api/guard/checkout/{sessionId}");
            return await SendAndReadAsync<CheckoutResultDto>(_httpClient, req, ct) ?? new();
        }

        public async Task<FlagResultDto> FlagAsync(Guid sessionId, string reason, CancellationToken ct = default)
        {
            var req = await CreateRequestAsync(HttpMethod.Post, $"api/guard/flag/{sessionId}", new { reason });
            return await SendAndReadAsync<FlagResultDto>(_httpClient, req, ct) ?? new();
        }

        public async Task<VoidResultDto> VoidAsync(Guid sessionId, CancellationToken ct = default)
        {
            var req = await CreateRequestAsync(HttpMethod.Post, $"api/guard/void/{sessionId}");
            return await SendAndReadAsync<VoidResultDto>(_httpClient, req, ct) ?? new();
        }

        public async Task<TodaySessionsResultDto> GetTodaySessionsAsync(string? status = null, int page = 1, int pageSize = 20, CancellationToken ct = default)
        {
            var query = $"api/guard/sessions/today?page={page}&pageSize={pageSize}";
            if (!string.IsNullOrWhiteSpace(status))
                query += $"&status={status}";
            var req = await CreateRequestAsync(HttpMethod.Get, query);
            return await SendAndReadAsync<TodaySessionsResultDto>(_httpClient, req, ct) ?? new();
        }

        public async Task<SessionDetailResultDto> GetSessionAsync(Guid sessionId, CancellationToken ct = default)
        {
            var req = await CreateRequestAsync(HttpMethod.Get, $"api/guard/sessions/{sessionId}");
            return await SendAndReadAsync<SessionDetailResultDto>(_httpClient, req, ct) ?? new();
        }
    }

    // === DTOs (match Gateway API response shapes, camelCase JSON) ===

    public class PresignUploadResultDto
    {
        public string PlatePhotoKey { get; set; } = string.Empty;
        public string PlatePhotoUploadUrl { get; set; } = string.Empty;
        public string CustomerPhotoKey { get; set; } = string.Empty;
        public string CustomerPhotoUploadUrl { get; set; } = string.Empty;
    }

    public class IssueRequestDto
    {
        public string PlateNumber { get; set; } = string.Empty;
        public string PlatePhotoKey { get; set; } = string.Empty;
        public string CustomerPhotoKey { get; set; } = string.Empty;
        public string? CustomerPhone { get; set; }
    }

    public class IssueResultDto
    {
        public Guid SessionId { get; set; }
        public string QrPayload { get; set; } = string.Empty;
        public string ShortCode { get; set; } = string.Empty;
    }

    public class VerifyResultDto
    {
        public Guid SessionId { get; set; }
        public string PlateNumber { get; set; } = string.Empty;
        public string PlatePhotoUrl { get; set; } = string.Empty;
        public string CustomerPhotoUrl { get; set; } = string.Empty;
        public DateTime IssuedAt { get; set; }
        public string Status { get; set; } = string.Empty;
        public Guid? CustomerId { get; set; }
    }

    public class CheckoutResultDto
    {
        public Guid SessionId { get; set; }
        public string Status { get; set; } = string.Empty;
        public DateTime CheckedOutAt { get; set; }
    }

    public class FlagResultDto
    {
        public Guid SessionId { get; set; }
        public string Status { get; set; } = string.Empty;
        public string FlagReason { get; set; } = string.Empty;
    }

    public class VoidResultDto
    {
        public Guid SessionId { get; set; }
        public string Status { get; set; } = string.Empty;
    }

    public class TodaySessionsResultDto
    {
        public int Total { get; set; }
        public int CheckInCount { get; set; }
        public int CheckOutCount { get; set; }
        public int InLotCount { get; set; }
        public List<SessionSummaryDto> Items { get; set; } = new();
    }

    public class SessionSummaryDto
    {
        public Guid SessionId { get; set; }
        public string PlateNumber { get; set; } = string.Empty;
        public string ShortCode { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public DateTime IssuedAt { get; set; }
        public DateTime? CheckedOutAt { get; set; }
        public Guid? CustomerId { get; set; }
    }

    public class SessionDetailResultDto
    {
        public Guid SessionId { get; set; }
        public string PlateNumber { get; set; } = string.Empty;
        public string ShortCode { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public DateTime IssuedAt { get; set; }
        public DateTime? ClaimedAt { get; set; }
        public DateTime? CheckedOutAt { get; set; }
        public string? FlagReason { get; set; }
        public Guid? CustomerId { get; set; }
        public string PlatePhotoUrl { get; set; } = string.Empty;
        public string CustomerPhotoUrl { get; set; } = string.Empty;
    }
}
