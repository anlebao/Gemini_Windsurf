using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace VanAn.KhachLink.Services.Http;

/// <summary>
/// #126 R2 Sprint 4: KhachLink HTTP client for Guard QR Claim + Wallet sync.
/// All methods require X-Customer-Token header (authenticated customer).
/// Calls Gateway /api/guard/claim + /api/guard/my-sessions.
/// </summary>
public class GuardQrApiClient(IHttpClientFactory httpClientFactory, ILogger<GuardQrApiClient> logger)
{
    private readonly HttpClient _httpClient = httpClientFactory.CreateClient("gateway");
    private readonly ILogger<GuardQrApiClient> _logger = logger;

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    /// <summary>POST /api/guard/claim — claim a QR session by scanning QR or entering 6-digit code.</summary>
    public async Task<ClaimResponse?> ClaimAsync(string? qrPayload, string? shortCode, string customerToken)
    {
        try
        {
            var req = new HttpRequestMessage(HttpMethod.Post, "/api/guard/claim");
            req.Headers.Add("X-Customer-Token", customerToken);
            req.Content = JsonContent.Create(new { qrPayload, shortCode });

            var resp = await _httpClient.SendAsync(req);
            var body = await resp.Content.ReadAsStringAsync();

            if (resp.IsSuccessStatusCode)
                return JsonSerializer.Deserialize<ClaimResponse>(body, JsonOpts);

            _logger.LogWarning("Claim failed: {Status} {Body}", resp.StatusCode, body);
            return new ClaimResponse { Error = ExtractError(body) };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ClaimAsync exception");
            return new ClaimResponse { Error = "Lỗi kết nối. Vui lòng thử lại." };
        }
    }

    /// <summary>POST /api/guard/my-sessions — sync wallet session statuses with server.</summary>
    public async Task<List<SessionStatusItem>> GetMySessionsAsync(List<Guid> sessionIds, string customerToken)
    {
        try
        {
            if (sessionIds.Count == 0) return new List<SessionStatusItem>();

            var req = new HttpRequestMessage(HttpMethod.Post, "/api/guard/my-sessions");
            req.Headers.Add("X-Customer-Token", customerToken);
            req.Content = JsonContent.Create(new { sessionIds });

            var resp = await _httpClient.SendAsync(req);
            if (!resp.IsSuccessStatusCode)
            {
                _logger.LogWarning("My-sessions failed: {Status}", resp.StatusCode);
                return new List<SessionStatusItem>();
            }

            var result = await resp.Content.ReadFromJsonAsync<MySessionsResponse>(JsonOpts);
            return result?.Items ?? new List<SessionStatusItem>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GetMySessionsAsync exception");
            return new List<SessionStatusItem>();
        }
    }

    private static string ExtractError(string body)
    {
        try
        {
            using var doc = JsonDocument.Parse(body);
            if (doc.RootElement.TryGetProperty("error", out var err))
                return err.GetString() ?? "Đã có lỗi xảy ra.";
        }
        catch { }
        return "Đã có lỗi xảy ra. Vui lòng thử lại.";
    }

    // === DTOs (match Gateway API response shapes, camelCase JSON) ===

    public class ClaimResponse
    {
        public Guid SessionId { get; set; }
        public string? PlateNumber { get; set; }  // PHASE-1: nullable — guard may skip OCR
        public string ShortCode { get; set; } = string.Empty;
        public string PlatePhotoUrl { get; set; } = string.Empty;
        public string CustomerPhotoUrl { get; set; } = string.Empty;
        public DateTime IssuedAt { get; set; }
        public string Status { get; set; } = string.Empty;
        public string? Error { get; set; }
        public bool Success => string.IsNullOrEmpty(Error);
    }

    public class SessionStatusItem
    {
        public Guid SessionId { get; set; }
        public string? PlateNumber { get; set; }  // PHASE-1: nullable
        public string ShortCode { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public DateTime IssuedAt { get; set; }
        public DateTime? CheckedOutAt { get; set; }
        public Guid? CustomerId { get; set; }
        public Guid TenantId { get; set; }
    }

    private class MySessionsResponse
    {
        public List<SessionStatusItem> Items { get; set; } = new();
    }
}
