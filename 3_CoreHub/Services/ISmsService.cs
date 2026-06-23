namespace VanAn.CoreHub.Services;

/// <summary>
/// Wave 1 [W1-T3] — SMS channel abstraction.
/// Implemented by EsmsNotificationService. Can be swapped for other SMS gateways.
/// </summary>
public interface ISmsService
{
    /// <summary>
    /// Sends an SMS message. Supports Unicode (tiếng Việt).
    /// </summary>
    /// <param name="phoneNumber">Recipient phone number (Vietnamese format: 0912345678 or +84912345678)</param>
    /// <param name="message">SMS message body (Unicode supported)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if sent successfully; false on error after retry</returns>
    Task<bool> SendSmsAsync(string phoneNumber, string message, CancellationToken cancellationToken = default);
}
