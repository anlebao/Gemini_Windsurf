using VanAn.CoreHub.Domain;

namespace VanAn.CoreHub.Services;

/// <summary>
/// Service interface for Facebook Lead integration
/// Handles webhook processing and lead creation from Facebook Lead Ads
/// </summary>
public interface IFacebookLeadService
{
    /// <summary>
    /// Processes a Facebook webhook payload to create or update a lead
    /// </summary>
    /// <param name="payload">Facebook webhook payload data</param>
    /// <returns>The created or updated FacebookLead entity</returns>
    Task<FacebookLead> ProcessFacebookWebhookAsync(FacebookWebhookPayload payload);

    /// <summary>
    /// Validates the signature of a Facebook webhook request
    /// </summary>
    /// <param name="signature">X-Hub-Signature header value</param>
    /// <param name="payload">Raw request body</param>
    /// <returns>True if signature is valid, false otherwise</returns>
    Task<bool> ValidateFacebookWebhookAsync(string signature, string payload);
}
