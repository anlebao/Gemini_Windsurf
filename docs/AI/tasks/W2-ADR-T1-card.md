# TASK CARD: W2-ADR-T1 — Tạo docker-compose.edge.yml

**Wave:** 2 — Create docker-compose.edge.yml
**Branch:** `feature/adr001-wave2-edge-compose`
**Estimated effort:** 2-3 hours
**Dependency:** Wave 1 merged ✅

---

## 1. GOAL & CONTEXT

Tạo file `docker-compose.edge.yml` cho v2 Edge deployment theo ADR-001.  
File này **ĐỘC LẬP** với `docker-compose.prod.yml` — v1 SaaS không bị ảnh hưởng.

**Không được sửa:** `docker-compose.prod.yml`

---

## 2. VERIFIED FACTS (từ codebase investigation)

| Fact | Source |
|------|--------|
| NATS service đã có trong prod compose: `nats:2.10-alpine` | `docker-compose.prod.yml` L43 |
| PostgreSQL service đã có: `postgres:15-alpine` | `docker-compose.prod.yml` L9 |
| ShopERP image: `${IMAGE_PREFIX}/vanan-shoperp:${IMAGE_TAG}` | `docker-compose.prod.yml` L116 |
| KhachLink image: `${IMAGE_PREFIX}/vanan-khachlink:${IMAGE_TAG}` | `docker-compose.prod.yml` L143 |
| ShopERP đã dùng SQLite trong code (`vanan_shoperp.db`) | `ShopERP/Program.cs` |
| CoreHub/Gateway cần PostgreSQL — không thay đổi | `docker-compose.prod.yml` |

---

## 3. IMPLEMENTATION SPEC

### File tạo mới: `docker-compose.edge.yml` (root project)

**Cấu trúc tổng thể:**
```
services:
  postgres        (giữ nguyên từ prod — accounting luôn online)
  nats            (giữ nguyên từ prod)
  seq             (giữ nguyên từ prod)
  corehub         (giữ nguyên từ prod — PostgreSQL)
  gateway         (giữ nguyên từ prod)
  shoperp         (override env: SQLite path)
  khachlink       (giữ nguyên)
  nginx           (giữ nguyên)
  certbot         (giữ nguyên)
  shoperp-nats-sync  [NEW] BackgroundService worker
volumes:
  shoperp_sqlite_data  [NEW]
```

### Chi tiết service `shoperp` override trong edge:
```yaml
shoperp:
  environment:
    - ASPNETCORE_ENVIRONMENT=Production
    - ASPNETCORE_URLS=http://+:80
    - SQLITE_DB_PATH=Data Source=/data/shoperp.db
    - NATS__Url=nats://nats:4222
    - Serilog__WriteTo__1__Args__serverUrl=http://seq:5341
    - Authentication__Authority=https://api.vanantech.io.vn
    - Seed__OwnerUsername=${SHOPERP_OWNER_USERNAME:-adminvanan1}
    - Seed__OwnerPassword=${SHOPERP_OWNER_PASSWORD:-2026@vanan}
    - Seed__TenantId=${SHOPERP_TENANT_ID:-00000000-0000-0000-0000-000000000001}
  volumes:
    - shoperp_sqlite_data:/data
```

### Chi tiết service `shoperp-nats-sync` [NEW]:
```yaml
shoperp-nats-sync:
  image: ${IMAGE_PREFIX:-ghcr.io/anlebao}/vanan-shoperp:${IMAGE_TAG:-latest}
  container_name: vanan-shoperp-nats-sync
  command: ["dotnet", "VanAn.ShopERP.dll", "--sync-worker"]
  environment:
    - ASPNETCORE_ENVIRONMENT=Production
    - SQLITE_DB_PATH=Data Source=/data/shoperp.db
    - NATS__Url=nats://nats:4222
    - Sync__PollIntervalMs=1000
    - Seed__TenantId=${SHOPERP_TENANT_ID:-00000000-0000-0000-0000-000000000001}
  volumes:
    - shoperp_sqlite_data:/data
  networks:
    - vanan-network
  depends_on:
    nats:
      condition: service_healthy
    shoperp:
      condition: service_healthy
  restart: unless-stopped
  deploy:
    resources:
      limits:
        memory: 256m
  logging:
    driver: "json-file"
    options:
      max-size: "10m"
      max-file: "3"
```

### Volumes section append:
```yaml
volumes:
  postgres_data:
  nats_data:
  seq_data:
  shoperp_data:
  shoperp_sqlite_data:    # NEW — persisted SQLite DB for edge
  certbot_www:
  certbot_conf:
```

---

## 4. HARDENING GATES

- [ ] `docker-compose.prod.yml` KHÔNG được sửa
- [ ] `shoperp-nats-sync` chỉ start sau `shoperp` healthy
- [ ] SQLite volume named `shoperp_sqlite_data` (không dùng bind mount)
- [ ] Memory limits đặt hợp lý (sync worker ≤ 256m)
- [ ] KhachLink SQLite DEFER sang Wave sau (không implement trong wave này)

---

## 5. VALIDATION

```powershell
# Kiểm tra file tồn tại
Test-Path "docker-compose.edge.yml"

# Kiểm tra syntax yaml (nếu có docker)
docker compose -f docker-compose.edge.yml config --quiet

# Kiểm tra services list
docker compose -f docker-compose.edge.yml config --services
# Expected: postgres, nats, seq, corehub, gateway, shoperp, khachlink, nginx, certbot, shoperp-nats-sync
```

---

## 6. EXIT CRITERIA

- [ ] `docker-compose.edge.yml` tồn tại tại root project
- [ ] Service `shoperp-nats-sync` có trong file
- [ ] Volume `shoperp_sqlite_data` declared
- [ ] `docker-compose.prod.yml` không thay đổi (git diff clean)
- [ ] Proceed to W2-ADR-T2 (add architecture test)
