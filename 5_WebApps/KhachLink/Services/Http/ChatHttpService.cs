using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace VanAn.KhachLink.Services.Http;

/// <summary>
/// CC-S3 (Sprint 3): HTTP client for Chat endpoints.
/// KhachLink calls Gateway → CommunityController chat endpoints.
/// All methods require X-Customer-Token header (authenticated shipper/customer).
/// </summary>
public class ChatHttpService(IHttpClientFactory httpClientFactory, ILogger<ChatHttpService> logger)
{
    private readonly HttpClient _httpClient = httpClientFactory.CreateClient("gateway");
    private readonly ILogger<ChatHttpService> _logger = logger;

    /// <summary>
    /// GET /api/community/chat/conversations/{orderId}
    /// Returns chat history for the given order.
    /// </summary>
    public async Task<ChatHistoryResult> GetHistoryAsync(string customerToken, Guid orderId)
    {
        try
        {
            var request = new HttpRequestMessage(HttpMethod.Get, $"/api/community/chat/conversations/{orderId}");
            request.Headers.Add("X-Customer-Token", customerToken);

            var resp = await _httpClient.SendAsync(request);
            var body = await resp.Content.ReadAsStringAsync();

            if (resp.IsSuccessStatusCode)
            {
                var data = System.Text.Json.JsonSerializer.Deserialize<ChatHistoryResponse>(body,
                    new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                return new ChatHistoryResult
                {
                    Success = true,
                    ConversationId = data?.ConversationId ?? Guid.Empty,
                    Messages = data?.Messages ?? new List<ChatMessageDto>()
                };
            }

            var err = System.Text.Json.JsonSerializer.Deserialize<ErrorResponse>(body);
            return new ChatHistoryResult
            {
                Success = false,
                ErrorCode = (int)resp.StatusCode,
                ErrorMessage = err?.Error ?? $"Lỗi {resp.StatusCode}"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GetHistoryAsync failed for order {OrderId}", orderId);
            return new ChatHistoryResult { Success = false, ErrorMessage = "Lỗi kết nối." };
        }
    }

    /// <summary>
    /// POST /api/community/chat/messages
    /// Send a chat message.
    /// </summary>
    public async Task<SendMessageResult> SendMessageAsync(string customerToken, Guid orderId, string content)
    {
        try
        {
            var request = new HttpRequestMessage(HttpMethod.Post, "/api/community/chat/messages");
            request.Headers.Add("X-Customer-Token", customerToken);
            request.Content = JsonContent.Create(new { OrderId = orderId, Content = content });

            var resp = await _httpClient.SendAsync(request);
            var body = await resp.Content.ReadAsStringAsync();

            if (resp.IsSuccessStatusCode)
            {
                var data = System.Text.Json.JsonSerializer.Deserialize<SendMessageResponse>(body,
                    new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                return new SendMessageResult
                {
                    Success = true,
                    MessageId = data?.MessageId ?? Guid.Empty,
                    SentAt = DateTime.TryParse(data?.SentAt, out var sentAt) ? sentAt : DateTime.UtcNow
                };
            }

            var err = System.Text.Json.JsonSerializer.Deserialize<ErrorResponse>(body);
            return new SendMessageResult
            {
                Success = false,
                ErrorCode = (int)resp.StatusCode,
                ErrorMessage = err?.Error ?? $"Lỗi {resp.StatusCode}"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "SendMessageAsync failed for order {OrderId}", orderId);
            return new SendMessageResult { Success = false, ErrorMessage = "Lỗi kết nối." };
        }
    }
}

// === DTOs ===

public class ChatHistoryResult
{
    public bool Success { get; set; }
    public Guid ConversationId { get; set; }
    public List<ChatMessageDto> Messages { get; set; } = new();
    public int ErrorCode { get; set; }
    public string ErrorMessage { get; set; } = string.Empty;
}

public class ChatHistoryResponse
{
    public Guid ConversationId { get; set; }
    public Guid OrderId { get; set; }
    public List<ChatMessageDto> Messages { get; set; } = new();
}

public class ChatMessageDto
{
    public Guid Id { get; set; }
    public Guid SenderId { get; set; }
    public string Content { get; set; } = string.Empty;
    public DateTime SentAt { get; set; }
    public bool IsRead { get; set; }
}

public class SendMessageResult
{
    public bool Success { get; set; }
    public Guid MessageId { get; set; }
    public DateTime SentAt { get; set; }
    public int ErrorCode { get; set; }
    public string ErrorMessage { get; set; } = string.Empty;
}

public class SendMessageResponse
{
    public Guid MessageId { get; set; }
    public string SentAt { get; set; } = string.Empty;
}
