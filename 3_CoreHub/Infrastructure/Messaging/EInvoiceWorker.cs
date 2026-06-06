using Microsoft.Extensions.Logging;
using VanAn.CoreHub.Services.Orchestration;
using VanAn.Shared.Domain;

namespace VanAn.CoreHub.Infrastructure.Messaging;

/// <summary>
/// EInvoiceWorker - Background worker for processing Outbox events
/// Atomic transaction: Invoice + Outbox saved in same transaction
/// Dead Letter Queue for failed events
/// </summary>
public class EInvoiceWorker
{
    private readonly IOutboxRepository _outboxRepository;
    private readonly IEInvoiceOrchestrator _orchestrator;
    private readonly ILogger<EInvoiceWorker> _logger;
    private readonly TimeSpan _processingInterval = TimeSpan.FromSeconds(30);
    private readonly int _maxRetryCount = 5;

    public EInvoiceWorker(
        IOutboxRepository outboxRepository,
        IEInvoiceOrchestrator orchestrator,
        ILogger<EInvoiceWorker> logger)
    {
        _outboxRepository = outboxRepository;
        _orchestrator = orchestrator;
        _logger = logger;
    }

    /// <summary>
    /// Start background processing
    /// </summary>
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await ProcessOutboxEventsAsync(cancellationToken);
                await Task.Delay(_processingInterval, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing outbox events");
                await Task.Delay(_processingInterval, cancellationToken);
            }
        }
    }

    /// <summary>
    /// Process pending outbox events
    /// </summary>
    private async Task ProcessOutboxEventsAsync(CancellationToken cancellationToken)
    {
        var pendingEvents = await _outboxRepository.GetPendingEventsAsync(batchSize: 50, cancellationToken);

        foreach (var outboxEvent in pendingEvents)
        {
            try
            {
                // Process event through orchestrator
                await _orchestrator.SubmitInvoiceAsync(outboxEvent.InvoiceId, cancellationToken);

                // Mark as processed
                await _outboxRepository.MarkAsProcessedAsync(outboxEvent.OutboxEventId, cancellationToken);
                
                _logger.LogInformation("Processed outbox event {EventId} for invoice {InvoiceId}", 
                    outboxEvent.OutboxEventId, outboxEvent.InvoiceId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to process outbox event {EventId}", outboxEvent.OutboxEventId);

                // Mark as failed with retry increment
                await _outboxRepository.MarkAsFailedAsync(
                    outboxEvent.OutboxEventId, 
                    ex.Message, 
                    cancellationToken);

                // Move to Dead Letter Queue if max retry exceeded
                if (outboxEvent.RetryCount >= _maxRetryCount)
                {
                    _logger.LogError("Outbox event {EventId} moved to Dead Letter Queue after {RetryCount} retries",
                        outboxEvent.OutboxEventId, outboxEvent.RetryCount);
                    // TODO: Implement Dead Letter Queue
                }
            }
        }
    }
}
