namespace VanAn.CoreHub.Services;

/// <summary>
/// Wave 1 [W1-T2] — Email channel abstraction.
/// Implemented by BrevoEmailService. Can be swapped for SendGrid, SES, etc.
/// </summary>
public interface IEmailService
{
    /// <summary>
    /// Sends a transactional email with HTML content.
    /// </summary>
    /// <param name="toEmail">Recipient email address</param>
    /// <param name="subject">Email subject line</param>
    /// <param name="htmlContent">HTML body (supports template variables)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if sent successfully; false on error</returns>
    Task<bool> SendEmailAsync(string toEmail, string subject, string htmlContent, CancellationToken cancellationToken = default);
}
