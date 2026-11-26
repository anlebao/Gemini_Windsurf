using System.Net.Http.Json;

namespace VanAn.KhachLink.Services.Http;

/// <summary>
/// 2026-11-26: Customer email-OTP auth (Brevo 0đ) — anonymous endpoints
/// POST /api/customer-identity/otp/send + /otp/verify via Gateway → ShopERP.
/// Email-first: server gửi mã qua Brevo; phone/SMS vẫn được backend hỗ trợ.
/// </summary>
public class CustomerAuthHttpService(IHttpClientFactory httpClientFactory, ILogger<CustomerAuthHttpService> logger)
{
    private readonly HttpClient _httpClient = httpClientFactory.CreateClient("gateway");
    private readonly ILogger<CustomerAuthHttpService> _logger = logger;

    /// <summary>POST /api/customer-identity/otp/send — gửi OTP tới email (hoặc SĐT).</summary>
    public async Task<CustomerOtpSendResult> SendOtpAsync(string email, CancellationToken ct = default)
    {
        try
        {
            var resp = await _httpClient.PostAsJsonAsync("/api/customer-identity/otp/send",
                new { email }, ct);
            var body = await resp.Content.ReadAsStringAsync(ct);
            if (!resp.IsSuccessStatusCode)
                return new CustomerOtpSendResult { Success = false, Message = ExtractErrorMessage(body) };

            var data = System.Text.Json.JsonSerializer.Deserialize<OtpSendResponseDto>(body,
                new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            return new CustomerOtpSendResult
            {
                Success = true,
                Channel = data?.Channel ?? "email",
                ContactSuffix = data?.ContactSuffix ?? "",
                Message = data?.Message ?? "Mã xác minh đã được gửi."
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "CustomerAuthHttpService.SendOtpAsync failed");
            return new CustomerOtpSendResult { Success = false, Message = "Lỗi kết nối. Vui lòng thử lại." };
        }
    }

    /// <summary>POST /api/customer-identity/otp/verify — verify mã → customer token.</summary>
    public async Task<CustomerOtpVerifyResult> VerifyOtpAsync(
        string email, string otp, string? channel, Guid? deviceId, CancellationToken ct = default)
    {
        try
        {
            var resp = await _httpClient.PostAsJsonAsync("/api/customer-identity/otp/verify",
                new { email, otp, channel, deviceId }, ct);
            var body = await resp.Content.ReadAsStringAsync(ct);
            if (!resp.IsSuccessStatusCode)
                return new CustomerOtpVerifyResult { Success = false, Message = ExtractErrorMessage(body) };

            var data = System.Text.Json.JsonSerializer.Deserialize<CustomerIdentityResponseDto>(body,
                new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            if (data?.CustomerToken == null)
                return new CustomerOtpVerifyResult { Success = false, Message = "Phản hồi không hợp lệ." };

            return new CustomerOtpVerifyResult
            {
                Success = true,
                CustomerToken = data.CustomerToken,
                CustomerId = data.CustomerId,
                FullName = data.FullName ?? "",
                Message = "Đăng nhập thành công!"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "CustomerAuthHttpService.VerifyOtpAsync failed");
            return new CustomerOtpVerifyResult { Success = false, Message = "Lỗi kết nối. Vui lòng thử lại." };
        }
    }

    private static string ExtractErrorMessage(string body)
    {
        try
        {
            var doc = System.Text.Json.JsonDocument.Parse(body);
            if (doc.RootElement.TryGetProperty("error", out var err))
                return err.GetString() ?? "Đã có lỗi xảy ra.";
        }
        catch { }
        return "Đã có lỗi xảy ra. Vui lòng thử lại.";
    }

    private class OtpSendResponseDto
    {
        public string Message { get; set; } = "";
        public string Channel { get; set; } = "email";
        public string ContactSuffix { get; set; } = "";
    }

    private class CustomerIdentityResponseDto
    {
        public Guid CustomerId { get; set; }
        public string? FullName { get; set; }
        public string? CustomerToken { get; set; }
    }
}

public class CustomerOtpSendResult
{
    public bool Success { get; set; }
    public string Channel { get; set; } = "";
    public string ContactSuffix { get; set; } = "";
    public string Message { get; set; } = "";
}

public class CustomerOtpVerifyResult
{
    public bool Success { get; set; }
    public string CustomerToken { get; set; } = "";
    public Guid CustomerId { get; set; }
    public string FullName { get; set; } = "";
    public string Message { get; set; } = "";
}
