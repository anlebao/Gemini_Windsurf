# Scaling to 1000 Tenants — Master Plan

**Created:** 2026-08-22
**Status:** PLANNED (awaiting implementation approval)
**Branch target:** `main` (P0/P1) + dedicated infra work (P2/P3)
**Source:** User request — hệ thống hiện 3 VPS e2-small 2GB, cần scale lên 1000 tenant + customer mà không sập.

## Context

### Trạng thái deployment hiện tại (verify 2026-08-22)

| VPS | Spec | Vai trò | Containers | Memory limit/container |
|---|---|---|---|---|
| vanan-gateway | e2-small (2GB / 0.5 vCPU) | PostgreSQL + NATS + Gateway + nginx | 4 | 512MB mỗi cái → 2GB tổng, **0 headroom** |
| vanan-shop-a | e2-small (2GB / 0.5 vCPU) | ShopERP (Blazor Server + SQLite per-tenant + NATS subscriber) | 1 | 512MB |
| vanan-khachlink | e2-small (2GB / 0.5 vCPU) | KhachLink PWA + Seq + Certbot | 3 | ~512MB |

**ShopInstance ID duy nhất:** `9e94f876-27bd-4a16-a85b-b5f42620bc6e` (trên vanan-shop-a).

### Capacity hiện tại (theo `docs/operations/ShopInstance_Capacity_Handbook.md` §3.1)

| Spec | Bandwidth tier | MaxTenants đề xuất |
|---|---|---|
| e2-small (2GB) | Free (200MB/mo) | **8–12 tenant** (ảnh trên CDN) |
| e2-small (2GB) | Paid 1GB/mo | 25–40 tenant |

→ **Hiện tại tối đa ~8-12 tenant** trên Free Tier. Để lên 1000 tenant cần thay đổi đáng kể.

### 10 Bottleneck đã verify từ code

**Nhóm A — Sẽ sập NGAY khi tăng tenant (P0):**
- A1: Npgsql pool size = 100 (default) — không config `MaximumPoolSize` (`2_Gateway/Program.cs` L78-87)
- A2: PostgreSQL `max_connections` = 100 (default) — `docker-compose.gateway.yml` postgres không set
- A3: Memory limit 512MB/container trên e2-small 2GB — `docker-compose.gateway.yml` L124-127 + `docker-compose.shoperp.yml` L75-78
- A4: No rate limiting trên Gateway API — grep `AddRateLimiter` trong 2_Gateway = 0 match
- A5: No response caching trên Gateway — grep `UseResponseCaching` trong 2_Gateway = 0 match
- A6: **Blazor circuit disconnect UX** — `KeepAliveInterval=30s` + `ClientTimeoutInterval=60s` (`5_WebApps/ShopERP/Program.cs` L97-104) + nginx/Cloudflare WebSocket idle timeout < 120s → modal "Kết nối bị gián đoạn" hiện mỗi vài chục giây. Modal full-screen che hết UI (`App.razor` L20-27 + CSS L75-87) gây phiền user trên cả trang không-realtime.

**Nhóm B — Sẽ sập khi traffic tăng (P1):**
- B1: No Redis backplane cho SignalR Gateway — `2_Gateway/Program.cs` L75 `AddSignalR()` không có `AddStackExchangeRedis`
- B2: Redis NOT configured trong ShopERP — `docker-compose.shoperp.yml` L51 `ConnectionStrings__Redis=not-configured`
- B3: Product images serve từ ShopERP (TD-5) — `ShopInstance_Capacity_Handbook.md` §5/§6
- B4: Single ShopERP VPS cho tất cả tenant — chỉ `vanan-shop-a` với 1 ShopInstance
- B5: ShopInstance capacity KHÔNG enforce (TD-2) — `ShopInstance_Capacity_Handbook.md` §6
- B6: **No JS keepalive ping cho Blazor circuit** — chỉ `guard-camera.js` có 15s ping (`5_WebApps/ShopERP/Program.cs` L78-79 comment). Các trang khác không có → circuit dễ bị proxy idle kill.

**Nhóm C — Sẽ chậm nhưng không sập (P2):**
- C1: No CDN cho static assets (Blazor WASM, JS, CSS)
- C2: No DB connection pooling cho SQLite per-tenant (EF Core `AddDbContext` không `AddDbContextPool`)
- C3: No health-check based auto-scaling
- C4: No backup strategy cho 1000 SQLite file
- C5: NATS single instance (no cluster)

### Hard stops (governance)

- **Domain PURE** — không sửa Domain.cs để fix capacity (chỉ thêm field nếu Domain Phase active + user approval)
- **UI Platform components bắt buộc** — không bypass
- **AccountingEntry immutable** — không động vào
- **Gateway = Order Creator + Routed Async Delivery (Option C)** — không revert
- **Multi-tenancy enforced at every layer**

### Quyết định (user-confirmed 2026-08-22)

- **Phase 1 (P0)**: 5 thay đổi code + VPS upgrade — BẮT BUỘC trước khi thêm tenant
- **Phase 2 (P1)**: Multi-VPS ShopERP + Redis + CDN images — trước 100 tenant
- **Phase 3 (P2)**: Production hardening — trước 500 tenant
- **Phase 4 (P3)**: Scale 1000+ tenant — trước 1000 tenant

---

## Phase 1 — Critical Bottleneck Fix (P0)

**Mục tiêu:** Sửa 5 bottleneck sẽ sập ngay khi tăng tenant. Không thêm tenant mới cho đến khi Phase 1 xong.

**Task card:** `phase1_critical_bottleneck_task_card.md`

**Scope:**
1. Tăng Npgsql pool 100 → 300 + PostgreSQL `max_connections=300`
2. Tăng memory limit container (Gateway 512m→1g, ShopERP 512m→1.5g) + upgrade VPS e2-small → e2-medium
3. Thêm rate limiting trên Gateway API (checkout 10 req/min/IP, catalog 60 req/min/IP, auth 5 req/min/IP)
4. Thêm response caching cho catalog/recommended endpoints
5. Thêm health check endpoint chi tiết (connections, memory, circuit count)
6. **Sửa Blazor circuit disconnect UX (Giải pháp 2 + 3):**
   - Tăng `KeepAliveInterval` 30s → 15s + `ClientTimeoutInterval` 60s → 120s (`5_WebApps/ShopERP/Program.cs` L97-104)
   - nginx WebSocket timeout ≥ 120s (`proxy_read_timeout 3600s; proxy_send_timeout 3600s;` trong `nginx/templates/vanan.multivps.conf.template`)
   - Cloudflare WebSocket timeout ≥ 120s (dashboard config hoặc tắt nếu không cần)
   - Đổi modal full-screen → toast nhỏ cho trạng thái "reconnecting" (`App.razor` L20-27 + CSS L75-87)
   - Chỉ giữ modal to cho `components-reconnect-rejected` (phiên hết hạn, cần reload)

**Files sửa:**
- `2_Gateway/Program.cs` — Npgsql pool, rate limiting, response caching
- `docker-compose.gateway.yml` — postgres `max_connections`, gateway memory limit
- `docker-compose.shoperp.yml` — shoperp memory limit
- `5_WebApps/ShopERP/Program.cs` — KeepAliveInterval 15s, ClientTimeoutInterval 120s (L97-104)
- `5_WebApps/ShopERP/Components/App.razor` — modal → toast CSS (L20-27, L75-87)
- `nginx/templates/vanan.multivps.conf.template` — WebSocket timeout ≥ 120s
- `2_Gateway/Controllers/CatalogController.cs` — `[ResponseCache]` attribute

**Effort:** 1-2 ngày code + 30 phút VPS upgrade (GCP console)

**Verification:**
- `dotnet build VanAn.sln` PASS
- Load test: 100 concurrent request → không 503, không OOM
- `guard-check.ps1` PASS
- RV L1-L3 trên VPS sau deploy

**Sau Phase 1:** Gateway chịu ~300 concurrent request, ShopERP chịu ~80 circuit, modal "Kết nối bị gián đoạn" không hiện mỗi vài chục giây nữa.

---

## Phase 2 — Horizontal Scale (P1)

**Mục tiêu:** Multi-VPS ShopERP + Redis + CDN images. Cho phép scale ngang khi thêm tenant.

**Task card:** `phase2_horizontal_scale_task_card.md`

**Scope:**
1. Product images → Cloud Storage/CDN (GCS bucket + Cloud CDN + signed URL upload)
2. Redis container trên gateway VPS + `AddStackExchangeRedis` cho SignalR backplane
3. Redis cho ShopERP distributed cache (`ConnectionStrings__Redis` trong docker-compose.shoperp.yml)
4. Thêm vanan-shop-b VPS (e2-medium 4GB) + ShopInstance ID mới + routing table
5. Enforce capacity check trong `TenantManagementService.AssignShopInstanceAsync` (TD-2 fix)
6. Admin UI: hiển thị capacity real-time (RAM/bandwidth usage per Instance) — TD-4 fix
7. **JS keepalive ping cho Blazor circuit (Giải pháp 5):**
   - Thêm `wwwroot/js/circuit-keepalive.js` — `setInterval` gọi `.NET method` nhỏ mỗi 10s (giống `guard-camera.js` 15s ping đã có)
   - Inject script vào `App.razor` (chạy cho tất cả trang Interactive)
   - C# method `[JSInvokable]` trả về `Task<bool>` — chỉ để giữ circuit busy, không logic
   - Giữ circuit sống qua proxy idle timeout (kể cả khi user không tương tác)

**Files sửa:**
- `3_CoreHub/Services/TenantManagementService.cs` — `AssignShopInstanceAsync` thêm capacity check
- `2_Gateway/Program.cs` — `AddStackExchangeRedis` cho SignalR
- `docker-compose.gateway.yml` — thêm redis service
- `docker-compose.shoperp.yml` — `ConnectionStrings__Redis` + `SHOP_INSTANCE_ID` per VPS
- `5_WebApps/ShopERP/Services/ProductImageService.cs` — upload sang GCS, serve từ CDN URL
- `5_WebApps/ShopERP/Components/Pages/Admin/ShopInstances.razor` — capacity dashboard
- `5_WebApps/ShopERP/Components/App.razor` — inject `circuit-keepalive.js` (sau `blazor.web.js`)
- `scripts/deploy-shoperp.sh` — parameterize cho multi-VPS

**Files mới:**
- `3_CoreHub/Services/IProductImageStorageService.cs` — abstraction cho GCS/R2
- `3_CoreHub/Services/GcsProductImageStorageService.cs` — GCS implementation
- `5_WebApps/ShopERP/wwwroot/js/circuit-keepalive.js` — JS keepalive ping (10s interval)
- `docs/operations/Multi_VPS_Deployment_Guide.md` — append vanan-shop-b section

**Files sửa:**
- `3_CoreHub/Services/TenantManagementService.cs` — `AssignShopInstanceAsync` thêm capacity check
- `2_Gateway/Program.cs` — `AddStackExchangeRedis` cho SignalR
- `docker-compose.gateway.yml` — thêm redis service
- `docker-compose.shoperp.yml` — `ConnectionStrings__Redis` + `SHOP_INSTANCE_ID` per VPS
- `5_WebApps/ShopERP/Services/ProductImageService.cs` — upload sang GCS, serve từ CDN URL
- `5_WebApps/ShopERP/Components/Pages/Admin/ShopInstances.razor` — capacity dashboard
- `scripts/deploy-shoperp.sh` — parameterize cho multi-VPS

**Files mới:**
- `3_CoreHub/Services/IProductImageStorageService.cs` — abstraction cho GCS/R2
- `3_CoreHub/Services/GcsProductImageStorageService.cs` — GCS implementation
- `docs/operations/Multi_VPS_Deployment_Guide.md` — append vanan-shop-b section

**Effort:** 3-5 ngày code + 1 ngày VPS setup (vanan-shop-b)

**Verification:**
- `dotnet build VanAn.sln` PASS
- Test: tạo tenant mới → auto-assign vào VPS có capacity thấp nhất
- Test: upload product image → URL trả về là CDN, không phải ShopERP
- Test: Redis backplane — SignalR message broadcast qua 2 Gateway instance
- Load test: 200 concurrent request → không 503
- RV L1-L5 trên cả 2 VPS

**Sau Phase 2:** 2 ShopERP VPS × 300 tenant = 600 tenant capacity, circuit ổn định qua proxy idle.

---

## Phase 3 — Production Hardening (P2)

**Mục tiêu:** Hardening cho production với 500+ tenant. Backup, monitoring, NATS HA.

**Task card:** `phase3_production_hardening_task_card.md`

**Scope:**
1. Cloud CDN cho static assets (Blazor WASM, JS, CSS) — giảm egress 90%
2. SQLite connection pooling — `AddDbContextPool` thay vì `AddDbContext` + `ulimit -n 65535` trên VPS
3. NATS cluster (3 node — 1 per VPS) — HA, không single point of failure
4. Backup script song song cho SQLite per-tenant → GCS
5. Monitoring stack: Prometheus + Grafana + AlertManager
6. Auto-scaling: GCP Managed Instance Group + autoscaler (CPU > 70% → thêm VPS)

**Files sửa:**
- `5_WebApps/ShopERP/Program.cs` — `AddDbContextPool` cho ShopERPDbContext
- `docker-compose.gateway.yml` — NATS cluster config
- `docker-compose.shoperp.yml` — NATS cluster config
- `nginx/templates/vanan.multivps.conf.template` — CDN headers cho static assets

**Files mới:**
- `scripts/backup-shoperp.sh` — backup song song SQLite → GCS
- `scripts/restore-shoperp.sh` — restore từ GCS
- `docs/operations/Monitoring_Setup_Guide.md` — Prometheus/Grafana setup
- `docs/operations/Backup_Restore_Guide.md` — backup/restore procedure

**Effort:** 1-2 tuần

**Verification:**
- `dotnet build VanAn.sln` PASS
- Test: backup 100 SQLite file → GCS trong < 5 phút
- Test: restore 1 tenant từ backup
- Test: NATS cluster — kill 1 node → order vẫn deliver
- Test: autoscaler — CPU > 70% → VPS mới spawn trong 5 phút
- Monitoring: Grafana dashboard hiển thị circuit count, PG connections, NATS lag

**Sau Phase 3:** 3-4 ShopERP VPS × 300 tenant = 900-1200 tenant capacity + HA + backup.

---

## Phase 4 — Scale 1000+ Tenant (P3)

**Mục tiêu:** Scale lên 1000+ tenant + customer với chi phí tối ưu.

**Task card:** `phase4_scale_1000_task_card.md`

**Scope:**
1. Gateway VPS upgrade e2-small → e2-standard-2 (8GB / 2 vCPU)
2. 4-5 ShopERP VPS (e2-medium 4GB mỗi cái) — 1000 tenant / 300 per VPS = 4 VPS + 1 dự phòng
3. Dedicated Redis VPS (e2-small 2GB) — backplane + distributed cache + rate limit store
4. PostgreSQL tuning: `shared_buffers=2GB`, `work_mem=16MB`, `effective_cache_size=4GB`, `max_wal_size=4GB`
5. Cloud CDN cho tất cả static assets (WASM, JS, CSS, images)
6. Capacity auto-rebalance: tenant migrate tự động khi VPS quá tải

**Files sửa:**
- `docker-compose.gateway.yml` — PostgreSQL tuning params
- `docker-compose.shoperp.yml` — memory limit 1.5g → 2g
- `3_CoreHub/Services/TenantManagementService.cs` — auto-rebalance logic
- `docs/operations/ShopInstance_Capacity_Handbook.md` — update capacity table cho 1000 tenant

**Files mới:**
- `scripts/capacity-rebalance.sh` — script auto-migrate tenant khi VPS quá tải
- `docs/operations/Scale_1000_Tenant_Runbook.md` — runbook cho 1000 tenant

**Effort:** 1-2 tháng (bao gồm VPS provisioning + testing)

**Verification:**
- `dotnet build VanAn.sln` PASS
- Load test: 1000 concurrent circuit → không OOM, không 503
- Load test: 5000 SignalR connection → Redis backplane hoạt động
- Test: auto-rebalance — VPS #1 CPU > 80% → tenant migrate sang VPS #2
- Cost verification: < $200/tháng cho 1000 tenant

**Sau Phase 4:** 1000+ tenant + customer, HA, backup, monitoring, auto-scaling.

---

## Ước tính chi phí VPS cho 1000 tenant

| VPS | Spec | Số lượng | Cost/tháng (GCP) |
|---|---|---|---|
| Gateway | e2-standard-2 (8GB / 2 vCPU) | 1 | ~$50 |
| ShopERP | e2-medium (4GB / 1 vCPU) | 4 | ~$100 ($25 mỗi cái) |
| KhachLink | e2-small (2GB) | 1 | ~$13 |
| Redis | e2-small (2GB) | 1 | ~$13 |
| **Tổng VPS** | | **7 VPS** | **~$176/tháng** |
| Bandwidth (paid 50GB) | | | ~$5 |
| Cloud Storage (product images) | 50GB | | ~$1 |
| Cloud CDN | 50GB egress | | ~$5 |
| **Tổng** | | | **~$187/tháng** |

→ **~$0.19/tenant/tháng** cho 1000 tenant.

---

## Dependency Graph

```
Phase 1 (P0) ──── Phase 2 (P1) ──── Phase 3 (P2) ──── Phase 4 (P3)
  5 code fixes     Multi-VPS         Hardening         Scale 1000+
  VPS upgrade      Redis + CDN       Backup + Monitor  7 VPS total
  1-2 days         3-5 days          1-2 weeks         1-2 months
```

**Không được skip Phase 1.** Phase 2-4 đều phụ thuộc Phase 1 (Npgsql pool, rate limiting, memory limit).

**Có thể chạy song song:**
- Phase 2 task 1 (CDN images) || Phase 2 task 4 (thêm VPS)
- Phase 3 task 4 (backup) || Phase 3 task 5 (monitoring)

## Risk Register

| # | Risk | Probability | Impact | Mitigation |
|---|---|---|---|---|
| R1 | VPS upgrade gây downtime | Medium | High | Upgrade ngoài giờ, có rollback plan |
| R2 | Npgsql pool tăng nhưng PG `max_connections` quên tăng | Low | High | Cả 2 cùng thay đổi trong Phase 1 |
| R3 | Redis thêm vào nhưng ShopERP code không dùng | Low | Medium | Verify `IDistributedCache` usage sau deploy |
| R4 | CDN images migration mất ảnh cũ | Medium | High | Backup R2 trước migrate, giữ R2 URL fallback 30 ngày |
| R5 | NATS cluster config sai → order không deliver | Medium | Critical | Test trên staging trước, có rollback sang single NATS |
| R6 | Auto-rebalance migrate tenant sai VPS | Low | High | Dry-run mode + manual approval trước khi auto |

## Related Documents

- `docs/operations/Multi_VPS_Deployment_Guide.md` — 3-VPS split hiện tại
- `docs/operations/ShopInstance_Capacity_Handbook.md` — capacity reference
- `docs/AI/tasks/api_rate_limit_classification_task_card.md` — rate limit classification (deferred, Phase 1 sẽ implement)
- `docs/AI/tasks/archive/khachlink/community-commerce-master-plan-2c5017.md` — ST3 Redis backplane plan (Phase 2 sẽ dùng)

## Maintenance

- Cập nhật `docs/operations/ShopInstance_Capacity_Handbook.md` §3.1 khi thêm VPS spec mới
- Cập nhật `docs/AI/project_state.md` Section 4 (Next Actions) khi hoàn thành mỗi phase
- Cập nhật `docs/operations/Multi_VPS_Deployment_Guide.md` khi thêm VPS mới
