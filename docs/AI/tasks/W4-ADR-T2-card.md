# TASK CARD: W4-ADR-T2 — Tạo appsettings.Edge.json cho ShopERP

**Wave:** 4 — ShopERP SQLite + Feature Flag
**Branch:** `feature/adr001-wave4-sqlite-config`
**Estimated effort:** 30 minutes
**Dependency:** W4-ADR-T1 complete ✅

---

## 1. GOAL & CONTEXT

Tạo `appsettings.Edge.json` để:
1. Cấu hình NATS connection URL cho edge deployment
2. Cấu hình Sync poll interval + batch size
3. Disable Serilog file logging (không cần trong container)
4. Override health check thresholds cho edge environment

File này được load khi `ASPNETCORE_ENVIRONMENT=Edge` (optional — docker-compose.edge.yml có thể dùng `Production` + env vars thay thế).

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
    "Url": "nats://nats:4222"
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

`docker-compose.edge.yml` sử dụng `ASPNETCORE_ENVIRONMENT=Production` (không phải `Edge`) vì:
- Image đã có `appsettings.Production.json` trong `/app`
- Env vars override toàn bộ — `NATS__Url`, `Sync__PollIntervalMs` trong compose đã cover

**appsettings.Edge.json** chủ yếu dùng cho:**local development** khi test edge mode:
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
- [ ] `NATS.Url` point đến `nats:4222` (service name trong edge compose)
- [ ] KHÔNG override `ConnectionStrings` trong json — dùng env var `SQLITE_DB_PATH` thay
- [ ] Csproj được update để copy file ra output

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
- [ ] Wave 4 COMPLETE → có thể sang Wave 5
