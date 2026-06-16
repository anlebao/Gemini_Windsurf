using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using VanAn.CoreHub.Services.Orchestration;

namespace VanAn.CoreHub.Infrastructure.Messaging;

/// <summary>
/// EInvoiceWorker - Background service for processing Outbox events
/// Uses IServiceScopeFactory to safely consume Scoped services from a Singleton-lifetime host.
/// Dead Letter Queue: events exceeding max retry are logged for manual review.
/// </summary>
public class EInvoiceWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<EInvoiceWorker> _logger;
    private readonly TimeSpan _processingInterval = TimeSpan.FromSeconds(30);
    private readonly int _maxRetryCount = 5;

    public EInvoiceWorker(
        IServiceScopeFactory scopeFactory,
        ILogger<EInvoiceWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessOutboxEventsAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Error processing outbox events");
            }

            await Task.Delay(_processingInterval, stoppingToken);
        }
    }

    private async Task ProcessOutboxEventsAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var outboxRepository = scope.ServiceProvider.GetRequiredService<IOutboxRepository>();
        var orchestrator = scope.ServiceProvider.GetRequiredService<IEInvoiceOrchestrator>();

        var pendingEvents = await outboxRepository.GetPendingEventsAsync(batchSize: 50, cancellationToken);

        foreach (var outboxEvent in pendingEvents)
        {
            try
            {
                await orchestrator.SubmitInvoiceAsync(outboxEvent.InvoiceId, cancellationToken);
                await outboxRepository.MarkAsProcessedAsync(outboxEvent.OutboxEventId, cancellationToken);

                _logger.LogInformation("Processed outbox event {EventId} for invoice {InvoiceId}",
                    outboxEvent.OutboxEventId, outboxEvent.InvoiceId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to process outbox event {EventId}", outboxEvent.OutboxEventId);

                await outboxRepository.MarkAsFailedAsync(
                    outboxEvent.OutboxEventId,
                    ex.Message,
                    cancellationToken);

                if (outboxEvent.RetryCount >= _maxRetryCount)
                {
                    _logger.LogWarning("Outbox event {EventId} exceeded max retries ({MaxRetry}). Requires manual review.",
                        outboxEvent.OutboxEventId, _maxRetryCount);
                }
            }
        }
    }
}
