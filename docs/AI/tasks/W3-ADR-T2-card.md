# TASK CARD: W3-ADR-T2 — Implement NatsSyncWorker BackgroundService

**Wave:** 3 — Implement NatsSyncWorker
**Branch:** `feature/adr001-wave3-nats-worker`
**Estimated effort:** 2-3 hours
**Dependency:** W3-ADR-T1 complete ✅ (`INatsEventPublisher` tồn tại)

---

## 1. GOAL & CONTEXT

Implement `NatsSyncWorker` — một `BackgroundService` poll Outbox theo interval, publish pending events lên NATS, rồi mark as processed.  

Worker này sẽ được activate qua `--sync-worker` command-line arg (implement ở Wave 4).  
Trong Wave 3, chỉ viết class + tests — chưa đăng ký DI trong `Program.cs`.

---

## 2. VERIFIED FACTS

| Fact | Source |
|------|--------|
| `IOutboxRepository.GetPendingEventsAsync(batchSize)` | `IOutboxRepository.cs` L20 |
| `IOutboxRepository.MarkAsProcessedAsync(id)` | `IOutboxRepository.cs` L27 |
| `IOutboxRepository.MarkAsFailedAsync(id, error)` | `IOutboxRepository.cs` L34 |
| `OutboxEvent.OutboxEventId` là Guid (PK) | `1_Shared/Domain/` |
| `OutboxEvent.EventType` là string | `1_Shared/Domain/` |
| `OutboxEvent.EventData` là JSON string | `1_Shared/Domain/` |
| `INatsEventPublisher.PublishAsync(subject, payload)` | W3-ADR-T1 |
| `INatsEventPublisher.IsConnected` | W3-ADR-T1 |
| Namespace: `VanAn.CoreHub.Services` | `3_CoreHub/Services/` folder |
| CoreHub là Class Library — KHÔNG có `WebApplication` | hard stop |

---

## 3. IMPLEMENTATION SPEC

### File: `3_CoreHub/Services/NatsSyncWorker.cs`

```csharp
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using VanAn.CoreHub.Infrastructure.Messaging;

namespace VanAn.CoreHub.Services;

/// <summary>
/// BackgroundService that polls the Outbox and publishes pending events to NATS.
/// Activated when ShopERP starts with --sync-worker argument.
/// Data flow: SQLite Outbox → NATS → CoreHub (PostgreSQL)
/// </summary>
public sealed class NatsSyncWorker : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly INatsEventPublisher _publisher;
    private readonly ILogger<NatsSyncWorker> _logger;
    private readonly TimeSpan _pollInterval;
    private readonly int _batchSize;

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
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("NatsSyncWorker started. PollInterval={Interval}ms, BatchSize={Batch}",
            _pollInterval.TotalMilliseconds, _batchSize);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessPendingEventsAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                // Graceful shutdown — expected
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

    private async Task ProcessPendingEventsAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var outbox = scope.ServiceProvider.GetRequiredService<IOutboxRepository>();

        var pendingEvents = await outbox.GetPendingEventsAsync(_batchSize, cancellationToken);

        if (!pendingEvents.Any()) return;

        _logger.LogDebug("NatsSyncWorker: processing {Count} pending events", pendingEvents.Count);

        foreach (var ev in pendingEvents)
        {
            try
            {
                var subject = BuildSubject(ev.EventType);
                var payload = Encoding.UTF8.GetBytes(ev.EventData);

                await _publisher.PublishAsync(subject, payload, cancellationToken);
                await outbox.MarkAsProcessedAsync(ev.OutboxEventId, cancellationToken);

                _logger.LogDebug("Published event {EventId} → {Subject}", ev.OutboxEventId, subject);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to publish event {EventId}, marking as failed", ev.OutboxEventId);
                await outbox.MarkAsFailedAsync(ev.OutboxEventId, ex.Message, cancellationToken);
            }
        }
    }

    private static string BuildSubject(string eventType)
    {
        // Normalize: "Order.Created" → "vanan.shoperp.order.created"
        var normalized = eventType.ToLowerInvariant().Replace('.', '.');
        return $"vanan.shoperp.{normalized}";
    }
}
```

---

## 4. DESIGN DECISIONS

| Decision | Lý do |
|----------|-------|
| `IServiceProvider` scope per poll cycle | Outbox repo cần fresh DbContext (avoid stale reads) |
| `INatsEventPublisher` singleton, injected trực tiếp | Publisher holds NATS connection — không tạo lại mỗi cycle |
| Catch per-event exception | 1 event fail không block cả batch |
| `OperationCanceledException` catch riêng | Graceful shutdown không log as error |
| `BuildSubject` lowercase normalize | NATS subject case-sensitive — chuẩn hóa tránh duplication |

---

## 5. HARDENING GATES

- [ ] KHÔNG inject `VanAnDbContext` trực tiếp vào constructor (dùng scope thay)
- [ ] KHÔNG sửa `IOutboxRepository` interface
- [ ] KHÔNG sửa Domain layer
- [ ] Per-event error handling — 1 fail không dừng cả batch
- [ ] `stoppingToken` được pass xuống mọi async call
- [ ] Worker log level: Info cho start/stop, Debug cho per-event

---

## 6. UNIT TEST SPEC

**File:** `6_Tests/VanAn.Core.Tests/Services/NatsSyncWorkerTests.cs`

```csharp
// Setup: Mock IOutboxRepository + FakeNatsEventPublisher + Mock ILogger + Mock IServiceProvider

// Test 1: ExecuteAsync when no pending events → no publish calls
[Fact] void ExecuteAsync_NoPendingEvents_DoesNotCallPublish()

// Test 2: ExecuteAsync with 2 pending events → publish 2 times, mark 2 as processed
[Fact] async Task ExecuteAsync_WithPendingEvents_PublishesAndMarksProcessed()

// Test 3: PublishAsync throws → event marked as failed, worker continues
[Fact] async Task ExecuteAsync_PublishFails_MarksEventAsFailed_ContinuesProcessing()

// Test 4: CancellationToken cancelled → worker exits gracefully (no exception)
[Fact] async Task ExecuteAsync_WhenCancelled_ExitsGracefully()
```

**Approach:** Dùng `Mock<IOutboxRepository>` (Moq đã có trong CoreHub csproj).

---

## 7. VALIDATION

```powershell
dotnet build c:/VibeCoding/Gemini_Windsurf/VanAn.sln
# Expected: 0 errors

dotnet test c:/VibeCoding/Gemini_Windsurf/6_Tests/VanAn.Core.Tests/ --filter "NatsSyncWorker"
# Expected: 4 tests pass
```

---

## 8. EXIT CRITERIA

- [ ] `NatsSyncWorker.cs` tạo mới trong `3_CoreHub/Services/`
- [ ] Worker poll → publish → mark processed flow hoạt động đúng
- [ ] Per-event error isolation hoạt động
- [ ] Unit tests 4/4 pass
- [ ] `dotnet build VanAn.sln` 0 errors
- [ ] Wave 3 COMPLETE → có thể sang Wave 4
