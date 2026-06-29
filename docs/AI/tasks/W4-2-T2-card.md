# TASK CARD: W4-2-T2 — Create appsettings.Edge.json for ShopERP ✅ COMPLETE

**Wave:** 6 (ADR001-W4.2) — NATS Sync Worker Mode
**Branch:** `feature/adr001-wave4-sync-worker-mode` → commit `078ee6e`
**Estimated effort:** 30 minutes
**Status:** ✅ COMPLETE
**Dependency:** W4-2-T1 complete ✅ (conditional DI added)

---

## 1. GOAL & CONTEXT

Tạo `appsettings.Edge.json` để cấu hình NATS connection URL, sync settings, và logging cho v2 hybrid edge deployment.

**Critical:** File này chủ yếu dùng cho local development testing. Production deployment sẽ dùng env vars trong docker-compose.prod.yml.

**Architecture Reference:** `docs/Architecture/ADR001-Station-Architecture.md` (Step 4: Configure ShopERP for SQLite)

---

## 2. VERIFIED FACTS

| Fact | Source |
|------|--------|
| `appsettings.json` tồn tại (base config) | `5_WebApps/ShopERP/appsettings.json` |
| `appsettings.Development.json` tồn tại | `5_WebApps/ShopERP/appsettings.Development.json` |
| `appsettings.Production.json` tồn tại | `5_WebApps/ShopERP/appsettings.Production.json` |
| `Sync__PollIntervalMs` được đọc trong `NatsSyncWorker` constructor | W3-ADR-T2-card.md |
| `Sync__BatchSize` được đọc trong `NatsSyncWorker` constructor | W3-ADR-T2-card.md |
| `NATS__Url` được đọc trong `NatsEventPublisher` constructor | W3-ADR-T1-card.md |
| `LoggingConfig:EnableFileLogging` disable disk I/O logging | `ShopERP/Program.cs` L34 |

---

## 3. IMPLEMENTATION SPEC

### File tạo mới: `5_WebApps/ShopERP/appsettings.Edge.json`

```json
{
  "NATS": {
    "Url": "nats://localhost:4222"
  },
  "Sync": {
    "PollIntervalMs": 1000,
    "BatchSize": 50
  },
  "LoggingConfig": {
    "EnableFileLogging": false
  },
  "Serilog": {
    "MinimumLevel": {
      "Default": "Information",
      "Override": {
        "Microsoft": "Warning",
        "System": "Warning"
      }
    }
  },
  "AllowedHosts": "*"
}
```

### Lưu ý quan trọng: appsettings.Edge.json vs env vars

`docker-compose.prod.yml` sử dụng `ASPNETCORE_ENVIRONMENT=Production` (không phải `Edge`) vì:
- Image đã có `appsettings.Production.json` trong `/app`
- Env vars override toàn bộ — `NATS__Url`, `Sync__PollIntervalMs` trong compose đã cover

**appsettings.Edge.json** chủ yếu dùng cho **local development** khi test edge mode:
```powershell
# Local test edge mode
$env:ASPNETCORE_ENVIRONMENT = "Edge"
dotnet run --project 5_WebApps/ShopERP -- --sync-worker
```

### Copy to output: cần thêm vào `VanAn.ShopERP.csproj`

```xml
<ItemGroup>
  <Content Include="appsettings.Edge.json">
    <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
  </Content>
</ItemGroup>
```

---

## 4. HARDENING GATES

- [ ] `appsettings.Edge.json` hợp lệ JSON (validate trước khi commit)
- [ ] `EnableFileLogging: false` — tránh disk I/O trong container
- [ ] `NATS.Url` point đến `localhost:4222` cho local dev (production dùng env var)
- [ ] KHÔNG override `ConnectionStrings` trong json — dùng env var `SQLITE_DB_PATH` thay
- [ ] Csproj được update để copy file ra output
- [ ] KHÔNG add sensitive data (API keys, secrets) vào config file

---

## 5. VALIDATION

```powershell
# Validate JSON syntax
Get-Content "c:/VibeCoding/Gemini_Windsurf/5_WebApps/ShopERP/appsettings.Edge.json" | ConvertFrom-Json

# Build pass
dotnet build c:/VibeCoding/Gemini_Windsurf/VanAn.sln

# Verify file copied to output
Test-Path "c:/VibeCoding/Gemini_Windsurf/5_WebApps/ShopERP/bin/Debug/net8.0/appsettings.Edge.json"
```

---

## 6. EXIT CRITERIA

- [ ] `appsettings.Edge.json` tạo mới trong `5_WebApps/ShopERP/`
- [ ] Valid JSON với NATS, Sync, LoggingConfig sections
- [ ] `VanAn.ShopERP.csproj` updated để copy file
- [ ] `dotnet build VanAn.sln` 0 errors
- [ ] Proceed to W4-2-T3 (Add sync worker service definitions to docker-compose.prod.yml)