using System.Net.Http.Json;

namespace VanAn.KhachLink.Services.Http;

/// <summary>
/// CC-S6-T5: HTTP client for Collaborator SMS OTP verification endpoints.
/// KhachLink calls Gateway → CollaboratorVerificationController.
/// All collaborator methods require X-Customer-Token header.
/// </summary>
public class CollaboratorVerificationHttpService(IHttpClientFactory httpClientFactory, ILogger<CollaboratorVerificationHttpService> logger)
{
    private readonly HttpClient _httpClient = httpClientFactory.CreateClient("gateway");
    private readonly ILogger<CollaboratorVerificationHttpService> _logger = logger;

    /// <summary>GET /api/collaborator-verification/status — check if SMS verification is required.</summary>
    public async Task<VerificationStatusResult> GetStatusAsync(string customerToken)
    {
        try
        {
            var request = new HttpRequestMessage(HttpMethod.Get, "/api/collaborator-verification/status");
            request.Headers.Add("X-Customer-Token", customerToken);

            var resp = await _httpClient.SendAsync(request);
            var body = await resp.Content.ReadAsStringAsync();

            if (resp.IsSuccessStatusCode)
            {
                var data = System.Text.Json.JsonSerializer.Deserialize<StatusResponse>(body,
                    new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                return new VerificationStatusResult
                {
                    Success = true,
                    VerificationRequired = data?.VerificationRequired ?? false,
                    SmsVerificationEnabled = data?.SmsVerificationEnabled ?? false,
                    FeePerVerification = data?.FeePerVerification ?? 0m,
                    MinDeposit = data?.MinDeposit ?? 0m
                };
            }

            return new VerificationStatusResult { Success = false, ErrorMessage = body };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting verification status");
            return new VerificationStatusResult { Success = false, ErrorMessage = "Lỗi kết nối." };
        }
    }

    /// <summary>POST /api/collaborator-verification/init — initiate SMS OTP.</summary>
    public async Task<InitOtpResult> InitVerificationAsync(string customerToken, string phoneNumber)
    {
        try
        {
            var request = new HttpRequestMessage(HttpMethod.Post, "/api/collaborator-verification/init");
            request.Headers.Add("X-Customer-Token", customerToken);
            request.Content = JsonContent.Create(new { PhoneNumber = phoneNumber });

            var resp = await _httpClient.SendAsync(request);
            var body = await resp.Content.ReadAsStringAsync();

            if (resp.IsSuccessStatusCode)
            {
                var data = System.Text.Json.JsonSerializer.Deserialize<InitResponse>(body,
                    new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                return new InitOtpResult
                {
                    Success = true,
                    Message = data?.Message ?? "Đã gửi mã OTP.",
                    FeeDeducted = data?.FeeDeducted ?? 0m,
                    BalanceAfter = data?.BalanceAfter ?? 0m
                };
            }

            return new InitOtpResult { Success = false, ErrorMessage = body };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error initiating verification");
            return new InitOtpResult { Success = false, ErrorMessage = "Lỗi kết nối." };
        }
    }

    /// <summary>POST /api/collaborator-verification/verify — verify OTP code.</summary>
    public async Task<VerifyOtpResult> VerifyOtpAsync(string customerToken, string otpCode)
    {
        try
        {
            var request = new HttpRequestMessage(HttpMethod.Post, "/api/collaborator-verification/verify");
            request.Headers.Add("X-Customer-Token", customerToken);
            request.Content = JsonContent.Create(new { OtpCode = otpCode });

            var resp = await _httpClient.SendAsync(request);
            var body = await resp.Content.ReadAsStringAsync();

            if (resp.IsSuccessStatusCode)
            {
                return new VerifyOtpResult { Success = true };
            }

            return new VerifyOtpResult { Success = false, ErrorMessage = body };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error verifying OTP");
            return new VerifyOtpResult { Success = false, ErrorMessage = "Lỗi kết nối." };
        }
    }

    /// <summary>POST /api/collaborator-verification/deposit — deposit money for SMS fees.</summary>
    public async Task<DepositResult> DepositAsync(string customerToken, decimal amount)
    {
        try
        {
            var request = new HttpRequestMessage(HttpMethod.Post, "/api/collaborator-verification/deposit");
            request.Headers.Add("X-Customer-Token", customerToken);
            request.Content = JsonContent.Create(new { Amount = amount });

            var resp = await _httpClient.SendAsync(request);
            var body = await resp.Content.ReadAsStringAsync();

            if (resp.IsSuccessStatusCode)
            {
                return new DepositResult { Success = true };
            }

            return new DepositResult { Success = false, ErrorMessage = body };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error depositing");
            return new DepositResult { Success = false, ErrorMessage = "Lỗi kết nối." };
        }
    }

    // Response DTOs
    private class StatusResponse
    {
        public bool VerificationRequired { get; set; }
        public bool SmsVerificationEnabled { get; set; }
        public decimal FeePerVerification { get; set; }
        public decimal MinDeposit { get; set; }
    }

    private class InitResponse
    {
        public string Message { get; set; } = string.Empty;
        public decimal FeeDeducted { get; set; }
        public decimal BalanceAfter { get; set; }
    }

    // Result DTOs
    public class VerificationStatusResult
    {
        public bool Success { get; set; }
        public bool VerificationRequired { get; set; }
        public bool SmsVerificationEnabled { get; set; }
        public decimal FeePerVerification { get; set; }
        public decimal MinDeposit { get; set; }
        public string ErrorMessage { get; set; } = string.Empty;
    }

    public class InitOtpResult
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public decimal FeeDeducted { get; set; }
        public decimal BalanceAfter { get; set; }
        public string ErrorMessage { get; set; } = string.Empty;
    }

    public class VerifyOtpResult
    {
        public bool Success { get; set; }
        public string ErrorMessage { get; set; } = string.Empty;
    }

    public class DepositResult
    {
        public bool Success { get; set; }
        public string ErrorMessage { get; set; } = string.Empty;
    }
}
