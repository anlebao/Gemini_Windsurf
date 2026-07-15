using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using VanAn.CoreHub.Infrastructure.Messaging;

namespace VanAn.CoreHub.Services;

/// <summary>
/// BackgroundService that polls the SQLite Outbox and publishes pending events to NATS.
///
/// ADR-001 v2 Edge data flow:
///   OrderWorkflow / InvoiceService → Outbox (SQLite)
///   → NatsSyncWorker (this class)
///   → NATS "vanan.shoperp.{eventType}" subject
///   → CoreHub subscriber (future) → PostgreSQL cloud sync
///
/// Activated when ShopERP starts with the --sync-worker CLI argument.
/// DI registration is done in Wave 4 (ADR001-W4 / ShopERP Program.cs).
/// </summary>
public sealed class NatsSyncWorker : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly INatsEventPublisher _publisher;
    private readonly ILogger<NatsSyncWorker> _logger;
    private readonly TimeSpan _pollInterval;
    private readonly int _batchSize;
    private readonly string _subjectPrefix;

    public NatsSyncWorker(
        IServiceProvider serviceProvider,
        INatsEventPublisher publisher,
        ILogger<NatsSyncWorker> logger,
        IConfiguration configuration)
    {
        _serviceProvider = serviceProvider;
        _publisher = publisher;
        _logger = logger;
        _pollInterval = TimeSpan.FromMilliseconds(
            configuration.GetValue<int>("Sync__PollIntervalMs", 1000));
        _batchSize = configuration.GetValue<int>("Sync__BatchSize", 50);
        // RC-2 fix: subject prefix separates PG→SQLite (vanan.cloud.*) from SQLite→PG (vanan.shoperp.*).
        // Gateway sets Sync:SubjectPrefix=cloud in appsettings.json; ShopERP (Edge) keeps default shoperp.
        _subjectPrefix = configuration.GetValue<string>("Sync:SubjectPrefix") ?? "shoperp";
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "NatsSyncWorker started. PollInterval={Interval}ms, BatchSize={Batch}",
            _pollInterval.TotalMilliseconds, _batchSize);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessPendingEventsAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                // Graceful shutdown — expected during app stop
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "NatsSyncWorker: unhandled error during poll cycle");
            }

            await Task.Delay(_pollInterval, stoppingToken);
        }

        _logger.LogInformation("NatsSyncWorker stopped.");
    }

    // ──────────────────────────────────────────────────────────
    // Private helpers
    // ──────────────────────────────────────────────────────────

    private async Task ProcessPendingEventsAsync(CancellationToken cancellationToken)
    {
        // Scope per poll cycle so DbContext is fresh (avoids EF change-tracker stale reads)
        using var scope = _serviceProvider.CreateScope();
        var outbox = scope.ServiceProvider.GetRequiredService<IOutboxRepository>();

        var pendingEvents = await outbox.GetPendingEventsAsync(_batchSize, cancellationToken);

        if (pendingEvents.Count == 0) return;

        _logger.LogInformation("NatsSyncWorker: processing {Count} pending event(s)", pendingEvents.Count);

        foreach (var ev in pendingEvents)
        {
            try
            {
                var subject = BuildSubject(ev.EventType, _subjectPrefix);
                var payload = Encoding.UTF8.GetBytes(ev.EventData);

                await _publisher.PublishAsync(subject, payload, cancellationToken);
                await outbox.MarkAsProcessedAsync(ev.OutboxEventId, cancellationToken);

                _logger.LogInformation(
                    "NatsSyncWorker: published event {EventId} type={EventType} → subject={Subject}",
                    ev.OutboxEventId, ev.EventType, subject);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "NatsSyncWorker: failed to publish event {EventId}, marking as failed",
                    ev.OutboxEventId);
                await outbox.MarkAsFailedAsync(ev.OutboxEventId, ex.Message, cancellationToken);
            }
        }
    }

    /// <summary>
    /// Builds a canonical NATS subject from the domain event type.
    /// "OrderCreated" → "vanan.{prefix}.order.created"
    /// "OrderStatusChanged" → "vanan.{prefix}.order.status.changed"
    /// RC-2 fix: prefix separates directions (cloud=PG→SQLite, shoperp=SQLite→PG).
    /// CamelCase split ensures subject matches subscriber expectations (dotted words).
    /// </summary>
    private static string BuildSubject(string eventType, string prefix)
    {
        // Split camelCase: "OrderCreated" → "order.created"
        var normalized = System.Text.RegularExpressions.Regex.Replace(
            eventType, "([a-z])([A-Z])", "$1.$2").ToLowerInvariant().Replace('_', '.');
        return $"vanan.{prefix}.{normalized}";
    }
}
