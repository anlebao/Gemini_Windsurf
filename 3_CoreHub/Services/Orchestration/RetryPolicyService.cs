using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using VanAn.Shared.Domain;

namespace VanAn.CoreHub.Services.Orchestration;

/// <summary>
/// RetryPolicyService - Retry logic implementation
/// Focused service: ONLY retry logic
/// </summary>
public class RetryPolicyService : IRetryPolicyService
{
    private const int MaxRetryCount = 3;
    private static readonly TimeSpan[] BackoffDelays =
    [
        TimeSpan.FromSeconds(1),
        TimeSpan.FromSeconds(2),
        TimeSpan.FromSeconds(4)
    ];

    private readonly Func<ElectronicInvoiceId, CancellationToken, Task> _submitAction;
    private readonly ILogger<RetryPolicyService> _logger;

    public RetryPolicyService(
        Func<ElectronicInvoiceId, CancellationToken, Task> submitAction)
        : this(submitAction, NullLogger<RetryPolicyService>.Instance) { }

    public RetryPolicyService(
        Func<ElectronicInvoiceId, CancellationToken, Task> submitAction,
        ILogger<RetryPolicyService> logger)
    {
        _submitAction = submitAction ?? throw new ArgumentNullException(nameof(submitAction));
        _logger = logger;
    }

    public async Task SubmitWithRetryAsync(
        ElectronicInvoiceId invoiceId,
        CancellationToken cancellationToken = default)
    {
        if (invoiceId is null || invoiceId.Value == Guid.Empty)
            throw new ArgumentException("InvoiceId must be a valid non-empty GUID.", nameof(invoiceId));

        Exception? lastException = null;
        int attempt = 0;

        while (ShouldRetry(attempt))
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (attempt > 0)
            {
                var delay = BackoffDelays[Math.Min(attempt - 1, BackoffDelays.Length - 1)];
                _logger.LogInformation(
                    "RetryPolicyService: backing off {DelayMs}ms before attempt {Attempt}/{Max} for invoice {InvoiceId}",
                    delay.TotalMilliseconds, attempt + 1, MaxRetryCount, invoiceId.Value);
                await Task.Delay(delay, cancellationToken);
            }

            attempt++;
            _logger.LogDebug(
                "RetryPolicyService: attempt {Attempt}/{Max} for invoice {InvoiceId}",
                attempt, MaxRetryCount, invoiceId.Value);

            try
            {
                await _submitAction(invoiceId, cancellationToken);
                _logger.LogInformation(
                    "RetryPolicyService: submission succeeded on attempt {Attempt}/{Max} for invoice {InvoiceId}",
                    attempt, MaxRetryCount, invoiceId.Value);
                return;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                lastException = ex;
                _logger.LogWarning(ex,
                    "RetryPolicyService: attempt {Attempt}/{Max} failed for invoice {InvoiceId}: {Error}",
                    attempt, MaxRetryCount, invoiceId.Value, ex.Message);
            }
        }

        _logger.LogError(lastException,
            "RetryPolicyService: all {Max} attempts exhausted for invoice {InvoiceId}",
            MaxRetryCount, invoiceId.Value);
        throw lastException!;
    }

    public bool ShouldRetry(int failureCount, string? errorDetails = null)
    {
        return failureCount < MaxRetryCount;
    }

    public int GetMaxRetryCount()
    {
        return MaxRetryCount;
    }
}
