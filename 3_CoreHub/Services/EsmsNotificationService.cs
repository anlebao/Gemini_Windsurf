using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace VanAn.CoreHub.Services;

/// <summary>
/// Wave 1 [W1-T3] — ESMS.vn SMS service.
/// Uses ESMS REST API v4 (https://esms.vn/api/v4/).
/// Supports Unicode tiếng Việt. Retries once on transient failure.
/// Config keys: Esms:ApiKey, Esms:SecretKey, Esms:BrandName, Esms:SmsType
/// </summary>
public class EsmsNotificationService : ISmsService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<EsmsNotificationService> _logger;
    private readonly string _apiKey;
    private readonly string _secretKey;
    private readonly string _brandName;
    private readonly int _smsType;

    private const string EsmsApiUrl = "https://rest.esms.vn/MainService.svc/json/SendMultipleMessage_V4_post_json/";

    public EsmsNotificationService(HttpClient httpClient, IConfiguration configuration, ILogger<EsmsNotificationService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
        _apiKey = configuration["Esms:ApiKey"] ?? string.Empty;
        _secretKey = configuration["Esms:SecretKey"] ?? string.Empty;
        _brandName = configuration["Esms:BrandName"] ?? "VanAn";
        _smsType = int.TryParse(configuration["Esms:SmsType"], out var t) ? t : 2;
        // SmsType: 2 = Unicode brandname, 4 = Unicode brandname (fixed), 8 = Vinaphone brandname
    }

    /// <summary>
    /// Sends SMS via ESMS API with 1 retry on transient failure.
    /// </summary>
    public async Task<bool> SendSmsAsync(string phoneNumber, string message, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_apiKey) || string.IsNullOrWhiteSpace(_secretKey))
        {
            _logger.LogWarning("[ESMS] ApiKey/SecretKey not configured. SMS to {Phone} skipped.", phoneNumber);
            return false;
        }

        if (string.IsNullOrWhiteSpace(phoneNumber))
        {
            _logger.LogWarning("[ESMS] Empty phone number. SMS skipped.");
            return false;
        }

        // Normalize phone: strip leading + for ESMS API
        var normalizedPhone = phoneNumber.TrimStart('+');

        // Attempt once, then retry once on failure
        for (var attempt = 1; attempt <= 2; attempt++)
        {
            try
            {
                var result = await SendOnceAsync(normalizedPhone, message, cancellationToken);
                if (result) return true;

                if (attempt < 2)
                    _logger.LogWarning("[ESMS] Attempt {Attempt} failed. Retrying for {Phone}...", attempt, phoneNumber);
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "[ESMS] HTTP error on attempt {Attempt} for {Phone}", attempt, phoneNumber);
                if (attempt == 2) return false;
            }
            catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
            {
                _logger.LogError(ex, "[ESMS] Timeout on attempt {Attempt} for {Phone}", attempt, phoneNumber);
                if (attempt == 2) return false;
            }
        }

        return false;
    }

    private async Task<bool> SendOnceAsync(string phone, string message, CancellationToken cancellationToken)
    {
        var payload = new
        {
            ApiKey = _apiKey,
            Content = message,
            Phone = phone,
            SecretKey = _secretKey,
            Brandname = _brandName,
            SmsType = _smsType,
            IsUnicode = 1  // 1 = Unicode (support tiếng Việt)
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, EsmsApiUrl);
        request.Content = JsonContent.Create(payload);

        var response = await _httpClient.SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("[ESMS] HTTP {Status} for {Phone}: {Body}", (int)response.StatusCode, phone, body);
            return false;
        }

        // ESMS returns CodeResult: 100 = success
        try
        {
            using var doc = JsonDocument.Parse(body);
            if (doc.RootElement.TryGetProperty("CodeResult", out var code))
            {
                var codeValue = code.GetString();
                if (codeValue == "100")
                {
                    _logger.LogInformation("[ESMS] SMS sent to {Phone}", phone);
                    return true;
                }

                _logger.LogError("[ESMS] API error CodeResult={Code} for {Phone}: {Body}", codeValue, phone, body);
                return false;
            }
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "[ESMS] Invalid JSON response for {Phone}: {Body}", phone, body);
        }

        return false;
    }
}
