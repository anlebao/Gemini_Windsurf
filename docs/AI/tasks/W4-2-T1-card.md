# TASK CARD: W4-2-T1 — Add Conditional DI for --sync-worker Mode ✅ COMPLETE

**Wave:** 6 (ADR001-W4.2) — NATS Sync Worker Mode
**Branch:** `feature/adr001-wave4-sync-worker-mode` → commit `078ee6e`
**Estimated effort:** 1-2 hours
**Status:** ✅ COMPLETE
**Dependency:** Wave 5 (ADR001-W4.1) complete ✅ (SQLite sidecars in place) + Wave 3 (ADR001-W3) complete ✅ (NatsSyncWorker exists)

---

## 1. GOAL & CONTEXT

Thêm conditional DI registration vào `ShopERP/Program.cs` để khi start với arg `--sync-worker`, app đăng ký `NatsSyncWorker` như `IHostedService` cho v2 hybrid mode.

**Critical:** v1 SaaS mode (không có `--sync-worker`) — app chạy bình thường, không có NatsSyncWorker.

**Architecture Reference:** `docs/Architecture/ADR001-Station-Architecture.md` (Step 4: Configure ShopERP for SQLite)

---

## 2. VERIFIED FACTS

| Fact | Source |
|------|--------|
| `Program.cs` dùng `WebApplication.CreateBuilder(args)` — args được pass vào | `ShopERP/Program.cs` L26 |
| SQLite connection: `connectionString = GetConnectionString("DefaultConnection") ?? Data Source=...` | `ShopERP/Program.cs` L61-64 |
| `IVanAnDbContext` mapped đến `ShopERPDbContext` | `ShopERP/Program.cs` L83 |
| `IOutboxRepository` hiện chưa đăng ký trong ShopERP DI | Searched Program.cs — not found |
| `OutboxRepository` trong `VanAn.CoreHub` namespace | `3_CoreHub/Infrastructure/Messaging/OutboxRepository.cs` L5 |
| `NatsSyncWorker` namespace: `VanAn.CoreHub.Services` | W3-ADR-T2-card.md |
| `INatsEventPublisher` namespace: `VanAn.CoreHub.Infrastructure.Messaging` | W3-ADR-T1-card.md |
| ShopERP already references CoreHub (uses CoreHub services) | `ShopERP/Program.cs` L94+ |

---

## 3. IMPLEMENTATION SPEC

### 3.1 Edit location: `5_WebApps/ShopERP/Program.cs`

**Thêm using statement** (cùng block với các using đầu file):
```csharp
using VanAn.CoreHub.Infrastructure.Messaging;
using VanAn.CoreHub.Services;
```

**Thêm conditional block** — insert SAU dòng đăng ký `IVanAnDbContext` (line 83), TRƯỚC các AddScoped services:

```csharp
// ADR-001 Edge: Conditional NATS sync worker (activated via --sync-worker arg)
if (args.Contains("--sync-worker"))
{
    // Register Outbox for NATS sync (uses same SQLite ShopERPDbContext)
    builder.Services.AddScoped<IOutboxRepository, OutboxRepository>();
    
    // Register NATS publisher as Singleton (holds NATS connection)
    builder.Services.AddSingleton<INatsEventPublisher, NatsEventPublisher>();
    
    // Register NatsSyncWorker as BackgroundService
    builder.Services.AddHostedService<NatsSyncWorker>();
    
    Log.Information("NatsSyncWorker registered — running in edge sync mode");
}
```

### 3.2 SQLITE_DB_PATH env var override

Hiện tại ShopERP đọc connection string từ `DefaultConnection` hoặc fallback hardcode.  
Thêm env var `SQLITE_DB_PATH` override để docker-compose.prod.yml có thể inject path `/data/shoperp.db`:

**Replace** block connection string (lines 61-64) thành:

```csharp
// ADR-001 Edge: Allow SQLITE_DB_PATH env var override for Docker volume mounting
string connectionString = 
    Environment.GetEnvironmentVariable("SQLITE_DB_PATH") 
    ?? builder.Configuration.GetConnectionString("DefaultConnection")
    ?? $"Data Source={Path.Combine(AppContext.BaseDirectory, "vanan_shoperp.db")}";

builder.Services.AddDbContext<ShopERPDbContext>(options =>
    options.UseSqlite(connectionString));
```

**Lý do:** `SQLITE_DB_PATH` cho phép Docker volume mount point `/data/shoperp.db` override connection string mà không cần thay đổi appsettings.

---

## 4. HARDENING GATES

- [ ] `args.Contains("--sync-worker")` check — KHÔNG dùng bool flag từ config (có thể bị cache)
- [ ] `IOutboxRepository` chỉ đăng ký khi `--sync-worker` (tránh duplicate registration)
- [ ] `NatsEventPublisher` là Singleton (không tạo mới NATS connection mỗi request)
- [ ] `NatsSyncWorker` là `AddHostedService` (không phải Scoped)
- [ ] `SQLITE_DB_PATH` không ảnh hưởng v1 SaaS (env var không set = fallback như cũ)
- [ ] KHÔNG thay đổi `ShopERPDbContext` class
- [ ] KHÔNG sửa Domain layer

---

## 5. VALIDATION

```powershell
# Build pass
dotnet build c:/VibeCoding/Gemini_Windsurf/VanAn.sln

# Chạy thử v1 mode (không có --sync-worker): không đăng ký NatsSyncWorker
# Chạy thử v2 mode (với --sync-worker): NatsSyncWorker đăng ký

# Architecture tests vẫn pass
dotnet test c:/VibeCoding/Gemini_Windsurf/6_Tests/VanAn.Architecture.Tests/
```

---

## 6. EXIT CRITERIA

- [ ] `--sync-worker` arg conditional DI block added
- [ ] `SQLITE_DB_PATH` env var override added
- [ ] V1 SaaS mode: không có NatsSyncWorker đăng ký (verified via no `AddHostedService<NatsSyncWorker>` when no arg)
- [ ] `dotnet build VanAn.sln` 0 errors
- [ ] Architecture tests 23/23 PASS
- [ ] Proceed to W4-2-T2 (appsettings.Edge.json)