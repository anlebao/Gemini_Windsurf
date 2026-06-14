using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace VanAn.CoreHub.Services;

/// <summary>
/// Background service for batch processing of electronic invoices.
/// Polls for pending invoices and submits them to e-invoice providers (Viettel, MISA).
/// TODO: Full implementation in Sprint 4 - E-Invoice batch processing phase.
/// </summary>
public class BatchInvoiceProcessor(ILogger<BatchInvoiceProcessor> logger) : BackgroundService
{
    private readonly ILogger<BatchInvoiceProcessor> _logger = logger;
    private static readonly TimeSpan PollingInterval = TimeSpan.FromMinutes(5);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("BatchInvoiceProcessor started. Polling every {Interval}.", PollingInterval);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessPendingInvoicesAsync(stoppingToken);
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                _logger.LogError(ex, "BatchInvoiceProcessor encountered an error during batch run.");
            }

            await Task.Delay(PollingInterval, stoppingToken);
        }
    }

    private Task ProcessPendingInvoicesAsync(CancellationToken cancellationToken)
    {
        // TODO: Sprint 4 - Query pending invoices, submit to e-invoice provider, update status
        _logger.LogDebug("BatchInvoiceProcessor: no pending invoices (stub).");
        return Task.CompletedTask;
    }
}
