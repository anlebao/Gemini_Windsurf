using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace VanAn.KhachLink.Services.Http;

/// <summary>
/// CC-S1-T1/T2 (Sprint 1): HTTP client for Community Commerce endpoints.
/// KhachLink calls Gateway → CommunityController (Gateway-native, no YARP forward).
/// All methods require X-Customer-Token header (authenticated shipper).
/// </summary>
public class CommunityHttpService(IHttpClientFactory httpClientFactory, ILogger<CommunityHttpService> logger)
{
    private readonly HttpClient _httpClient = httpClientFactory.CreateClient("gateway");
    private readonly ILogger<CommunityHttpService> _logger = logger;

    /// <summary>
    /// GET /api/community/role — returns isShipper flag. Used by NavMenu to show/hide shipper tab.
    /// </summary>
    public async Task<bool> GetIsShipperAsync(string customerToken)
    {
        try
        {
            var request = new HttpRequestMessage(HttpMethod.Get, "/api/community/role");
            request.Headers.Add("X-Customer-Token", customerToken);

            var resp = await _httpClient.SendAsync(request);
            if (!resp.IsSuccessStatusCode) return false;

            var body = await resp.Content.ReadAsStringAsync();
            var data = System.Text.Json.JsonSerializer.Deserialize<RoleResponse>(body,
                new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            return data?.IsShipper ?? false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GetIsShipperAsync failed");
            return false;
        }
    }

    /// <summary>
    /// GET /api/community/nearby-orders?lat={lat}&amp;lng={lng}&amp;radiusKm={radius}
    /// Returns DELIVERY orders within radius, sorted by distance.
    /// </summary>
    public async Task<NearbyOrdersResult> GetNearbyOrdersAsync(string customerToken, double lat, double lng, int radiusKm)
    {
        try
        {
            var request = new HttpRequestMessage(HttpMethod.Get,
                $"/api/community/nearby-orders?lat={lat}&lng={lng}&radiusKm={radiusKm}");
            request.Headers.Add("X-Customer-Token", customerToken);

            var resp = await _httpClient.SendAsync(request);
            var body = await resp.Content.ReadAsStringAsync();

            if (resp.IsSuccessStatusCode)
            {
                var orders = System.Text.Json.JsonSerializer.Deserialize<List<NearbyOrderDto>>(body,
                    new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true })
                    ?? new List<NearbyOrderDto>();
                return new NearbyOrdersResult { Success = true, Orders = orders };
            }

            var err = System.Text.Json.JsonSerializer.Deserialize<ErrorResponse>(body);
            return new NearbyOrdersResult
            {
                Success = false,
                ErrorCode = (int)resp.StatusCode,
                ErrorMessage = err?.Error ?? $"Lỗi {resp.StatusCode}"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GetNearbyOrdersAsync failed");
            return new NearbyOrdersResult { Success = false, ErrorMessage = "Lỗi kết nối." };
        }
    }

    /// <summary>
    /// POST /api/community/orders/{orderId}/accept
    /// Accept an order for delivery. Returns 409 if already assigned.
    /// </summary>
    public async Task<AcceptOrderResult> AcceptOrderAsync(string customerToken, Guid orderId)
    {
        try
        {
            var request = new HttpRequestMessage(HttpMethod.Post, $"/api/community/orders/{orderId}/accept");
            request.Headers.Add("X-Customer-Token", customerToken);

            var resp = await _httpClient.SendAsync(request);
            var body = await resp.Content.ReadAsStringAsync();

            if (resp.IsSuccessStatusCode)
            {
                var data = System.Text.Json.JsonSerializer.Deserialize<AcceptSuccessResponse>(body,
                    new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                return new AcceptOrderResult
                {
                    Success = true,
                    DeliveryTaskId = data?.DeliveryTaskId ?? Guid.Empty,
                    Status = data?.Status ?? "Assigned"
                };
            }

            var err = System.Text.Json.JsonSerializer.Deserialize<ErrorResponse>(body);
            return new AcceptOrderResult
            {
                Success = false,
                ErrorCode = (int)resp.StatusCode,
                ErrorMessage = err?.Error ?? $"Lỗi {resp.StatusCode}"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "AcceptOrderAsync failed for {OrderId}", orderId);
            return new AcceptOrderResult { Success = false, ErrorMessage = "Lỗi kết nối." };
        }
    }
}

// === DTOs ===

public class NearbyOrderDto
{
    public Guid OrderId { get; set; }
    public Guid TenantId { get; set; }
    public string ShopName { get; set; } = string.Empty;
    public double ShopLat { get; set; }
    public double ShopLng { get; set; }
    public string? DeliveryAddress { get; set; }
    public double? DeliveryLat { get; set; }
    public double? DeliveryLng { get; set; }
    public decimal TotalAmount { get; set; }
    public string Status { get; set; } = string.Empty;
    public double DistanceKm { get; set; }
}

public class NearbyOrdersResult
{
    public bool Success { get; set; }
    public List<NearbyOrderDto> Orders { get; set; } = new();
    public int ErrorCode { get; set; }
    public string ErrorMessage { get; set; } = string.Empty;
}

public class AcceptOrderResult
{
    public bool Success { get; set; }
    public Guid DeliveryTaskId { get; set; }
    public string Status { get; set; } = string.Empty;
    public int ErrorCode { get; set; }
    public string ErrorMessage { get; set; } = string.Empty;
}

public class AcceptSuccessResponse
{
    public Guid DeliveryTaskId { get; set; }
    public Guid OrderId { get; set; }
    public string Status { get; set; } = string.Empty;
}

public class ErrorResponse
{
    public string? Error { get; set; }
}

public class RoleResponse
{
    public bool IsShipper { get; set; }
}
