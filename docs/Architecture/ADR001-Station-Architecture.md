# ADR-001 Station Architecture Design

**Created:** 2026-06-29
**Status:** Design Draft (Revised for Two-Version Deployment)
**Purpose:** Define SQLite station deployment architecture for ADR-001 compliance

---

## Two-Version System Strategy

This project supports **two distinct deployment versions** with different database strategies:

| Version | Name | Target Customers | Database Strategy | Accounting |
|---------|------|-------------------|-------------------|------------|
| **v1** | SaaS Online | All customers (default) | PostgreSQL cloud | Online PostgreSQL |
| **v2** | Hybrid Edge/Cloud | Segment: vùng sâu, quán cà phê, mạng không ổn định | SQLite local + NATS + PostgreSQL cloud | **Always online PostgreSQL** |

### Version Boundaries

- **v1 (SaaS Online):** `docker-compose.prod.yml`
  - ShopERP dùng SQLite local file trong container (đã implement trong code)
  - CoreHub dùng PostgreSQL
  - KhachLink dùng HTTP via Gateway → CoreHub
  - KHÔNG có NATS sync workers (SQLite là local cache, không sync)

- **v2 (Hybrid Edge/Cloud):** `docker-compose.edge.yml`
  - ShopERP dùng SQLite với volume persist
  - NATS sync worker publish events từ SQLite Outbox
  - CoreHub vẫn PostgreSQL cho accounting
  - PostgreSQL là sync target cho order/loyalty data (không phải primary write)

### CI/CD Strategy

| Pipeline | File | Purpose | Trigger |
|----------|------|---------|---------|
| **ci.yml** | `.github/workflows/ci.yml` | Build + test v1 SaaS | Push to main, PR |
| **ci-edge.yml** | `.github/workflows/ci-edge.yml` | Build + test v2 edge | Push to feature/edge*, manual |
| **Shared** | Both | Architecture tests, unit tests | Reuse test projects |

**Recommendation:** Dùng chung codebase, tách CI/CD pipeline. v2 edge build chỉ khác ở docker-compose và feature flag.

---

## Current State (Architecture Drift)

### Codebase Status
- **ShopERP/Program.cs**: Đã dùng SQLite (`vanan_shoperp.db`) ✅
- **CoreHub/Program.cs**: Dùng PostgreSQL (accounting online) ✅
- **KhachLink/Program.cs**: Không dùng SQLite trực tiếp, dùng HTTP services ✅

### Deployment Status
- **docker-compose.prod.yml**: PostgreSQL-based, phù hợp v1 SaaS ✅
- **docker-compose.edge.yml**: **CHƯA TỒN TẠI** ❌
- **NATS sync worker**: **CHƯA TỒN TẠI** ❌
- **SQLite sidecar/volume**: **CHƯA TỒN TẠI** trong deployment ❌

### ADR-001 Requirements (Applicable to v2 only)
- **SQLite local**: Each station has SQLite for offline operation
- **NATS sync**: Background workers publish events from Outbox
- **PostgreSQL cloud**: Sync target for order/loyalty (accounting remains online)
- **Outbox pattern**: Events persisted before NATS publish

---

## Proposed Architecture

### v1: SaaS Online (docker-compose.prod.yml) - UNCHANGED

```
┌─────────────────────────────────────────┐
│         docker-compose.prod.yml         │
├─────────────────────────────────────────┤
│                                         │
│  ┌─────────┐  ┌─────────┐  ┌─────────┐ │
│  │ KhachLink│  │ Gateway │  │ ShopERP │ │
│  │ (HTTP)  │→│ (YARP)  │→│(SQLite) │ │
│  └─────────┘  └────┬────┘  └────┬────┘ │
│                    │            │       │
│                    └─────┬──────┘       │
│                          │              │
│                   ┌──────▼──────┐       │
│                   │   CoreHub   │       │
│                   │ (PostgreSQL)│       │
│                   └──────┬──────┘       │
│                          │              │
│                   ┌──────▼──────┐       │
│                   │  PostgreSQL │       │
│                   │   (Cloud)   │       │
│                   └─────────────┘       │
│                                         │
└─────────────────────────────────────────┘
```

- **KHÔNG THAY ĐỔI** docker-compose.prod.yml
- ShopERP vẫn dùng SQLite local file (non-persistent, single container)
- CoreHub vẫn PostgreSQL cho accounting
- KhachLink qua Gateway

### v2: Hybrid Edge/Cloud (docker-compose.edge.yml) - NEW

```
┌─────────────────────────────────────────────────────────────┐
│                    docker-compose.edge.yml                  │
├─────────────────────────────────────────────────────────────┤
│                                                             │
│  ┌─────────────────┐       ┌─────────────────┐           │
│  │  shoperp-sqlite │       │  khachlink-sqlite│           │
│  │  (SQLite Volume)│       │  (SQLite Volume) │           │
│  └───────┬─────────┘       └───────┬─────────┘           │
│          │                          │                      │
│          │                          │                      │
│  ┌───────▼──────┐          ┌───────▼──────┐              │
│  │    ShopERP   │          │   KhachLink  │              │
│  │   (Blazor)   │          │   (Blazor)   │              │
│  └──────┬───────┘          └──────┬───────┘              │
│         │                          │                      │
│         │                          │                      │
│         └──────────┬───────────────┘                      │
│                    │                                      │
│           ┌────────▼────────┐                            │
│           │ shoperp-nats-sync│                            │
│           │   (Worker)     │                            │
│           └────────┬────────┘                            │
│                    │                                      │
│           ┌────────▼────────┐                            │
│           │     NATS       │                            │
│           │   (Broker)     │                            │
│           └────────┬────────┘                            │
│                    │                                      │
│           ┌────────▼────────┐     ┌──────────────┐       │
│           │    CoreHub     │────→│  PostgreSQL  │       │
│           │  (PostgreSQL)   │     │   (Cloud)    │       │
│           └─────────────────┘     └──────────────┘       │
│                                                             │
└─────────────────────────────────────────────────────────────┘
```

### v2 Station Types

| Station | SQLite DB | Sync Worker | Purpose | Scope |
|---------|-----------|-------------|---------|-------|
| ShopERP | shoperp-sqlite | shoperp-nats-sync | Staff/admin operations | Order, loyalty, inventory |
| KhachLink | khachlink-sqlite | khachlink-nats-sync | Customer-facing PWA | Cart, loyalty, catalog |

**Note:** KhachLink hiện tại không dùng SQLite trong code. v2 edge có thể defer KhachLink SQLite đến wave sau, hoặc implement nếu cần.

### SQLite Deployment Strategy

**Option A: Sidecar Pattern (Recommended for v2)**
- Each app has a sidecar SQLite container
- SQLite DB file mounted as Docker volume
- Simple, isolated, easy to scale

**Option B: Shared SQLite Service**
- Single SQLite service with multiple databases
- More complex, single point of failure

**Decision: Option A (Sidecar)** for v2 edge deployment

---

## NATS Sync Worker Design

### Worker Responsibilities

```csharp
public class NatsSyncWorker : BackgroundService
{
    // 1. Poll Outbox table in SQLite
    // 2. Publish events to NATS
    // 3. Mark events as processed
    // 4. Handle retry logic
    // 5. Subscribe to NATS for updates from other stations
}
```

### Event Types

| Event | Source | Destination |
|-------|--------|-------------|
| OrderCreated | ShopERP | All stations |
| OrderUpdated | ShopERP | All stations |
| CustomerCreated | ShopERP | All stations |
| ProductUpdated | CoreHub | All stations |
| SyncAck | PostgreSQL | Source station |

---

## Migration Strategy

### Phase 1: Add SQLite Sidecars (Zero Downtime)
1. Add SQLite containers to docker-compose.prod.yml
2. Keep PostgreSQL as primary (read-write)
3. SQLite initialized from PostgreSQL (one-time sync)
4. NATS sync workers start but do NOT publish yet

### Phase 2: Switch to SQLite Primary
1. Update connection strings to point to SQLite
2. NATS sync workers start publishing
3. PostgreSQL becomes read-only sync target
4. Monitor for sync lag

### Phase 3: Remove PostgreSQL Direct Access
1. All writes go through SQLite → NATS → PostgreSQL
2. PostgreSQL accessed only via NATS consumer
3. Verify data consistency

### Rollback Plan

If Phase 2 fails:
1. Switch connection strings back to PostgreSQL
2. Stop NATS sync workers
3. Zero data loss (SQLite can be discarded)

---

## Configuration Changes

### docker-compose.prod.yml Changes

```yaml
# Add SQLite sidecar for ShopERP
shoperp-sqlite:
  image: alpine:latest
  container_name: vanan-shoperp-sqlite
  command: ["sh", "-c", "mkdir -p /data && tail -f /dev/null"]
  volumes:
    - shoperp_sqlite_data:/data
  networks:
    - vanan-network

# Add NATS sync worker for ShopERP
shoperp-nats-sync:
  image: ${IMAGE_PREFIX}/vanan-shoperp:${IMAGE_TAG}
  container_name: vanan-shoperp-nats-sync
  command: ["dotnet", "VanAn.ShopERP.dll", "--sync-worker"]
  environment:
    - ASPNETCORE_ENVIRONMENT=Production
    - ConnectionStrings__DefaultConnection=Data Source=/data/shoperp.db
    - NATS__Url=nats://nats:4222
    - Sync__Mode=Worker
  volumes:
    - shoperp_sqlite_data:/data
  depends_on:
    - nats
    - shoperp-sqlite
  restart: unless-stopped
```

### ShopERP Program.cs Changes

```csharp
// Detect sync worker mode vs web app mode
if (args.Contains("--sync-worker"))
{
    builder.Services.AddHostedService<NatsSyncWorker>();
}
else
{
    // Web app configuration
    var dbPath = Environment.GetEnvironmentVariable("SQLITE_DB_PATH") ?? "Data Source=shoperp.db";
    builder.Services.AddDbContext<VanAnDbContext>(options =>
        options.UseSqlite(dbPath));
}
```

---

## Open Questions

1. **Initial Data Sync**: How to populate SQLite from PostgreSQL initially?
   - Option A: One-time migration script
   - Option B: NATS replay of recent events
   - Option C: Manual export/import

2. **Conflict Resolution**: What strategy for multi-station conflicts?
   - Option A: Last-write-wins (timestamp)
   - Option B: Version vector
   - Option C: Manual resolution UI

3. **Sync Frequency**: How often to poll Outbox?
   - Option A: Continuous polling (100ms)
   - Option B: Event-driven (SQLite triggers)
   - Option C: Scheduled (every 1s)

---

## Success Criteria

- [ ] docker-compose.prod.yml includes SQLite sidecars
- [ ] NATS sync workers deployed and running
- [ ] ShopERP can operate offline (disconnect NATS)
- [ ] Data syncs correctly between stations
- [ ] ADR-001 compliance test PASSES
- [ ] Rollback plan documented and tested

---

## References

- ADR-001: SQLite + NATS Offline First
- Outbox Pattern Implementation Guide
- NATS Integration Guide

---

## DETAILED IMPLEMENTATION PLAN

### Step 1: Verify Existing Infrastructure

| Action | File | Purpose | Risk |
|--------|------|---------|------|
| Verify NATS.Client package exists | `3_CoreHub/VanAn.CoreHub.csproj` | Required for NATS publisher | LOW |
| Verify Outbox implementation exists | `3_CoreHub/Infrastructure/Outbox/` | Must reuse existing pattern | LOW |
| Verify SQLite support exists | `3_CoreHub/VanAn.CoreHub.csproj`, `5_WebApps/ShopERP/VanAn.ShopERP.csproj` | Must confirm provider exists | LOW |

### Step 2: Implement NATS Sync Worker

**New File:** `3_CoreHub/Services/NatsSyncWorker.cs`

```csharp
public class NatsSyncWorker : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<NatsSyncWorker> _logger;
    private readonly string _natsUrl;
    private readonly TimeSpan _pollInterval;

    public NatsSyncWorker(IServiceProvider serviceProvider, ILogger<NatsSyncWorker> logger, IConfiguration configuration)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _natsUrl = configuration.GetValue<string>("NATS__Url") ?? "nats://nats:4222";
        _pollInterval = TimeSpan.FromMilliseconds(configuration.GetValue<int>("Sync__PollIntervalMs", 1000));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var outbox = scope.ServiceProvider.GetRequiredService<IOutboxRepository>();
                var publisher = scope.ServiceProvider.GetRequiredService<INatsEventPublisher>();
                
                var pendingEvents = await outbox.GetPendingEventsAsync(stoppingToken);
                foreach (var ev in pendingEvents)
                {
                    await publisher.PublishAsync(ev.Subject, ev.Payload, stoppingToken);
                    await outbox.MarkAsPublishedAsync(ev.Id, stoppingToken);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "NATS sync worker failed");
            }

            await Task.Delay(_pollInterval, stoppingToken);
        }
    }
}
```

**New File:** `3_CoreHub/Services/NatsEventPublisher.cs`

```csharp
public interface INatsEventPublisher
{
    Task PublishAsync(string subject, byte[] payload, CancellationToken cancellationToken = default);
}

public class NatsEventPublisher : INatsEventPublisher, IDisposable
{
    private readonly IConnection _connection;

    public NatsEventPublisher(IConfiguration configuration)
    {
        var url = configuration.GetValue<string>("NATS__Url") ?? "nats://nats:4222";
        _connection = new ConnectionFactory().CreateConnection(url);
    }

    public Task PublishAsync(string subject, byte[] payload, CancellationToken cancellationToken = default)
    {
        _connection.Publish(subject, payload);
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        _connection?.Dispose();
    }
}
```

**New File:** `3_CoreHub/Infrastructure/Outbox/IOutboxRepository.cs` (if not exists)

```csharp
public interface IOutboxRepository
{
    Task<IReadOnlyList<OutboxEvent>> GetPendingEventsAsync(CancellationToken cancellationToken = default);
    Task MarkAsPublishedAsync(Guid eventId, CancellationToken cancellationToken = default);
}
```

**Reverse Impact Analysis - Step 2:**

| File | Impact | Mitigation |
|------|--------|------------|
| `3_CoreHub/Services/NatsSyncWorker.cs` | New service, no existing code affected | Isolated background service |
| `3_CoreHub/Services/NatsEventPublisher.cs` | New service, no existing code affected | Interface-based, can be mocked |
| `3_CoreHub/Program.cs` | Need to register `NatsSyncWorker` when running as sync worker | Conditional registration based on command-line arg |

### Step 3: Add SQLite Sidecars to docker-compose.prod.yml

**Insert after `services:` section (after `khachlink` service):**

```yaml
  # SQLite local databases for offline-first stations
  shoperp-sqlite:
    image: alpine:latest
    container_name: vanan-shoperp-sqlite
    command: ["sh", "-c", "mkdir -p /data && touch /data/shoperp.db && tail -f /dev/null"]
    volumes:
      - shoperp_sqlite_data:/data
    networks:
      - vanan-network
    restart: unless-stopped

  khachlink-sqlite:
    image: alpine:latest
    container_name: vanan-khachlink-sqlite
    command: ["sh", "-c", "mkdir -p /data && touch /data/khachlink.db && tail -f /dev/null"]
    volumes:
      - khachlink_sqlite_data:/data
    networks:
      - vanan-network
    restart: unless-stopped

  order-station-sqlite:
    image: alpine:latest
    container_name: vanan-order-station-sqlite
    command: ["sh", "-c", "mkdir -p /data && touch /data/order.db && tail -f /dev/null"]
    volumes:
      - order_sqlite_data:/data
    networks:
      - vanan-network
    restart: unless-stopped

  # NATS sync workers
  shoperp-nats-sync:
    image: ${IMAGE_PREFIX:-ghcr.io/anlebao}/vanan-shoperp:${IMAGE_TAG:-latest}
    container_name: vanan-shoperp-nats-sync
    command: ["dotnet", "VanAn.ShopERP.dll", "--sync-worker"]
    environment:
      - ASPNETCORE_ENVIRONMENT=Production
      - ConnectionStrings__DefaultConnection=Data Source=/data/shoperp.db
      - NATS__Url=nats://nats:4222
      - Sync__PollIntervalMs=1000
    volumes:
      - shoperp_sqlite_data:/data
    networks:
      - vanan-network
    depends_on:
      - nats
      - shoperp-sqlite
    restart: unless-stopped

  khachlink-nats-sync:
    image: ${IMAGE_PREFIX:-ghcr.io/anlebao}/vanan-khachlink:${IMAGE_TAG:-latest}
    container_name: vanan-khachlink-nats-sync
    command: ["dotnet", "VanAn.KhachLink.dll", "--sync-worker"]
    environment:
      - ASPNETCORE_ENVIRONMENT=Production
      - ConnectionStrings__DefaultConnection=Data Source=/data/khachlink.db
      - NATS__Url=nats://nats:4222
      - Sync__PollIntervalMs=1000
    volumes:
      - khachlink_sqlite_data:/data
    networks:
      - vanan-network
    depends_on:
      - nats
      - khachlink-sqlite
    restart: unless-stopped

  order-station-nats-sync:
    image: ${IMAGE_PREFIX:-ghcr.io/anlebao}/vanan-shoperp:${IMAGE_TAG:-latest}
    container_name: vanan-order-station-nats-sync
    command: ["dotnet", "VanAn.ShopERP.dll", "--sync-worker", "--station=order"]
    environment:
      - ASPNETCORE_ENVIRONMENT=Production
      - ConnectionStrings__DefaultConnection=Data Source=/data/order.db
      - NATS__Url=nats://nats:4222
      - Sync__PollIntervalMs=1000
    volumes:
      - order_sqlite_data:/data
    networks:
      - vanan-network
    depends_on:
      - nats
      - order-station-sqlite
    restart: unless-stopped
```

**Add to `volumes:` section:**

```yaml
volumes:
  postgres_data:
  nats_data:
  seq_data:
  shoperp_data:
  khachlink_data:
  certbot_www:
  certbot_conf:
  shoperp_sqlite_data:
  khachlink_sqlite_data:
  order_sqlite_data:
```

**Reverse Impact Analysis - Step 3:**

| File | Impact | Mitigation |
|------|--------|------------|
| `docker-compose.prod.yml` | Production deployment changes | Test locally first, keep PostgreSQL service during migration |
| `shoperp` service | May need to depend on `shoperp-sqlite` | Add `depends_on` conditionally |
| `khachlink` service | May need to depend on `khachlink-sqlite` | Add `depends_on` conditionally |
| `nginx` service | No impact | No changes needed |

### Step 4: Configure ShopERP for SQLite

**File:** `5_WebApps/ShopERP/Program.cs`

**Change connection string selection logic:**

```csharp
var dbConnectionString = builder.Configuration.GetConnectionString("DefaultConnection");

if (args.Contains("--sync-worker"))
{
    // Sync worker mode: use SQLite
    var sqlitePath = Environment.GetEnvironmentVariable("SQLITE_DB_PATH") ?? "Data Source=/data/shoperp.db";
    builder.Services.AddDbContext<IVanAnDbContext, VanAnDbContext>(options =>
        options.UseSqlite(sqlitePath));
    
    builder.Services.AddHostedService<NatsSyncWorker>();
    builder.Services.AddSingleton<INatsEventPublisher, NatsEventPublisher>();
    
    var app = builder.Build();
    await app.RunAsync();
    return;
}

// Web app mode: keep existing PostgreSQL via CoreHub
builder.Services.AddHttpClient<ICoreHubClient, CoreHubClient>(...)
```

**Reverse Impact Analysis - Step 4:**

| File | Impact | Mitigation |
|------|--------|------------|
| `5_WebApps/ShopERP/Program.cs` | Entry point changes for sync worker mode | Conditional logic preserves web app behavior |
| `5_WebApps/ShopERP/VanAn.ShopERP.csproj` | May need to reference `NATS.Client` | Check existing package references |

### Step 5: Update Architecture Test

**File:** `6_Tests/VanAn.Architecture.Tests/ArchitectureRulesTests.cs`

**Current test checks:** `sqlite`, `SQLite`, `shoperp-sqlite`, `khachlink-sqlite`

**After docker-compose changes, test will find:** `shoperp-sqlite`, `khachlink-sqlite`, `order-station-sqlite`, and `nats-sync` workers

**No code changes needed** - test will pass automatically.

**Reverse Impact Analysis - Step 5:**

| File | Impact | Mitigation |
|------|--------|------------|
| `ArchitectureRulesTests.cs` | Test assertion should pass after deployment changes | Verify by running test after each step |

### Step 6: Testing Strategy

**Unit Tests:**
- `NatsEventPublisher.PublishAsync` - verify NATS publish call
- `NatsSyncWorker.ExecuteAsync` - verify Outbox polling and publish

**Integration Tests:**
- NATS publish/subscribe round trip
- SQLite Outbox write and read

**E2E / Deployment Tests:**
- `docker-compose -f docker-compose.prod.yml config` (validate YAML)
- `docker-compose -f docker-compose.prod.yml up -d` (local smoke test)
- Verify all SQLite containers start
- Verify all NATS sync workers start
- Run ADR-001 architecture test

### Step 7: Rollback Plan

**Rollback Trigger:**
- ADR-001 test fails after changes
- `docker-compose up` fails locally
- SQLite sync worker crashes

**Rollback Steps:**
1. Revert `docker-compose.prod.yml` to previous version
2. Revert `5_WebApps/ShopERP/Program.cs` sync worker logic
3. Keep `NatsSyncWorker.cs` and `NatsEventPublisher.cs` (they are inert when not registered)
4. Run `docker-compose up` with original PostgreSQL configuration
5. Verify original tests pass

### Step 8: Migration Strategy

**Phase 1 (Immediate - Add Infrastructure):**
- Add SQLite sidecars and NATS workers
- Keep PostgreSQL as primary database
- No connection string changes yet

**Phase 2 (After Verification - Switch Primary):**
- Change `shoperp` connection string to SQLite
- Change `khachlink` connection string to SQLite
- Ensure NATS sync is publishing

**Phase 3 (Future - Remove PostgreSQL Direct Access):**
- PostgreSQL becomes sync target only
- All writes go through SQLite → NATS → PostgreSQL

**Decision for This Wave:**
Implement Phase 1 only. This satisfies ADR-001 compliance test (has SQLite stations + NATS sync workers) without breaking existing PostgreSQL primary database.

### Implementation Order

| Order | Step | Depends On | Risk |
|-------|------|------------|------|
| 1 | Verify packages | None | LOW |
| 2 | Implement NatsSyncWorker | Step 1 | MEDIUM |
| 3 | Implement NatsEventPublisher | Step 2 | MEDIUM |
| 4 | Update docker-compose.prod.yml | Step 2 | HIGH |
| 5 | Update ShopERP Program.cs | Step 2 | HIGH |
| 6 | Run docker-compose config | Step 4 | LOW |
| 7 | Run ADR-001 architecture test | Step 4 | LOW |
| 8 | Run guard-check.ps1 | Step 7 | LOW |
| 9 | Run dotnet build | Step 5 | LOW |
| 10 | Commit | Step 9 | LOW |

### Reverse Impact Summary

| Changed File | Existing Function | New Function | Reverse Impact | Mitigation |
|--------------|-----------------|--------------|----------------|------------|
| `docker-compose.prod.yml` | PostgreSQL-based deployment | Adds SQLite + NATS workers | High | Phase 1 only, no connection string changes |
| `5_WebApps/ShopERP/Program.cs` | Web app only | Adds sync worker mode | Medium | Conditional logic, web app unchanged |
| `3_CoreHub/Services/NatsSyncWorker.cs` | New file | Background sync worker | None | New file |
| `3_CoreHub/Services/NatsEventPublisher.cs` | New file | NATS publish wrapper | None | New file |
| `3_CoreHub/Program.cs` | Web API host | Register sync worker services | Medium | Conditional registration |
| `architecture-guard.ps1` | Architecture checks | No changes | None | No changes |
| `ArchitectureRulesTests.cs` | Test rules | No changes | None | Will pass automatically |

### Decision Points for User

1. **Phase 1 Only?** Yes/No - Should we only add SQLite sidecars + NATS workers without switching primary database?
2. **Station Count**: ShopERP + KhachLink + OrderStation (3 stations) or fewer?
3. **Sync Worker Image**: Reuse ShopERP image or create separate worker image?
4. **Initial Sync Strategy**: Defer to future wave or implement now?

### Recommended Approach

- **Phase 1 only** (lowest risk)
- **3 stations** (ShopERP, KhachLink, OrderStation)
- **Reuse existing images** (ShopERP for order station, KhachLink for PWA)
- **Defer initial sync** to future wave (wave 3?)
- **Sync frequency: 1 second** (poll interval)

This approach satisfies ADR-001 test with minimal production risk.