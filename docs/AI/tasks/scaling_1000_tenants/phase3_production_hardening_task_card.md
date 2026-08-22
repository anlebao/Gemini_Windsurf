# Task Card: Phase 3 — Production Hardening (P2)

> **Status:** PLANNED (awaiting Phase 2 completion + implementation approval)
> **Priority:** P2 — trước 500 tenant
> **Created:** 2026-08-22
> **Master plan:** `docs/AI/tasks/scaling_1000_tenants/master_plan.md`
> **Prerequisite:** Phase 2 complete (multi-VPS, Redis, CDN images)
> **Effort:** 1-2 tuần

## Problem

Sau Phase 2, hệ thống có 2 ShopERP VPS + Redis + CDN images, capacity ~600 tenant. Nhưng thiếu:

1. **No CDN cho static assets** — mỗi page load KhachLink tải ~2MB WASM → 1000 customer × 2MB = 2GB egress/lần load
2. **No SQLite connection pooling** — 1000 SQLite file mở concurrent → file handle limit (Linux default 1024)
3. **NATS single instance** — Gateway sập = NATS sập = order không deliver
4. **No backup strategy** — 1000 SQLite file không có backup tự động
5. **No monitoring** — không biết VPS nào quá tải, PG connection count, NATS lag

## Solution

### Task 3.1 — Cloud CDN cho static assets

**Files sửa:**

#### `nginx/templates/vanan.multivps.conf.template`
```nginx
# Static assets — cache 1 năm, serve qua CDN
location ~* \.(js|css|woff2|png|jpg|jpeg|gif|svg|ico|wasm)$ {
    proxy_pass http://$SHOPERP_REMOTE_HOST;
    add_header Cache-Control "public, max-age=31536000, immutable";
    add_header X-CDN-Cache "hit";
    # CDN sẽ cache response, giảm egress ShopERP
}

# Blazor WASM — cache aggressively
location /_framework/ {
    proxy_pass http://$KHACHLINK_REMOTE_HOST;
    add_header Cache-Control "public, max-age=31536000, immutable";
}
```

**CDN setup (manual, GCP console):**
1. Tạo Cloud CDN backend bucket cho static assets
2. Map domain `cdn.vanan.cloud` → Cloud CDN
3. Update KhachLink `_framework/blazor.webassembly.js` path → CDN URL (nếu cần)

### Task 3.2 — SQLite connection pooling + ulimit

**Files sửa:**

#### `5_WebApps/ShopERP/Program.cs` — thay `AddDbContext` bằng `AddDbContextPool`
```csharp
// BEFORE:
builder.Services.AddDbContext<ShopERPDbContext>(options => options.UseSqlite(connectionString));

// AFTER:
builder.Services.AddDbContextPool<ShopERPDbContext>(options => options.UseSqlite(connectionString), poolSize: 128);
```

**VPS setup (manual, SSH):**
```bash
# Tăng file handle limit cho Docker container
echo "* soft nofile 65535" >> /etc/security/limits.conf
echo "* hard nofile 65535" >> /etc/security/limits.conf

# Docker daemon config
echo '{"default-ulimits":{"nofile":{"Name":"nofile","Hard":65535,"Soft":65535}}}' > /etc/docker/daemon.json
systemctl restart docker
```

### Task 3.3 — NATS cluster (3 node, HA)

**Files sửa:**

#### `docker-compose.gateway.yml` (nats service)
```yaml
  nats:
    image: nats:2.10-alpine
    command: [
      "-js",
      "-sd", "/data/jetstream",
      "-m", "8222",
      "--cluster_name", "vanan-nats-cluster",
      "--routes", "nats://nats-1:6222,nats://nats-2:6222,nats://nats-3:6222"
    ]
    # ... rest unchanged
```

**VPS setup:**
- Cài NATS trên vanan-shop-a + vanan-shop-b (mỗi VPS 1 node)
- Gateway VPS NATS = node 1 (đã có)
- vanan-shop-a NATS = node 2
- vanan-shop-b NATS = node 3
- Cấu hình `--routes` trỏ tới 3 node

**Files sửa:**

#### `docker-compose.shoperp.yml`
```yaml
      - Nats__Url=nats://${NATS_REMOTE_HOST}:4222,nats://${NATS_LOCAL_HOST}:4222
      - ConnectionStrings__Nats=nats://${NATS_REMOTE_HOST}:4222,nats://${NATS_LOCAL_HOST}:4222
```

### Task 3.4 — Backup script song song cho SQLite

**Files mới:**

#### `scripts/backup-shoperp.sh`
```bash
#!/bin/bash
# Backup tất cả SQLite file trên ShopERP VPS → GCS
# Chạy song song (xargs -P 4) để backup nhanh
# Cron: 0 2 * * * (2h sáng mỗi ngày)

SHOPERP_DATA_DIR="/app/keys"
GCS_BUCKET="vanan-backups-prod"
DATE=$(date +%Y%m%d)
VPS_ID="${SHOP_INSTANCE_ID:-unknown}"

# Find all .db files, backup song song
find "$SHOPERP_DATA_DIR" -name "*.db" -type f | xargs -P 4 -I {} bash -c '
  db_file="{}"
  tenant_id=$(basename "$db_file" .db)
  gsutil cp "$db_file" "gs://'"$GCS_BUCKET"'/shoperp/'"$VPS_ID"'/'"$DATE"'/${tenant_id}.db"
  echo "Backed up: $tenant_id"
'

# Cleanup backups cũ hơn 30 ngày
gsutil rm -r "gs://$GCS_BUCKET/shoperp/$VPS_ID/$(date -d '30 days ago' +%Y%m%d 2>/dev/null || date -v-30d +%Y%m%d)" 2>/dev/null || true

echo "Backup complete: $VPS_ID / $DATE"
```

#### `scripts/restore-shoperp.sh`
```bash
#!/bin/bash
# Restore 1 tenant từ GCS backup
# Usage: ./restore-shoperp.sh <tenantId> <dateYYYYMMDD>

TENANT_ID="$1"
DATE="$2"
GCS_BUCKET="vanan-backups-prod"
VPS_ID="${SHOP_INSTANCE_ID:-unknown}"

if [ -z "$TENANT_ID" ] || [ -z "$DATE" ]; then
  echo "Usage: $0 <tenantId> <dateYYYYMMDD>"
  exit 1
fi

gsutil cp "gs://$GCS_BUCKET/shoperp/$VPS_ID/$DATE/$TENANT_ID.db" "/app/keys/$TENANT_ID.db"
echo "Restored: $TENANT_ID from $DATE"
```

**Cron setup (SSH vào mỗi ShopERP VPS):**
```bash
crontab -e
# Add: 0 2 * * * /opt/vanan/scripts/backup-shoperp.sh >> /var/log/vanan-backup.log 2>&1
```

### Task 3.5 — Monitoring stack (Prometheus + Grafana)

**Files mới:**

#### `docker-compose.monitoring.yml`
```yaml
version: '3.8'
services:
  prometheus:
    image: prom/prometheus:latest
    volumes:
      - ./monitoring/prometheus.yml:/etc/prometheus/prometheus.yml:ro
      - prometheus_data:/prometheus
    ports:
      - "9090:9090"
    restart: unless-stopped

  grafana:
    image: grafana/grafana:latest
    volumes:
      - grafana_data:/var/lib/grafana
    ports:
      - "3000:3000"
    environment:
      - GF_SECURITY_ADMIN_PASSWORD=${GRAFANA_PASSWORD:-admin}
    restart: unless-stopped

  node-exporter:
    image: prom/node-exporter:latest
    ports:
      - "9100:9100"
    restart: unless-stopped

volumes:
  prometheus_data:
  grafana_data:
```

#### `monitoring/prometheus.yml`
```yaml
global:
  scrape_interval: 15s

scrape_configs:
  - job_name: 'gateway'
    static_configs:
      - targets: ['gateway:80']
    metrics_path: /metrics

  - job_name: 'shoperp-a'
    static_configs:
      - targets: ['shoperp-a:80']
    metrics_path: /metrics

  - job_name: 'shoperp-b'
    static_configs:
      - targets: ['shoperp-b:80']
    metrics_path: /metrics

  - job_name: 'node'
    static_configs:
      - targets: ['node-exporter:9100']
```

**Files sửa:**

#### `2_Gateway/Program.cs` — thêm `app.UseHttpMetrics()` (package `prometheus-net.AspNetCore`)
#### `5_WebApps/ShopERP/Program.cs` — thêm `app.UseHttpMetrics()`

**Grafana dashboards (manual import):**
- Dashboard 1: Gateway — request rate, p95 latency, PG connections, error rate
- Dashboard 2: ShopERP — circuit count, memory usage, SQLite lock count
- Dashboard 3: NATS — message rate, lag, cluster health
- Dashboard 4: VPS — CPU, RAM, disk, network per node

#### `docs/operations/Monitoring_Setup_Guide.md` (file mới)
- Hướng dẫn deploy `docker-compose.monitoring.yml`
- Hướng dẫn import Grafana dashboards
- Alert rules: CPU > 80%, RAM > 85%, PG connections > 250, NATS lag > 1000

### Task 3.6 — Auto-scaling (GCP MIG)

**GCP setup (manual, GCP console):**
1. Tạo Managed Instance Group template cho ShopERP (e2-medium, Debian 12, deploy script)
2. Autoscaler: CPU > 70% → spawn VPS mới (max 5 VPS)
3. Health check: `http://localhost/health` — unhealthy VPS tự động replace
4. Load balancer: route traffic tới các VPS trong MIG

**Files sửa:**

#### `docs/operations/Multi_VPS_Deployment_Guide.md` — append MIG section
#### `scripts/deploy-shoperp.sh` — parameterize cho MIG template (user-data script)

## Scope Checklist

- [ ] Task 3.1: Cloud CDN cho static assets (WASM, JS, CSS)
- [ ] Task 3.2: SQLite connection pooling + ulimit 65535
- [ ] Task 3.3: NATS cluster 3 node (HA)
- [ ] Task 3.4: Backup script song song + cron + restore script
- [ ] Task 3.5: Prometheus + Grafana + node-exporter + dashboards
- [ ] Task 3.6: GCP MIG + autoscaler (CPU > 70%)
- [ ] `dotnet build VanAn.sln` PASS
- [ ] Test: backup 100 SQLite file → GCS < 5 phút
- [ ] Test: restore 1 tenant từ backup
- [ ] Test: NATS cluster — kill 1 node → order vẫn deliver
- [ ] Test: autoscaler — CPU > 70% → VPS mới spawn < 5 phút
- [ ] Monitoring: Grafana dashboard hiển thị metrics

## Prerequisites

- Phase 2 complete (multi-VPS, Redis, CDN images)
- GCP console access — tạo MIG, autoscaler, Cloud CDN
- User approval cho monitoring stack (chi phí ~$5-10/tháng)

## Verification

1. **Build:** `dotnet build VanAn.sln -c Release` → 0 errors
2. **Backup:** Chạy `backup-shoperp.sh` → 100 file backup trong < 5 phút
3. **Restore:** Chạy `restore-shoperp.sh <tenantId> <date>` → file restore OK
4. **NATS HA:** `docker stop nats` trên 1 VPS → order vẫn deliver qua 2 node còn lại
5. **Autoscaler:** Stress test CPU > 70% → VPS mới spawn trong 5 phút
6. **Monitoring:** Grafana `http://<vps>:3000` → dashboard hiển thị metrics real-time
7. **Alert:** CPU > 80% → AlertManager gửi email/Slack

## Risks

| # | Risk | Mitigation |
|---|---|---|
| R3.1 | NATS cluster config sai → split-brain | Test trên staging, có rollback sang single NATS |
| R3.2 | Backup script chạy sai → backup rỗng | Verify backup size > 0 sau mỗi lần chạy |
| R3.3 | Autoscaler spawn VPS liên tục (thrashing) | Set cooldown period 10 phút, max 5 VPS |
| R3.4 | Prometheus ăn RAM quá nhiều | Giữ retention 15 ngày, scrape interval 15s |
| R3.5 | `AddDbContextPool` break existing code | Test kỹ — DbContext pool có khác biệt với AddDbContext (không dùng `OnConfiguring` runtime) |

## Related

- Master plan: `docs/AI/tasks/scaling_1000_tenants/master_plan.md`
- Phase 2 task card: `docs/AI/tasks/scaling_1000_tenants/phase2_horizontal_scale_task_card.md`
- Phase 4 task card: `docs/AI/tasks/scaling_1000_tenants/phase4_scale_1000_task_card.md`
