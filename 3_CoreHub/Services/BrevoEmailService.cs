using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace VanAn.CoreHub.Services;

/// <summary>
/// Wave 1 [W1-T2] — Brevo (formerly Sendinblue) email service.
/// Uses Brevo Transactional Email REST API v3.
/// Free tier: 300 emails/day. Supports HTML templates.
/// Config keys: Brevo:ApiKey, Brevo:SenderEmail, Brevo:SenderName
/// </summary>
public class BrevoEmailService : IEmailService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<BrevoEmailService> _logger;
    private readonly string _apiKey;
    private readonly string _senderEmail;
    private readonly string _senderName;

    private const string BrevoApiUrl = "https://api.brevo.com/v3/smtp/email";

    public BrevoEmailService(HttpClient httpClient, IConfiguration configuration, ILogger<BrevoEmailService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
        _apiKey = configuration["Brevo:ApiKey"] ?? string.Empty;
        _senderEmail = configuration["Brevo:SenderEmail"] ?? "noreply@vanan.vn";
        _senderName = configuration["Brevo:SenderName"] ?? "Vạn An";
    }

    /// <summary>
    /// Sends a transactional email via Brevo API.
    /// Returns true on success, false on API error or misconfiguration.
    /// </summary>
    public async Task<bool> SendEmailAsync(string toEmail, string subject, string htmlContent, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_apiKey))
        {
            _logger.LogWarning("[Brevo] ApiKey not configured. Email to {Email} skipped.", toEmail);
            return false;
        }

        if (string.IsNullOrWhiteSpace(toEmail))
        {
            _logger.LogWarning("[Brevo] Empty recipient address. Email skipped.");
            return false;
        }

        try
        {
            var payload = new
            {
                sender = new { email = _senderEmail, name = _senderName },
                to = new[] { new { email = toEmail } },
                subject,
                htmlContent
            };

            using var request = new HttpRequestMessage(HttpMethod.Post, BrevoApiUrl);
            request.Headers.Add("api-key", _apiKey);
            request.Content = JsonContent.Create(payload);

            var response = await _httpClient.SendAsync(request, cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("[Brevo] Email sent to {Email} | Subject: {Subject}", toEmail, subject);
                return true;
            }

            var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogError("[Brevo] API error {StatusCode} sending to {Email}: {Body}",
                (int)response.StatusCode, toEmail, errorBody);
            return false;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "[Brevo] HTTP error sending email to {Email}", toEmail);
            return false;
        }
        catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogError(ex, "[Brevo] Timeout sending email to {Email}", toEmail);
            return false;
        }
    }
}
