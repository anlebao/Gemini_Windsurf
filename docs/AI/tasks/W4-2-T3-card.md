# TASK CARD: W4-2-T3 — Add Sync Worker Service Definitions to docker-compose.prod.yml ✅ COMPLETE

**Wave:** 6 (ADR001-W4.2) — NATS Sync Worker Mode
**Branch:** `feature/adr001-wave4-sync-worker-mode` → commit `078ee6e`
**Estimated effort:** 1 hour
**Status:** ✅ COMPLETE
**Dependency:** W4-2-T1 ✅ (conditional DI) + W4-2-T2 ✅ (appsettings.Edge.json) + Wave 5 (sidecars) ✅

---

## 1. GOAL & CONTEXT

Thêm NATS sync worker service definitions vào `docker-compose.prod.yml` để enable background sync từ SQLite Outbox → NATS → PostgreSQL trong v2 hybrid mode.

**Critical:** Sync workers chỉ active khi `DEPLOYMENT_MODE=hybrid`. v1 SaaS mode — sync workers không start.

**Architecture Reference:** `docs/Architecture/ADR001-Station-Architecture.md` (Step 3: Add NATS sync workers)

---

## 2. VERIFIED FACTS

| Fact | Source |
|------|--------|
| NATS service đã tồn tại (nats:2.10-alpine) với port 4222 | `docker-compose.prod.yml` L49-68 |
| Sidecar containers đã thêm trong Wave 5: shoperp-sqlite, khachlink-sqlite, order-station-sqlite | W4-1-T1-card.md |
| ShopERP image có `--sync-worker` arg support từ W4-2-T1 | W4-2-T1-card.md |
| Architecture document yêu cầu 3 sync workers: shoperp-nats-sync, khachlink-nats-sync, order-station-nats-sync | ADR001-Station-Architecture.md L450-503 |
| Sync worker env vars: NATS__Url, Sync__PollIntervalMs, ConnectionStrings__DefaultConnection | ADR001-Station-Architecture.md L455-459 |

---

## 3. IMPLEMENTATION SPEC

### 3.1 Edit location: `docker-compose.prod.yml`

**Insert SAU sidecar containers (sau order-station-sqlite), TRƯỚC nginx service:**

```yaml
  # NATS sync workers for v2 Hybrid Edge/Cloud (Phase 2: active when DEPLOYMENT_MODE=hybrid)
  shoperp-nats-sync:
    image: ${IMAGE_PREFIX:-ghcr.io/anlebao}/vanan-shoperp:${IMAGE_TAG:-latest}
    container_name: vanan-shoperp-nats-sync
    command: ["dotnet", "VanAn.ShopERP.dll", "--sync-worker"]
    environment:
      - ASPNETCORE_ENVIRONMENT=Production
      - DEPLOYMENT_MODE=${DEPLOYMENT_MODE:-saas}
      - SQLITE_DB_PATH=/data/shoperp.db
      - NATS__Url=nats://nats:4222
      - Sync__PollIntervalMs=1000
      - Sync__BatchSize=50
      - Serilog__WriteTo__1__Args__serverUrl=http://seq:5341
    volumes:
      - shoperp_sqlite_data:/data
    networks:
      - vanan-network
    depends_on:
      nats:
        condition: service_healthy
      shoperp-sqlite:
        condition: service_started
    profiles:
      - hybrid
    restart: unless-stopped
    logging:
      driver: "json-file"
      options:
        max-size: "10m"
        max-file: "3"
    deploy:
      resources:
        limits:
          memory: 256m

  khachlink-nats-sync:
    image: ${IMAGE_PREFIX:-ghcr.io/anlebao}/vanan-khachlink:${IMAGE_TAG:-latest}
    container_name: vanan-khachlink-nats-sync
    command: ["dotnet", "VanAn.KhachLink.dll", "--sync-worker"]
    environment:
      - ASPNETCORE_ENVIRONMENT=Production
      - DEPLOYMENT_MODE=${DEPLOYMENT_MODE:-saas}
      - SQLITE_DB_PATH=/data/khachlink.db
      - NATS__Url=nats://nats:4222
      - Sync__PollIntervalMs=1000
      - Sync__BatchSize=50
      - Serilog__WriteTo__1__Args__serverUrl=http://seq:5341
    volumes:
      - khachlink_sqlite_data:/data
    networks:
      - vanan-network
    depends_on:
      nats:
        condition: service_healthy
      khachlink-sqlite:
        condition: service_started
    profiles:
      - hybrid
    restart: unless-stopped
    logging:
      driver: "json-file"
      options:
        max-size: "10m"
        max-file: "3"
    deploy:
      resources:
        limits:
          memory: 256m

  order-station-nats-sync:
    image: ${IMAGE_PREFIX:-ghcr.io/anlebao}/vanan-shoperp:${IMAGE_TAG:-latest}
    container_name: vanan-order-station-nats-sync
    command: ["dotnet", "VanAn.ShopERP.dll", "--sync-worker", "--station=order"]
    environment:
      - ASPNETCORE_ENVIRONMENT=Production
      - DEPLOYMENT_MODE=${DEPLOYMENT_MODE:-saas}
      - SQLITE_DB_PATH=/data/order.db
      - NATS__Url=nats://nats:4222
      - Sync__PollIntervalMs=1000
      - Sync__BatchSize=50
      - Serilog__WriteTo__1__Args__serverUrl=http://seq:5341
    volumes:
      - order_sqlite_data:/data
    networks:
      - vanan-network
    depends_on:
      nats:
        condition: service_healthy
      order-station-sqlite:
        condition: service_started
    profiles:
      - hybrid
    restart: unless-stopped
    logging:
      driver: "json-file"
      options:
        max-size: "10m"
        max-file: "3"
    deploy:
      resources:
        limits:
          memory: 256m
```

**Lý do design:**
- `profiles: [hybrid]` — sync workers chỉ start khi explicitly enabled via `--profile hybrid`
- `DEPLOYMENT_MODE` env var cho consistency với main services
- `SQLITE_DB_PATH` point đến sidecar volume mount paths
- Memory limits 256m (nhỏ hơn main services 512m)
- Depends on NATS health check + sidecar containers
- Logging config nhất quán

### 3.2 Usage Instructions

**v1 SaaS mode (default):**
```bash
docker compose -f docker-compose.prod.yml up -d
# Sync workers KHÔNG start (no --profile hybrid)
```

**v2 Hybrid mode:**
```bash
DEPLOYMENT_MODE=hybrid docker compose -f docker-compose.prod.yml --profile hybrid up -d
# Sync workers START với --profile hybrid
```

---

## 4. HARDENING GATES

- [ ] Sync workers có `profiles: [hybrid]` — KHÔNG auto-start trong v1 SaaS
- [ ] `--sync-worker` arg trong command (required cho conditional DI)
- [ ] Depends on NATS health check + sidecar containers
- [ ] Memory limits 256m (resource isolation)
- [ ] Logging config nhất quán với existing services
- [ ] KHÔNG affect existing services (shoperp, khachlink, nginx, etc.)
- [ ] Environment variables documented cho deployment

---

## 5. VALIDATION

```powershell
# Validate docker-compose syntax
docker compose -f docker-compose.prod.yml config

# Test v1 SaaS mode (sync workers should not start)
docker compose -f docker-compose.prod.yml config | grep "nats-sync"
# Should return empty (no sync workers in default profile)

# Test v2 hybrid mode (sync workers should start)
docker compose -f docker-compose.prod.yml --profile hybrid config | grep "nats-sync"
# Should return 3 sync worker definitions

# Verify volume mounts
docker compose -f docker-compose.prod.yml --profile hybrid config | grep "shoperp_sqlite_data"
```

---

## 6. EXIT CRITERIA

- [ ] 3 sync worker services added: shoperp-nats-sync, khachlink-nats-sync, order-station-nats-sync
- [ ] All sync workers have `profiles: [hybrid]`
- [ ] Sync workers depend on NATS health check + respective sidecar containers
- [ ] Environment variables configured: SQLITE_DB_PATH, NATS__Url, Sync__PollIntervalMs
- [ ] docker-compose.prod.yml syntax valid in both modes
- [ ] v1 SaaS mode: sync workers not started (verified via default config)
- [ ] v2 hybrid mode: sync workers started (verified via --profile hybrid)
- [ ] Wave 6 (ADR001-W4.2) COMPLETE → Proceed to Wave 7 (ADR001-W4.3: Phased Migration Validation)