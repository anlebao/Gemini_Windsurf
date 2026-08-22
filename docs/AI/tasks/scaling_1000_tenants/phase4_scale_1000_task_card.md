# Task Card: Phase 4 — Scale 1000+ Tenant (P3)

> **Status:** PLANNED (awaiting Phase 3 completion + implementation approval)
> **Priority:** P3 — trước 1000 tenant
> **Created:** 2026-08-22
> **Master plan:** `docs/AI/tasks/scaling_1000_tenants/master_plan.md`
> **Prerequisite:** Phase 3 complete (NATS HA, backup, monitoring, autoscaler)
> **Effort:** 1-2 tháng (bao gồm VPS provisioning + testing)

## Problem

Sau Phase 3, hệ thống có HA + backup + monitoring + autoscaler, capacity ~900-1200 tenant. Nhưng để chạy ổn định 1000+ tenant + customer cần:

1. **Gateway VPS quá nhỏ** — e2-medium 4GB không đủ cho PostgreSQL 300 connection + SignalR 5000 connection + YARP
2. **4 ShopERP VPS chưa đủ** — 1000 tenant / 300 per VPS = 4 VPS, cần +1 dự phòng = 5 VPS
3. **No dedicated Redis VPS** — Redis trên gateway VPS tranh RAM với PostgreSQL
4. **PostgreSQL chưa tune** — default config không tối ưu cho 1000 tenant
5. **No capacity auto-rebalance** — tenant không tự migrate khi VPS quá tải

## Solution

### Task 4.1 — Gateway VPS upgrade e2-standard-2

**VPS upgrade (manual, GCP console):**
- vanan-gateway: e2-medium → e2-standard-2 (8GB / 2 vCPU)
- Downtime ~5 phút, làm ngoài giờ

**Files sửa:**

#### `docker-compose.gateway.yml`
```yaml
  gateway:
    deploy:
      resources:
        limits:
          memory: 2g  # was 1g (Phase 1)

  postgres:
    command: postgres -c max_connections=300 -c shared_buffers=2GB -c work_mem=16MB -c effective_cache_size=4GB -c max_wal_size=4GB -c maintenance_work_mem=512MB

  redis:
    deploy:
      resources:
        limits:
          memory: 512m  # was 384m
```

### Task 4.2 — 4-5 ShopERP VPS (e2-medium 4GB)

**VPS provisioning (manual, GCP console + MIG):**
- vanan-shop-c (e2-medium, asia-southeast1-b) — ShopInstance ID #3
- vanan-shop-d (e2-medium, asia-southeast1-b) — ShopInstance ID #4
- vanan-shop-e (e2-medium, asia-southeast1-b) — ShopInstance ID #5 (dự phòng)

**Files sửa:**

#### `docs/operations/Multi_VPS_Deployment_Guide.md` — append vanan-shop-c/d/e
#### `.github/workflows/cd-multivps.yml` — thêm deploy step cho 3 VPS mới
#### `docs/operations/ShopInstance_Capacity_Handbook.md` §3.1 — update capacity table

**ShopInstance records (Gateway API):**
```
POST /api/v1/shop-instances
{ label: "vanan-shop-c", shopInstanceId: "<guid-3>", maxTenants: 300, region: "asia-southeast1" }
{ label: "vanan-shop-d", shopInstanceId: "<guid-4>", maxTenants: 300, region: "asia-southeast1" }
{ label: "vanan-shop-e", shopInstanceId: "<guid-5>", maxTenants: 300, region: "asia-southeast1" }
```

### Task 4.3 — Dedicated Redis VPS

**VPS setup (manual, GCP console):**
- vanan-redis (e2-small 2GB, asia-southeast1-b, same VPC)
- Cài Redis 7 (docker-compose.redis.yml)

**Files mới:**

#### `docker-compose.redis.yml`
```yaml
version: '3.8'
services:
  redis:
    image: redis:7-alpine
    command: redis-server --maxmemory 1gb --maxmemory-policy allkeys-lru --appendonly yes
    volumes:
      - redis_data:/data
    ports:
      - "6379:6379"
    networks:
      - vanan-network
    healthcheck:
      test: ["CMD", "redis-cli", "ping"]
      interval: 10s
      timeout: 5s
      retries: 5
    restart: unless-stopped
    deploy:
      resources:
        limits:
          memory: 1536m

volumes:
  redis_data:

networks:
  vanan-network:
    driver: bridge
```

**Files sửa:**

#### `docker-compose.gateway.yml` — xóa redis service (chuyển sang dedicated VPS)
#### `docker-compose.gateway.yml` (gateway env) — `ConnectionStrings__Redis=redis://vanan-redis-internal-ip:6379`
#### `docker-compose.shoperp.yml` (env) — `ConnectionStrings__Redis=redis://vanan-redis-internal-ip:6379`

### Task 4.4 — PostgreSQL tuning

**Files sửa:**

#### `docker-compose.gateway.yml` (postgres command)
```yaml
  postgres:
    command: >
      postgres
      -c max_connections=300
      -c shared_buffers=2GB
      -c work_mem=16MB
      -c effective_cache_size=4GB
      -c max_wal_size=4GB
      -c maintenance_work_mem=512MB
      -c wal_compression=on
      -c checkpoint_completion_target=0.9
      -c random_page_cost=1.1
      -c effective_io_concurrency=200
      -c log_min_duration_statement=1000
```

**PostgreSQL config verification (SQL):**
```sql
-- Verify config applied
SHOW max_connections;        -- 300
SHOW shared_buffers;         -- 2GB
SHOW work_mem;               -- 16MB
SHOW effective_cache_size;   -- 4GB

-- Monitor connection usage
SELECT count(*), state FROM pg_stat_activity GROUP BY state;

-- Monitor slow queries (> 1s)
SELECT query, mean_exec_time, calls FROM pg_stat_statements ORDER BY mean_exec_time DESC LIMIT 10;
```

### Task 4.5 — Cloud CDN cho tất cả static assets

**CDN setup (manual, GCP console):**
1. Tạo Cloud CDN backend cho `cdn.vanan.cloud`
2. Origin: nginx gateway VPS
3. Cache rules: `*.js`, `*.css`, `*.wasm`, `*.woff2`, `*.png`, `*.jpg` → cache 1 năm
4. Update KhachLink + ShopERP base URL cho static assets → `https://cdn.vanan.cloud/`

**Files sửa:**

#### `5_WebApps/KhachLink/wwwroot/index.html` — update base href nếu cần
#### `nginx/templates/vanan.multivps.conf.template` — CDN headers (đã làm Phase 3, verify)

### Task 4.6 — Capacity auto-rebalance

**Files mới:**

#### `scripts/capacity-rebalance.sh`
```bash
#!/bin/bash
# Auto-migrate tenant khi VPS quá tải (CPU > 80% liên tục 10 phút)
# Chạy mỗi 30 phút qua cron
# Dry-run mode mặc định — cần set AUTO_REBALANCE_ENABLED=true để thực sự migrate

GATEWAY_API="http://gateway:80"
THRESHOLD_CPU=80
THRESHOLD_RAM=85
DRY_RUN="${AUTO_REBALANCE_ENABLED:-false}"

# 1. Query tất cả ShopInstance + capacity từ Gateway API
# 2. Query health check mỗi VPS (CPU, RAM)
# 3. Nếu VPS quá tải → tìm VPS target có capacity thấp nhất
# 4. Migrate 10% tenant từ VPS quá tải sang VPS target
# 5. Log + alert (không migrate nếu DRY_RUN=true)

echo "Capacity rebalance check at $(date)"
# Implementation details...
```

**Files sửa:**

#### `3_CoreHub/Services/TenantManagementService.cs` — thêm `MigrateTenantAsync(tenantId, newShopInstanceId)`
- Export SQLite file từ VPS cũ → upload GCS → import vào VPS mới
- Update `Tenant.ShopInstanceId` trong Gateway PG
- Notify NATS: `vanan.cloud.tenant.migrated.{tenantId}`

#### `2_Gateway/Controllers/TenantsController.cs` — thêm `POST /tenants/{id}/migrate`
- Body: `{ "targetShopInstanceId": "<guid>" }`
- Gọi `MigrateTenantAsync`
- Return 202 Accepted (async operation)

#### `docs/operations/ShopInstance_Capacity_Handbook.md` — update §5 FAQ Q5 (migrate)
#### `docs/operations/Scale_1000_Tenant_Runbook.md` (file mới) — runbook cho 1000 tenant

## Scope Checklist

- [ ] Task 4.1: Gateway VPS upgrade e2-standard-2 (8GB)
- [ ] Task 4.2: 3 ShopERP VPS mới (vanan-shop-c/d/e) + ShopInstance records
- [ ] Task 4.3: Dedicated Redis VPS (vanan-redis)
- [ ] Task 4.4: PostgreSQL tuning (shared_buffers=2GB, work_mem=16MB, etc.)
- [ ] Task 4.5: Cloud CDN cho tất cả static assets
- [ ] Task 4.6: Capacity auto-rebalance script + migrate API
- [ ] `dotnet build VanAn.sln` PASS
- [ ] Load test: 1000 concurrent circuit → không OOM, không 503
- [ ] Load test: 5000 SignalR connection → Redis backplane hoạt động
- [ ] Test: auto-rebalance — VPS #1 CPU > 80% → tenant migrate sang VPS #2
- [ ] Cost verification: < $200/tháng cho 1000 tenant

## Prerequisites

- Phase 3 complete (NATS HA, backup, monitoring, autoscaler)
- GCP console access — tạo 4 VPS mới (vanan-redis, vanan-shop-c/d/e)
- User approval cho chi phí ~$187/tháng (7 VPS + bandwidth + CDN)

## Verification

1. **Build:** `dotnet build VanAn.sln -c Release` → 0 errors
2. **Load test 1000 circuit:** k6 hoặc Locust — 1000 concurrent user trên 5 VPS → p95 < 1s, 0 OOM
3. **Load test 5000 SignalR:** 5000 WebSocket connection → Redis backplane broadcast < 100ms
4. **Auto-rebalance:** Stress test VPS #1 CPU > 80% trong 10 phút → 10% tenant migrate sang VPS #2
5. **PostgreSQL:** `SELECT count(*) FROM pg_stat_activity` < 250 khi 1000 tenant active
6. **Cost:** GCP billing < $200/tháng
7. **Monitoring:** Grafana dashboard hiển thị 5 VPS + Redis + Gateway
8. **Backup:** 1000 SQLite file backup trong < 30 phút (song song 4 VPS)
9. **RV L1-L5:** Tất cả 7 VPS healthy

## Risks

| # | Risk | Mitigation |
|---|---|---|
| R4.1 | Gateway upgrade gây downtime dài | Upgrade ngoài giờ, có rollback plan |
| R4.2 | 3 VPS mới provisioning sai → deploy fail | Test deploy script trên 1 VPS trước |
| R4.3 | PostgreSQL tuning gây OOM | shared_buffers=2GB trên VPS 8GB — còn 6GB cho Gateway + nginx |
| R4.4 | Auto-rebalance migrate sai tenant | Dry-run mode mặc định, cần approval để enable |
| R4.5 | Chi phí vượt $200/tháng | Monitor GCP billing weekly, set budget alert $250 |
| R4.6 | 1000 SQLite file backup chậm | Song song 4 VPS (mỗi VPS backup 250 file) |

## Cost Breakdown (1000 tenant)

| Item | Spec | Qty | Cost/tháng |
|---|---|---|---|
| Gateway VPS | e2-standard-2 (8GB / 2 vCPU) | 1 | ~$50 |
| ShopERP VPS | e2-medium (4GB / 1 vCPU) | 4 | ~$100 ($25 mỗi cái) |
| KhachLink VPS | e2-small (2GB) | 1 | ~$13 |
| Redis VPS | e2-small (2GB) | 1 | ~$13 |
| **Tổng VPS** | | **7** | **~$176** |
| Bandwidth (paid 50GB) | | | ~$5 |
| Cloud Storage (product images) | 50GB | | ~$1 |
| Cloud CDN | 50GB egress | | ~$5 |
| **Tổng** | | | **~$187/tháng** |

→ **~$0.19/tenant/tháng** cho 1000 tenant.

## Related

- Master plan: `docs/AI/tasks/scaling_1000_tenants/master_plan.md`
- Phase 3 task card: `docs/AI/tasks/scaling_1000_tenants/phase3_production_hardening_task_card.md`
- `docs/operations/ShopInstance_Capacity_Handbook.md` — capacity reference (update §3.1)
- `docs/operations/Multi_VPS_Deployment_Guide.md` — deployment guide (append new VPS)
