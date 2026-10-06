using System.Net;
using System.Net.Http.Json;
using VanAn.KhachLink.Models;

namespace VanAn.KhachLink.Services.Http;

/// <summary>
/// HTTP client cho Gateway public booking API (P4.1 — anonymous, rate-limited).
/// KHÔNG báo thành công trước server persist (§29): mọi lỗi HTTP → throw BookingApiException
/// với message thân thiện từ server (§6.5 — không lộ mã kỹ thuật cho khách).
/// </summary>
public class BookingPublicHttpService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<BookingPublicHttpService> _logger;

    public BookingPublicHttpService(IHttpClientFactory httpClientFactory, ILogger<BookingPublicHttpService> logger)
    {
        _httpClient = httpClientFactory.CreateClient("gateway");
        _logger = logger;
    }

    public async Task<QrResolveResultDto?> ResolveQrAsync(string qrToken, string anonymousSessionId, CancellationToken ct = default)
    {
        string url = $"api/public/booking/qr/{Uri.EscapeDataString(qrToken)}?anonymousSessionId={Uri.EscapeDataString(anonymousSessionId)}";
        HttpResponseMessage response = await _httpClient.GetAsync(url, ct);
        if (response.StatusCode == HttpStatusCode.NotFound)
            return null;
        await EnsureSuccessAsync(response, ct);
        return await response.Content.ReadFromJsonAsync<QrResolveResultDto>(cancellationToken: ct);
    }

    public async Task<BookingCatalogDto?> GetCatalogAsync(Guid tenantId, CancellationToken ct = default)
    {
        HttpResponseMessage response = await _httpClient.GetAsync($"api/public/booking/tenants/{tenantId}/services", ct);
        if (response.StatusCode == HttpStatusCode.NotFound)
            return null;
        await EnsureSuccessAsync(response, ct);
        return await response.Content.ReadFromJsonAsync<BookingCatalogDto>(cancellationToken: ct);
    }

    public async Task<List<AvailableSlotDto>> GetAvailabilityAsync(
        Guid tenantId, Guid offeringId, DateOnly date, Guid? staffId = null, CancellationToken ct = default)
    {
        string url = $"api/public/booking/availability?tenantId={tenantId}&offeringId={offeringId}&date={date:yyyy-MM-dd}";
        if (staffId is not null)
            url += $"&staffId={staffId}";
        HttpResponseMessage response = await _httpClient.GetAsync(url, ct);
        await EnsureSuccessAsync(response, ct);
        return await response.Content.ReadFromJsonAsync<List<AvailableSlotDto>>(cancellationToken: ct) ?? [];
    }

    /// <summary>Create booking — Idempotency-Key header (§21.1); retry cùng key → server trả booking gốc.</summary>
    public async Task<CreateBookingResultDto> CreateBookingAsync(CreateBookingRequestDto request, string idempotencyKey, CancellationToken ct = default)
    {
        using var message = new HttpRequestMessage(HttpMethod.Post, "api/public/booking/bookings");
        message.Headers.TryAddWithoutValidation("Idempotency-Key", idempotencyKey);
        message.Content = JsonContent.Create(request);

        HttpResponseMessage response = await _httpClient.SendAsync(message, ct);
        await EnsureSuccessAsync(response, ct);
        return await response.Content.ReadFromJsonAsync<CreateBookingResultDto>(cancellationToken: ct)
            ?? throw new BookingApiException("Không nhận được phản hồi từ máy chủ. Vui lòng thử lại.");
    }

    /// <summary>Status polling (§10). Trả (dto, etag); 404 → null.</summary>
    public async Task<(PublicBookingStatusDto? Dto, string? ETag)> GetStatusAsync(string publicBookingCode, CancellationToken ct = default)
    {
        string url = $"api/public/booking/bookings/{Uri.EscapeDataString(publicBookingCode)}";
        HttpResponseMessage response = await _httpClient.GetAsync(url, ct);
        if (response.StatusCode == HttpStatusCode.NotFound)
            return (null, null);
        await EnsureSuccessAsync(response, ct);
        PublicBookingStatusDto? dto = await response.Content.ReadFromJsonAsync<PublicBookingStatusDto>(cancellationToken: ct);
        return (dto, response.Headers.ETag?.Tag);
    }

    /// <summary>Hủy lịch (khách tự hủy). Trả (success, message, status).</summary>
    public async Task<(bool Success, string Message, string? Status)> CancelBookingAsync(string publicBookingCode, string? reason, CancellationToken ct = default)
    {
        using var message = new HttpRequestMessage(HttpMethod.Post, $"api/public/booking/bookings/{Uri.EscapeDataString(publicBookingCode)}/cancel");
        message.Content = JsonContent.Create(new { reason });
        HttpResponseMessage response = await _httpClient.SendAsync(message, ct);
        if (response.IsSuccessStatusCode)
        {
            var ok = await response.Content.ReadFromJsonAsync<CancelOkDto>(cancellationToken: ct);
            return (true, ok?.Message ?? "Đã hủy lịch hẹn.", ok?.Status);
        }
        string msg = await ReadErrorMessageAsync(response, ct);
        return (false, msg, null);
    }

    // ── Helpers ─────────────────────────────────────────────────────────────

    private static async Task EnsureSuccessAsync(HttpResponseMessage response, CancellationToken ct)
    {
        if (response.IsSuccessStatusCode)
            return;
        string message = await ReadErrorMessageAsync(response, ct);
        throw new BookingApiException(message);
    }

    private static async Task<string> ReadErrorMessageAsync(HttpResponseMessage response, CancellationToken ct)
    {
        try
        {
            BookingApiError? error = await response.Content.ReadFromJsonAsync<BookingApiError>(cancellationToken: ct);
            if (!string.IsNullOrWhiteSpace(error?.Message))
                return error.Message;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Booking error parse failed: {ex.Message}");
        }
        return response.StatusCode switch
        {
            HttpStatusCode.Conflict => "Khung giờ này vừa có người đặt. Vui lòng chọn khung giờ khác.",
            HttpStatusCode.NotFound => "Không tìm thấy lịch hẹn hoặc cửa hàng chưa mở đặt lịch.",
            HttpStatusCode.TooManyRequests => "Bạn đang gửi yêu cầu quá nhanh. Vui lòng thử lại sau ít phút.",
            _ => "Máy chủ đang bận. Vui lòng thử lại sau."
        };
    }

    private class CancelOkDto
    {
        public string Message { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
    }
}

/// <summary>Lỗi business từ API (message thân thiện — §6.5).</summary>
public class BookingApiException : Exception
{
    public BookingApiException(string message) : base(message) { }
}
