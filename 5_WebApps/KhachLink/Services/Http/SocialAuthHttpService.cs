using System.Net.Http.Json;
using System.Net.Http.Headers;

namespace VanAn.KhachLink.Services.Http;

/// <summary>
/// Tiered Auth Phase 3: HTTP client for loyalty redeem endpoints.
/// KhachLink calls Gateway → ShopERP via YARP.
/// All methods require X-Customer-Token header (authenticated customer).
/// CC-S1-T0c (v1.5): OTP upgrade methods were removed — modal used 3 buttons only.
/// 2026-11-21: re-added — backend now delivers real OTP via IIdentityOtpChannel (email-first, Brevo;
/// phone → SMS eSMS fallback). Methods below call /api/customer-identity/upgrade/*.
/// </summary>
public class SocialAuthHttpService(IHttpClientFactory httpClientFactory, ILogger<SocialAuthHttpService> logger)
{
    private readonly HttpClient _httpClient = httpClientFactory.CreateClient("gateway");
    private readonly ILogger<SocialAuthHttpService> _logger = logger;

    /// <summary>POST /api/loyalty/redeem — deduct points. Returns 403 payload if IdentityLevel insufficient.</summary>
    public async Task<RedeemResult> RedeemPointsAsync(string customerToken, int points, string reason)
    {
        try
        {
            var request = new HttpRequestMessage(HttpMethod.Post, "/api/loyalty/redeem");
            request.Headers.Add("X-Customer-Token", customerToken);
            request.Content = JsonContent.Create(new { Points = points, Reason = reason });

            var resp = await _httpClient.SendAsync(request);
            var body = await resp.Content.ReadAsStringAsync();

            if (resp.IsSuccessStatusCode)
            {
                var data = System.Text.Json.JsonSerializer.Deserialize<RedeemSuccessResponse>(body);
                return new RedeemResult
                {
                    Success = true,
                    NewBalance = data?.NewBalance ?? 0,
                    PointsRedeemed = data?.PointsRedeemed ?? points
                };
            }

            if (resp.StatusCode == System.Net.HttpStatusCode.Forbidden)
            {
                var blocked = System.Text.Json.JsonSerializer.Deserialize<RedeemBlockedResponse>(body);
                return new RedeemResult
                {
                    Success = false,
                    RequiresUpgrade = blocked?.RequiresUpgrade ?? false,
                    CurrentLevel = blocked?.CurrentLevel ?? "",
                    RequiredLevel = blocked?.RequiredLevel ?? "",
                    Message = blocked?.Error ?? "Không đủ quyền để đổi điểm."
                };
            }

            return new RedeemResult
            {
                Success = false,
                Message = ExtractErrorMessage(body)
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception during RedeemPointsAsync");
            return new RedeemResult { Success = false, Message = "Lỗi kết nối. Vui lòng thử lại." };
        }
    }

    /// <summary>
    /// POST /api/customer-identity/upgrade/send-otp — server chọn kênh email-first
    /// (email đã đăng ký → Brevo; fallback SĐT → SMS). Trả channel + contactSuffix đã mask.
    /// </summary>
    public async Task<OtpSendResult> SendUpgradeOtpAsync(string customerToken)
    {
        try
        {
            var request = new HttpRequestMessage(HttpMethod.Post, "/api/customer-identity/upgrade/send-otp");
            request.Headers.Add("X-Customer-Token", customerToken);
            var resp = await _httpClient.SendAsync(request);
            var body = await resp.Content.ReadAsStringAsync();
            if (!resp.IsSuccessStatusCode)
                return new OtpSendResult { Success = false, Message = ExtractErrorMessage(body) };
            var data = System.Text.Json.JsonSerializer.Deserialize<UpgradeSendOtpResponseDto>(body);
            return new OtpSendResult
            {
                Success = true,
                Channel = data?.Channel ?? "email",
                ContactSuffix = data?.ContactSuffix ?? data?.PhoneNumberSuffix ?? "",
                Message = data?.Message ?? "Mã xác minh đã được gửi."
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception during SendUpgradeOtpAsync");
            return new OtpSendResult { Success = false, Message = "Lỗi kết nối. Vui lòng thử lại." };
        }
    }

    /// <summary>POST /api/customer-identity/upgrade/verify-otp — verify → IdentityLevel=Verified.</summary>
    public async Task<OtpVerifyResult> VerifyUpgradeOtpAsync(string customerToken, string otp, string? channel)
    {
        try
        {
            var request = new HttpRequestMessage(HttpMethod.Post, "/api/customer-identity/upgrade/verify-otp");
            request.Headers.Add("X-Customer-Token", customerToken);
            request.Content = JsonContent.Create(new { Otp = otp, Channel = channel });
            var resp = await _httpClient.SendAsync(request);
            var body = await resp.Content.ReadAsStringAsync();
            if (!resp.IsSuccessStatusCode)
                return new OtpVerifyResult { Success = false, Message = ExtractErrorMessage(body) };
            return new OtpVerifyResult { Success = true, Message = "Nâng cấp xác thực thành công!" };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception during VerifyUpgradeOtpAsync");
            return new OtpVerifyResult { Success = false, Message = "Lỗi kết nối. Vui lòng thử lại." };
        }
    }

    private class UpgradeSendOtpResponseDto
    {
        public string Message { get; set; } = "";
        public string PhoneNumberSuffix { get; set; } = "";
        public string Channel { get; set; } = "email";
        public string ContactSuffix { get; set; } = "";
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

    // Response DTOs (match ShopERP controller response shapes)
    private class RedeemSuccessResponse
    {
        public bool Success { get; set; }
        public int NewBalance { get; set; }
        public int PointsRedeemed { get; set; }
    }

    private class RedeemBlockedResponse
    {
        public string Error { get; set; } = "";
        public bool RequiresUpgrade { get; set; }
        public string CurrentLevel { get; set; } = "";
        public string RequiredLevel { get; set; } = "";
    }
}

// Public result types for Blazor components
public class RedeemResult
{
    public bool Success { get; set; }
    public bool RequiresUpgrade { get; set; }
    public string CurrentLevel { get; set; } = "";
    public string RequiredLevel { get; set; } = "";
    public int NewBalance { get; set; }
    public int PointsRedeemed { get; set; }
    public string Message { get; set; } = "";
}

// OTP upgrade result types (2026-11-21 email-first)
public class OtpSendResult
{
    public bool Success { get; set; }
    public string Channel { get; set; } = "";
    public string ContactSuffix { get; set; } = "";
    public string Message { get; set; } = "";
}

public class OtpVerifyResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = "";
}
