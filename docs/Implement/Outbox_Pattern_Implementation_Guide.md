# Outbox Pattern Implementation Guide

## Overview

This guide provides comprehensive documentation for implementing the Outbox Pattern in the Van An ecosystem, ensuring reliable event publishing with exactly-once delivery semantics and built-in retry mechanisms.

## What is the Outbox Pattern?

The Outbox Pattern is a design pattern that ensures reliable event publishing by storing events in the same database transaction as the business operation, then processing them asynchronously. This prevents data inconsistency between the application database and the event store.

### Problem Solved
- **Traditional Approach**: Direct event publishing can fail after database commit
- **Outbox Solution**: Events stored locally, published reliably via background processing
- **Guarantee**: Either both business operation AND event publishing succeed, or neither does

## Architecture Overview

```
┌─────────────────┐    ┌──────────────────┐    ┌─────────────────┐
│   Application   │───▶│   Database       │───▶│   Outbox Table  │
│   (Business     │    │   (Transaction)  │    │   (Events)      │
│    Logic)       │    │                  │    │                 │
└─────────────────┘    └──────────────────┘    └─────────────────┘
                                                      │
                                                      ▼
┌─────────────────┐    ┌──────────────────┐    ┌─────────────────┐
│   Event Bus     │◀───│  Outbox         │◀───│   Background    │
│   (NATS)        │    │  Processor      │    │   Service       │
│                 │    │                 │    │                 │
└─────────────────┘    └──────────────────┘    └─────────────────┘
```

## Implementation Components

### 1. OutboxMessage Entity

```csharp
public sealed class OutboxMessage : BaseEntity
{
    public string EventType { get; private set; } = string.Empty;
    public string Payload { get; private set; } = string.Empty;
    public DateTime OccurredOn { get; private set; }
    public DateTime? ProcessedOn { get; private set; }
    public int RetryCount { get; private set; } = 0;
    public string? LastError { get; private set; }
    public DateTime? NextRetryAt { get; private set; }
    
    // Factory method for creation
    public static OutboxMessage Create(string eventType, object payload)
    {
        return new OutboxMessage
        {
            Id = Guid.NewGuid(),
            EventType = eventType,
            Payload = JsonSerializer.Serialize(payload, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            }),
            OccurredOn = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow
        };
    }
    
    // State transition methods
    public void MarkAsProcessed() 
    {
        ProcessedOn = DateTime.UtcNow;
        NextRetryAt = null;
        LastError = null;
        UpdatedAt = DateTime.UtcNow;
    }
    
    public void MarkAsFailed(string error)
    {
        RetryCount++;
        LastError = error;
        
        // Exponential backoff: 1, 2, 4, 8, 16 minutes
        var delayMinutes = Math.Min(60, Math.Pow(2, RetryCount));
        NextRetryAt = DateTime.UtcNow.AddMinutes(delayMinutes);
        UpdatedAt = DateTime.UtcNow;
    }
}
```

### 2. OutboxMessage Configuration

```csharp
public class OutboxMessageConfiguration : IEntityTypeConfiguration<OutboxMessage>
{
    public void Configure(EntityTypeBuilder<OutboxMessage> builder)
    {
        builder.ToTable("OutboxMessages");
        
        builder.HasKey(x => x.Id);
        
        builder.Property(x => x.EventType)
            .IsRequired()
            .HasMaxLength(100);
            
        builder.Property(x => x.Payload)
            .IsRequired();
            
        builder.Property(x => x.OccurredOn)
            .IsRequired();
            
        builder.Property(x => x.LastError)
            .HasMaxLength(500);
            
        // Performance index for processing
        builder.HasIndex(x => new { x.ProcessedOn, x.NextRetryAt, x.OccurredOn })
            .HasFilter("ProcessedOn IS NULL");
            
        // Tenant isolation
        builder.HasQueryFilter(x => x.TenantId == _currentTenantId);
    }
}
```

### 3. SimpleOutboxProcessor

```csharp
public class SimpleOutboxProcessor : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<SimpleOutboxProcessor> _logger;
    private readonly TimeSpan _processingInterval = TimeSpan.FromSeconds(1);
    private const int BATCH_SIZE = 15;
    private const int ERROR_DELAY_MS = 5000;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessOutboxMessagesAsync(stoppingToken);
                await Task.Delay(_processingInterval, stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in outbox processor loop");
                await Task.Delay(ERROR_DELAY_MS, stoppingToken);
            }
        }
    }

    public async Task ProcessOutboxMessagesAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ShopERPDbContext>();
        var natsConnection = scope.ServiceProvider.GetRequiredService<IConnection>();

        // Get messages ready for processing
        var messages = await context.Set<OutboxMessage>()
            .Where(m => m.ProcessedOn == null && 
                       (m.NextRetryAt == null || m.NextRetryAt <= DateTime.UtcNow))
            .OrderBy(m => m.OccurredOn)
            .Take(BATCH_SIZE)
            .ToListAsync(cancellationToken);

        if (messages.Count == 0)
        {
            return;
        }

        _logger.LogInformation("Processing {Count} outbox messages", messages.Count);

        foreach (var message in messages)
        {
            try
            {
                await PublishMessageAsync(message, natsConnection);
                
                message.MarkAsProcessed();
                _logger.LogDebug("Successfully processed outbox message {MessageId}", message.Id);
            }
            catch (Exception ex)
            {
                message.MarkAsFailed(ex.Message);
                _logger.LogWarning(ex, "Failed to process outbox message {MessageId}", message.Id);
            }
        }

        await context.SaveChangesAsync(cancellationToken);
    }

    private async Task PublishMessageAsync(OutboxMessage message, IConnection connection)
    {
        var subject = GetSubjectForEventType(message.EventType);
        await connection.PublishAsync(subject, Encoding.UTF8.GetBytes(message.Payload));
    }

    private static string GetSubjectForEventType(string eventType)
    {
        return eventType.ToLowerInvariant() switch
        {
            "ordercompleted" => "orders.completed",
            "accountingentrycreated" => "accounting.entries.created",
            "hdkbooksgenerated" => "accounting.hdk.generated",
            _ => $"events.{eventType.ToLowerInvariant()}"
        };
    }
}
```

## Event Types and Payloads

### 1. OrderCompletedEvent

```csharp
public sealed record OrderCompletedEvent
{
    public Guid EventId { get; init; }
    public Guid OrderId { get; init; }
    public Guid? CustomerId { get; init; }
    public string CustomerDeviceId { get; init; } = string.Empty;
    public TenantId TenantId { get; init; }
    public decimal TotalAmount { get; init; }
    public List<OrderItemEvent> Items { get; init; } = new();
    public decimal SubTotal { get; init; }
    public decimal TotalVatAmount { get; init; }
    public DateTime CompletedAt { get; init; }
    public string? TrackingCode { get; init; }
}
```

### 2. AccountingEntryCreatedEvent

```csharp
public sealed record AccountingEntryCreatedEvent
{
    public Guid EventId { get; init; }
    public Guid EntryId { get; init; }
    public TenantId TenantId { get; init; }
    public AccountingEntryType EntryType { get; init; }
    public decimal Amount { get; init; }
    public string Description { get; init; } = string.Empty;
    public AccountingBookType BookType { get; init; }
    public DateTime TransactionDate { get; init; }
    public int PeriodYear { get; init; }
    public int PeriodMonth { get; init; }
    public Guid? ReversalEntryId { get; init; }
}
```

### 3. HKDBooksGeneratedEvent

```csharp
public sealed record HKDBooksGeneratedEvent
{
    public Guid EventId { get; init; }
    public TenantId TenantId { get; init; }
    public int PeriodYear { get; init; }
    public int PeriodMonth { get; init; }
    public decimal TotalRevenue { get; init; }
    public decimal TotalExpenses { get; init; }
    public decimal NetProfit { get; init; }
    public int EntryCount { get; init; }
    public DateTime GeneratedAt { get; init; }
}
```

## Integration with Business Logic

### 1. OrderQueueService Integration

```csharp
public class OrderQueueService : BackgroundService
{
    private async Task ProcessBatchAsync(List<Order> orders, CancellationToken stoppingToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ShopERPDbContext>();

        foreach (var order in orders)
        {
            try
            {
                // Process order business logic
                await ProcessOrderAsync(order, context, stoppingToken);
                
                // Create outbox message for completed order
                if (order.Status.Value == "completed")
                {
                    var outboxMessage = OutboxMessage.Create(EventTypes.OrderCompleted, new OrderCompletedEvent
                    {
                        EventId = Guid.NewGuid(),
                        OrderId = order.Id,
                        CustomerId = order.CustomerId,
                        CustomerDeviceId = order.CustomerDeviceId ?? $"device_{Guid.NewGuid():N}",
                        TenantId = order.TenantId,
                        TotalAmount = order.TotalAmount,
                        Items = order.Items.Select(i => new OrderItemEvent
                        {
                            ProductId = i.ProductId,
                            ProductName = i.ProductName,
                            Quantity = i.Quantity,
                            UnitPrice = i.UnitPrice,
                            TotalAmount = i.TotalAmount
                        }).ToList(),
                        SubTotal = order.SubTotal,
                        TotalVatAmount = order.TotalVatAmount,
                        CompletedAt = order.CompletedAt ?? DateTime.UtcNow,
                        TrackingCode = order.TrackingCode
                    });

                    await context.OutboxMessages.AddAsync(outboxMessage, stoppingToken);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing order {OrderId}", order.Id);
            }
        }

        await context.SaveChangesAsync(stoppingToken);
    }
}
```

### 2. Accounting Integration

```csharp
public class SimpleAccountingEventHandler : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await using var connection = new ConnectionFactory()
            .CreateConnection(_natsUrl);

        await connection.SubscribeAsyncAsync("orders.completed", async (args) =>
        {
            try
            {
                var eventData = JsonSerializer.Deserialize<OrderCompletedEvent>(args.Message.Data);
                if (eventData == null) return;

                // Create accounting entry
                var entry = new AccountingEntry(
                    eventData.TenantId,
                    AccountingEntryType.Revenue,
                    eventData.TotalAmount,
                    $"Order completion - {eventData.OrderId}",
                    AccountingBookType.RevenueBook,
                    eventData.CompletedAt
                );

                // Save accounting entry
                using var scope = _serviceProvider.CreateScope();
                var context = scope.ServiceProvider.GetRequiredService<CoreHubDbContext>();
                
                await context.AccountingEntries.AddAsync(entry, stoppingToken);
                await context.SaveChangesAsync(stoppingToken);

                // Create outbox message for accounting entry
                var outboxMessage = OutboxMessage.Create(EventTypes.AccountingEntryCreated, new AccountingEntryCreatedEvent
                {
                    EventId = Guid.NewGuid(),
                    EntryId = entry.Id,
                    TenantId = entry.TenantId,
                    EntryType = entry.EntryType,
                    Amount = entry.Amount,
                    Description = entry.Description,
                    BookType = entry.BookType,
                    TransactionDate = entry.TransactionDate,
                    PeriodYear = entry.PeriodYear,
                    PeriodMonth = entry.PeriodMonth
                });

                await context.OutboxMessages.AddAsync(outboxMessage, stoppingToken);
                await context.SaveChangesAsync(stoppingToken);

                _logger.LogInformation("Created accounting entry {EntryId} for order {OrderId}", 
                    entry.Id, eventData.OrderId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing order completed event");
            }
        });

        await Task.Delay(Timeout.Infinite, stoppingToken);
    }
}
```

## Configuration and Setup

### 1. Database Configuration

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Data Source=vanan_shoperp.db"
  }
}
```

### 2. NATS Configuration

```json
{
  "NATS": {
    "Url": "nats://localhost:4222",
    "MaxReconnectAttempts": 10,
    "ReconnectWait": 2000
  }
}
```

### 3. Outbox Configuration

```json
{
  "Outbox": {
    "BatchSize": 15,
    "ProcessingIntervalMs": 1000,
    "ErrorDelayMs": 5000,
    "MaxRetryMinutes": 60
  }
}
```

### 4. Service Registration

```csharp
// In Program.cs
services.AddDbContext<ShopERPDbContext>(options =>
    options.UseSqlite(connectionString));

services.AddSingleton<IConnection>(sp =>
    new ConnectionFactory().CreateConnection(natsUrl));

services.AddHostedService<SimpleOutboxProcessor>();
```

## Monitoring and Observability

### 1. Logging Strategy

```csharp
// Structured logging with correlation
_logger.LogInformation("Processing outbox message {MessageId} of type {EventType}", 
    message.Id, message.EventType);

_logger.LogWarning(ex, "Failed to process outbox message {MessageId}, attempt {AttemptCount}", 
    message.Id, message.RetryCount);

_logger.LogInformation("Successfully processed {ProcessedCount} outbox messages", 
    processedCount);
```

### 2. Metrics Collection

```csharp
public class OutboxMetrics
{
    public int TotalMessages { get; set; }
    public int ProcessedMessages { get; set; }
    public int FailedMessages { get; set; }
    public int PendingMessages { get; set; }
    public double ProcessingRate { get; set; }
    public TimeSpan AverageProcessingTime { get; set; }
    public Dictionary<string, int> MessagesByType { get; set; } = new();
}
```

### 3. Health Checks

```csharp
public class OutboxHealthCheck : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context, 
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var scope = _serviceProvider.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<ShopERPDbContext>();
            
            var pendingCount = await dbContext.OutboxMessages
                .CountAsync(m => m.ProcessedOn == null, cancellationToken);
                
            var failedCount = await dbContext.OutboxMessages
                .CountAsync(m => m.RetryCount > 5 && m.ProcessedOn == null, cancellationToken);

            if (pendingCount > 1000)
            {
                return HealthCheckResult.Unhealthy(
                    $"High pending message count: {pendingCount}");
            }

            if (failedCount > 10)
            {
                return HealthCheckResult.Degraded(
                    $"High failed message count: {failedCount}");
            }

            return HealthCheckResult.Healthy(
                $"Pending: {pendingCount}, Failed: {failedCount}");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("Database connection failed", ex);
        }
    }
}
```

## Testing Strategy

### 1. Unit Tests

```csharp
public class OutboxMessageTests
{
    [Fact]
    public void Create_ShouldInitializeCorrectly()
    {
        // Arrange
        var eventType = "OrderCompleted";
        var payload = new { OrderId = Guid.NewGuid() };

        // Act
        var message = OutboxMessage.Create(eventType, payload);

        // Assert
        Assert.Equal(eventType, message.EventType);
        Assert.NotNull(message.Payload);
        Assert.Null(message.ProcessedOn);
        Assert.Equal(0, message.RetryCount);
    }

    [Fact]
    public void MarkAsProcessed_ShouldSetProcessedOn()
    {
        // Arrange
        var message = OutboxMessage.Create("Test", new { });

        // Act
        message.MarkAsProcessed();

        // Assert
        Assert.NotNull(message.ProcessedOn);
        Assert.Null(message.LastError);
        Assert.Null(message.NextRetryAt);
    }

    [Fact]
    public void MarkAsFailed_ShouldIncrementRetryCount()
    {
        // Arrange
        var message = OutboxMessage.Create("Test", new { });
        var error = "Test error";

        // Act
        message.MarkAsFailed(error);

        // Assert
        Assert.Equal(1, message.RetryCount);
        Assert.Equal(error, message.LastError);
        Assert.NotNull(message.NextRetryAt);
    }
}
```

### 2. Integration Tests

```csharp
public class OutboxProcessorIntegrationTests : IDisposable
{
    private readonly ServiceProvider _serviceProvider;
    private readonly ShopERPDbContext _context;
    private readonly SimpleOutboxProcessor _processor;

    public OutboxProcessorIntegrationTests()
    {
        var services = new ServiceCollection();
        services.AddDbContext<ShopERPDbContext>(options =>
            options.UseSqlite("DataSource=:memory:"));
        
        services.AddSingleton<IConnection>(sp =>
            Mock.Of<IConnection>());
        
        services.AddSingleton<ILogger<SimpleOutboxProcessor>>(
            Mock.Of<ILogger<SimpleOutboxProcessor>>());
        
        services.AddSingleton<SimpleOutboxProcessor>();

        _serviceProvider = services.BuildServiceProvider();
        _context = _serviceProvider.GetRequiredService<ShopERPDbContext>();
        _processor = _serviceProvider.GetRequiredService<SimpleOutboxProcessor>();
        
        _context.Database.EnsureCreated();
    }

    [Fact]
    public async Task ProcessOutboxMessagesAsync_ShouldProcessMessages()
    {
        // Arrange
        var message = OutboxMessage.Create(EventTypes.OrderCompleted, new OrderCompletedEvent
        {
            EventId = Guid.NewGuid(),
            OrderId = Guid.NewGuid(),
            TenantId = new TenantId(Guid.NewGuid()),
            TotalAmount = 100.50m,
            CompletedAt = DateTime.UtcNow
        });

        await _context.OutboxMessages.AddAsync(message);
        await _context.SaveChangesAsync();

        // Act
        await _processor.ProcessOutboxMessagesAsync(CancellationToken.None);

        // Assert
        var processedMessage = await _context.OutboxMessages.FindAsync(message.Id);
        Assert.NotNull(processedMessage.ProcessedOn);
        Assert.Equal(0, processedMessage.RetryCount);
    }
}
```

## Performance Optimization

### 1. Database Optimization

```sql
-- Create efficient indexes
CREATE INDEX IX_OutboxMessages_Processing 
ON OutboxMessages (ProcessedOn, NextRetryAt, OccurredOn)
WHERE ProcessedOn IS NULL;

-- Partition by tenant for large datasets
CREATE TABLE OutboxMessages_Partitioned (
    -- Same columns as OutboxMessages
) PARTITION BY LIST (TenantId);
```

### 2. Batch Processing Optimization

```csharp
// Optimize batch size based on load
private int GetOptimalBatchSize(int pendingCount)
{
    return pendingCount switch
    {
        < 100 => 15,    // Small load - frequent small batches
        < 1000 => 50,   // Medium load - larger batches
        _ => 100        // High load - maximum batch size
    };
}
```

### 3. Memory Management

```csharp
// Use streaming for large payloads
public async Task ProcessLargePayloadAsync(OutboxMessage message)
{
    await using var stream = new MemoryStream(Encoding.UTF8.GetBytes(message.Payload));
    await using var reader = new StreamReader(stream);
    
    // Process in chunks if needed
    var chunk = await reader.ReadLineAsync();
    while (chunk != null)
    {
        // Process chunk
        chunk = await reader.ReadLineAsync();
    }
}
```

## Troubleshooting Guide

### Common Issues

#### 1. Message Processing Stuck
**Symptoms**: Messages not being processed, growing queue
**Causes**: 
- NATS connection issues
- Database connection problems
- Processor service not running

**Solutions**:
```bash
# Check NATS connection
nats-server -js -m 8222 -sd /tmp/nats

# Monitor outbox table
sqlite3 vanan_shoperp.db "
SELECT COUNT(*) FROM OutboxMessages 
WHERE ProcessedOn IS NULL;
"

# Check processor logs
docker logs vanan-shoperp-outbox-processor
```

#### 2. High Retry Count
**Symptoms**: Messages with high retry counts
**Causes**:
- Persistent serialization errors
- NATS subject not found
- Network connectivity issues

**Solutions**:
```csharp
// Add validation before publishing
private void ValidateMessage(OutboxMessage message)
{
    if (string.IsNullOrWhiteSpace(message.EventType))
        throw new ArgumentException("EventType is required");
        
    if (string.IsNullOrWhiteSpace(message.Payload))
        throw new ArgumentException("Payload is required");
}
```

#### 3. Database Performance Issues
**Symptoms**: Slow processing, high CPU usage
**Causes**:
- Missing indexes
- Large payload sizes
- Database locks

**Solutions**:
```sql
-- Analyze query performance
EXPLAIN QUERY PLAN 
SELECT * FROM OutboxMessages 
WHERE ProcessedOn IS NULL 
ORDER BY OccurredOn 
LIMIT 15;

-- Optimize indexes
ANALYZE OutboxMessages;

-- Check database size
SELECT 
    name,
    SUM(length(payload)) as total_payload_size
FROM OutboxMessages 
GROUP BY name 
ORDER BY total_payload_size DESC;
```

### Debug Commands

```sql
-- Check message status distribution
SELECT 
    CASE 
        WHEN ProcessedOn IS NOT NULL THEN 'Processed'
        WHEN RetryCount > 5 THEN 'Failed'
        ELSE 'Pending'
    END as Status,
    COUNT(*) as Count
FROM OutboxMessages 
GROUP BY Status;

-- Find oldest pending messages
SELECT 
    Id,
    EventType,
    OccurredOn,
    RetryCount
FROM OutboxMessages 
WHERE ProcessedOn IS NULL 
ORDER BY OccurredOn ASC 
LIMIT 10;

-- Check error patterns
SELECT 
    LastError,
    COUNT(*) as Count
FROM OutboxMessages 
WHERE RetryCount > 0 
GROUP BY LastError 
ORDER BY Count DESC;
```

## Best Practices

### 1. Event Design
- Keep events immutable
- Include all necessary data (no lazy loading)
- Use meaningful event names
- Version events for compatibility

### 2. Error Handling
- Log all failures with context
- Implement circuit breakers for external services
- Use exponential backoff for retries
- Monitor failed message patterns

### 3. Performance
- Use appropriate batch sizes
- Monitor database performance
- Implement message archiving for old data
- Use connection pooling

### 4. Security
- Validate event payloads
- Use encrypted connections for NATS
- Implement access controls
- Audit event publishing

---

**Document Version**: 1.0  
**Last Updated**: April 28, 2026  
**Author**: Van An Development Team
