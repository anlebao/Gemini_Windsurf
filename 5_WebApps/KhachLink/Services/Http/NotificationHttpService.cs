using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace VanAn.KhachLink.Services.Http;

/// <summary>
/// Notification Hub (2026-10-15): HTTP client cho inbox thông báo khách (KhachLink).
/// Gateway /api/v1/notifications/customer/* — yêu cầu X-Customer-Token.
/// </summary>
public class NotificationHttpService(IHttpClientFactory httpClientFactory, ILogger<NotificationHttpService> logger)
{
    private readonly HttpClient _httpClient = httpClientFactory.CreateClient("gateway");
    private readonly ILogger<NotificationHttpService> _logger = logger;

    /// <summary>GET /api/v1/notifications/customer — danh sách thông báo khách.</summary>
    public async Task<CustomerNotificationResult> GetListAsync(string customerToken, int take = 100)
    {
        try
        {
            var request = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/notifications/customer?take={take}");
            request.Headers.Add("X-Customer-Token", customerToken);
            var resp = await _httpClient.SendAsync(request);
            var body = await resp.Content.ReadAsStringAsync();
            if (resp.IsSuccessStatusCode)
            {
                var data = System.Text.Json.JsonSerializer.Deserialize<CustomerNotificationResponse>(body,
                    new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                return new CustomerNotificationResult { Success = true, Items = data?.Items ?? new() };
            }
            return new CustomerNotificationResult { Success = false, ErrorMessage = body };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading customer notifications");
            return new CustomerNotificationResult { Success = false, ErrorMessage = "Lỗi kết nối." };
        }
    }

    /// <summary>GET /api/v1/notifications/customer/unread-count.</summary>
    public async Task<int> GetUnreadCountAsync(string customerToken)
    {
        try
        {
            var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/notifications/customer/unread-count");
            request.Headers.Add("X-Customer-Token", customerToken);
            var resp = await _httpClient.SendAsync(request);
            var body = await resp.Content.ReadAsStringAsync();
            if (resp.IsSuccessStatusCode)
            {
                var data = System.Text.Json.JsonSerializer.Deserialize<UnreadCountResponse>(body,
                    new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                return data?.UnreadCount ?? 0;
            }
            return 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading customer unread count");
            return 0;
        }
    }

    /// <summary>POST /api/v1/notifications/customer/{notificationId}/read.</summary>
    public async Task<bool> MarkReadAsync(string customerToken, Guid notificationId)
    {
        try
        {
            var request = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/notifications/customer/{notificationId}/read");
            request.Headers.Add("X-Customer-Token", customerToken);
            var resp = await _httpClient.SendAsync(request);
            return resp.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error marking customer notification read");
            return false;
        }
    }

    /// <summary>POST /api/v1/notifications/customer/read-all.</summary>
    public async Task<bool> MarkAllReadAsync(string customerToken)
    {
        try
        {
            var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/notifications/customer/read-all");
            request.Headers.Add("X-Customer-Token", customerToken);
            var resp = await _httpClient.SendAsync(request);
            return resp.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error marking all customer notifications read");
            return false;
        }
    }
}

public class CustomerNotificationResult
{
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public List<CustomerNotificationItem> Items { get; set; } = new();
}

public class CustomerNotificationResponse
{
    public Guid CustomerId { get; set; }
    public List<CustomerNotificationItem> Items { get; set; } = new();
}

public class CustomerNotificationItem
{
    public Guid NotificationId { get; set; }
    public string? Category { get; set; }
    public string? Title { get; set; }
    public string? Body { get; set; }
    public string? ActionUrl { get; set; }
    public bool IsRead { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class UnreadCountResponse
{
    public Guid CustomerId { get; set; }
    public int UnreadCount { get; set; }
}
