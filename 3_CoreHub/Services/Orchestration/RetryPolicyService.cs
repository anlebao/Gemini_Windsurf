namespace VanAn.CoreHub.Services.Orchestration;

/// <summary>
/// RetryPolicyService - Retry logic implementation
/// Focused service: ONLY retry logic
/// </summary>
public class RetryPolicyService : IRetryPolicyService
{
    private const int MaxRetryCount = 3;

    public async Task SubmitWithRetryAsync(
        VanAn.Shared.Domain.ElectronicInvoiceId invoiceId,
        System.Threading.CancellationToken cancellationToken = default)
    {
        // Stub: Submit with retry logic
        // TODO: Implement with actual retry loop and provider submission
        await Task.CompletedTask;
    }

    public bool ShouldRetry(int failureCount, string? errorDetails = null)
    {
        // Retry if failure count < max retry count
        return failureCount < MaxRetryCount;
    }

    public int GetMaxRetryCount()
    {
        return MaxRetryCount;
    }
}
