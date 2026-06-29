# TASK CARD: W3-ADR-T1 — Implement INatsEventPublisher + NatsEventPublisher

**Wave:** 3 — Implement NatsSyncWorker
**Branch:** `feature/adr001-wave3-nats-worker`
**Estimated effort:** 1-2 hours
**Dependency:** Wave 2 complete ✅

---

## 1. GOAL & CONTEXT

Tạo interface `INatsEventPublisher` và implementation `NatsEventPublisher` trong `3_CoreHub/Infrastructure/Messaging/`.  
Đây là tầng publish — nhận `OutboxEvent` từ Outbox và gửi lên NATS subject.

**Pattern tham chiếu:** `IOutboxRepository` / `OutboxRepository` đã có trong cùng folder.

---

## 2. VERIFIED FACTS

| Fact | Source |
|------|--------|
| `NATS.Client` package đã có trong CoreHub.csproj | `3_CoreHub/VanAn.CoreHub.csproj` |
| `OutboxEvent` domain object tồn tại | `1_Shared/Domain/` (OutboxEvent) |
| `IOutboxRepository.GetPendingEventsAsync(batchSize)` đã có | `3_CoreHub/Infrastructure/Messaging/IOutboxRepository.cs` L20 |
| `IOutboxRepository.MarkAsProcessedAsync(id)` đã có | `3_CoreHub/Infrastructure/Messaging/IOutboxRepository.cs` L27 |
| `IOutboxRepository.MarkAsFailedAsync(id, error)` đã có | `3_CoreHub/Infrastructure/Messaging/IOutboxRepository.cs` L34 |
| `OutboxMessage.EventData` là JSON string | `3_CoreHub/Infrastructure/OutboxMessage.cs` L14 |
| Namespace pattern: `VanAn.CoreHub.Infrastructure.Messaging` | `OutboxRepository.cs` L5 |
| CoreHub là pure Class Library — KHÔNG có Exe | `.windsurfrules` hard stop |

---

## 3. IMPLEMENTATION SPEC

### 3.1 Interface: `INatsEventPublisher`

**File:** `3_CoreHub/Infrastructure/Messaging/INatsEventPublisher.cs`

```csharp
namespace VanAn.CoreHub.Infrastructure.Messaging;

/// <summary>
/// Contract for publishing events to NATS message broker.
/// Used by NatsSyncWorker to flush Outbox → NATS.
/// </summary>
public interface INatsEventPublisher : IDisposable
{
    /// <summary>
    /// Publish a raw byte payload to a NATS subject.
    /// </summary>
    Task PublishAsync(string subject, byte[] payload, CancellationToken cancellationToken = default);

    /// <summary>
    /// Check if the NATS connection is alive.
    /// </summary>
    bool IsConnected { get; }
}
```

### 3.2 Implementation: `NatsEventPublisher`

**File:** `3_CoreHub/Infrastructure/Messaging/NatsEventPublisher.cs`

```csharp
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using NATS.Client;

namespace VanAn.CoreHub.Infrastructure.Messaging;

/// <summary>
/// NATS.Client-based publisher for the Outbox → NATS sync path.
/// Registered as Singleton in edge/sync-worker DI.
/// </summary>
public sealed class NatsEventPublisher : INatsEventPublisher
{
    private readonly IConnection _connection;
    private readonly ILogger<NatsEventPublisher> _logger;
    private bool _disposed;

    public bool IsConnected => _connection.State == ConnState.CONNECTED;

    public NatsEventPublisher(IConfiguration configuration, ILogger<NatsEventPublisher> logger)
    {
        _logger = logger;
        var url = configuration.GetValue<string>("NATS__Url") ?? "nats://localhost:4222";

        var opts = ConnectionFactory.GetDefaultOptions();
        opts.Url = url;
        opts.MaxReconnect = 5;
        opts.ReconnectWait = 2000; // ms
        opts.Name = "shoperp-nats-sync";

        _connection = new ConnectionFactory().CreateConnection(opts);
        _logger.LogInformation("NatsEventPublisher connected to {Url}", url);
    }

    public Task PublishAsync(string subject, byte[] payload, CancellationToken cancellationToken = default)
    {
        if (!IsConnected)
        {
            _logger.LogWarning("NATS not connected, skipping publish to {Subject}", subject);
            return Task.CompletedTask;
        }

        _connection.Publish(subject, payload);
        _logger.LogDebug("Published {Bytes} bytes to NATS subject {Subject}", payload.Length, subject);
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _connection?.Drain();
        _connection?.Dispose();
        _disposed = true;
    }
}
```

### 3.3 Subject naming convention

| Event Type | NATS Subject |
|-----------|-------------|
| `order.created` | `vanan.shoperp.order.created` |
| `order.updated` | `vanan.shoperp.order.updated` |
| `loyalty.earned` | `vanan.shoperp.loyalty.earned` |
| Generic fallback | `vanan.shoperp.{eventType}` |

Subject được build từ `OutboxEvent.EventType`:
```csharp
var subject = $"vanan.shoperp.{outboxEvent.EventType.ToLowerInvariant()}";
```

---

## 4. HARDENING GATES

- [ ] `NatsEventPublisher` implement đúng `INatsEventPublisher`
- [ ] Constructor KHÔNG throw nếu NATS không available — log warning thay
- [ ] `Dispose()` gọi `Drain()` trước `Dispose()` để flush in-flight messages
- [ ] `IsConnected` check trước mỗi publish
- [ ] KHÔNG inject `VanAnDbContext` vào publisher — publisher thuần NATS
- [ ] CoreHub vẫn là Class Library — KHÔNG thêm `<OutputType>Exe</OutputType>`

---

## 5. UNIT TEST SPEC

**File:** `6_Tests/VanAn.Core.Tests/Infrastructure/Messaging/NatsEventPublisherTests.cs`

Test cases cần cover (dùng Mock/Fake, không cần NATS server thật):

```csharp
// Test 1: PublishAsync when connected → calls _connection.Publish
[Fact] void PublishAsync_WhenConnected_CallsNatsPublish()

// Test 2: PublishAsync when NOT connected → logs warning, returns gracefully
[Fact] void PublishAsync_WhenDisconnected_LogsWarning_DoesNotThrow()

// Test 3: Dispose → calls Drain then Dispose
[Fact] void Dispose_CallsDrainFirst()
```

**Approach:** Tạo `FakeNatsEventPublisher : INatsEventPublisher` cho unit test thay vì mock NATS.Client trực tiếp.

---

## 6. VALIDATION

```powershell
dotnet build c:/VibeCoding/Gemini_Windsurf/VanAn.sln
# Expected: 0 errors

dotnet test c:/VibeCoding/Gemini_Windsurf/6_Tests/VanAn.Core.Tests/ --filter "NatsEventPublisher"
# Expected: 3 tests pass
```

---

## 7. EXIT CRITERIA

- [ ] `INatsEventPublisher.cs` tạo mới trong `3_CoreHub/Infrastructure/Messaging/`
- [ ] `NatsEventPublisher.cs` tạo mới trong `3_CoreHub/Infrastructure/Messaging/`
- [ ] `NatsEventPublisher` không throw khi NATS offline
- [ ] Unit tests tạo trong `6_Tests/VanAn.Core.Tests/`
- [ ] `dotnet build VanAn.sln` 0 errors
- [ ] Proceed to W3-ADR-T2 (NatsSyncWorker)
