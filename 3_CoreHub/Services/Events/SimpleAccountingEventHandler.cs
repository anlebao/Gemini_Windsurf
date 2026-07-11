using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NATS.Client;
using System.Text;
using System.Text.Json;
using VanAn.Shared.Domain;
using VanAn.Shared.Domain.Events;
using VanAn.Shared.DTOs;

namespace VanAn.CoreHub.Services.Events
{
    /// <summary>
    /// Simple accounting event handler for processing OrderCompleted events
    /// Generates accounting entries and HKD books
    /// </summary>
    public class SimpleAccountingEventHandler(
        IServiceProvider serviceProvider,
        ILogger<SimpleAccountingEventHandler> logger,
        IConfiguration configuration) : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider = serviceProvider;
        private readonly ILogger<SimpleAccountingEventHandler> _logger = logger;
        private readonly IConfiguration _configuration = configuration;

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Accounting event handler started");

            string natsUrl = _configuration["NATS:Url"] ?? "nats://localhost:4222";

            // Degraded mode: retry NATS connection every 10s instead of crashing on startup.
            // This matches DataSyncSubscriber's degraded mode pattern — Gateway can start
            // without NATS and resume subscription when NATS becomes available.
            while (!stoppingToken.IsCancellationRequested)
            {
                IConnection? connection = CreateNatsConnection();
                if (connection == null)
                {
                    // NATS unavailable — log once and retry after 10s (no exception for control flow)
                    _logger.LogWarning(
                        "SimpleAccountingEventHandler: NATS unavailable at {Url}. Running in degraded mode — will retry in 10s.",
                        natsUrl);
                    try
                    {
                        await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                    continue;
                }

                try
                {
                    // W-1-T6: Subscribe to subject that matches NatsSyncWorker.BuildSubject output.
                    // NatsSyncWorker builds: "vanan.shoperp.{eventType.ToLowerInvariant().Replace('_', '.')}"
                    // For "OrderCompleted" → "vanan.shoperp.ordercompleted"
                    IAsyncSubscription subscription = connection.SubscribeAsync("vanan.shoperp.ordercompleted", async (sender, args) =>
                    {
                        await HandleOrderCompletedEventAsync(args.Message, stoppingToken);
                    });

                    _logger.LogInformation("Subscribed to OrderCompleted events (subject: vanan.shoperp.ordercompleted)");

                    // Keep running until cancellation
                    await Task.Delay(Timeout.Infinite, stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex,
                        "SimpleAccountingEventHandler: subscription error. Will retry connection in 10s.");
                    try
                    {
                        await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                }
            }

            _logger.LogInformation("Accounting event handler stopped");
        }

        /// <summary>
        /// Handles OrderCompleted event
        /// </summary>
        private async Task HandleOrderCompletedEventAsync(Msg message, CancellationToken cancellationToken)
        {
            try
            {
                string eventData = Encoding.UTF8.GetString(message.Data);
                OrderCompletedEvent? orderEvent = JsonSerializer.Deserialize<OrderCompletedEvent>(eventData, new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                });

                if (orderEvent == null)
                {
                    _logger.LogWarning("Failed to deserialize OrderCompleted event");
                    return;
                }

                _logger.LogInformation("Processing OrderCompleted event for Order {OrderId}", orderEvent.OrderId);

                using IServiceScope scope = _serviceProvider.CreateScope();

                // Create accounting entry
                IAccountingService accountingService = scope.ServiceProvider.GetRequiredService<IAccountingService>();
                AccountingEntryDto accountingEntry = await accountingService.CreateEntryAsync(new AccountingEntryDto
                {
                    TenantId = orderEvent.TenantId.Value,
                    Amount = orderEvent.TotalAmount,
                    Description = $"Order #{orderEvent.OrderId}",
                    CreatedAt = orderEvent.CompletedAt,
                    EntryType = AccountingEntryType.Revenue,
                    AccountingBookType = AccountingBookType.RevenueBook,
                    PeriodYear = orderEvent.CompletedAt.Year,
                    PeriodMonth = orderEvent.CompletedAt.Month,
                    TransactionDate = orderEvent.CompletedAt
                });

                _logger.LogInformation("Created accounting entry {EntryId} for order {OrderId}",
                    accountingEntry.Id, orderEvent.OrderId);

                // Generate HKD books (same container - direct call)
                IHKDBookService hkdService = scope.ServiceProvider.GetRequiredService<IHKDBookService>();
                AccountingEntry coreEntry = ConvertToCoreAccountingEntry(accountingEntry);
                _ = await hkdService.RecordRevenueAsync(
                    coreEntry.TenantId,
                    coreEntry.Amount,
                    coreEntry.Description,
                    coreEntry.CreatedAt,
                    industrySector: coreEntry.IndustrySector,
                    cancellationToken: cancellationToken);

                _logger.LogInformation("Generated HKD books for order {OrderId}", orderEvent.OrderId);

                // Acknowledge message
                message.Ack();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to process OrderCompleted event");
                // Don't acknowledge - NATS will redeliver
            }
        }

        /// <summary>
        /// Converts AccountingEntryDto to CoreAccountingEntry
        /// </summary>
        private static AccountingEntry ConvertToCoreAccountingEntry(AccountingEntryDto dto)
        {
            return AccountingEntry.CreateRevenue(
                new TenantId(dto.TenantId),
                AccountingPeriod.FromDateTime(dto.CreatedAt),
                new Money(dto.Amount),
                dto.Description);
        }

        /// <summary>
        /// Creates NATS connection. Returns null if unavailable (degraded mode).
        /// </summary>
        private IConnection? CreateNatsConnection()
        {
            string natsUrl = _configuration["NATS:Url"] ?? "nats://localhost:4222";

            try
            {
                IConnection connection = new ConnectionFactory().CreateConnection(natsUrl);
                _logger.LogDebug("Connected to NATS at {Url}", natsUrl);
                return connection;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to create NATS connection to {Url}", natsUrl);
                return null;
            }
        }
    }
}
