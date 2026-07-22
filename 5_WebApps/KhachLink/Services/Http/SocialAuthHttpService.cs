using System.Net.Http.Json;
using System.Net.Http.Headers;

namespace VanAn.KhachLink.Services.Http;

/// <summary>
/// Tiered Auth Phase 3: HTTP client for identity upgrade + loyalty redeem endpoints.
/// KhachLink calls Gateway â†’ ShopERP via YARP.
/// All methods require X-Customer-Token header (authenticated customer).
/// </summary>
public class SocialAuthHttpService(IHttpClientFactory httpClientFactory, ILogger<SocialAuthHttpService> logger)
{
    private readonly HttpClient _httpClient = httpClientFactory.CreateClient("gateway");
    private readonly ILogger<SocialAuthHttpService> _logger = logger;

    /// <summary>POST /api/customer-identity/upgrade/send-otp â€” send OTP to customer's registered phone.</summary>
    public async Task<UpgradeSendOtpResult> SendUpgradeOtpAsync(string customerToken)
    {
        try
        {
            var request = new HttpRequestMessage(HttpMethod.Post, "/api/customer-identity/upgrade/send-otp");
            request.Headers.Add("X-Customer-Token", customerToken);

            var resp = await _httpClient.SendAsync(request);
            if (resp.IsSuccessStatusCode)
            {
                var body = await resp.Content.ReadFromJsonAsync<UpgradeSendOtpResponse>();
                return new UpgradeSendOtpResult
                {
                    Success = true,
                    Message = body?.Message ?? "OTP Ä‘Ã£ Ä‘Æ°á»£c gá»­i.",
                    PhoneNumberSuffix = body?.PhoneNumberSuffix ?? ""
                };
            }

            var error = await resp.Content.ReadAsStringAsync();
            _logger.LogWarning("Upgrade send-otp failed: {Status} {Error}", resp.StatusCode, error);
            return new UpgradeSendOtpResult { Success = false, Message = ExtractErrorMessage(error) };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception during SendUpgradeOtpAsync");
            return new UpgradeSendOtpResult { Success = false, Message = "Lá»—i káº¿t ná»‘i. Vui lÃ²ng thá»­ láº¡i." };
        }
    }

    /// <summary>POST /api/customer-identity/upgrade/verify-otp â€” verify OTP and upgrade to Verified.</summary>
    public async Task<UpgradeVerifyOtpResult> VerifyUpgradeOtpAsync(string customerToken, string otp)
    {
        try
        {
            var request = new HttpRequestMessage(HttpMethod.Post, "/api/customer-identity/upgrade/verify-otp");
            request.Headers.Add("X-Customer-Token", customerToken);
            request.Content = JsonContent.Create(new { Otp = otp });

            var resp = await _httpClient.SendAsync(request);
            if (resp.IsSuccessStatusCode)
            {
                var body = await resp.Content.ReadFromJsonAsync<UpgradeVerifyOtpResponse>();
                return new UpgradeVerifyOtpResult
                {
                    Success = true,
                    IdentityLevel = body?.IdentityLevel ?? "Verified",
                    Message = body?.Message ?? "NÃ¢ng cáº¥p thÃ nh cÃ´ng."
                };
            }

            var error = await resp.Content.ReadAsStringAsync();
            _logger.LogWarning("Upgrade verify-otp failed: {Status} {Error}", resp.StatusCode, error);
            return new UpgradeVerifyOtpResult { Success = false, Message = ExtractErrorMessage(error) };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception during VerifyUpgradeOtpAsync");
            return new UpgradeVerifyOtpResult { Success = false, Message = "Lá»—i káº¿t ná»‘i. Vui lÃ²ng thá»­ láº¡i." };
        }
    }

    /// <summary>POST /api/loyalty/redeem â€” deduct points. Returns 403 payload if IdentityLevel insufficient.</summary>
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
                    Message = blocked?.Error ?? "KhÃ´ng Ä‘á»§ quyá»n Ä‘á»ƒ Ä‘á»•i Ä‘iá»ƒm."
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
            return new RedeemResult { Success = false, Message = "Lá»—i káº¿t ná»‘i. Vui lÃ²ng thá»­ láº¡i." };
        }
    }

    private static string ExtractErrorMessage(string body)
    {
        try
        {
            var doc = System.Text.Json.JsonDocument.Parse(body);
            if (doc.RootElement.TryGetProperty("error", out var err))
                return err.GetString() ?? "ÄÃ£ cÃ³ lá»—i xáº£y ra.";
        }
        catch { }
        return "ÄÃ£ cÃ³ lá»—i xáº£y ra. Vui lÃ²ng thá»­ láº¡i.";
    }

    // Response DTOs (match ShopERP controller response shapes)
    private class UpgradeSendOtpResponse
    {
        public string Message { get; set; } = "";
        public string PhoneNumberSuffix { get; set; } = "";
    }

    private class UpgradeVerifyOtpResponse
    {
        public bool Success { get; set; }
        public string IdentityLevel { get; set; } = "";
        public string Message { get; set; } = "";
    }

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
public class UpgradeSendOtpResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = "";
    public string PhoneNumberSuffix { get; set; } = "";
}

public class UpgradeVerifyOtpResult
{
    public bool Success { get; set; }
    public string IdentityLevel { get; set; } = "";
    public string Message { get; set; } = "";
}

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
