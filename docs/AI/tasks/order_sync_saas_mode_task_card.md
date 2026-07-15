# TASK CARD — Phase S: SaaS Mode Sync Cleanup

> **Master plan:** `docs/AI/tasks/order_sync_saas_edge_master_plan.md` (Section 3 Phase S)
> **Branch:** `feature/order-sync-saas-mode`
> **Priority:** 1 (Critical — fix user pain Dashboard metrics, unblock Phase E)
> **Mode:** IMPLEMENT (user approval granted 2026-07-15)
> **Prerequisite:** Master plan approved, investigation session 2026-07-15 complete
> **Estimated tasks:** 8

---

## 0. CONTEXT & DECISIONS (locked)

### Architecture decision (locked 2026-07-15)
- **SaaS Mode = PostgreSQL SSoT.** KHÔNG sync PG→SQLite.
- **Flag `Sync__EdgeMode=false`** = default, gate mọi sync PG↔SQLite.
- **ShopERP UI đọc qua Gateway HTTP** cho order data (Orders page đã làm, Dashboard cần follow).
- **NATS sync workers DISABLED** trong SaaS Mode:
  - `NatsSyncWorker` (Gateway Program.cs:301) — gate bằng flag
  - `OrderSyncSubscriber` (ShopERP Program.cs:322) — gate bằng flag
  - Outbox enqueue block ở `OrderService.CreateOrderFromCommandAsync:573-630` — gate bằng flag

### Verified facts (2026-07-15)
- PostgreSQL `VanAnLocal`: 13 orders, **0 OutboxMessages** → Outbox path không chạy trong production hiện tại
- ShopERP `Orders/Index.razor:147-149`: đã đọc qua `IHttpClientFactory("gateway")` → SaaS Owner Orders page **đã work**
- ShopERP `DashboardController.cs:106-108`: đọc `_dbContext.Orders` (SQLite) → **hiển thị 0 cho orders tạo qua KhachLink**
- ShopERP `OrderManagementService.cs:173-189`: tương tự, đọc SQLite → metrics sai
- KhachLink `Checkout.razor:295-296`: luôn gọi `api/public/orders/checkout` qua Gateway → PostgreSQL
- Gateway `Program.cs:70-77`: `IVanAnDbContext` auto-detect SQLite/Npgsql từ connection string (PostgreSQL khi prod)

### User decisions (locked 2026-07-15)
- **Option C:** Sync PG→SQLite chỉ cho Edge Mode, SaaS Mode bỏ đi
- **Verify-first:** Mỗi task có verify step, KHÔNG commit nếu chưa có evidence

---

## 1. TASKS

| # | Task ID | Task | Files | Status |
|---|---------|------|-------|--------|
| 1 | S-T1 | **VERIFY-REPRO:** Bật `docker compose up -d postgres nats gateway shoperp`. Tạo order qua `POST http://localhost:5001/api/public/orders/checkout` (body: items + customerName). Chụp baseline: (a) PostgreSQL `Orders` count, (b) `GET http://localhost:5003/api/dashboard/shop-metrics/{shopId}` response — record `TodayOrders` field. **Expected:** PostgreSQL count +1, Dashboard `TodayOrders` = 0 (reproduce bug). | Terminal only | ⬜ |
| 2 | S-T2 | Thêm endpoint `GET /api/dashboard/shop-metrics/{shopId:guid}` ở Gateway. Tạo `DashboardController.cs` ở `2_Gateway/Controllers/` (hoặc thêm vào existing controller). Đọc `_dbContext.Orders.Where(o => o.TenantId == tenantId && o.CreatedAt >= today && o.CreatedAt < tomorrow)` qua `IVanAnDbContext` (PostgreSQL). Return DTO cùng shape với `ShopERP DashboardController.ShopDashboardMetrics`. **Lưu ý:** Gateway `IVanAnDbContext` đã register (Program.cs:70), inject thẳng. | `2_Gateway/Controllers/DashboardController.cs` (NEW) hoặc extend existing | ⬜ |
| 3 | S-T3 | Refactor `DashboardController.GetShopMetrics` (ShopERP `5_WebApps/ShopERP/Controllers/DashboardController.cs:96-144`): thay `_dbContext.Orders.Where(...)` bằng `IHttpClientFactory.CreateClient("gateway").GetFromJsonAsync<ShopDashboardMetrics>("api/dashboard/shop-metrics/{shopId}")`. Inject `IHttpClientFactory` vào constructor. Giữ nguyên DTO `ShopDashboardMetrics` (đã có ở line 152-168). | `5_WebApps/ShopERP/Controllers/DashboardController.cs` | ⬜ |
| 4 | S-T4 | Refactor `OrderManagementService.GetOrderMetricsAsync` (`5_WebApps/ShopERP/Services/OrderManagementService.cs:169-189`): thay `_orderWorkflowService.GetOrdersByStatusAsync` (đọc SQLite) bằng `IHttpClientFactory.CreateClient("gateway").GetFromJsonAsync<OrderMetrics>("api/orders/metrics")` (Gateway endpoint đã có — `OrdersController.GetMetrics` Gateway version). **Lưu ý:** verify Gateway có endpoint `GET /api/orders/metrics` — nếu chưa có, thêm vào. | `5_WebApps/ShopERP/Services/OrderManagementService.cs` | ⬜ |
| 5 | S-T5 | **Disable sync PG→SQLite cho SaaS Mode.** Gate 3 điểm bằng `Sync__EdgeMode` flag (default false):<br>(a) `2_Gateway/Program.cs:301` — `if (Configuration.GetValue<bool>("Sync__EdgeMode", false)) AddHostedService<NatsSyncWorker>()`<br>(b) `5_WebApps/ShopERP/Program.cs:322` — `if (Configuration.GetValue<bool>("Sync__EdgeMode", false)) AddHostedService<OrderSyncSubscriber>()`<br>(c) `3_CoreHub/Services/OrderService.cs:573` — `if (_syncEdgeMode && _outboxRepository != null)` thay vì `if (_outboxRepository != null)`. Thêm field `private readonly bool _syncEdgeMode` đọc từ `IConfiguration` (inject thêm `IConfiguration?` vào constructor, default null → false).<br>**Giữ nguyên `NatsSyncWorker` (ShopERP Program.cs:130)** cho Edge Mode SQLite→PG path — đã gate bằng `Sync:Enabled` (rename hoặc reuse). | `2_Gateway/Program.cs`, `5_WebApps/ShopERP/Program.cs`, `3_CoreHub/Services/OrderService.cs` | ⬜ |
| 6 | S-T6 | Set `Sync__EdgeMode=false` explicit trong `docker-compose.prod.yml` (ShopERP service env). Đảm bảo `docker-compose.yml` (local dev) cũng có flag (default false). **Lưu ý:** KHÔNG set ở `docker-compose.edge.yml` — Edge task card sẽ set `true`. | `docker-compose.prod.yml`, `docker-compose.yml` | ⬜ |
| 7 | S-T7 | **VERIFY-FIX:** Re-run S-T1 scenario. **Expected:** (a) PostgreSQL `Orders` count +1, (b) Dashboard `GET /api/dashboard/shop-metrics/{shopId}` response `TodayOrders >= 1`, (c) `OrderManagementService.GetOrderMetricsAsync()` return non-zero `TotalOrders`. Record evidence (curl output, log lines). | Terminal only | ⬜ |
| 8 | S-T8 | **BUILD + COMMIT:** `guard-check.ps1` PASS + `dotnet build VanAn.sln` 0 errors. Update `docs/AI/project_state.md`:<br>- Section 3: change "Last commit" + add SaaS Mode complete<br>- Section 4: remove SaaS-related next actions<br>- Section 6: add history entry "[2026-07-15] ORDER SYNC SAAS MODE CLEANUP COMPLETE"<br>- Section 9 (Maintenance Log): mark commit `db49639c` "VERIFY PENDING" as **SUPERSEDED** (sai diagnosis)<br>Commit message: `[SYNC-SAAS] Disable PG→SQLite sync for SaaS Mode + Dashboard HTTP refactor` | Solution-wide | ⬜ |

---

## 2. EXIT CRITERIA

- [ ] S-T1 evidence: Dashboard `TodayOrders = 0` khi PostgreSQL có order (reproduce)
- [ ] Gateway endpoint `GET /api/dashboard/shop-metrics/{shopId}` tồn tại + return data từ PostgreSQL
- [ ] ShopERP `DashboardController.GetShopMetrics` gọi Gateway HTTP thay vì đọc SQLite
- [ ] ShopERP `OrderManagementService.GetOrderMetricsAsync` gọi Gateway HTTP
- [ ] `NatsSyncWorker` (Gateway) + `OrderSyncSubscriber` (ShopERP) disabled khi `Sync__EdgeMode=false`
- [ ] Outbox enqueue block ở `OrderService.CreateOrderFromCommandAsync` gated bằng flag
- [ ] `docker-compose.prod.yml` có `Sync__EdgeMode=false`
- [ ] S-T7 evidence: Dashboard `TodayOrders >= 1` sau checkout (fix confirmed)
- [ ] Build 0 errors, guard-check PASS
- [ ] `project_state.md` updated — "VERIFY PENDING" superseded

---

## 3. ANTI-PATTERNS (KHÔNG làm)

- ❌ Refactor `Orders/Index.razor` hoặc `Detail.razor` — đã work, KHÔNG đụng
- ❌ Xóa `OrderSyncSubscriber.cs` hoặc `NatsSyncWorker.cs` — chỉ disable, không xóa (Edge Mode dùng)
- ❌ Disable `NatsSyncWorker` ở ShopERP Program.cs:130 (Edge Mode SQLite→PG path) — chỉ disable ở Gateway Program.cs:301
- ❌ Sửa `OrderRepository.AddAsync` public API (RC-1 fix là Phase E)
- ❌ Sửa `Domain.cs` (Phase E có thể cần, Phase S KHÔNG)
- ❌ Bật Playwright (governance: disabled trong IMPLEMENT)
- ❌ Commit mà không có S-T7 evidence
- ❌ Hardcode `Sync__EdgeMode=true` ở bất kỳ file nào trong Phase S

---

## 4. ROLLBACK PLAN

Nếu Phase S fail sau 3 rounds:
1. Revert commit trên `feature/order-sync-saas-mode`
2. Report: task fail tại S-T{x}, evidence cụ thể, root cause mới (nếu có)
3. **KHÔNG** re-enable sync PG→SQLite — confirm với user trước

---

## 5. VERIFICATION CHECKLIST

```powershell
# 1. Build
dotnet build VanAn.sln
# Expected: 0 errors

# 2. Guard check
.\scripts\guard-check.ps1
# Expected: PASS

# 3. Local docker verify (S-T1 + S-T7)
docker compose up -d postgres nats gateway shoperp
# Wait ~30s for healthcheck

# Reproduce (S-T1)
$body = @{ items = @(@{ productId = "00000000-0000-0000-0000-000000000001"; quantity = 1; unitPrice = 10000 }); customerName = "Test" } | ConvertTo-Json
Invoke-RestMethod -Uri "http://localhost:5001/api/public/orders/checkout" -Method Post -Body $body -ContentType "application/json"
# Verify PG count
docker exec vanan-postgres-local psql -U vanan_dev -d VanAnLocal -c "SELECT COUNT(*) FROM \"Orders\";"
# Verify Dashboard (expected 0 before fix)
Invoke-RestMethod -Uri "http://localhost:5003/api/dashboard/shop-metrics/00000000-0000-0000-0000-000000000001"

# Fix verify (S-T7) — after implementation
# Re-run checkout + Dashboard call — expected TodayOrders >= 1

# 4. Flag verify
docker exec vanan-shoperp printenv | findstr Sync__EdgeMode
# Expected: Sync__EdgeMode=false (after S-T6)
```

---

## 6. NOTES

- **S-T4 lưu ý:** Cần verify Gateway có endpoint `GET /api/orders/metrics`. Nếu chưa có, thêm vào `2_Gateway/Controllers/OrdersController.cs` (đọc PG qua `IVanAnDbContext`). Pattern reference: `5_WebApps/ShopERP/Controllers/OrdersController.cs:108-122`.
- **S-T5(c) lưu ý:** `OrderService` constructor hiện đã có nhiều dep — thêm `IConfiguration?` cuối cùng (default null). Đọc `_syncEdgeMode = configuration?.GetValue<bool>("Sync__EdgeMode", false) ?? false`.
- **Không đụng `OrderWorkflowService.RecordOrderCompletedEvent`** — path SQLite→PG cho Edge Mode, đã gate bằng `Sync:Enabled` flag (ShopERP Program.cs:127).
- **`DataSyncSubscriber` (Gateway Program.cs:293)** — KHÔNG disable ở Phase S. Subscribe `vanan.shoperp.>` (SQLite→PG) vẫn cần cho Edge Mode. SaaS Mode: subscriber chạy nhưng không có event (vì `NatsSyncWorker` ShopERP vẫn chạy nếu `Sync:Enabled=true`). Đảm bảo `docker-compose.prod.yml` set `Sync__Enabled=false` (hoặc `Sync:Enabled=false`) để disable `NatsSyncWorker` ShopERP trong SaaS Mode. **Verify trong S-T6.**
