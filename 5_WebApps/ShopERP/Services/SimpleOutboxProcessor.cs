using Microsoft.EntityFrameworkCore;
using NATS.Client;
using System.Text;
using VanAn.ShopERP.Infrastructure;
using CoreOutboxMessage = VanAn.CoreHub.Infrastructure.OutboxMessage;

namespace VanAn.ShopERP.Services
{
    /// <summary>
    /// Simple outbox processor for publishing events to NATS
    /// Handles retry logic and error recovery
    /// </summary>
    public class SimpleOutboxProcessor(
        IServiceProvider serviceProvider,
        ILogger<SimpleOutboxProcessor> logger,
        INatsConnectionFactory natsConnectionFactory,
        string natsUrl = "nats://localhost:4222") : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider = serviceProvider;
        private readonly ILogger<SimpleOutboxProcessor> _logger = logger;
        private readonly INatsConnectionFactory _natsConnectionFactory = natsConnectionFactory;
        private readonly string _natsUrl = natsUrl;
        private readonly int _batchSize = 15;
        private readonly TimeSpan _processingInterval = TimeSpan.FromSeconds(4);
        private readonly TimeSpan _errorDelay = TimeSpan.FromSeconds(15);

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Outbox processor started");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await ProcessOutboxMessagesAsync(stoppingToken);
                    await Task.Delay(_processingInterval, stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Outbox processor error - entering delay period");
                    await Task.Delay(_errorDelay, stoppingToken);
                }
            }

            _logger.LogInformation("Outbox processor stopped");
        }

        /// <summary>
        /// Processes pending outbox messages
        /// </summary>
        public async Task ProcessOutboxMessagesAsync(CancellationToken cancellationToken)
        {
            using IServiceScope scope = _serviceProvider.CreateScope();
            ShopERPDbContext context = scope.ServiceProvider.GetRequiredService<ShopERPDbContext>();

            // Get messages ready for processing
            List<CoreOutboxMessage> messages = await context.Set<CoreOutboxMessage>()
                .Where(m => m.ProcessedAt == null &&
                           (m.NextRetryAt == null || m.NextRetryAt <= DateTime.UtcNow))
                .OrderBy(m => m.CreatedAt)
                .Take(_batchSize)
                .ToListAsync(cancellationToken);

            if (messages.Count == 0)
            {
                _logger.LogDebug("No outbox messages to process");
                return;
            }

            _logger.LogInformation("Processing {Count} outbox messages", messages.Count);

            int processedCount = 0;
            int failedCount = 0;

            foreach (CoreOutboxMessage? message in messages)
            {
                try
                {
                    await PublishMessageAsync(message);
                    message.MarkAsProcessed();
                    processedCount++;

                    _logger.LogDebug("Published outbox message {Id} - {EventType}",
                        message.Id, message.EventType);
                }
                catch (Exception ex)
                {
                    message.MarkAsFailed(ex.Message);
                    failedCount++;

                    _logger.LogWarning(ex, "Failed to publish outbox message {Id} (attempt {Attempt})",
                        message.Id, message.RetryCount);
                }
            }

            _ = await context.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Outbox processing completed: {Processed} processed, {Failed} failed",
                processedCount, failedCount);
        }

        /// <summary>
        /// Publishes single message to NATS
        /// </summary>
        private async Task PublishMessageAsync(CoreOutboxMessage message)
        {
            using IConnection connection = CreateNatsConnection();
            string subject = $"vanan.events.{message.EventType.ToLower(System.Globalization.CultureInfo.CurrentCulture)}";

            connection.Publish(subject, Encoding.UTF8.GetBytes(message.EventData));

            _logger.LogDebug("Published event {EventType} to subject {Subject}",
                message.EventType, subject);
        }

        /// <summary>
        /// Creates NATS connection using injected factory
        /// TODO: Consider connection pooling for better performance
        /// </summary>
        private IConnection CreateNatsConnection()
        {
            return _natsConnectionFactory.CreateConnection(_natsUrl);
        }
    }
}
