# TASK CARD — Order Lifecycle Wave -1: Sync Mechanism Fix (Outbox → NATS → PostgreSQL)

> **Status:** 📋 PLANNING — awaiting user review
> **Prerequisite:** Master plan approved · **Branch:** `feature/order-w-1-sync-mechanism-fix` (to create from `main`)
> **Estimated sessions:** 2-3
> **Gaps fixed:** S1 (NatsSyncWorker flag), S2 (SimpleOutboxProcessor commented), S3 (OutboxRepository wrong DbContext), S4 (missing DataSyncSubscriber), S5 (AccountingEventHandler not registered)

## Objective

Kích hoạt cơ chế đồng bộ SQLite (ShopERP) → PostgreSQL (Gateway/CoreHub) qua Outbox Pattern + NATS. Đây là nền tảng tuyệt đối — mọi wave sau (W0-W5) phụ thuộc data đã sync.

## Architecture (ADR-001 v2 — verified from docs/Architecture/ADR001-Station-Architecture.md)

```
ShopERP SQLite (vanan_shoperp.db)
  │
  │ 1. Order created/updated → OrderWorkflowService → OutboxRepository.EnqueueAsync
  │    → OutboxMessages table (SQLite) — Status=Pending
  │
  ↓ NatsSyncWorker (poll every 1s, batch 50)
  │
  │ 2. GetPendingEventsAsync → publish to NATS "vanan.shoperp.{eventType}"
  │    → MarkAsProcessedAsync (Status=Processed)
  │
  ↓ NATS Broker (nats://localhost:4222)
  │
  │ 3. Subscribers consume events:
  │    ├─ DataSyncSubscriber (NEW) → write Order/Customer to PostgreSQL
  │    ├─ SimpleAccountingEventHandler → create AccountingEntry + HKD books in PostgreSQL
  │    └─ PushNotificationBackgroundService → send Web Push (already works)
  │
  ↓ PostgreSQL (VanAnCoreHub)
  │
  │ 4. Data persisted — Gateway reads from PostgreSQL for accounting/loyalty
```

## Architecture Decisions (D8-D11, D9, D10, D11)

- **D8:** Fix S1-S5 trước W0 — sync phải hoạt động trước khi thêm SignalR
- **D9:** `OutboxRepository` đổi `VanAnDbContext` → `IVanAnDbContext` (ShopERP=SQLite, Gateway=PostgreSQL)
- **D10:** `NatsSyncWorker` chạy mặc định (config `Sync__Enabled=true`, default true)
- **D11:** Tạo `DataSyncSubscriber` BackgroundService subscribe NATS → write PostgreSQL (Gateway scope)

## Prerequisites (to verify in INVESTIGATE)

- [ ] `3_CoreHub/Infrastructure/Messaging/OutboxRepository.cs:13` — `private readonly VanAnDbContext _dbContext` (WRONG — should be `IVanAnDbContext`)
- [ ] `5_WebApps/ShopERP/Program.cs:102-108` — `if (args.Contains("--sync-worker"))` gates NatsSyncWorker
- [ ] `5_WebApps/ShopERP/Program.cs:115-116` — `// builder.Services.AddHostedService<SimpleOutboxProcessor>();` (commented out)
- [ ] `3_CoreHub/Services/Events/SimpleAccountingEventHandler.cs` — exists, subscribes `vanan.events.ordercompleted`, NOT registered
- [ ] `3_CoreHub/Services/NatsSyncWorker.cs` — uses `IOutboxRepository` + `INatsEventPublisher` (correct abstractions)
- [ ] `3_CoreHub/Services/OrderWorkflowService.cs:96-124` — `RecordOrderCompletedEvent` only logs, does NOT enqueue to Outbox
- [ ] `5_WebApps/ShopERP/Program.cs:99` — `AddSingleton<INatsEventPublisher, NatsEventPublisher>` (registered)
- [ ] `5_WebApps/ShopERP/Program.cs:105` — `AddScoped<IOutboxRepository, OutboxRepository>` (registered, but only inside `--sync-worker` block)
- [ ] `2_Gateway/Program.cs:63-64` — `AddDbContext<IVanAnDbContext, VanAnDbContext>(UseNpgsql)` (PostgreSQL)
- [ ] `5_WebApps/ShopERP/Program.cs:75-76` — `AddDbContext<ShopERPDbContext>(UseSqlite)` (SQLite)
- [ ] `5_WebApps/ShopERP/Program.cs:95` — `AddScoped<IVanAnDbContext>(provider => provider.GetRequiredService<ShopERPDbContext>())` (SQLite)

## Open Questions

| Q | Question | Default answer |
|---|----------|----------------|
| Q1 | `NatsSyncWorker` chạy ở ShopERP hay Gateway? | ShopERP (poll SQLite Outbox) — Gateway không có SQLite |
| Q2 | `DataSyncSubscriber` chạy ở ShopERP hay Gateway? | Gateway (write PostgreSQL) — Gateway có `VanAnDbContext` (PostgreSQL) |
| Q3 | `SimpleAccountingEventHandler` chạy ở ShopERP hay Gateway? | Gateway (needs `IAccountingService` + `IHKDBookService` → PostgreSQL) |
| Q4 | `IOutboxRepository` registration: trong `--sync-worker` block hay ngoài? | Ngoài (luôn registered) — NatsSyncWorker dùng nó |
| Q5 | `OrderWorkflowService` inject `IOutboxRepository`? | Yes (nullable) — enqueue events trong cùng transaction với order update |
| Q6 | `OutboxRepository.ToMessage` hardcode `invoiceId` — cần generalize? | Yes — serialize toàn bộ `EventData` thay vì wrap trong `{invoiceId, originalData}` |

## Files to Modify (estimated 8 files)

| File | Action | Lines |
|------|--------|-------|
| `3_CoreHub/Infrastructure/Messaging/OutboxRepository.cs` | UPDATE — `VanAnDbContext` → `IVanAnDbContext`, generalize `ToMessage` | +15 lines |
| `5_WebApps/ShopERP/Program.cs` | UPDATE — move `IOutboxRepository` registration outside `--sync-worker` block, register `NatsSyncWorker` by default (config flag), remove `SimpleOutboxProcessor` comment | +10 lines |
| `2_Gateway/Services/DataSyncSubscriber.cs` | CREATE — BackgroundService subscribe NATS → write PostgreSQL | +120 lines |
| `2_Gateway/Program.cs` | UPDATE — register `DataSyncSubscriber` + `SimpleAccountingEventHandler` as HostedServices | +5 lines |
| `3_CoreHub/Services/OrderWorkflowService.cs` | UPDATE — inject `IOutboxRepository?`, replace `RecordOrderCompletedEvent` log with `EnqueueAsync` | +20 lines |
| `3_CoreHub/Services/Events/SimpleAccountingEventHandler.cs` | UPDATE — fix NATS subject to match NatsSyncWorker output (`vanan.shoperp.ordercompleted`) | +3 lines |
| `5_WebApps/ShopERP/appsettings.Development.json` | UPDATE — add `Sync__Enabled: true` config | +2 lines |
| `6_Tests/VanAn.Core.Tests/Services/OutboxRepositoryTests.cs` | ADD — test with `IVanAnDbContext` mock (both SQLite + PostgreSQL scenarios) | +40 lines |

## Detailed Task List

### W-1-T1: Fix `OutboxRepository` — `VanAnDbContext` → `IVanAnDbContext` (S3)

```csharp
// 3_CoreHub/Infrastructure/Messaging/OutboxRepository.cs
// BEFORE:
public class OutboxRepository : IOutboxRepository
{
    private readonly VanAnDbContext _dbContext;
    public OutboxRepository(VanAnDbContext dbContext) { _dbContext = dbContext; }
    // ... uses _dbContext.OutboxMessages
}

// AFTER:
public class OutboxRepository : IOutboxRepository
{
    private readonly IVanAnDbContext _dbContext;
    public OutboxRepository(IVanAnDbContext dbContext) { _dbContext = dbContext; }
    // ... uses _dbContext.OutboxMessages (IVanAnDbContext has OutboxMessages DbSet)
}
```

**Note:** `IVanAnDbContext` must have `DbSet<OutboxMessage> OutboxMessages` — verify in interface. Both `VanAnDbContext` (PostgreSQL) and `ShopERPDbContext` (SQLite) implement `IVanAnDbContext`, so DI resolves correctly per scope.

### W-1-T2: Generalize `OutboxRepository.ToMessage` (R14)

```csharp
// BEFORE (line 67-87): wraps EventData in {invoiceId, originalData} — only works for Invoice events
private static OutboxMessage ToMessage(OutboxEvent e)
{
    var data = JsonSerializer.Serialize(new { invoiceId = e.InvoiceId.Value, originalData = e.EventData });
    // ...
}

// AFTER: serialize OutboxEvent directly — works for any event type
private static OutboxMessage ToMessage(OutboxEvent e)
{
    return new OutboxMessage
    {
        Id = e.OutboxEventId,
        EventType = e.EventType,
        EventData = e.EventData,  // ← Store raw EventData, no wrapping
        CreatedAt = DateTime.UtcNow,
        TenantId = e.TenantId,
        Status = MapToMessageStatus(e.Status),
        RetryCount = e.RetryCount,
        ProcessedAt = e.ProcessedAt,
        Error = e.ErrorDetails
    };
}
```

**Note:** `ToDomain` also needs update — remove `ExtractInvoiceId` hardcode, pass `Guid.Empty` or make `OutboxEvent` constructor accept null invoiceId.

### W-1-T3: Register `IOutboxRepository` + `NatsSyncWorker` outside `--sync-worker` block (S1)

```csharp
// 5_WebApps/ShopERP/Program.cs
// BEFORE (line 101-110): inside if (args.Contains("--sync-worker"))

// AFTER: always register IOutboxRepository
builder.Services.AddScoped<IOutboxRepository, OutboxRepository>();

// NatsSyncWorker: run by default, configurable via Sync__Enabled
bool syncEnabled = builder.Configuration.GetValue<bool>("Sync__Enabled", true);
if (syncEnabled)
{
    builder.Services.AddHostedService<NatsSyncWorker>();
    Log.Information("NatsSyncWorker registered (Sync__Enabled=true)");
}
else
{
    Log.Information("NatsSyncWorker disabled (Sync__Enabled=false)");
}

// Remove SimpleOutboxProcessor comment (S2) — NatsSyncWorker is the single processor
// Delete line 115-116: // builder.Services.AddHostedService<SimpleOutboxProcessor>();
```

### W-1-T4: Create `DataSyncSubscriber` (S4) — Gateway scope

```csharp
// 2_Gateway/Services/DataSyncSubscriber.cs
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NATS.Client;
using System.Text.Json;
using VanAn.CoreHub.Infrastructure;
using VanAn.Shared.Domain;

namespace VanAn.Gateway.Services
{
    /// <summary>
    /// Subscribes to NATS events from ShopERP (SQLite Outbox sync) and writes to PostgreSQL.
    /// Runs in Gateway scope (has VanAnDbContext = PostgreSQL).
    /// </summary>
    public class DataSyncSubscriber : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly IConfiguration _configuration;
        private readonly ILogger<DataSyncSubscriber> _logger;
        private IConnection? _connection;

        public DataSyncSubscriber(IServiceProvider serviceProvider, IConfiguration configuration, ILogger<DataSyncSubscriber> logger)
        {
            _serviceProvider = serviceProvider;
            _configuration = configuration;
            _logger = logger;
        }

        protected override Task ExecuteAsync(CancellationToken stoppingToken)
        {
            string url = _configuration.GetValue<string>("NATS:Url") ?? "nats://localhost:4222";
            try
            {
                var opts = ConnectionFactory.GetDefaultOptions();
                opts.Url = url;
                opts.MaxReconnect = 5;
                opts.ReconnectWait = 2000;
                opts.Name = "vanan-gateway-data-sync-subscriber";
                _connection = new ConnectionFactory().CreateConnection(opts);

                // Subscribe to all ShopERP sync events
                _ = _connection.SubscribeAsync("vanan.shoperp.>", async (sender, args) =>
                {
                    await HandleSyncEventAsync(args.Message.Data, stoppingToken);
                });

                _logger.LogInformation("DataSyncSubscriber connected to NATS {Url}, subscribed to vanan.shoperp.>", url);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "DataSyncSubscriber: NATS unavailable at {Url}. Running in degraded mode.", url);
            }

            return Task.CompletedTask;
        }

        private async Task HandleSyncEventAsync(byte[] data, CancellationToken cancellationToken)
        {
            try
            {
                string json = System.Text.Encoding.UTF8.GetString(data);
                using var doc = JsonDocument.Parse(json);
                string eventType = doc.RootElement.GetProperty("eventType").GetString() ?? "";

                using IServiceScope scope = _serviceProvider.CreateScope();
                var dbContext = scope.ServiceProvider.GetRequiredService<IVanAnDbContext>();

                switch (eventType.ToLowerInvariant())
                {
                    case "order.created":
                    case "ordercreated":
                        await SyncOrderAsync(doc.RootElement, dbContext, cancellationToken);
                        break;
                    case "order.statuschanged":
                    case "orderstatuschanged":
                        await SyncOrderStatusAsync(doc.RootElement, dbContext, cancellationToken);
                        break;
                    case "customer.created":
                    case "customercreated":
                        await SyncCustomerAsync(doc.RootElement, dbContext, cancellationToken);
                        break;
                    default:
                        _logger.LogDebug("DataSyncSubscriber: unhandled event type {EventType}", eventType);
                        break;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "DataSyncSubscriber: failed to process sync event");
            }
        }

        private async Task SyncOrderAsync(JsonElement data, IVanAnDbContext dbContext, CancellationToken ct)
        {
            // Deserialize order from event data → upsert to PostgreSQL
            // TODO: implement based on OrderEvent payload structure
            _logger.LogInformation("SyncOrderAsync: order sync to PostgreSQL");
        }

        private async Task SyncOrderStatusAsync(JsonElement data, IVanAnDbContext dbContext, CancellationToken ct)
        {
            // Update order status in PostgreSQL
            Guid orderId = data.GetProperty("orderId").GetGuid();
            string status = data.GetProperty("status").GetString() ?? "";
            var order = await dbContext.Orders.FirstOrDefaultAsync(o => o.Id == orderId, ct);
            if (order != null)
            {
                order.UpdateOrderStatus(new OrderStatusId(status));
                await dbContext.SaveChangesAsync(ct);
                _logger.LogInformation("Synced order {OrderId} status to {Status} in PostgreSQL", orderId, status);
            }
        }

        private async Task SyncCustomerAsync(JsonElement data, IVanAnDbContext dbContext, CancellationToken ct)
        {
            // TODO: implement customer sync
            _logger.LogInformation("SyncCustomerAsync: customer sync to PostgreSQL");
        }
    }
}
```

### W-1-T5: Register `DataSyncSubscriber` + `SimpleAccountingEventHandler` in Gateway (S4, S5)

```csharp
// 2_Gateway/Program.cs — add after other HostedService registrations
builder.Services.AddHostedService<VanAn.Gateway.Services.DataSyncSubscriber>();
builder.Services.AddHostedService<VanAn.CoreHub.Services.Events.SimpleAccountingEventHandler>();
```

### W-1-T6: Fix `SimpleAccountingEventHandler` NATS subject (S5)

```csharp
// 3_CoreHub/Services/Events/SimpleAccountingEventHandler.cs:35
// BEFORE: subscribes "vanan.events.ordercompleted"
// AFTER: subscribes "vanan.shoperp.ordercompleted" (matches NatsSyncWorker.BuildSubject output)
IAsyncSubscription subscription = connection.SubscribeAsync("vanan.shoperp.ordercompleted", async (sender, args) =>
```

### W-1-T7: `OrderWorkflowService` — enqueue events to Outbox (R12)

```csharp
// 3_CoreHub/Services/OrderWorkflowService.cs
// Add to constructor:
IOutboxRepository? outboxRepository = null

// Add field:
private readonly IOutboxRepository? _outboxRepository = outboxRepository;

// Replace RecordOrderCompletedEvent (line 96-124):
private void RecordOrderCompletedEvent(Order order)
{
    // BEFORE: only _logger.LogInformation("📋 OUTBOX EVENT: ...")
    // AFTER: enqueue to Outbox table (within same transaction)
    if (_outboxRepository == null)
    {
        _logger.LogWarning("OutboxRepository not available — event not persisted");
        return;
    }

    var outboxEvent = new OutboxEvent(
        order.TenantId,
        new ElectronicInvoiceId(Guid.Empty), // TODO: generalize OutboxEvent to not require InvoiceId
        "OrderCompleted",
        JsonSerializer.Serialize(new
        {
            orderId = order.Id,
            order.CustomerId,
            order.TotalAmount,
            order.Status.Value,
            completedAt = DateTime.UtcNow
        }));

    _ = _outboxRepository.EnqueueAsync(outboxEvent);
    _logger.LogInformation("Enqueued OrderCompleted event to Outbox for order {OrderId}", order.Id);
}
```

**Note:** `OutboxEvent` constructor requires `ElectronicInvoiceId` — this is a design issue (R14). For non-invoice events, pass `Guid.Empty`. Future refactor: make `OutboxEvent` generic (no InvoiceId requirement).

### W-1-T8: Add `Sync__Enabled` config

```json
// 5_WebApps/ShopERP/appsettings.Development.json
{
  "Sync": {
    "Enabled": true,
    "PollIntervalMs": 1000,
    "BatchSize": 50
  }
}
```

### W-1-T9: Build + verify

- `dotnet build VanAn.sln` — 0 errors
- Verify DI: `IOutboxRepository` resolves to `OutboxRepository` with `IVanAnDbContext` (SQLite in ShopERP, PostgreSQL in Gateway)
- Verify: `NatsSyncWorker` registered by default (no `--sync-worker` flag needed)
- Verify: `DataSyncSubscriber` registered in Gateway
- Verify: `SimpleAccountingEventHandler` registered in Gateway

## Verification Checklist

- [ ] Build 0 errors
- [ ] `OutboxRepository` injects `IVanAnDbContext` (not `VanAnDbContext`)
- [ ] `OutboxRepository.ToMessage` stores raw `EventData` (no `{invoiceId, originalData}` wrapping)
- [ ] `IOutboxRepository` registered in ShopERP (outside `--sync-worker` block)
- [ ] `NatsSyncWorker` registered by default (config `Sync__Enabled=true`)
- [ ] `SimpleOutboxProcessor` comment removed (NatsSyncWorker is single processor)
- [ ] `DataSyncSubscriber` created in Gateway, subscribes `vanan.shoperp.>`
- [ ] `DataSyncSubscriber` registered in Gateway Program.cs
- [ ] `SimpleAccountingEventHandler` registered in Gateway Program.cs
- [ ] `SimpleAccountingEventHandler` subscribes `vanan.shoperp.ordercompleted` (matches NatsSyncWorker output)
- [ ] `OrderWorkflowService.RecordOrderCompletedEvent` calls `_outboxRepository.EnqueueAsync` (not just log)
- [ ] `OrderWorkflowService` injects `IOutboxRepository?` (nullable — graceful if not registered)
- [ ] `Sync__Enabled` config in appsettings.Development.json
- [ ] Unit test: `OutboxRepository` works with `IVanAnDbContext` mock (SQLite + PostgreSQL)

## Rollback Plan

1. Revert `OutboxRepository` to inject `VanAnDbContext` (PostgreSQL only)
2. Revert `NatsSyncWorker` to `--sync-worker` flag gate
3. Delete `DataSyncSubscriber.cs`
4. Remove `DataSyncSubscriber` + `SimpleAccountingEventHandler` from Gateway Program.cs
5. Revert `OrderWorkflowService.RecordOrderCompletedEvent` to log-only
6. Build passes (pre-existing state — sync not active by default)

## Downstream Impact

| Wave | Impact | Note |
|------|--------|------|
| **W0** | `OrderWorkflowService` now enqueues Outbox events — SignalR broadcast can also trigger NATS publish | W0 adds `IOrderNotificationService` alongside existing NATS publish |
| **W1** | Kitchen → Ready transition will enqueue `OrderStatusChanged` event to Outbox | Same pattern as W-1-T7 |
| **W2** | Admin Orders UI reads from SQLite (ShopERP) — data syncs to PostgreSQL in background | No direct impact — UI uses ShopERP DbContext |
| **W3** | Payment confirm writes to PostgreSQL (Gateway) — `DataSyncSubscriber` syncs back to SQLite? | **Note:** Sync is SQLite→PostgreSQL only (one-way). Payment confirm goes directly to PostgreSQL via Gateway. ShopERP reads payment status from PostgreSQL via Gateway API. |
| **W5** | Tests verify Outbox → NATS → PostgreSQL sync flow | Integration tests with NATS test container |
