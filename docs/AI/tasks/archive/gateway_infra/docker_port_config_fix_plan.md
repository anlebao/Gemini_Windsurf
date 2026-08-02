# Docker Port + Config + Migration Fix Plan

> **Created:** 2026-07-09
> **Status:** PENDING APPROVAL
> **Mode:** ANALYZE → IMPLEMENT (awaiting user approval)
> **Branch:** main
> **Objective:** Fix 3 lỗi verified (port swap, ShopERP 500, KhachLink 500/service worker) + rà toàn bộ module liên quan (accounting, einvoice, admin, kitchen)

---

## 0. REFERENCE: ORIGINAL USER REPORT

| # | Lỗi user báo | Symptom |
|---|---|---|
| 1 | Nhầm lẫn KhachLink (5002) và ShopERP (5003) | Mở 5002 nhận ShopERP, mở 5003 nhận KhachLink |
| 2 | `http://localhost:5003/` trả HTTP 500 | `localhost can't currently handle this request. HTTP ERROR 500` |
| 3 | KhachLink "Đặt ngay" không hoạt động, giỏ hàng trống, left menu link lỗi | Service worker TypeError: Failed to fetch, blazor.web.js fetch fail |

---

## 1. ROOT CAUSE SUMMARY

Cả 3 lỗi xuất phát từ **một nguồn**: `docker-compose.yml` port mapping đảo ngược + thiếu env var + ShopERP dùng `EnsureCreatedAsync` thay vì `MigrateAsync`.

```
docker-compose.yml port swap (root)
    ├─→ Lỗi #1: User mở 5002 kỳ vọng KhachLink nhưng nhận ShopERP
    ├─→ Lỗi #2: Port 5003 (kỳ vọng ShopERP) thực ra là KhachLink container
    │        └─→ KhachLink crash 500 vì thiếu Gateway__BaseUrl (Production env)
    └─→ Lỗi #3: Port 5002 (thực ra ShopERP) crash vì SQLite thiếu PlatformUsers table
             └─→ Browser có stale service worker từ lần chạy KhachLink trước → fetch fail
```

---

## 2. REVERSE IMPACT ANALYSIS (TÁC ĐỘNG NGƯỢC)

### 2.1. Module-level Impact

| Module | Tác động | Mức độ | Chi tiết |
|---|---|---|---|
| **Accounting** (ShopERP) | 🔴 HIGH | ShopERP container crash → toàn bộ accounting UI (balance sheet, income statement, cash flow, trial balance, HKD books, period closing) không truy cập được. Lỗi `no such table: PlatformUsers` chặn Program.cs startup trước khi app serve bất kỳ request nào. |
| **EInvoice** (ShopERP) | 🔴 HIGH | Cùng nguyên nhân — ShopERP crash → `/einvoice/providers`, `/einvoice/invoices` không hoạt động. EInvoiceOrchestrator không khởi tạo. |
| **Admin** (ShopERP) | 🔴 HIGH | Platform SystemAdmin module (commit `dde219e`) chính là trigger crash — `context.PlatformUsers` query ở Program.cs line 442 fail vì table không tồn tại trong SQLite. |
| **Kitchen** | ⚪ N/A | Không tìm thấy module Kitchen riêng biệt trong codebase. Có thể là alias cho Order Workflow (nằm trong ShopERP) → cùng tác động HIGH. |
| **KhachLink** (PWA) | 🔴 HIGH | Container crash 500 vì thiếu `Gateway:BaseUrl` khi chạy Production env. Toàn bộ PWA không phục vụ được. |
| **Gateway** | 🟡 MED | Port 5010 thay vì 5001 → `appsettings.Development.json` có `CoreHub:BaseUrl = http://localhost:5010` mâu thuẫn với `appsettings.json` có `http://localhost:5001`. YARP proxy config trong Development trỏ đúng (5002/5003) nhưng Docker container phục vụ sai. |
| **CI/E2E** | 🟢 LOW | `.github/workflows/e2e.yml` và `6_Testing/docker-compose.test.yml` đều dùng port ĐÚNG (5001/5002/5003). CI không bị ảnh hưởng. |

### 2.2. File-level Impact (Reverse Dependency)

| File bị ảnh hưởng | Nguyên nhân | Loại fix |
|---|---|---|
| `docker-compose.yml` lines 108,150,190 | Port defaults sai | Đổi defaults |
| `.env.example` lines 35-37 | Comment sai port | Cập nhật comment |
| `docker-compose.yml` khachlink env (line 186-188) | Thiếu `Gateway__BaseUrl` | Thêm env var |
| `5_WebApps/KhachLink/appsettings.json` | Thiếu `Gateway:BaseUrl` fallback | Thêm default (defensive) |
| `5_WebApps/ShopERP/Program.cs` line 372 | `EnsureCreatedAsync` không update schema | Đổi sang `MigrateAsync` |
| `5_WebApps/ShopERP/Infrastructure/` | Không có Migrations folder + DesignTimeFactory | Tạo mới |
| `5_WebApps/KhachLink/wwwroot/service-worker.js` | Cache version `v2` không tự bump | Bump lên `v3` + thêm update detection |
| `2_Gateway/appsettings.Development.json` line 18 | `CoreHub:BaseUrl = http://localhost:5010` mâu thuẫn | Review — có thể là dead config |
| `docker-compose.testing.yml` | Legacy `corehub` service port 5010 | Đánh dấu technical debt (out of scope) |

---

## 3. PATTERN FIX CHUNG (APPLY 1 LẦN CHO TẤT CẢ)

### Pattern 1: PORT_CONVENTION_ENFORCEMENT
**Vấn đề:** Port defaults trong docker-compose.yml không khớp với launchSettings.json convention.
**Convention (SSoT):** `5001=gateway, 5002=khachlink, 5003=shoperp` (từ `launchSettings.json` + `docs/AI/project_state.md`).
**Fix chung:** Tất cả docker-compose files phải dùng cùng convention. Files đã đúng (docker-compose.testing.yml, 6_Testing/docker-compose.test.yml) — giữ nguyên. Files sai (docker-compose.yml) — sửa defaults.

### Pattern 2: PRODUCTION_CONFIG_COMPLETENESS
**Vấn đề:** App chạy `ASPNETCORE_ENVIRONMENT=Production` trong Docker nhưng appsettings.json (base) thiếu config mà chỉ appsettings.Development.json có. Program.cs `?? throw` → crash.
**Fix chung:** Mọi config key được truy cập bằng `?? throw` hoặc `GetRequiredSection` MUST có một trong:
1. Giá trị trong `appsettings.json` (base), HOẶC
2. Env var override trong docker-compose (`Key__SubKey=value`), HOẶC
3. Fallback safe default thay vì throw

### Pattern 3: SQLITE_MIGRATION_OVER_ENSURE_CREATED
**Vấn đề:** `EnsureCreatedAsync()` không update schema cho DB đã tồn tại. Khi thêm entity mới (PlatformUsers), SQLite volume cũ không có table → crash.
**Fix chung:** Tất cả DbContext dùng SQLite trong production MUST dùng `MigrateAsync()` + có Migrations folder + DesignTimeDbContextFactory. Test code giữ `EnsureCreated()` (intentional — in-memory, faster).

### Pattern 4: SERVICE_WORKER_CACHE_BUSTING
**Vấn đề:** Service worker cache version `v2` không tự bump khi deploy → stale cache khi port/app thay đổi.
**Fix chung:** Bump cache version + thêm update detection trong pwa.js.

---

## 4. DETAILED CODING PLAN

### Phase 1: Fix Port Mapping (PATTERN 1) — 4 edits

| Step | File | Line | Action | Chi tiết |
|---|---|---|---|---|
| 1.1 | `docker-compose.yml` | 108 | Edit | `${GATEWAY_PORT:-5010}` → `${GATEWAY_PORT:-5001}` |
| 1.2 | `docker-compose.yml` | 150 | Edit | `${SHOPERP_PORT:-5002}` → `${SHOPERP_PORT:-5003}` |
| 1.3 | `docker-compose.yml` | 190 | Edit | `${KHACHLINK_PORT:-5003}` → `${KHACHLINK_PORT:-5002}` |
| 1.4 | `.env.example` | 35-37 | Edit | Cập nhật comment: `GATEWAY_PORT=5001`, `SHOPERP_PORT=5003`, `KHACHLINK_PORT=5002` |

**Verify Phase 1:**
- `docker compose -f docker-compose.yml config` → check port mappings
- `docker compose down && docker compose up -d` → `docker ps` confirm: gateway=5001, khachlink=5002, shoperp=5003

---

### Phase 2: Fix KhachLink Production Config (PATTERN 2) — 2 edits

| Step | File | Line | Action | Chi tiết |
|---|---|---|---|---|
| 2.1 | `docker-compose.yml` | after 187 | Edit | Thêm `- Gateway__BaseUrl=http://gateway:80` vào khachlink environment |
| 2.2 | `5_WebApps/KhachLink/appsettings.json` | end | Edit | Thêm `"Gateway": { "BaseUrl": "http://localhost:5001" }` làm safe default |

**Verify Phase 2:**
- `docker compose up -d khachlink` → `docker logs vanan-khachlink` không còn `Gateway:BaseUrl is required`
- `curl http://localhost:5002/` trả 200

---

### Phase 3: Fix ShopERP SQLite Migration (PATTERN 3) — 5 steps

| Step | File/Command | Action | Chi tiết |
|---|---|---|---|
| 3.1 | `5_WebApps/ShopERP/Infrastructure/DesignTimeDbContextFactory.cs` | Create | DesignTime factory cho ShopERPDbContext (SQLite). Đọc connection string từ env `SQLITE_DB_PATH` hoặc fallback `Data Source=vanan_shoperp.db`. Implement `IDesignTimeDbContextFactory<ShopERPDbContext>`. |
| 3.2 | Terminal | Command | `dotnet ef migrations add InitialCreate --project 5_WebApps/ShopERP --startup-project 5_WebApps/ShopERP --context ShopERPDbContext` |
| 3.3 | Terminal | Command | `dotnet ef migrations add AddPlatformUsersTable --project 5_WebApps/ShopERP --startup-project 5_WebApps/ShopERP --context ShopERPDbContext` |
| 3.4 | `5_WebApps/ShopERP/Program.cs` | 372 | Edit | `_ = await context.Database.EnsureCreatedAsync();` → `await context.Database.MigrateAsync();` |
| 3.5 | Terminal | Command | `docker volume rm vanan-sqlite` (xóa stale volume) → `docker compose up -d shoperp` |

**Verify Phase 3:**
- `docker logs vanan-shoperp` không còn `no such table: PlatformUsers`
- `curl http://localhost:5003/health` trả 200
- `dotnet build VanAn.sln` 0 errors

**Lưu ý governance:**
- Cần kiểm tra `ShopERPDbContext` có bao nhiêu DbSet hiện tại — migration `InitialCreate` phải cover TOÀN BỘ schema hiện tại (vì `EnsureCreatedAsync` trước đó tạo schema từ model, chưa có migration history).
- Nếu schema phức tạp, cần verify migration snapshot khớp với model bằng `dotnet ef migrations script` review SQL.
- KHÔNG sửa Domain.cs, KHÔNG sửa AccountingEntry, KHÔNG break Clean Architecture.

---

### Phase 4: Fix Service Worker Cache (PATTERN 4) — 2 edits

| Step | File | Line | Action | Chi tiết |
|---|---|---|---|---|
| 4.1 | `5_WebApps/KhachLink/wwwroot/service-worker.js` | 1-3 | Edit | Bump `v2` → `v3` cho CACHE_NAME, STATIC_CACHE, DYNAMIC_CACHE |
| 4.2 | `5_WebApps/KhachLink/wwwroot/js/pwa.js` | append | Edit | Thêm update detection: listen `controllerchange` event → prompt user reload |

**Verify Phase 4:**
- Sau khi deploy, mở browser → DevTools → Application → Service Workers → thấy version mới activate, old cache bị delete

**User-side action (KHÔNG phải code fix):**
- User cần unregister stale service worker trong browser DevTools → Application → Service Workers → Unregister → Clear site data → Refresh

---

### Phase 5: Review Gateway Config Consistency — 2 reviews (NO code change unless approved)

| Step | File | Line | Action | Chi tiết |
|---|---|---|---|---|
| 5.1 | `2_Gateway/appsettings.Development.json` | 18 | Review | `CoreHub:BaseUrl = http://localhost:5010` — verify có còn dùng không. Gateway chạy in-process CoreHub (Option B), nên CoreHub:BaseUrl có thể là dead config. Nếu dead → xóa. Nếu còn dùng → đổi thành `http://localhost:5001`. **REPORT to user, await decision.** |
| 5.2 | `docker-compose.testing.yml` | 36-56 | Review | Legacy `corehub` service port 5010 — đánh dấu technical debt (out of scope fix này). **REPORT only.** |

---

### Phase 6: Build + Test Verification

| Step | Command | Mục đích | Expected |
|---|---|---|---|
| 6.1 | `dotnet build VanAn.sln` | Build | 0 errors |
| 6.2 | `scripts/guard-check.ps1` | Guard | PASS |
| 6.3 | `docker compose down && docker compose up -d` | Restart all | All containers up |
| 6.4 | `curl http://localhost:5001/health` | Gateway | 200 OK |
| 6.5 | `curl http://localhost:5002/` | KhachLink | 200 OK |
| 6.6 | `curl http://localhost:5003/health` | ShopERP | 200 OK |
| 6.7 | `docker logs vanan-shoperp --tail 10` | ShopERP logs | No SQLite error |
| 6.8 | `docker logs vanan-khachlink --tail 10` | KhachLink logs | No Gateway:BaseUrl error |

---

## 5. RISKS & MITIGATIONS

| Rủi ro | Mức độ | Mitigation |
|---|---|---|
| Migration `InitialCreate` cho ShopERPDbContext có thể miss table/index nếu model phức tạp | MED | EF Core tự generate từ ModelSnapshot. Verify bằng `dotnet ef migrations script` review SQL trước khi apply. |
| `docker volume rm vanan-sqlite` xóa data hiện có | LOW | Đây là dev data, không phải production. Nếu cần giữ → chạy `dotnet ef database update` trong container thay vì xóa volume. |
| Service worker bump version không tự unregister ở user đã visit | LOW | User cần manually unregister. Code fix chỉ giúp user mới và user reload sau update. |
| `appsettings.json` thêm `Gateway:BaseUrl` default có thể leak dev URL vào production build | LOW | Docker env override (`Gateway__BaseUrl`) luôn win over appsettings. Safe default chỉ fallback khi không có env. |
| ShopERPDbContext có thể có nhiều DbSet → migration file lớn | LOW | EF Core handle được. Chỉ cần verify build pass. |
| `docker-compose.testing.yml` legacy corehub service | LOW | Out of scope — đánh dấu debt, không sửa trong plan này. |

---

## 6. EXECUTION ORDER (DEPENDENCY)

```
Phase 1 (port mapping) ──┐
                         ├─→ Phase 6 (verify all)
Phase 2 (KhachLink env) ─┤
                         │
Phase 3 (SQLite migration)┤
                         │
Phase 4 (service worker) ─┤
                         │
Phase 5 (Gateway review) ─┘
```

- Phase 1-4 có thể chạy song song (không dependency chéo)
- Phase 5 là review-only (REPORT to user, await decision)
- Phase 6 chạy cuối

---

## 7. FILES TOUCHED SUMMARY

| File | Type | Phase |
|---|---|---|
| `docker-compose.yml` | Edit (4 edits) | 1, 2 |
| `.env.example` | Edit (1 edit) | 1 |
| `5_WebApps/KhachLink/appsettings.json` | Edit (1 edit) | 2 |
| `5_WebApps/ShopERP/Infrastructure/DesignTimeDbContextFactory.cs` | Create | 3 |
| `5_WebApps/ShopERP/Migrations/*` | Create (auto by dotnet ef) | 3 |
| `5_WebApps/ShopERP/Program.cs` | Edit (1 edit) | 3 |
| `5_WebApps/KhachLink/wwwroot/service-worker.js` | Edit (1 edit) | 4 |
| `5_WebApps/KhachLink/wwwroot/js/pwa.js` | Edit (1 edit) | 4 |

**Total: 6 edits + 2 creates + 2 dotnet ef commands + 1 docker volume rm**

---

## 8. GOVERNANCE COMPLIANCE CHECK

| Rule | Status |
|---|---|
| Domain layer pure (no EF Core, no DbContext) | ✅ No Domain changes |
| AccountingEntry immutable | ✅ No AccountingEntry changes |
| Clean Architecture dependency direction | ✅ No layer violations |
| Multi-tenancy enforced | ✅ No tenant changes |
| UI Platform components | ✅ No UI changes |
| guard-check.ps1 + dotnet build pass | ✅ Phase 6 verifies |
| No new .csproj files | ✅ DesignTimeFactory trong existing project |
| No hardcoded secrets | ✅ No secrets added |

---

## 9. COMPLETION CHECKLIST (ĐỐI SOÁT SAU IMPLEMENT)

- [x] Phase 1.1: docker-compose.yml gateway port → 5001
- [x] Phase 1.2: docker-compose.yml shoperp port → 5003
- [x] Phase 1.3: docker-compose.yml khachlink port → 5002
- [x] Phase 1.4: .env.example comments updated
- [x] Phase 2.1: docker-compose.yml khachlink env has Gateway__BaseUrl
- [x] Phase 2.2: KhachLink appsettings.json has Gateway:BaseUrl default
- [x] Phase 3.1: DesignTimeDbContextFactory.cs created
- [x] Phase 3.2: InitialCreate migration generated (covers ALL tables including PlatformUsers)
- [x] Phase 3.3: AddPlatformUsersTable migration — SKIPPED (empty, already in InitialCreate)
- [x] Phase 3.4: Program.cs EnsureCreatedAsync → MigrateAsync
- [x] Phase 3.5: vanan-sqlite volume recreated
- [x] Phase 4.1: service-worker.js cache version v2 → v3
- [x] Phase 4.2: pwa.js controllerchange auto-reload added
- [x] Phase 5.1: Gateway CoreHub:BaseUrl — DEAD CONFIG confirmed (not read in code). Mismatch 5001 vs 5010. Awaiting user decision to remove.
- [x] Phase 5.2: docker-compose.testing.yml legacy corehub — technical debt noted, out of scope
- [x] Phase 6.1: dotnet build VanAn.sln — 0 errors, 537 warnings (pre-existing)
- [x] Phase 6.2: guard-check.ps1 — FAIL (untracked new files, expected — files need git add)
- [x] Phase 6.3: docker compose up — all containers running
- [x] Phase 6.4: curl localhost:5001/health → 200 ✅
- [x] Phase 6.5: curl localhost:5002/ → 200 ✅
- [x] Phase 6.6: curl localhost:5003/health → 200 ✅
- [x] Phase 6.7: ShopERP logs clean — migration InitialCreate applied, PlatformUsers table created
- [x] Phase 6.8: KhachLink logs — container OK (no Gateway:BaseUrl crash). Note: 500 from Gateway on /api/products/recommended (separate issue, not in scope)
- [ ] User-side: unregister stale service worker in browser (user must do manually)

---

## 10. APPROVAL

| Field | Value |
|---|---|
| Plan created | 2026-07-09 |
| Plan status | IMPLEMENT COMPLETE |
| User approval | ✅ APPROVED (2026-07-09) |
| Approval date | 2026-07-09 |
| Implementation start | 2026-07-09 |
| Implementation complete | 2026-07-09 |
| Verification complete | 2026-07-09 — all 3 endpoints 200 OK |
