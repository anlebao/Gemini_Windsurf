# Project State

> **Mục đích:** Single Source of Truth cho AI về trạng thái dự án. BẮT BUỘC đọc đầu mỗi phiên.
> **Archived:** 2026-07-26 — All completed objectives + full history/maintenance log moved to `docs/AI/project_state_archive.md`

---

## 0. Maintenance Rules

1. One-and-only-one: Mỗi section chỉ tồn tại 1 lần.
2. No contradiction: Một hạng mục chỉ có 1 trạng thái.
3. Ground Truth first: Verify path/branch với codebase trước khi ghi.
4. Now over History: Section 2-4 chỉ mô tả việc ĐANG làm và KẾ TIẾP. Việc xong gom vào archive.
5. Actionable Next Actions: Xóa action đã quá hạn/sai bối cảnh.
6. Stamp every edit: Cập nhật Section 9 mỗi lần sửa.

---

## 1. Project Overview

**Dự án:** Vạn An Accounting System MVP — giải pháp kế toán HKD theo TT 152/2025/TT-BTC.
**Stack:** .NET 8 — EF Core — SQLite — Blazor Server (ShopERP) — Blazor WebAssembly (KhachLink PWA) — SignalR — YARP Gateway — xUnit — Playwright.
**Kiến trúc:** Clean Architecture + DDD + Multi-tenancy. Data flow: `KhachLink WASM (5002) -> Gateway (5001) -> ShopERP (5003) -> SQLite`.

**Modules:** `1_Shared` (Domain + Services contracts) — `2_Gateway` (YARP) — `3_CoreHub` (Services, in-process) — `5_WebApps/ShopERP` (Blazor Server) — `5_WebApps/KhachLink` (Blazor WASM, served by nginx) — `UI.Platform` (Shared components) — `6_Tests`.

**Hard stops:** Domain PURE — `AccountingEntry` immutable — Gateway = Order Creator + Routed Async Delivery (Option C) — KhachLink HTTP-only — ShopERP SQLite (Business) + PostgreSQL (Accounting) — ALWAYS dùng UI Platform components.

---

## 2. Current Objective

**Loyalty/CRM Audit Fix — P1-T1 CustomerListGlobal full-stack (COMPLETE 2026-07-27, S2)**

Cross-tenant customer list (SystemAdmin-only) — TDD full-stack: repo `GetAllCustomersAcrossTenantsAsync` (IgnoreQueryFilters) + `CustomerController.ListGlobal` (`[Authorize(Policy="SystemAdmin")]` + `GlobalCustomerDto` with TenantId) + `CustomerListGlobal.razor` rewrite (customer table + Tenant column + filter bar, replaces placeholder campaign overview). 6 new tests (3 repo + 3 HTTP auth) all PASS. Build 0 errors, guard-check ALL PASSED. Commit `2059f403` on `fix/loyalty-crm-audit-fix`.

**Next in audit fix plan: P1-T2 (19 missing tests) + P1-T3 (Missions pagination) — per `loyalty_crm_audit_fix_master_plan-2c5017.md` (S3, S4).**

Branch: `fix/loyalty-crm-audit-fix`. P0 commit `4aa0c6e2`, P1-T1 commit `2059f403`. Merge to `main` after P3 + VPS RV pass.

---

**Previous: Community Commerce Sprint 0 — Foundation (COMPLETE 2026-07-26)** — 11 Domain entities + 42 tests + migration. Merged to `main`, VPS deployed, RV 18/18 PASS. Branch `feature/community-sprint0-foundation` (commits `e1a75bbf` + `f563e415`).

---

## 3. Current Status

- **Branch:** `fix/loyalty-crm-audit-fix`
- **Last commit:** `2059f403` feat(crm): AF-P1-T1 CustomerListGlobal full-stack cross-tenant customer list (TDD)
- **.NET SDK:** 8.0.422
- **DB:** SQLite `vanan_shoperp.db` (business) + PostgreSQL `VanAnCoreHub` (accounting + Gateway + Community tables)
- **Build (2026-07-27):** 0 errors, 1047 warnings (pre-existing CA). guard-check ALL PASSED.
- **Loyalty/CRM Audit Fix — P1-T1 (2026-07-27 COMPLETE, commit `2059f403`, NOT yet merged):** Cross-tenant customer list full-stack TDD. (1) Repo: `ICustomerRepository.GetAllCustomersAcrossTenantsAsync` + `CustomerRepository` impl using `IgnoreQueryFilters()` (bypasses global TenantId filter — SystemAdmin only), filters `!IsDeleted && IsActive`, ordered by TenantId then FullName. (2) Controller: `CustomerController.ListGlobal` `[HttpGet("global")]` + `[Authorize(Policy = "SystemAdmin")]` (combines with controller-level `[Authorize(Policy = "OwnerOnly")]` from P0 → SystemAdmin only; Owner/Staff → 403). New `GlobalCustomerDto` with `TenantId` field. Optional filters: minPoints/maxPoints, lastOrderWithinDays, birthdayMonth, minTotalSpent/maxTotalSpent. (3) Blazor: `CustomerListGlobal.razor` REWRITE — removed campaign overview + "coming soon" note; now renders customer table with Tenant column + filter bar + pagination 20/page + empty state. Keeps `[Authorize(Policy = "SystemAdmin")]` + route `/admin/customers-global`. Uses UI Platform components. (4) Tests: 6 new (3 repo cross-tenant + 3 HTTP auth: SystemAdmin→200, Staff→403, Anonymous→auth-enforced). All PASS. No regressions (31/31 Customer/Excel/LoyaltyRewards tests PASS). **Deviation:** "Push Subscribed" column deferred to P2 (requires PushSubscriptions join); replaced with "Trạng thái" (Active/Inactive).
- **Loyalty/CRM Audit Fix — P0 (2026-07-27 COMPLETE, commit `4aa0c6e2`):** (1) `CustomerController` + `PromoCampaignController` `[Authorize]` → `[Authorize(Policy = "OwnerOnly")]` — blocks Staff/StoreKeeper/Guard from promo/customer admin APIs. (2) `IPromoCampaignService` moved from `3_CoreHub/Services/` to `1_Shared/Services/` (contract layer). (3) `CustomerSegmentCriteria` moved to `1_Shared/Domain/` to break circular dependency.
- **KhachLink Bugs 1-3 Fix (2026-07-27 COMPLETE + MERGED + DEPLOYED + RV PASS):**
  1. **Bug 1 (Profile: points/birthday/push not working) + Bug 2 (Missions: no data):** Root cause — customer-facing ShopERP endpoints (`CustomerIdentityController`, `CustomerProfileController`, `LoyaltyController`, `MissionsController`, `NotificationsController`, `RedemptionController`) are `[AllowAnonymous]` (token auth via `X-Customer-Token` header, no cookie/claim). `ITenantProvider.TenantId = Guid.Empty` because there's no `tenant_id` claim in token-only auth → global `TenantId` query filter on `VanAnDbContext` excluded all customer/mission/loyalty/push-subscription records → endpoints returned 404 or empty data. Fix: new `[ResolveCustomerTenant]` action filter (`5_WebApps/ShopERP/Filters/ResolveCustomerTenantAttribute.cs`) validates the customer token, loads the customer's `TenantId` with `IgnoreQueryFilters`, and calls `ITenantProvider.SetTenant()` so all subsequent queries in the request scope use the correct tenant context. Applied to all 6 customer-facing controllers. Commit `35dc9de6`.
  2. **Bug 3 (Order history: order ID mismatch with ShopERP):** KhachLink `OrderHistory.razor` displayed last 8 chars of OrderId (`[^8..]`) while ShopERP `Orders/Index.razor` displays `TrackingCode ?? first 8 chars` (`[..8]`). UUIDv7 orders have meaningful prefix (timestamp-based) so the suffix never matched. Fix: changed KhachLink display to `[..8]` (first 8 chars) to match ShopERP.
  **End-to-end RV (2026-07-27 07:50 UTC):** OTP login → customer `795a5a87-...` token obtained. (1) `GET /api/customers/me` → 200 with full profile (customerId, fullName, tier, pointBalance, identityLevel, birthday). (2) `POST /api/customer-profile/birthday` → 200 "Đã lưu ngày sinh." — birthday persisted (verified via second `GET /api/customers/me` showing `birthday: "1990-05-15T00:00:00"`). (3) `POST /api/notifications/push/subscribe` → 200 "Đã đăng ký nhận thông báo." with subscriptionId. (4) `GET /api/missions/my/progress` → 200 `[]` (empty because no missions configured for tenant, NOT because of tenant filter failure). (5) `GET /api/loyalty/my` → 200 with full loyalty info (tier, pointBalance, nextTierThreshold, progressPercent, history). All endpoints return 200 (previously would fail with tenant filter issues). Commit `35dc9de6`.
- **Bug 6 Loyalty Fix (2026-07-27 COMPLETE + MERGED + DEPLOYED + RV PASS):** Three sequential fixes for loyalty points not awarded on completed orders:
  1. **DeviceId fallback + Customer stub creation** (`OrderWorkflowService.ProcessLoyaltyPointsAsync`): Original code only looked up customer by `CustomerId` — all 77 completed orders had `CustomerId=NULL` (guest checkout) → points skipped. Fix: added `CustomerDeviceId` fallback lookup via `ICustomerRepository.GetByDeviceIdAsync`, and if no customer found, create a new Customer stub from `CustomerDeviceId` + `Order.CustomerInfo`. RV: customer stub `6d2f1300-...` created for order `019FA22A-...` (log: "Bug 6 fix: Created customer stub ...").
  2. **Nested transaction error** (`LoyaltyRewardsService.AddPointsAsync`): `TransitionStatusAsync` begins a DB transaction, then calls `ProcessLoyaltyPointsAsync` → `AddPointsAsync`. `AddPointsAsync` called `BeginTransactionAsync` again → SQLite does not support nested transactions → exception → order transition rolled back → 404 returned to client. Fix: `AddPointsAsync` now supports ambient transactions — if a transaction is already active on the DbContext, it joins it (no explicit BeginTransaction/Commit/Rollback — caller owns lifecycle). Commit `d5e67a80`.
  3. **Tenant filter excluded customer stub** (`LoyaltyRewardsRepository.GetCustomerByIdAsync` + `GetByCustomerIdAsync`): Both lookups used default DbContext query which applies the global `TenantId` query filter. When customer stub was created with `order.TenantId` but ambient `ITenantProvider.TenantId` did not match (SystemAdmin impersonation context), the lookup returned null → `AddPointsAsync` threw "Customer not found". Fix: added `IgnoreQueryFilters()` to both lookup methods. CustomerId is globally unique PK — tenant filtering not needed for direct ID lookups. Commit `743c32dd`.
  **End-to-end RV (2026-07-27 07:07 UTC):** New order `019fa266-075d-74d1-9415-7b60bec7fd66` (CustomerDeviceId `b2c3d4e5-...`, 3 × Cafe Sua Da @ 25,000 VND = 82,500 VND total) → transitioned pending→confirmed→preparing→ready→delivered→completed (all HTTP 200) → **8,250 loyalty points awarded** (82,500 × 0.1 rate = 8,250) to customer `5e375512-6f5a-4175-ad63-7142947ede36`. Log evidence: "🎁 LOYALTY: Awarded 8250 points to customer 5e375512-... from order 019fa266-... (rate=0.1, min=10, max=none)" + "Enqueued LoyaltyPointsChanged event to Outbox (PointsChange=8250, NewBalance=8250)" + "Published loyalty points changed event to NATS".
- **Bug 5 SignalR Fix (2026-07-27 COMPLETE + MERGED + DEPLOYED):** OrderHub `[Authorize]`→`[AllowAnonymous]` — SignalR `/orderHub/negotiate` was returning 401 (Blazor Server client cannot pass auth cookie), breaking real-time updates. Also added explicit `StateHasChanged()` after diff check in Index.razor + Kitchen/Display.razor. RV: `/orderHub/negotiate` now returns 200 (was 401).
- **4-Bug Fix (2026-07-27 COMPLETE + MERGED + DEPLOYED):** (1) Order List default filter EMPTY→ALL, (2) CustomerNotes sync PG→SQLite + UI render, (3) Remove AsNoTracking from GetByIdWithIncludesAsync (fix confirm order exception), (4) Parse CustomerId in OrderSyncSubscriber + auto-create Customer stub. RV verified: CustomerNotes + CustomerId sync to SQLite confirmed via direct DB query.
- **Sprint 0 (2026-07-26 COMPLETE + MERGED + DEPLOYED):** 11 entities + 42 tests + migration `20260726105331_CommunitySprint0` applied to local + VPS PG. RiskScoringService (8-factor deterministic) + WalletService base (atomic SELECT FOR UPDATE). FingerprintJS stub vendored.
- **VPS:** Live at `diemthuong.khachvip.online` (KhachLink), `app.khachvip.online` (ShopERP), `api.khachvip.online` (Gateway). 7 containers healthy. CD deploys automatically on push to main.
- **Local infra:** Docker PostgreSQL 15-alpine (5432) + NATS 2-alpine (4222) + ShopERP 5003 + KhachLink 5002 + Gateway 5001.
- **Tech debt:** TD-MVPS-001 through TD-MVPS-004 (see `docs/AI/tasks/tech_debt_multi_vps_checkout.md`). TD-PWA-001 (WASM conversion complete). Tier 5 — True Offline Edge (post-PoC). **TD-CUSTSYNC-001 (2026-07-27):** Customers created in ShopERP SQLite (CRM local) are NOT synced to Gateway PG — Gateway `OrderService.CreateOrderFromCommandAsync` validates CustomerId against PG and falls back to null if missing. Bug 6 fix mitigates this for guest checkout (DeviceId fallback + stub creation in SQLite), but full Customer sync SQLite→PG still needed for cross-system customer identity.

---

## 4. Next Actions

1. **Loyalty/CRM Audit Fix — P1-T2 (S3):** 19 missing tests (5 toggle + 10 URL validation + 4 cross-tenant) per `loyalty_crm_audit_fix_master_plan-2c5017.md` §2 P1-T2. TDD violation fix.
2. **Loyalty/CRM Audit Fix — P1-T3 (S4):** Missions pagination — `GET /api/missions/my/completions?page=2&pageSize=20` + `Missions.razor` "Xem thêm" button. Per master plan §2 P1-T3.
3. **Loyalty/CRM Audit Fix — P2 (S5):** UX completions (row action, bulk, progress, expand, column) — includes deferred "Push Subscribed" column from P1-T1.
4. **Loyalty/CRM Audit Fix — P3 (S6) + Final (S7):** Cosmetic (file extract + CSV) + build/guard-check/VPS RV/merge to `main`.
5. **Community Commerce Sprint 1 — Nearby Orders** — per `task_cc_sprint1_nearby_orders-2c5017.md`. Requires Domain Modification #2: OrderStatuses.Default[] + "delivering" status. **Needs user approval for Domain Modification.**
6. **Replace FingerprintJS stub** — Download real FingerprintJS v4 (MIT) before production deployment.
7. **Phase 8 — Multi-VPS E2E Validation (Playwright)** — per `phase8_multi_vps_e2e_task_card.md`. 7 E2E scenarios.
8. **Tech debt cleanup** — TD-MVPS-001 through TD-MVPS-004. **TD-CUSTSYNC-001:** Customer sync SQLite→PG.
9. **(Cosmetic)** Fix `?` in Checkout.razor. Fix `isTabVisible` display bug in OrderTracking.razor.
10. **(Env)** Fix local DB role mismatch — ShopERP `vanan_admin` vs Gateway `vanan_dev`.

---

## 5. Active Architecture Decisions

| Decision | Lý do |
|---|---|
| Gateway = Order Creator + Routed Async Delivery (Option C) | Multi-VPS support, PG source of truth, NATS routed by ShopInstanceId |
| CoreHub = in-process background service trong Gateway | Monolith Phase 1-2 |
| ShopERP = SQLite (Business) + PostgreSQL (Accounting) | ADR-001: accounting always online |
| CustomerToken = `IDataProtector` | Tránh library mới |
| `AccountingEntry` immutable, Reversal Entry | Audit trail bắt khu xâm phạm |
| Multi-tenancy `TenantId` filter mọi layer | Data isolation per HKD |
| EF Core Migrations = official schema management | Stream E |
| Dual Deployment Modes: SaaS (all-in-one) + Edge (tách biệt) | See Section 5a |

### 5a. Deployment Modes

**SaaS:** `docker-compose.prod.yml` — all modules on 1 VPS. Gateway → PG. ShopERP → SQLite. KhachLink → Gateway (HTTP).

**Edge:** `docker-compose.edge.yml` — Server A (Edge): ShopERP + SQLite + NATS sync. Server B (Central): Gateway + PG + KhachLink. Sync via NATS Outbox.

---

## 6. History Log (compressed — see archive + git log)

* [2026-07-27] **KHACHLINK BUGS 1-3 FIX COMPLETE.** Commit `35dc9de6`. (1) Profile page points/birthday/push not working + (2) Missions page no data — root cause: `[AllowAnonymous]` customer-facing endpoints had `ITenantProvider.TenantId=Guid.Empty` (no tenant claim in token auth) → global TenantId query filter excluded all customer data. Fix: new `[ResolveCustomerTenant]` action filter resolves tenant from customer token + `IgnoreQueryFilters` lookup + `ITenantProvider.SetTenant()`. Applied to 6 controllers. (3) Order history ID mismatch — changed `[^8..]` to `[..8]` to match ShopERP. CD #30247192483 PASS. RV: all 5 endpoints return 200 with correct data.
* [2026-07-27] **BUG 5+6 FIX COMPLETE.** Commit `30e42e69`. (5) OrderHub `[Authorize]`→`[AllowAnonymous]` — SignalR negotiate 401→200, real-time updates restored. Explicit `StateHasChanged()` added to Index.razor + Kitchen/Display.razor. (6) `ProcessLoyaltyPointsAsync` DeviceId fallback + Customer stub creation — fixes loyalty points for guest checkout orders (CustomerId=NULL). CD #30241063324 PASS. RV: `/orderHub/negotiate` 200, new order has CustomerDeviceId+CustomerInfo.
* [2026-07-27] **4-BUG CHECKOUT-TO-KITCHEN FIX COMPLETE.** Commit `4af5672e`. (1) Order List default filter, (2) CustomerNotes sync+UI, (3) AsNoTracking confirm fix, (4) CustomerId sync+stub. CD PASS. RV: CustomerNotes + CustomerId sync verified via SQLite query. TD-CUSTSYNC-001 logged.
* [2026-07-26] **SPRINT 0 COMPLETE.** 11 entities + 42 tests + migration. Merged + deployed. RV 18/18.
* [2026-07-26] **DOC v1.4-v1.1 COMPLETE.** 4 doc-only sessions. Hybrid architecture + cost + review fixes + anti-fraud.
* [2026-07-24] **LOYALTY L-C COMPLETE.** RV 57/57. Gamification + config UI + notification jobs.
* [2026-07-24] **LOYALTY L-B COMPLETE.** RV 13/13. Redemption system.
* [2026-07-24] **LOYALTY L-A + PHASE 5 PUSH COMPLETE.** Configurable formula + push notifications.
* [2026-07-23] **PRODUCT PICKER + ORDER STATUS UNIFICATION.** RV 4/4.
* [2026-07-23] **FONT FIX + FREEZE FIX.** Double-encoding + IAsyncDisposable.
* [2026-07-22] **THEME + PWA PHASES 1-3.** 5 themes. Blazor Server → WASM. Offline caching.
* [2026-07-20] **MULTI-VPS OPTION C PHASES 1-7 COMPLETE.** ShopInstance + Order Creator + NATS routed.
* [2026-07-18] **MULTI-TENANT BUG FIX + QUICK-SETUP REAL.** 5 commits.
* [2026-07-17] **SINGLE-IDENTITY REFACTOR COMPLETE.** All 5 entities. VPS verified.
* [2026-07-16] **UUIDv7 REFACTOR + DATA SYNC HARDENING.**
* [2026-07-15] **ORDER SYNC TRACK E1 COMPLETE.** Option D. RC-1/2/3 fixed.
* [2026-07-14] **KHACHLINK E2E VPS PASS + UI/UX FIX BATCH.**
* [2026-07-13] **TIERED AUTH P1-P3 RV COMPLETE.** 14/14.
* [2026-07-09-10] **ACCOUNTING POSTGRESQL ONLINE.** 3 waves. 1223/1223.
* **Older:** See `docs/AI/project_state_archive.md`.

---

## 7. Active Files Reference

| File | Role |
|---|---|
| `docs/AI/tasks/task_cc_sprint0_foundation-2c5017.md` | Sprint 0 task card (COMPLETE) |
| `docs/AI/tasks/task_cc_sprint1_nearby_orders-2c5017.md` | Sprint 1 task card (NEXT) |
| `docs/AI/tasks/sprint1_nearby_orders_detailed_plan-2c5017.md` | Sprint 1 detailed plan |
| `docs/AI/tasks/community-commerce-master-plan-2c5017.md` | Community Commerce master plan |
| `docs/AI/tasks/community-commerce-requirements-spec-2c5017.md` | Requirements spec v1.4 |
| `docs/AI/tasks/tech_debt_multi_vps_checkout.md` | Tech debt register |
| `docs/Architecture/ADR001-Station-Architecture.md` | ADR-001 v3 (Option C) |
| `docs/AI/project_state_archive.md` | Archived history |

---

## 8. Architecture Quick Reference

```
=== SaaS Mode (docker-compose.prod.yml) ===
KhachLink (5002) → Gateway (5001) → ShopERP (5003) → SQLite (local)
                       ↓
              [in-process CoreHub]
                       ↓
                  PostgreSQL (central)

=== Edge Mode (docker-compose.edge.yml) ===
Server A (Edge):              Server B (Central):
  ShopERP → SQLite              Gateway → PostgreSQL
  NATS sync worker              [in-process CoreHub]
       ↓ NATS ↓
  ---------------→ Gateway
                   KhachLink → Gateway (HTTP)
```

**Auth:** Cookie (Blazor Server) + JWT Bearer (API). `DevLoginController` (`#if DEBUG`) for E2E.
**Roles:** `UserRole` (tenant-scoped) + `PlatformRole` (cross-tenant: SystemAdmin).

---

## 9. AI Health Check

- **Assumptions:** 0
- **Verified Facts:** Branch=fix/loyalty-crm-audit-fix, commit=2059f403, Build=0 errors (1047 warnings pre-existing CA), guard-check ALL PASSED, 6/6 P1-T1 tests PASS (3 repo cross-tenant + 3 HTTP auth), 31/31 Customer/Excel/LoyaltyRewards tests PASS (no regression)
- **Open Questions:** 0
- **Gate 6 Status:** ✅ Assumptions < Verified Facts, Open Questions < 3

---

## 10. Maintenance Log

* **2026-07-27 — LOYALTY/CRM AUDIT FIX P1-T1 (S2).** Commit `2059f403` on `fix/loyalty-crm-audit-fix` (NOT yet merged). 6 files: `ICustomerRepository.cs` (+`GetAllCustomersAcrossTenantsAsync`), `CustomerRepository.cs` (impl with `IgnoreQueryFilters`), `CustomerController.cs` (+`ListGlobal` action `[Authorize(Policy="SystemAdmin")]` + `GlobalCustomerDto` with TenantId), `CustomerListGlobal.razor` (REWRITE — customer table + Tenant column + filter bar, replaces campaign overview), `CustomerRepositoryCrossTenantTests.cs` (3 tests), `CustomerGlobalEndpointAuthTests.cs` (3 tests + `StaffRoleWebApplicationFactory`). Build 0 errors, guard-check ALL PASSED, 6/6 new tests PASS, 31/31 regression tests PASS. Deviation: "Push Subscribed" column deferred to P2. Branch: `fix/loyalty-crm-audit-fix`.
* **2026-07-27 — LOYALTY/CRM AUDIT FIX P0 (S1).** Commit `4aa0c6e2` on `fix/loyalty-crm-audit-fix`. `CustomerController` + `PromoCampaignController` `[Authorize]`→`[Authorize(Policy="OwnerOnly")]`; `IPromoCampaignService` moved `3_CoreHub/Services`→`1_Shared/Services`; `CustomerSegmentCriteria` moved to `1_Shared/Domain`. Build 0 errors, guard-check PASSED.
* **2026-07-27 — KHACHLINK BUGS 1-3 FIX.** Commit `35dc9de6` merged + deployed. 8 files: new `Filters/ResolveCustomerTenantAttribute.cs` (action filter — resolves customer TenantId from token via IgnoreQueryFilters + ITenantProvider.SetTenant), 6 controllers decorated (`CustomerIdentityController`, `CustomerProfileController`, `LoyaltyController`, `MissionsController`, `NotificationsController`, `RedemptionController`), `OrderHistory.razor` (`[^8..]`→`[..8]` to match ShopERP). CD #30247192483 PASS. RV: OTP login → 5 endpoint tests all return 200 with correct data (profile, birthday save+persist, push subscribe, missions progress, loyalty info). Branch: `main`.
* **2026-07-27 — BUG 5+6 FIX.** Commit `30e42e69` merged + deployed. 4 files: OrderHub.cs (`[Authorize]`→`[AllowAnonymous]`), OrderWorkflowService.cs (DeviceId fallback + Customer stub creation in ProcessLoyaltyPointsAsync), Index.razor (explicit StateHasChanged), Display.razor (explicit StateHasChanged). CD #30241063324 PASS. RV: `/orderHub/negotiate` 200 (was 401), new order `019FA22A` confirmed has CustomerDeviceId+CustomerInfo in SQLite. Branch: `main`.
* **2026-07-27 — 4-BUG CHECKOUT-TO-KITCHEN FIX.** Commit `4af5672e` merged + deployed. 5 files: OrderRepository.cs (AsNoTracking), OrderService.cs (CustomerNotes payload), OrderSyncSubscriber.cs (parse notes + CustomerId + Customer stub), Index.razor (default filter + notes column), Display.razor (notes block). CD PASS. RV: checkout flow verified on VPS — CustomerNotes + CustomerId sync to SQLite confirmed. TD-CUSTSYNC-001 logged (Customer SQLite→PG sync gap). Branch: `main`.
* **2026-07-26 — PROJECT STATE ARCHIVED.** Reduced from 627 → ~170 lines. All Previous Objectives (Doc v1.1-v1.4, Phase 5, Loyalty L-A/L-B/L-C, Product Picker, Font/Freeze Fix, Theme, PWA Phases 1-3, Multi-VPS Option C) + full History Log + full Maintenance Log moved to `docs/AI/project_state_archive.md` (Section "Archived 2026-07-26"). Branch: `main`.
* **2026-07-26 — SPRINT 0 REVIEW + PARTIAL FIX.** Review-only audit found 8 items marked COMPLETE but not 100% production. Part 1: F2/F4/F5a (dead code pending callers) added to correct downstream sprint task cards (Sprint 1: AssignShipper/SetDeliveryLocation; Sprint 4: AssignSalesman + RiskScoringService caller + WalletService app-install caller; Sprint 5: MarkCodCollected + WalletService COD/Advance/Settlement caller). Sprint 4 + Sprint 5 task cards fixed: WalletService "Files cần CREATE" → "MODIFY" (Sprint 0 đã tạo base). Part 2 in progress: F5b (WalletService SQLite comment fixed), F6 (migration scope note added to SC9), F7 (SC13 phrasing fixed). Branch: `main`.
