using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using VanAn.UI.Platform.Core.Interfaces;
using VanAn.UI.Platform.Realtime;

namespace VanAn.UI.Platform.Adapters;

/// <summary>
/// Realtime Platform P4 (UI-4): HTTP adapter for the Gateway generic realtime surface
/// (<c>/api/realtime/*</c>), implements both <see cref="IRealtimeChatClient"/> and
/// <see cref="ILiveLocationClient"/>.
///
/// F4: the adapter never hard-codes a host. It builds absolute URLs from
/// <see cref="IRealtimeEndpointProvider"/> per call and uses the named "realtime" HttpClient
/// (registered by AddRealtimePlatform) with NO base address — which is what makes the same RCL
/// work on WASM (browser HttpClient) and Blazor Server (factory HttpClient).
///
/// Identity: sends X-Customer-Token when logged in, otherwise X-Customer-Device-Id so guests can
/// chat and track (P1 D6). Mirrors the legacy ChatHttpService.AddIdentityHeader.
/// </summary>
public class RealtimeHttpAdapter(
    IHttpClientFactory httpClientFactory,
    IRealtimeEndpointProvider endpoints,
    ILogger<RealtimeHttpAdapter> logger) : IRealtimeChatClient, ILiveLocationClient
{
    internal const string HttpClientName = "realtime";

    private readonly IHttpClientFactory _httpClientFactory = httpClientFactory;
    private readonly IRealtimeEndpointProvider _endpoints = endpoints;
    private readonly ILogger<RealtimeHttpAdapter> _logger = logger;

    private HttpClient CreateClient() => _httpClientFactory.CreateClient(HttpClientName);

    // === IRealtimeChatClient ===

    public async Task<RealtimeChatHistoryResult> GetHistoryAsync(
        string subjectType, Guid subjectId, string? customerToken, Guid? customerDeviceId,
        int take = 100, CancellationToken ct = default)
    {
        try
        {
            var request = BuildGet($"/api/realtime/conversations/{subjectType}/{subjectId}?take={take}",
                customerToken, customerDeviceId);
            var resp = await CreateClient().SendAsync(request, ct);
            var body = await resp.Content.ReadAsStringAsync(ct);

            if (resp.IsSuccessStatusCode)
            {
                var data = Deserialize<RealtimeHistoryResponse>(body);
                return new RealtimeChatHistoryResult
                {
                    Success = true,
                    ConversationId = data?.ConversationId ?? Guid.Empty,
                    Messages = data?.Messages ?? new List<RealtimeChatMessage>()
                };
            }

            return new RealtimeChatHistoryResult
            {
                Success = false,
                ErrorCode = (int)resp.StatusCode,
                ErrorMessage = ReadError(body) ?? $"Lỗi {resp.StatusCode}"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GetHistoryAsync failed for {SubjectType}/{SubjectId}", subjectType, subjectId);
            return new RealtimeChatHistoryResult { Success = false, ErrorMessage = "Lỗi kết nối." };
        }
    }

    public async Task<RealtimeSendResult> SendMessageAsync(
        string subjectType, Guid subjectId, string content, string? customerToken, Guid? customerDeviceId,
        CancellationToken ct = default)
    {
        try
        {
            var request = BuildPost("/api/realtime/conversations/messages",
                new { subjectType, subjectId, content }, customerToken, customerDeviceId);
            var resp = await CreateClient().SendAsync(request, ct);
            var body = await resp.Content.ReadAsStringAsync(ct);

            if (resp.IsSuccessStatusCode)
            {
                var data = Deserialize<RealtimeSendResponse>(body);
                return new RealtimeSendResult
                {
                    Success = true,
                    MessageId = data?.MessageId ?? Guid.Empty,
                    SentAt = data != null && TryParseUtc(data.SentAt, out var sentAt) ? sentAt : DateTime.UtcNow
                };
            }

            return new RealtimeSendResult
            {
                Success = false,
                ErrorCode = (int)resp.StatusCode,
                ErrorMessage = ReadError(body) ?? $"Lỗi {resp.StatusCode}"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "SendMessageAsync failed for {SubjectType}/{SubjectId}", subjectType, subjectId);
            return new RealtimeSendResult { Success = false, ErrorMessage = "Lỗi kết nối." };
        }
    }

    // === ILiveLocationClient ===

    public async Task<RealtimeLocationResult> GetLatestAsync(
        string subjectType, Guid subjectId, string? customerToken, Guid? customerDeviceId,
        CancellationToken ct = default)
    {
        try
        {
            var request = BuildGet($"/api/realtime/location/{subjectType}/{subjectId}/latest",
                customerToken, customerDeviceId);
            var resp = await CreateClient().SendAsync(request, ct);
            var body = await resp.Content.ReadAsStringAsync(ct);

            if (resp.IsSuccessStatusCode)
            {
                var data = Deserialize<RealtimeLocationResponse>(body);
                return new RealtimeLocationResult
                {
                    Success = true,
                    Lat = data?.Lat,
                    Lng = data?.Lng,
                    TrackerId = data?.TrackerId,
                    RecordedAt = data?.RecordedAt
                };
            }

            return new RealtimeLocationResult
            {
                Success = false,
                ErrorCode = (int)resp.StatusCode,
                ErrorMessage = ReadError(body) ?? $"Lỗi {resp.StatusCode}"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GetLatestAsync failed for {SubjectType}/{SubjectId}", subjectType, subjectId);
            return new RealtimeLocationResult { Success = false, ErrorMessage = "Lỗi kết nối." };
        }
    }

    public async Task<RealtimePingResult> RecordPingAsync(
        string subjectType, Guid subjectId, double lat, double lng, string? customerToken, Guid? customerDeviceId,
        CancellationToken ct = default)
    {
        try
        {
            var request = BuildPost("/api/realtime/location/ping",
                new { subjectType, subjectId, lat, lng }, customerToken, customerDeviceId);
            var resp = await CreateClient().SendAsync(request, ct);
            var body = await resp.Content.ReadAsStringAsync(ct);

            if (resp.IsSuccessStatusCode)
            {
                var data = Deserialize<RealtimePingResponse>(body);
                return new RealtimePingResult
                {
                    Success = true,
                    RecordedAt = data != null && TryParseUtc(data.RecordedAt, out var recordedAt) ? recordedAt : DateTime.UtcNow
                };
            }

            return new RealtimePingResult
            {
                Success = false,
                ErrorCode = (int)resp.StatusCode,
                ErrorMessage = ReadError(body) ?? $"Lỗi {resp.StatusCode}"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "RecordPingAsync failed for {SubjectType}/{SubjectId}", subjectType, subjectId);
            return new RealtimePingResult { Success = false, ErrorMessage = "Lỗi kết nối." };
        }
    }

    // === internals ===

    private HttpRequestMessage BuildGet(string path, string? customerToken, Guid? customerDeviceId)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, $"{_endpoints.GetGatewayBaseUrl()}{path}");
        AddIdentityHeader(request, customerToken, customerDeviceId);
        return request;
    }

    private HttpRequestMessage BuildPost(string path, object body, string? customerToken, Guid? customerDeviceId)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, $"{_endpoints.GetGatewayBaseUrl()}{path}");
        AddIdentityHeader(request, customerToken, customerDeviceId);
        request.Content = JsonContent.Create(body);
        return request;
    }

    private static void AddIdentityHeader(HttpRequestMessage request, string? customerToken, Guid? customerDeviceId)
    {
        if (!string.IsNullOrEmpty(customerToken))
            request.Headers.Add("X-Customer-Token", customerToken);
        else if (customerDeviceId.HasValue && customerDeviceId.Value != Guid.Empty)
            request.Headers.Add("X-Customer-Device-Id", customerDeviceId.Value.ToString());
    }

    private static T? Deserialize<T>(string body)
        => JsonSerializer.Deserialize<T>(body, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

    /// <summary>Parse an ISO-8601 round-trip timestamp ("O" format from the Gateway) as UTC.</summary>
    private static bool TryParseUtc(string value, out DateTime result)
        => DateTime.TryParse(value, System.Globalization.CultureInfo.InvariantCulture,
            System.Globalization.DateTimeStyles.RoundtripKind, out result);

    private static string? ReadError(string body)
    {
        try
        {
            var err = Deserialize<RealtimeErrorResponse>(body);
            return string.IsNullOrWhiteSpace(err?.Error) ? null : err.Error;
        }
        catch
        {
            return null;
        }
    }

    // === response DTOs (camelCase JSON from the Gateway) ===

    private class RealtimeHistoryResponse
    {
        public Guid ConversationId { get; set; }
        public List<RealtimeChatMessage> Messages { get; set; } = new();
    }

    private class RealtimeSendResponse
    {
        public Guid MessageId { get; set; }
        public string SentAt { get; set; } = string.Empty;
    }

    private class RealtimeLocationResponse
    {
        public double? Lat { get; set; }
        public double? Lng { get; set; }
        public Guid? TrackerId { get; set; }
        public DateTime? RecordedAt { get; set; }
    }

    private class RealtimePingResponse
    {
        public string RecordedAt { get; set; } = string.Empty;
    }

    private class RealtimeErrorResponse
    {
        public string? Error { get; set; }
    }
}
