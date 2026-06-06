using VanAn.Shared.Domain;

namespace VanAn.CoreHub.Services.Orchestration;

/// <summary>
/// IRetryPolicyService - Retry logic for provider submissions
/// Focused service: ONLY retry logic
/// </summary>
public interface IRetryPolicyService
{
    /// <summary>
    /// Submit invoice with retry logic
    /// </summary>
    Task SubmitWithRetryAsync(
        ElectronicInvoiceId invoiceId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Check if should retry based on failure count
    /// </summary>
    bool ShouldRetry(int failureCount, string? errorDetails = null);

    /// <summary>
    /// Get max retry count
    /// </summary>
    int GetMaxRetryCount();
}
