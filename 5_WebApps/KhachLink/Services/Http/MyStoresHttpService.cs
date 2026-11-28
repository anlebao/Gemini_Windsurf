using System.Net.Http.Json;
using System.Text.Json;

namespace VanAn.KhachLink.Services.Http;

/// <summary>
/// "Cửa hàng của tôi" (2026-11-28): HTTP client cho GET /api/customers/my-stores —
/// list tenant khách đã mua/tích điểm + số dư điểm per-tenant.
/// Headers: X-Customer-Token (ưu tiên — token-validated) + X-Customer-Device-Id
/// (guest fallback — pattern OrderHistory). Token hết hạn → retry deviceId-only
/// để guest-level data vẫn hiện. Fail-open: lỗi nào cũng trả list rỗng (section ẩn).
/// </summary>
public class MyStoresHttpService(IHttpClientFactory httpClientFactory, ILogger<MyStoresHttpService> logger)
{
    private readonly HttpClient _httpClient = httpClientFactory.CreateClient("gateway");
    private readonly ILogger<MyStoresHttpService> _logger = logger;

    private static readonly JsonSerializerOptions _jsonOpts = new() { PropertyNameCaseInsensitive = true };

    public async Task<List<MyStoreItem>> GetMyStoresAsync(string? customerToken, string? customerDeviceId,
        CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(customerToken) && string.IsNullOrEmpty(customerDeviceId))
            return [];

        try
        {
            var result = await SendAsync(customerToken, customerDeviceId, ct);
            if (result == null && !string.IsNullOrEmpty(customerToken) && !string.IsNullOrEmpty(customerDeviceId))
            {
                // Token invalid/expired → retry guest-level (deviceId only)
                result = await SendAsync(null, customerDeviceId, ct);
            }
            return result ?? [];
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error loading my-stores — section hides");
            return [];
        }
    }

    private async Task<List<MyStoreItem>?> SendAsync(string? token, string? deviceId, CancellationToken ct)
    {
        var req = new HttpRequestMessage(HttpMethod.Get, "/api/customers/my-stores");
        if (!string.IsNullOrEmpty(token)) req.Headers.Add("X-Customer-Token", token);
        if (!string.IsNullOrEmpty(deviceId)) req.Headers.Add("X-Customer-Device-Id", deviceId);

        var resp = await _httpClient.SendAsync(req, ct);
        if (!resp.IsSuccessStatusCode) return null;
        var data = await resp.Content.ReadFromJsonAsync<MyStoresResponse>(_jsonOpts, ct);
        return data?.Stores;
    }

    // === Response DTOs — mirror Gateway MyStoreDto ===

    public class MyStoresResponse
    {
        public List<MyStoreItem> Stores { get; set; } = new();
    }

    public class MyStoreItem
    {
        public Guid TenantId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Slug { get; set; }
        public string? LogoUrl { get; set; }
        public int OrderCount { get; set; }
        public DateTime? LastOrderAt { get; set; }
        public decimal TotalSpent { get; set; }
        public int PointBalance { get; set; }
        public string Mode { get; set; } = "Silo";
    }
}
