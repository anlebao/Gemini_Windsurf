using Microsoft.Extensions.Logging;

namespace VanAn.CoreHub.Services;

/// <summary>
/// Wave 1 [W1-T4] — Composite notification service.
/// Implements INotificationService by delegating:
///   - Email channel → IEmailService (BrevoEmailService)
///   - SMS channel   → ISmsService (EsmsNotificationService)
///   - Push channel  → logged/no-op (not implemented in Wave 1)
/// 
/// Registered as INotificationService in DI — replaces the stub NotificationService.
/// The original NotificationService.cs is preserved but no longer registered.
/// </summary>
public class CompositeNotificationService(
    IEmailService emailService,
    ISmsService smsService,
    ILogger<CompositeNotificationService> logger) : INotificationService
{
    private readonly IEmailService _emailService = emailService;
    private readonly ISmsService _smsService = smsService;
    private readonly ILogger<CompositeNotificationService> _logger = logger;

    /// <inheritdoc />
    public async Task<bool> SendEmailAsync(string email, string subject, string message)
    {
        // Wrap plain-text message in minimal HTML for Brevo compatibility
        var htmlContent = $"<p>{System.Net.WebUtility.HtmlEncode(message).Replace("\n", "<br/>")}</p>";
        return await _emailService.SendEmailAsync(email, subject, htmlContent);
    }

    /// <inheritdoc />
    public async Task<bool> SendSMSAsync(string phoneNumber, string message)
    {
        return await _smsService.SendSmsAsync(phoneNumber, message);
    }

    /// <inheritdoc />
    public Task<bool> SendPushNotificationAsync(Guid customerId, string title, string message)
    {
        // Push notifications are out of scope for Wave 1 — log and return true to avoid breaking callers
        _logger.LogInformation("[Push] Push notification skipped (Wave 1): Customer={CustomerId} Title={Title}", customerId, title);
        return Task.FromResult(true);
    }
}
