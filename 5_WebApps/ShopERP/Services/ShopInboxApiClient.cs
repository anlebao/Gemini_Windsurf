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
/// Realtime Platform P5 (2026-09-18): ShopERP client for the Gateway realtime shop-inbox surface.
/// Same pattern as <see cref="TenantCommunityAdminApiClient"/>: mints a short-lived Owner JWT
/// (tenant_id claim from the logged-in user) and calls
/// <c>GET /api/realtime/shop/conversations</c> — the shop's customer conversations, newest first.
/// The same JWT is exposed via <see cref="MintOwnerTokenAsync"/> so the RealtimeChatPanel can
/// authenticate the shop side of the conversation (staff identity on /hubs/messaging).
/// </summary>
public sealed class ShopInboxApiClient
{
    private readonly HttpClient _httpClient;
    private readonly IJwtTokenService _jwtTokenService;
    private readonly AuthenticationStateProvider _authStateProvider;
    private readonly ILogger<ShopInboxApiClient> _logger;

    public ShopInboxApiClient(
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        IJwtTokenService jwtTokenService,
        AuthenticationStateProvider authStateProvider,
        ILogger<ShopInboxApiClient> logger)
    {
        _httpClient = httpClientFactory.CreateClient("GatewayClient");
        _jwtTokenService = jwtTokenService;
        _authStateProvider = authStateProvider;
        _logger = logger;

        string baseUrl = configuration["Gateway:BaseUrl"] ?? "http://localhost:5001";
        _httpClient.BaseAddress = new Uri(baseUrl);
    }

    /// <summary>GET /api/realtime/shop/conversations — the caller's shop inbox (newest first).</summary>
    public async Task<ShopInboxResult> GetConversationsAsync(CancellationToken ct = default)
    {
        var req = await CreateRequestAsync(HttpMethod.Get, "api/realtime/shop/conversations");
        return await SendAndReadAsync<ShopInboxResult>(_httpClient, req, ct) ?? new();
    }

    /// <summary>
    /// Mint a short-lived Owner JWT for the current user — used as the RealtimeChatPanel.StaffToken
    /// so the chat authenticates as the shop side (staff identity + tenant_id claim).
    /// </summary>
    public async Task<string> MintOwnerTokenAsync()
    {
        AuthenticationState authState = await _authStateProvider.GetAuthenticationStateAsync();
        ClaimsPrincipal user = authState.User;

        string userId = user.FindFirst("sub")?.Value
            ?? user.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? Guid.NewGuid().ToString();

        string email = user.FindFirst("email")?.Value
            ?? user.FindFirst(ClaimTypes.Email)?.Value
            ?? "owner@vanan.vn";

        string? tenantIdStr = user.FindFirst("tenant_id")?.Value
            ?? user.FindFirst("TenantId")?.Value;
        Guid tenantId = Guid.TryParse(tenantIdStr, out Guid tid) ? tid : Guid.Empty;

        return _jwtTokenService.GenerateToken(
            Guid.TryParse(userId, out Guid id) ? id : Guid.NewGuid(),
            email,
            "Owner",
            tenantId);
    }

    /// <summary>The current user's id ("sub" claim) — the chat panel aligns bubbles by it.</summary>
    public async Task<Guid> GetCurrentUserIdAsync()
    {
        AuthenticationState authState = await _authStateProvider.GetAuthenticationStateAsync();
        string userId = authState.User.FindFirst("sub")?.Value
            ?? authState.User.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? Guid.Empty.ToString();
        return Guid.TryParse(userId, out Guid id) ? id : Guid.Empty;
    }

    private async Task<HttpRequestMessage> CreateRequestAsync(HttpMethod method, string relativeUri, object? body = null)
    {
        string token = await MintOwnerTokenAsync();
        var request = new HttpRequestMessage(method, relativeUri);
        request.Headers.Authorization = new AuthenticationHeaderValue(JwtBearerDefaults.AuthenticationScheme, token);
        if (body != null)
        {
            request.Content = JsonContent.Create(body, options: new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
            });
        }
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

/// <summary>GET /api/realtime/shop/conversations response (camelCase from Gateway).</summary>
public class ShopInboxResult
{
    public Guid TenantId { get; set; }
    public List<ShopInboxConversationItem> Conversations { get; set; } = new();
}

public class ShopInboxConversationItem
{
    public Guid ConversationId { get; set; }
    public Guid SubjectId { get; set; }
    public Guid CustomerId { get; set; }
    public string? CustomerName { get; set; }
    public string? LastMessage { get; set; }
    public DateTime? LastMessageAt { get; set; }
    public Guid? LastSenderId { get; set; }
}
