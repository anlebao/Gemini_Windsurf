using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Components.Authorization;
using VanAn.CoreHub.Services;

namespace VanAn.ShopERP.Services;

/// <summary>
/// Notification Hub (2026-10-15): ShopERP client cho Gateway API thông báo owner
/// (pattern ShopInboxApiClient — mint Owner JWT ngắn hạn + gọi /api/v1/notifications/owner/*).
/// </summary>
public sealed class NotificationApiClient
{
    private readonly HttpClient _httpClient;
    private readonly IJwtTokenService _jwtTokenService;
    private readonly AuthenticationStateProvider _authStateProvider;
    private readonly ILogger<NotificationApiClient> _logger;

    public NotificationApiClient(
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        IJwtTokenService jwtTokenService,
        AuthenticationStateProvider authStateProvider,
        ILogger<NotificationApiClient> logger)
    {
        _httpClient = httpClientFactory.CreateClient("GatewayClient");
        _jwtTokenService = jwtTokenService;
        _authStateProvider = authStateProvider;
        _logger = logger;

        string baseUrl = configuration["Gateway:BaseUrl"] ?? "http://localhost:5001";
        _httpClient.BaseAddress = new Uri(baseUrl);
    }

    /// <summary>GET /api/v1/notifications/owner — danh sách thông báo owner (filter category/unreadOnly).</summary>
    public async Task<List<OwnerNotificationItem>> GetListAsync(string? category = null, bool unreadOnly = false, CancellationToken ct = default)
    {
        string query = $"?take=100&unreadOnly={unreadOnly.ToString().ToLowerInvariant()}";
        if (!string.IsNullOrWhiteSpace(category))
            query += $"&category={Uri.EscapeDataString(category)}";
        var req = await CreateRequestAsync(HttpMethod.Get, $"api/v1/notifications/owner{query}");
        var result = await SendAndReadAsync<OwnerNotificationListResult>(_httpClient, req, ct);
        return result?.Items ?? new();
    }

    /// <summary>GET /api/v1/notifications/owner/unread-count — số chưa đọc (badge).</summary>
    public async Task<int> GetUnreadCountAsync(CancellationToken ct = default)
    {
        var req = await CreateRequestAsync(HttpMethod.Get, "api/v1/notifications/owner/unread-count");
        var result = await SendAndReadAsync<UnreadCountResult>(_httpClient, req, ct);
        return result?.UnreadCount ?? 0;
    }

    /// <summary>POST /api/v1/notifications/owner/{id}/read.</summary>
    public async Task MarkReadAsync(Guid id, CancellationToken ct = default)
    {
        var req = await CreateRequestAsync(HttpMethod.Post, $"api/v1/notifications/owner/{id}/read");
        _ = await _httpClient.SendAsync(req, ct);
    }

    /// <summary>POST /api/v1/notifications/owner/read-all.</summary>
    public async Task MarkAllReadAsync(CancellationToken ct = default)
    {
        var req = await CreateRequestAsync(HttpMethod.Post, "api/v1/notifications/owner/read-all");
        _ = await _httpClient.SendAsync(req, ct);
    }

    private async Task<HttpRequestMessage> CreateRequestAsync(HttpMethod method, string relativeUri)
    {
        AuthenticationState authState = await _authStateProvider.GetAuthenticationStateAsync();
        ClaimsPrincipal user = authState.User;

        string userId = user.FindFirst("sub")?.Value
            ?? user.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? Guid.NewGuid().ToString();
        string email = user.FindFirst("email")?.Value
            ?? user.FindFirst(ClaimTypes.Email)?.Value
            ?? "owner@vanan.vn";
        string? tenantIdStr = user.FindFirst("tenant_id")?.Value ?? user.FindFirst("TenantId")?.Value;
        Guid tenantId = Guid.TryParse(tenantIdStr, out Guid tid) ? tid : Guid.Empty;

        string token = _jwtTokenService.GenerateToken(
            Guid.TryParse(userId, out Guid id) ? id : Guid.NewGuid(),
            email,
            "Owner",
            tenantId);

        var request = new HttpRequestMessage(method, relativeUri);
        request.Headers.Authorization = new AuthenticationHeaderValue(JwtBearerDefaults.AuthenticationScheme, token);
        return request;
    }

    private static readonly JsonSerializerOptions GatewayJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    private static async Task<T?> SendAndReadAsync<T>(HttpClient client, HttpRequestMessage request, CancellationToken ct = default)
    {
        HttpResponseMessage response = await client.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<T>(GatewayJsonOptions, ct);
    }
}

public class OwnerNotificationListResult
{
    public Guid TenantId { get; set; }
    public List<OwnerNotificationItem> Items { get; set; } = new();
}

public class OwnerNotificationItem
{
    public Guid Id { get; set; }
    public string Category { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public string? ActionUrl { get; set; }
    public string Severity { get; set; } = "Info";
    public bool IsRead { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class UnreadCountResult
{
    public Guid TenantId { get; set; }
    public int UnreadCount { get; set; }
}
