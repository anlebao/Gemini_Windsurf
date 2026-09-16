# Project State

> **Mục đích:** Single Source of Truth cho AI về trạng thái dự án. BẮT BUỘC đọc đầu mỗi phiên.
> **Archived:** 2026-07-24 + 2026-08-03 + 2026-08-09 + 2026-08-23 + 2026-09-06 + 2026-09-15 — All completed objectives + full history/maintenance log moved to `docs/AI/project_state_archive.md`

---

## 0. Maintenance Rules

1. One-and-only-one: Mỗi section chỉ tồn tại 1 lần.
2. No contradiction: Một hạng mục chỉ có 1 trạng thái.
3. Ground Truth first: Verify path/branch với codebase trước khi ghi.
4. Now over History: Section 2-4 chỉ mô tả việc ĐANG làm và KẾ TIẾP. Việc xong gom vào archive.
5. Actionable Next Actions: Xóa action đã quá hạn/sai bối cảnh.
6. Stamp every edit: Cập nhật Section 10 mỗi lần sửa.

---

## 1. Project Overview

**Dự án:** Vạn An Accounting System MVP — giải pháp kế toán HKD theo TT 152/2025/TT-BTC.
**Stack:** .NET 8 — EF Core — SQLite — Blazor Server (ShopERP) — Blazor WebAssembly (KhachLink PWA) — Blazor SSR (Directory) — SignalR — YARP Gateway — xUnit — Playwright.
**Kiến trúc:** Clean Architecture + DDD + Multi-tenancy. Data flow: `KhachLink WASM/SSR (5002) -> Gateway (5001) -> ShopERP (5003) -> SQLite`.
**Modules:** `1_Shared` (Domain + Services contracts) — `2_Gateway` (YARP) — `3_CoreHub` (Services, in-process) — `5_WebApps/ShopERP` (Blazor Server) — `5_WebApps/KhachLink` (Blazor WASM, served by nginx) — `5_WebApps/Directory` (Blazor SSR, Directory-profile tenants) — `UI.Platform` (Shared components) — `6_Tests`.
**Hard stops:** Domain PURE — `AccountingEntry` immutable — Gateway = Order Creator + Routed Async Delivery (Option C) — KhachLink HTTP-only — ShopERP SQLite (Business) + PostgreSQL (Accounting) — ALWAYS dùng UI Platform components.

**VPS Access (GCP — for RV + manual deploy):**
- GCP project: `vanan-prod` (gcloud SDK at `C:\Users\lebao\AppData\Local\Google\Cloud SDK\google-cloud-sdk\bin\gcloud.cmd`)
- SSH command pattern: `gcloud compute ssh <INSTANCE_NAME> --zone <ZONE> --project vanan-prod`
- Instances (4): `vanan-gateway` (asia-southeast1-a, 136.85.94.119) · `vanan-shop-a` (asia-southeast1-b, 34.177.89.248) · `vanan-khachlink` (asia-southeast1-c, 136.85.111.51) · `vanan-khachlink-20260815-timlathay-com` (asia-southeast1-c, 136.85.78.51)
- CD: `cd-multivps.yml` (push to `main`) deploys to all 3 VPS + smoke tests. Legacy `cd.yml` (push to `oracle-prod`) — SSH broken since 2026-08-06, use multi-VPS CD only.

---

## 2. Current Objective

**COMMUNITY COMMERCE FULL FLOW — Batch 2 (4 RV defects) ✅ COMPLETE (session 2026-09-16).**

User requested RV full-flow Community Commerce (2026-09-16). RV found 4 pre-existing defects. Master plan at `docs/AI/tasks/community_commerce_fixes_batch2/master_plan.md`. All 4 fixed + deployed + RV PASS.

**Issue #1 (Gateway OrdersController 400 "Operation is not valid") — ✅ COMPLETE + DEPLOYED + RV PASS (commit `73b0133a` on `main`):**
- Root cause: `IOrderWorkflowService` NOT registered in Gateway `Program.cs` DI. `OrdersController` constructor injects it → DI resolution throws `InvalidOperationException` → `UnifiedErrorHandler` returns 400 for ALL `OrdersController` endpoints (GET /api/orders, PUT /api/orders/{id}/status, etc.).
- Fix (1 file, 1 line): Register `IOrderWorkflowService` → `OrderWorkflowService` in `2_Gateway/Program.cs`. ShopERP `Program.cs` line 216 already had this; Gateway was missing it.
- RV: `GET /api/orders` → 200 (was 400) ✅ · `PUT /api/orders/{id}/status` → 204 (was 400) ✅.

**Issue #2 (DeliveryWorkflowService bypassed OrderWorkflowService — no loyalty/NATS/accounting) — ✅ COMPLETE + DEPLOYED + RV PASS (commits `73b0133a` + `b9f0fc0a` + `67b91fe3` on `main`):**
- Root cause: `DeliveryWorkflowService.TransitionStatusAsync` directly called `order.UpdateOrderStatus("completed")` + `SaveChanges`, bypassing `OrderWorkflowService.TransitionStatusAsync` which handles loyalty points, NATS Outbox sync, accounting entries, referral commission.
- 3 sub-fixes:
  1. `73b0133a` — Inject `IOrderWorkflowService` into `DeliveryWorkflowService`, delegate "completed" transition.
  2. `b9f0fc0a` — Decouple loyalty/stats side-effects from order transition in `HandleOrderCompletedAsync` (loyalty failure no longer rolls back order transition — was causing 404 when Customer.PhoneNumber decryption failed on SQL-inserted test data).
  3. `67b91fe3` — Set tenant context from DeliveryTask before calling `OrderWorkflowService` (customer-token auth doesn't set tenant context like JWT auth → multi-tenancy query filter couldn't find order).
- RV: Shipper delivered → Order status=completed ✅ · 4 Outbox events all Processed (OrderCreated + OrderStatusChanged×2 + OrderCompleted) ✅.

**Issue #3 (Public tracking 404 khi CustomerId set) — ✅ COMPLETE + DEPLOYED + RV PASS (commit `0948658a` on `main`):**
- Root cause: `GetByIdWithIncludesAsync` + `GetByIdWithIncludesIgnoreFiltersAsync` used `Include(o => o.Customer)`. `Customer.PhoneNumber` encrypted via `EncryptedStringConverter` — when Customer row had corrupt/unencrypted phone data (SQL-inserted test data), Include triggered decryption → `CryptographicException` → repository catch → returns null → 404.
- Fix (1 file): Remove `Include(o => o.Customer)` from both methods. No caller accesses `order.Customer` — they only use `order.CustomerId` (FK value, not navigation).
- RV: `GET /api/public/orders/{id}` with CustomerId set → 200 (was 404) ✅.

**Issue #4 (Checkout orders có CustomerId=NULL) — ✅ COMPLETE + DEPLOYED + RV PASS (commit `65ab0d3b` on `main`):**
- Root cause: `KhachLinkLayout.SubmitFreeOrderDirectAsync` hardcoded `CustomerId = (Guid?)null`. When customer logged in (has `customer_token` + `customer_id` in localStorage), free-order bypass flow still created orders with CustomerId=NULL → chat unavailable + loyalty points not awarded.
- Fix (1 file): Read `customer_token` + `customer_id` from localStorage, validate token via `/api/customers/me`, pass CustomerId if valid. Same pattern as `Checkout.razor` line 545-565.
- RV: Free-order checkout with customer login → CustomerId set in PG ✅ · DELIVERY order with CustomerId → chat history 200 (conversation created) ✅ · Send customer message → 200 ✅.

**All 4 Batch 2 issues COMPLETE. Full chain works end-to-end.**

**Previous community commerce work (Batch 1 + related fixes — all COMPLETE):**
- Issue #175 (shipper GPS + salesman QR) — ✅ DEPLOYED + RV PASS (`f2f5dc2a`).
- Chat feature fix — ✅ DEPLOYED + RV PASS (`0cb12cd4` + `fba1fce4`).
- Floating cart + free/charity bypass checkout — ✅ DEPLOYED + RV PASS (`c33e89e5`).
- Community Commerce Issues #1-4 (Batch 1: OrderType DELIVERY + NATS sync + GPS mock + X-Dev-OTP gate) — ✅ COMPLETE + DEPLOYED + RV PASS.

---

## 3. Current Status

- **Branch:** `main` (Community Commerce Batch 1 + Batch 2 + Chat fix + Floating cart + Issue #175. KhachLink Profile Transition Sprint 1+2. GTM W2 + currency fix. W1 Merchant Audit + nginx Directory SSR. R2.2 Reseller Accounting — PR #169. Crawl-to-Onboard 8 phases. Issue #103/#157/#161/#156 deployed).
- **Build full sln:** 0 errors · **CI:** PASS · **.NET SDK:** 8.0.422
- **Community Commerce Full Flow (session 2026-09-16):** ✅ Batch 1 (Issues #1-4) + Batch 2 (4 RV defects) + Chat fix + Floating cart + Issue #175 ALL COMPLETE + DEPLOYED + RV PASS. Full chain works end-to-end: salesman QR → customer checkout (DELIVERY + CustomerId) → owner confirm via Gateway → shipper accept/pickup/delivering/delivered → order completed + Outbox events + NATS sync → chat customer↔shipper → loyalty points awarded → public tracking works. See Section 2.
- **KhachLink Profile Transition Sprint 3:** ✅ CODE COMPLETE on `feature/khachlink-sprint3-audit-sw` @ `d29e621b`. Pending: push → PR → merge → CD → RV Layer 1-5.
- **Financial Intelligence MVP-2:** ✅ All 5 phases complete on feature branch (61/61 tests PASS). Pending: push + PR + CD + RV.
- **Tech debt:** TD-MVPS-001→004, TD-CUSTSYNC-001, TD-ASYNCDP-001, TD-GCP-001, TD-NETDASH-001, TD-OCR-01→05

---

## 4. Next Actions

**Community Commerce Full Flow (✅ Batch 1 + Batch 2 + Chat + Cart + Issue #175 ALL COMPLETE):**
- ✅ Batch 1 Issues #1-4 (OrderType DELIVERY + NATS sync + GPS mock + X-Dev-OTP gate) — DEPLOYED + RV PASS
- ✅ Batch 2 Issue #1 (Gateway OrdersController DI) — DEPLOYED + RV PASS (`73b0133a`)
- ✅ Batch 2 Issue #2 (DeliveryWorkflowService bypass) — DEPLOYED + RV PASS (`73b0133a` + `b9f0fc0a` + `67b91fe3`)
- ✅ Batch 2 Issue #3 (Public tracking 404 with CustomerId) — DEPLOYED + RV PASS (`0948658a`)
- ✅ Batch 2 Issue #4 (Checkout CustomerId=NULL) — DEPLOYED + RV PASS (`65ab0d3b`)
- ✅ Issue #175 shipper GPS + salesman QR — DEPLOYED + RV PASS (`f2f5dc2a`)
- ✅ Chat feature fix — DEPLOYED + RV PASS (`0cb12cd4` + `fba1fce4`)
- ✅ Floating cart + free/charity bypass checkout — DEPLOYED + RV PASS (`c33e89e5`)
- Branch: `main` · Build: 0 errors · All 4 Batch 2 issues RV verified on production VPS.

**KhachLink Profile Transition Sprint 3 (pending push/PR/RV):**
- Push branch `feature/khachlink-sprint3-audit-sw` → `gh pr create` → merge → CD Multi-VPS deploy
- RV Layer 1-5 on production (timlathay.com + diemthuong2.khachvip.online + app2.khachvip.online):
  - L1 API: `GET /api/audit-trail/entity/12/{instanceId}` + `GET /api/audit-trail/recent?count=10`
  - L2 Static: `GET /js/onboarding-tour.js` → verify `vananTriggerSWUpdate` present
  - L3 Playwright: `npx playwright test profile-transition` → Sprint 3 tests PASS
  - L4 UI flow: admin change profile → audit row (chờ ~5-10s async flush) · `/admin/audit-trail` summary cards · failed login → FailedLogin row · rate limit → RateLimitHit row · toggle RV (tắt Audit_KhachLink → không row · bật lại → row) · accounting sync RV (tạo bút toán → audit row NGAY)
  - L5 Manual: KhachLink diemthuong2 → change profile → reopen → SW update triggered + nav updated
- Manual RV Sprint 2 gaps: admin change diemthuong2 FullCommerce→Directory (cart modal) + Directory→FullCommerce (onboarding tour)

**GTM Drill Machine MVP (✅ W1 + W2 COMPLETE — W3 NEXT):**
- Next: W3 Revenue Proof (D1 domain mod — counters on tenant GrowthDashboard) → W4 Referral (D2) → W5 consent + flag `GrowthMachine:Enabled` + deploy + RV
- DEFER theo Gates: AI SDR/auto scoring (G1 — 0 telemetry), search counter, sitemap toàn site (BOM #4 T2), payment gateway (G2)

**Financial Intelligence MVP-2 (active):**
1. Push branch `feature/financial-intelligence-mvp2` (await user approval)
2. `gh pr create` → merge → CD Multi-VPS deploy
3. RV L1-L5

**Pending manual RV (carry-over):**
- Crawler post-deploy RV (commit `942467e0`): trigger crawl → verify >20 Pending tenants + filters applied
- Post-deploy RV (commits `e7848be9`, `1eeb4615`, `6c9182da`, `5c5a07c5`): RV timlathay.com (Blazor web.js + WebSocket + markers) + diemthuong2 (ThemeType + SW v19) + Issue #161 (accounting date) + Issue #156 (nav groups)
- Issue closure: #130 (Guard QR creation) + #126 (Guard QR Verify) — pending VPS RV + close

**Deferred / monitoring:**
- R2 (S4 EasyOCR) — deferred until VPS upgrade (4GB RAM) + tenant demand
- GCP Data Seeding — seed production data (fresh DB only 3 test tenants)
- #99-3 Phase B — Alliance VND Normalization (awaiting user approval)
- Hybrid Strategy Bước 2 — trigger when CPU > 70% / Memory > 80%
- Post-Sprint 7 flaky tests — 4 EInvoiceOrchestratorTests (skipped via CI filter)
- v3.0 deferred — INV-009, payment provider (VNPay/Momo), Ops Cost, Tier Distribution
- nginx deferred task cards — per-user rate limit, Blazor API aggregation, API classification
- R2.2 follow-up: Configure `PlatformAccountingTenantId` SystemSetting in production DB (1-time SysAdmin setup)

---

## 5. Active Architecture Decisions

| Decision | Lý do |
|---|---|
| Gateway = Order Creator + Routed Async Delivery (Option C) | Multi-VPS support, PG source of truth, NATS routed by ShopInstanceId |
| CoreHub = in-process background service trong Gateway | Monolith Phase 1-2 |
| ShopERP = SQLite (Business) + PostgreSQL (Accounting) | ADR-001: accounting always online |
| `AccountingEntry` immutable, Reversal Entry | Audit trail bắt khu xâm phạm |
| Multi-tenancy `TenantId` filter mọi layer | Data isolation per HKD |
| Loyalty Alliance = Option B (HTTP proxy + cache + idempotency) | Multi-VPS ready, ShopERP does NOT connect to PG directly |
| nginx 5-layer rate limit | Separate API/page/auth/WebSocket/static quotas — prevents 503 on fast navigation |
| Directory SSR = separate container, nginx map-based routing | Directory-profile tenants get <1s SSR load; Commerce domains keep WASM |

**Deployment Modes:** SaaS (`docker-compose.prod.yml` — all on 1 VPS) ‖ Edge (`docker-compose.edge.yml` — Server A: ShopERP+SQLite+NATS, Server B: Gateway+PG+KhachLink).

---

## 6. History Log (compressed — see archive + git log)

* [2026-09-15] **COMMUNITY COMMERCE ISSUES #1-4 COMPLETE.** Issue #1 OrderType DELIVERY (`26b060ee`): `Order.SetOrderType()` + CreateOrderCommand + Checkout.razor selector. Issue #2 NATS sync (`369b2986`): `OrderSyncSubscriber` read OrderType from payload + `OrderService` add delivery fields to Outbox event. Issue #3 GPS mock (`5b97bcf9`): shared `gps-mock.ts` helper injected into 3 e2e specs. Issue #4 X-Dev-OTP gate: removed X-Dev-OTP from `/otp/send` + `/upgrade/send-otp` + added `POST /api/customer-identity/dev-token` (secret-gated). Full chain: checkout → DELIVERY order → NATS sync → SQLite → owner confirm → shipper sees order. See Section 2.
* [2026-09-14] **CHARITY CHECKOUT FLOW C1-C3 + 3 COMMUNITY COMMERCE BUGS.** Charity: `9b7d0c8e` + `f505a242` (ExecuteAtomicAsync + AllItemsFree + Charity_Donation_Enabled). Community: `f39c8649` + `88f3496f` + `6fe17d31` (DeliveryTracking route + GPS-optional + gateway HttpClient).
* **Older (2026-09-13 and before):** KhachLink Profile Transition Sprint 1-3, GTM W1-W2, R2.2 Reseller Accounting, Crawl-to-Onboard 8 phases, Directory SSR, Issue #103/#156/#161/#157, Financial Intelligence MVP-2, OCR Hub R1, Dynamic CORS, Multi-VPS Option C. See `docs/AI/project_state_archive.md`.

---

## 7. Active Files Reference

| File | Role |
|---|---|
| `docs/AI/tasks/community_commerce_fixes/` | Community Commerce master plan + 4 task cards (Issues #1-3 COMPLETE, #4 DEFER) |
| `docs/AI/tasks/khachlink_profile_transition_ux/` | KhachLink Profile Transition UX (Sprint 1+2 deployed, Sprint 3 pending push) |
| `docs/AI/tasks/gtm_drill_mvp/` | GTM Drill Machine MVP (W1-W2 COMPLETE, W3-W5 planned) |
| `docs/AI/tasks/task_financial_intelligence_mvp2.md` | Financial Intelligence MVP-2 task card (5 phases complete, pending PR) |
| `docs/Architecture/ADR001-Station-Architecture.md` | ADR-001 v3 (Option C) |
| `docs/AI/plans/ecosystem-master-business-model.md` | Ecosystem Master Business Model v1.0 |
| `docs/AI/project_state_archive.md` | Archived history (2026-07-24 + 2026-08-03 + 2026-08-09 + 2026-08-23 + 2026-09-06 + 2026-09-15) |

---

## 8. Architecture Quick Reference

```
=== SaaS Mode (docker-compose.prod.yml) ===
KhachLink WASM/SSR (5002) → Gateway (5001) → ShopERP (5003) → SQLite (local)
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
- **Verified Facts:** Branch=`main` @ `65ab0d3b` (Community Commerce Batch 2 — 4 RV defects fixed). Batch 2 Issue #1: Gateway `IOrderWorkflowService` DI registration → `GET /api/orders` 200 (was 400) + `PUT /api/orders/{id}/status` 204 (was 400). Issue #2: `DeliveryWorkflowService` delegates to `OrderWorkflowService` + tenant context set + loyalty/stats decoupled → shipper delivered → order completed + 4 Outbox events Processed. Issue #3: Remove `Include(o.Customer)` → public tracking 200 with CustomerId (was 404). Issue #4: KhachLink free-order checkout sends CustomerId from localStorage → chat works immediately after checkout. Build 0 errors · Guard ALL PASSED · 11/11 DeliveryWorkflowServiceTests PASS (incl. T11 delegation) · CI PASS · CD Multi-VPS PASS.
- **Open Questions:** 4 (PlatformAccountingTenantId not configured · Turnstile keys not configured · Rate limit uses container IP not forwarded client IP · Sprint 1 RV partial — Directory-profile WASM domain needed for P2.2/P2.3 RV)
- **Gate 6 Status:** ✅ Assumptions (0) < Verified Facts (30+), Open Questions (4) → CLEAR

---

## 10. Maintenance Log

> Full historical maintenance log: see `docs/AI/project_state_archive.md`.

* **2026-09-16 — COMMUNITY COMMERCE BATCH 2 (4 RV DEFECTS) COMPLETE + DEPLOYED + RV PASS.** Master plan: `docs/AI/tasks/community_commerce_fixes_batch2/master_plan.md`. Issue #1 (`73b0133a`): Gateway `IOrderWorkflowService` DI registration — `OrdersController` 400 "Operation is not valid" → 200/204. Issue #2 (`73b0133a` + `b9f0fc0a` + `67b91fe3`): `DeliveryWorkflowService` delegates to `OrderWorkflowService` + loyalty/stats decoupled + tenant context set → shipper delivered → order completed + 4 Outbox events Processed. Issue #3 (`0948658a`): Remove `Include(o.Customer)` from `GetByIdWithIncludesAsync` + `GetByIdWithIncludesIgnoreFiltersAsync` → public tracking 200 with CustomerId (was 404 due to `CryptographicException` on corrupt phone data). Issue #4 (`65ab0d3b`): `KhachLinkLayout.SubmitFreeOrderDirectAsync` reads `customer_id` + `customer_token` from localStorage + validates via `/api/customers/me` → CustomerId set in PG → chat works immediately after checkout. Build 0 errors · Guard ALL PASSED · 11/11 DeliveryWorkflowServiceTests PASS (incl. T11 delegation) · CI PASS · CD Multi-VPS PASS. RV: all 4 issues verified on production VPS.
* **2026-09-15 — ISSUE #175 FIX COMPLETE (pending commit/deploy).** Bug #1 shipper GPS: `NearbyOrders.razor` `GeolocationResult {Latitude,Longitude}` vs JS `vananPWA.getCurrentPosition` `{lat,lng}` mismatch → always 0,0 → "Không lấy được vị trí GPS" blocked all orders (Issue #3 GPS-mock masked it in Playwright). Same silent bug in `StoreFinder.razor`. Fix: `GpsPosition {Lat,Lng}` in both pages. Bug #2 salesman QR "Không thể tạo mã QR": `GetCompositeSalesmanQrAsync` returned null when `CommunityRole.SalesmanCode` NULL/empty (legacy rows; DB column allows NULL). Fix: `CommunityRole.EnsureSalesmanCode()/RegenerateSalesmanCode()` domain methods + `SalesmanService` backfill+persist (3-attempt unique-collision retry) + `SalesmanQR.razor` "Chọn sản phẩm" guidance for missing productId (nav "Mã QR của tôi" dead-end). Test T13 backfill added. Build 0 errors · Guard ALL PASSED · 13/13 SalesmanServiceTests PASS.
* **2026-09-15 — COMMUNITY COMMERCE ISSUES #1-4 COMPLETE + DEPLOYED + RV PASS (#1-2).** Issue #1 (`26b060ee`): OrderType DELIVERY — `Order.SetOrderType()` + CreateOrderCommand + Checkout.razor + 12 unit tests. RV: checkout → DELIVERY order → owner confirm → shipper sees order (distanceKm=28.56). Issue #2 (`369b2986`): NATS sync — `OrderSyncSubscriber` read OrderType from payload + `OrderService` add delivery fields to Outbox event. Debug: NATS sync WAS working (msgs=2), "empty logs" = red herring (Warning filter hides Information). RV: SQLite OrderType=DELIVERY + DeliveryAddress + ShippingFee + Lat/Lng. Issue #3 (`5b97bcf9`): GPS mock — `gps-mock.ts` helper (all property variants) + 3 e2e specs. Issue #4: X-Dev-OTP gate sealed — removed X-Dev-OTP from `/otp/send` + `/upgrade/send-otp` + added `POST /api/customer-identity/dev-token` (secret-gated via `X-Dev-Secret` + `DevToken:Secret` config). `CustomerTokenService.CreateLongLivedToken(365)`. 6/6 unit tests PASS. Pending: CD deploy + set `DEV_TOKEN_SECRET` env var on VPS.
* **2026-09-15 — PROJECT_STATE.MD ARCHIVE CLEANUP.** Reduced from 353 → ~190 lines. Moved: Section 2 PREVIOUS OBJECTIVE blocks (KhachLink Sprint 3, GTM W2) + Section 3 completed items + Section 4 completed items + Section 6 history (pre-2026-09-15) + Section 10 maintenance log (pre-2026-09-15) → `project_state_archive.md` (2918 → 2970 lines).
