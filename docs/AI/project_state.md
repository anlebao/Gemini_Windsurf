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

**COMMISSION BASE = REFERRED PRODUCT + SELF-REFERRAL BLOCKING — ✅ COMPLETE + DEPLOYED + RV PASS (session 2026-09-17, commit `05443115`).**

Two follow-ups from the referral QR work. Task card: `docs/AI/tasks/community_commerce_fixes/task_card_07_commission_base_selfreferral.md`.

- **Commission was on the WHOLE order** — `CreateCommissionAsync` passed `order.TotalAmount` (SubTotal + VAT + ShippingFee, i.e. every cart item). `WalletService` used a different formula (`margin × rate`) so the Reseller balance invariant could disagree with the payout. Fix: new `ReferralCommissionCalculator` (single source of truth) → base = the **referred line items' SubTotal** (pre-VAT, shipping excluded); referred product absent → **no commission**; Reseller OnMargin → order margin **pro-rated** by the referred share. `SalesReferral.CommissionBaseAmount` added for audit (+ migration `20260917011937`); `WalletService` includes Items and uses the same calculator.
- **Self-referral never blocked** — `SameFingerprint` was hardcoded `false` and salesman vs buyer was never compared → a salesman buying their own code got a Pending commission with RiskScore 0, auto-paid after 24h. Fix, layered: checkout drops the referral when it resolves to the buyer; `CreateCommissionAsync` detects self-referral by CustomerId / device fingerprint / device token (covers guest checkout via `Order.CustomerDeviceId`); new `RiskScoreInput.SelfReferral` factor weight 100 → **Rejected** + `FraudFlag(SelfDeal)`.

RV: migration applied · order 250,000 sub-total with a 50,000 referred line → `CommissionBaseAmount 50000`, `CommissionAmount 1500` (not 8,250) · self-referral at checkout → order created with no attribution · forced buyer==salesman → `Rejected`, `RiskScore 100`, `SelfReferral:+100`, `FraudFlag(SelfDeal)` · salesman commissions API shows `pending 3150 / rejected 1500`. Build 0 errors · Guard ALL PASSED · 50/50 tests · CD Multi-VPS all jobs + smoke test SUCCESS.

**Previous objective — SALESMAN REFERRAL QR — CORRECT DOMAIN + SCAN-TO-BUY + COMMISSION — ✅ COMPLETE + DEPLOYED + RV PASS (session 2026-09-16/17, commit `aceab325`).**

Reported: the referral QR scanned to the wrong domain, scanning did not let the customer buy the referred product, and the salesman got no commission. Task card: `docs/AI/tasks/community_commerce_fixes/task_card_06_referral_qr_scan_commission.md`.

Root causes (all 5 gaps): QR host hardcoded to `diemthuong.khachvip.online`; `/r/{code}` was never a real route (nor an nginx rule) and `|` is not URL-safe in a path; `Order` had the Sprint-0 referral fields but **no domain setter**; `CheckoutOrderRequest`/`Checkout.razor` never carried `ReferralCode`; **`CreateCommissionAsync` was never called outside tests**.

Fix: QR now emits `{khachLinkOrigin}/scan?ref={escaped code}` (origin supplied by `SalesmanQR.razor` via `?sourceDomain=`, fallback config `ExternalUrls:KhachLink`); new anonymous `GET /api/community/referral/{code}` resolves the code into the product; `Scan.razor` handles `?ref=`/`/r/` → resolve → add to cart + store the code; `Checkout.razor` sends `ReferralCode`; Gateway resolves it → `Order.SetSalesmanReferral(...)` (new domain method, approved); `OrderWorkflowService.HandleOrderCompletedAsync` creates the `SalesReferral` commission (optional `ISalesmanService` — Gateway-only). QR canvas made fluid for mobile.

RV: QR URL `https://diemthuong2.khachvip.online/scan?ref=DL9ZMQ%7CCOMRV1` · anonymous resolve 200 with product info · checkout set `SalesmanId`/`ReferralProductId`/`ReferralCode` · shipper delivered → order completed → `SalesReferrals` row `1650.00 = 55000 × 0.03` (Pending) · salesman commissions API shows 1650. Build 0 errors · Guard ALL PASSED · 21/21 tests · CD Multi-VPS all jobs + smoke test SUCCESS.

**Previous objective — CHAT + GPS + SALESMAN QR — 3 PRODUCTION DEFECTS — ✅ COMPLETE + DEPLOYED + RV PASS (session 2026-09-16, commit `26dc9e62`).**

Reported on both KhachLink domains (`diemthuong2.khachvip.online` + `commienphi.timlathay.com`) — identical failures because all 3 bugs live in the Blazor WASM client. Task card: `docs/AI/tasks/community_commerce_fixes/task_card_05_chat_gps_qr.md`.

- **Chat dead** — `CustomerToken="_customerToken"` (missing `@`) on a **string** parameter → Razor passed the literal text `"_customerToken"` as the token. Evidence: `GET /hubs/chat?customerToken=_customerToken` → ShopERP `/me` 401 → `HubException: Invalid customerToken`; chat history endpoint 401. Fix: `CustomerToken="@_customerToken"` (DeliveryTracking + OrderTracking).
- **GPS never recorded** — `StartGpsTracking()` passed `OrderId` as the `deliveryTaskId`; Gateway `RecordLocation` looks up `DeliveryTasks.Id` (PK) → every ping discarded. Evidence: `RecordLocation: DeliveryTask 01a0a926-… not found` every 10s. Fix: pass real `_deliveryTaskId` (from `my-deliveries` + pickup response).
- **QR blank canvas** — `OnAfterRenderAsync` gated `vananQR.generate` on `(firstRender && _qr != null)`, but `_qr` loads async so `firstRender` fires during the loading spinner (no canvas) → JS never called. Fix: `_qr != null && !_qrRendered` flag.

RV: deployment verified from the deployed `VanAn.KhachLink.wasm` (`_customerToken` literal gone; `_qrRendered` + `_deliveryTaskId` fields present) · chat `/me` 200 + history 200 · GPS `DeliveryTrackings` row written for the real task id · QR `vananQR.generate` drew 569 modules. Build 0 errors · Guard ALL PASSED · CD Multi-VPS all jobs + smoke test SUCCESS.

**Previous objective — KHACHLINK UX FIXES (GPS error message + search default) — ✅ COMPLETE + DEPLOYED (session 2026-09-16).**

Two small KhachLink UX fixes deployed to production via CD Multi-VPS:

**Fix 1 — NearbyProducts GPS error shows friendly message (`32d0bae8`):**
- Bug: `/community/nearby-products` showed "Không lấy được vị trí GPS [object GeolocationPositionError]" when GPS failed. Root cause: `pwa.js getCurrentPosition` rejected with raw `GeolocationPositionError` object (no `.message`) → Blazor JSRuntime serialized as `[object GeolocationPositionError]` → `NearbyProducts.razor` displayed it raw.
- Fix (2 files): `pwa.js` now rejects with proper `Error` carrying Vietnamese message mapped from `err.code` (1=permission denied, 2=position unavailable, 3=timeout). `NearbyProducts.razor` guards against raw `[object ...]` dumps. Fixes all pages using `vananPWA.getCurrentPosition` (NearbyProducts, NearbyOrders, StoreFinder, DeliveryTracking, OrderTracking).
- RV: `pwa.js` on `commienphi.timlathay.com` contains `GeolocationPositionError.code` mapping ✓ · `/community/nearby-products` HTTP 200 ✓.

**Fix 2 — KhachLink Home default search mode = "Tìm sản phẩm" (`a6e712e3`):**
- Change: `Home.razor` line 453 `_searchMode` default `"store"` → `"product"`. Nhu cầu tìm sản phẩm cao hơn tìm tên shop/doanh nghiệp. User vẫn toggle sang "Tìm cửa hàng" bất cứ lúc nào (`SetSearchMode()` unchanged). Directory app giữ nguyên default "store".
- RV: CD Multi-VPS all 6 jobs SUCCESS · `https://commienphi.timlathay.com/` HTTP 200 ✓. WASM bundle — user cần hard refresh (Ctrl+Shift+R) để load DLL mới.

**Previous objective — Community Commerce Full Flow (Batch 1 + Batch 2 + Chat + Cart + Issue #175) — ✅ ALL COMPLETE + DEPLOYED + RV PASS.** See Section 10 maintenance log for details.

## 3. Current Status

- **Branch:** `main` (Community Commerce Batch 1 + Batch 2 + Chat fix + Floating cart + Issue #175. KhachLink Profile Transition Sprint 1+2. GTM W2 + currency fix. W1 Merchant Audit + nginx Directory SSR. R2.2 Reseller Accounting — PR #169. Crawl-to-Onboard 8 phases. Issue #103/#157/#161/#156 deployed).
- **Build full sln:** 0 errors · **CI:** PASS · **.NET SDK:** 8.0.422
- **Community Commerce Full Flow (session 2026-09-16):** ✅ Batch 1 (Issues #1-4) + Batch 2 (4 RV defects) + Chat fix + Floating cart + Issue #175 ALL COMPLETE + DEPLOYED + RV PASS. Full chain works end-to-end: salesman QR → customer checkout (DELIVERY + CustomerId) → owner confirm via Gateway → shipper accept/pickup/delivering/delivered → order completed + Outbox events + NATS sync → chat customer↔shipper → loyalty points awarded → public tracking works. See Section 2.
- **Commission base + self-referral (session 2026-09-17):** ✅ per-product commission base + self-referral blocking — DEPLOYED + RV PASS (`05443115`). HEAD=`05443115`.
- **Salesman Referral QR (session 2026-09-16/17):** ✅ correct domain + scan-to-buy + commission — DEPLOYED + RV PASS (`aceab325`).
- **Chat + GPS + Salesman QR (session 2026-09-16):** ✅ 3 defects fixed + DEPLOYED + RV PASS (`26dc9e62`). Chat token literal (`@` prefix), GPS DeliveryTaskId, QR firstRender timing.
- **KhachLink UX Fixes (session 2026-09-16):** ✅ Fix 1 NearbyProducts GPS error message (`32d0bae8`) — DEPLOYED + RV PASS (pwa.js + NearbyProducts.razor). Fix 2 Home default search mode = "Tìm sản phẩm" (`a6e712e3`) — DEPLOYED + CD PASS.
- **KhachLink Profile Transition Sprint 3:** ✅ CODE COMPLETE on `feature/khachlink-sprint3-audit-sw` @ `d29e621b`. Pending: push → PR → merge → CD → RV Layer 1-5.
- **Financial Intelligence MVP-2:** ✅ All 5 phases complete on feature branch (61/61 tests PASS). Pending: push + PR + CD + RV.
- **Tech debt:** TD-MVPS-001→004, TD-CUSTSYNC-001, TD-ASYNCDP-001, TD-GCP-001, TD-NETDASH-001, TD-OCR-01→05

---

## 4. Next Actions

**Commission base + self-referral (✅ COMPLETE + DEPLOYED + RV PASS session 2026-09-17, `05443115`):**
- ✅ Per-product commission base (referred line SubTotal, pre-VAT, no shipping) + audit column + migration
- ✅ WalletService uses the same calculator (balance invariant matches payout)
- ✅ Self-referral blocked at checkout + at commission time (CustomerId / fingerprint / device) → Rejected + FraudFlag
- ⏳ Optional: browser re-test by user
- 📌 Follow-up: Reseller OnMargin pro-rates by sub-total share (no per-item cost); pre-fix SalesReferrals keep old amounts

**Salesman Referral QR (✅ COMPLETE + DEPLOYED + RV PASS session 2026-09-16/17, `aceab325`):**
- ✅ QR URL uses the KhachLink instance host + `/scan?ref=` (was hardcoded Oracle domain + dead `/r/` route)
- ✅ Scan → resolve (anonymous) → add to cart → checkout carries `ReferralCode`
- ✅ `Order.SetSalesmanReferral` (approved Domain addition) + commission on order completion
- ✅ QR canvas fluid on mobile
- ⏳ Optional: browser re-test by user (visual confirm QR size + scan → cart)
- 📌 Follow-up: commission uses `Order.TotalAmount` (whole order), not just the referred line item

**Chat + GPS + QR (✅ COMPLETE + DEPLOYED + RV PASS session 2026-09-16, `26dc9e62`):**
- ✅ Chat token literal fix (`@_customerToken`) — RV: `/me` 200 + chat history 200
- ✅ GPS DeliveryTaskId fix — RV: `DeliveryTrackings` row written for real task id
- ✅ QR firstRender timing fix — RV: `vananQR.generate` drew 569 modules + field present in deployed bundle
- ⏳ Optional: browser re-test by user (visual confirm QR + live map marker)
- 📌 Follow-up: `GetCompositeSalesmanQrAsync` hardcodes QR host `diemthuong.khachvip.online`; composite code contains `|` (not URL-safe)

**KhachLink UX Fixes (✅ COMPLETE + DEPLOYED session 2026-09-16):**
- ✅ Fix 1 NearbyProducts GPS error friendly message — DEPLOYED + RV PASS (`32d0bae8`)
- ✅ Fix 2 KhachLink Home default search mode = "Tìm sản phẩm" — DEPLOYED + CD PASS (`a6e712e3`)
- Branch: `main` @ `26dc9e62` · Build: 0 errors · CD Multi-VPS all jobs + smoke test SUCCESS.

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
- **Verified Facts:** Branch=`main` @ `05443115` (Commission base = referred product + self-referral blocking, RV PASS: migration `20260917011937` applied; order sub-total 250,000 with a 50,000 referred line → `CommissionBaseAmount 50000` / `CommissionAmount 1500` (not 8250); self-referral at checkout → order with no attribution; forced buyer==salesman → `Rejected`, `RiskScore 100`, `SelfReferral:+100`, `FraudFlag(SelfDeal)`; commissions API pending 3150 / rejected 1500; 50/50 tests; CD all jobs + smoke test SUCCESS). Prior: Salesman referral QR — correct domain + scan-to-buy + commission, RV PASS: QR URL `https://diemthuong2.khachvip.online/scan?ref=DL9ZMQ%7CCOMRV1`; anonymous resolve 200; checkout set `SalesmanId`/`ReferralProductId`/`ReferralCode`; shipper delivered → completed → `SalesReferrals` 1650.00 Pending; salesman commissions API 1650; 21/21 tests; CD all jobs + smoke test SUCCESS). Prior: Chat + GPS + QR — 3 production defects fixed + RV PASS: chat token literal `@_customerToken` → `/me` 200 + chat history 200; GPS real `DeliveryTaskId` → `DeliveryTrackings` row written; QR `_qrRendered` timing fix → `vananQR.generate` drew 569 modules; deployed WASM has no `_customerToken` literal + has `_qrRendered`/`_deliveryTaskId`; CD all jobs + smoke test SUCCESS). Prior: KhachLink UX fixes: GPS error message `32d0bae8` + Home search default `a6e712e3`; prior Community Commerce Batch 2 — 4 RV defects fixed at `65ab0d3b`). Batch 2 Issue #1: Gateway `IOrderWorkflowService` DI registration → `GET /api/orders` 200 (was 400) + `PUT /api/orders/{id}/status` 204 (was 400). Issue #2: `DeliveryWorkflowService` delegates to `OrderWorkflowService` + tenant context set + loyalty/stats decoupled → shipper delivered → order completed + 4 Outbox events Processed. Issue #3: Remove `Include(o.Customer)` → public tracking 200 with CustomerId (was 404). Issue #4: KhachLink free-order checkout sends CustomerId from localStorage → chat works immediately after checkout. Build 0 errors · Guard ALL PASSED · 11/11 DeliveryWorkflowServiceTests PASS (incl. T11 delegation) · CI PASS · CD Multi-VPS PASS.
- **Open Questions:** 4 (PlatformAccountingTenantId not configured · Turnstile keys not configured · Rate limit uses container IP not forwarded client IP · Sprint 1 RV partial — Directory-profile WASM domain needed for P2.2/P2.3 RV)
- **Gate 6 Status:** ✅ Assumptions (0) < Verified Facts (30+), Open Questions (4) → CLEAR

---

## 10. Maintenance Log

> Full historical maintenance log: see `docs/AI/project_state_archive.md`.

* **2026-09-17 — COMMISSION BASE = REFERRED PRODUCT + SELF-REFERRAL BLOCKING (`05443115`).** (1) Commission was computed on the whole order (`order.TotalAmount` = SubTotal + VAT + ShippingFee) and `WalletService` used a different formula (`margin × rate`) → balance invariant could disagree with the payout. New `ReferralCommissionCalculator` = single source of truth: base = referred line items' SubTotal (pre-VAT, no shipping); referred product absent → no commission; Reseller OnMargin → order margin pro-rated by the referred share. `SalesReferral.CommissionBaseAmount` (audit) + migration `20260917011937`; WalletService includes Items + same calculator. (2) Self-referral was never blocked (`SameFingerprint` hardcoded false, no salesman-vs-buyer check) → a salesman buying their own code got a Pending commission auto-paid after 24h. Layered fix: checkout drops the referral when it resolves to the buyer; `CreateCommissionAsync` detects via CustomerId / device fingerprint / device token (guest via `Order.CustomerDeviceId`); new `RiskScoreInput.SelfReferral` weight 100 → Rejected + `FraudFlag(SelfDeal)`. Build 0 errors · Guard ALL PASSED · 50/50 tests · CD Multi-VPS SUCCESS. RV: migration applied; order sub-total 250,000 with a 50,000 referred line → base 50000 / commission 1500 (not 8250); self-referral at checkout → no attribution; forced buyer==salesman → Rejected, RiskScore 100, `SelfReferral:+100`, FraudFlag SelfDeal; commissions API pending 3150 / rejected 1500. Task card: `task_card_07_commission_base_selfreferral.md`.
* **2026-09-17 — SALESMAN REFERRAL QR — CORRECT DOMAIN + SCAN-TO-BUY + COMMISSION (`aceab325`).** 5 gaps fixed: (1) QR host hardcoded to `diemthuong.khachvip.online`; (2) `/r/{code}` was never a route (no nginx rule) + `|` not URL-safe in a path; (3) `Order` had Sprint-0 referral fields with **no domain setter**; (4) `CheckoutOrderRequest`/`Checkout.razor` never sent `ReferralCode`; (5) `CreateCommissionAsync` **never called outside tests**. Fix: QR → `{khachLinkOrigin}/scan?ref={escaped}` (origin from `SalesmanQR.razor` via `?sourceDomain=`), anonymous `GET /api/community/referral/{code}` → product info, `Scan.razor` resolves + adds to cart + stores code, checkout sends `ReferralCode`, Gateway → `Order.SetSalesmanReferral` (approved Domain addition), `OrderWorkflowService.HandleOrderCompletedAsync` creates the commission (optional Gateway-only `ISalesmanService`). QR canvas fluid on mobile. Build 0 errors · Guard ALL PASSED · 21/21 tests · CD Multi-VPS SUCCESS. RV: QR URL `https://diemthuong2.khachvip.online/scan?ref=DL9ZMQ%7CCOMRV1`; anonymous resolve 200; checkout set `SalesmanId`/`ReferralProductId`/`ReferralCode`; shipper delivered → completed → `SalesReferrals` 1650.00 (55000 × 0.03, Pending); salesman commissions API 1650. Task card: `task_card_06_referral_qr_scan_commission.md`. Temp `DevToken__Secret` re-used then removed.
* **2026-09-16 — CHAT + GPS + QR — 3 PRODUCTION DEFECTS FIXED + DEPLOYED + RV PASS (`26dc9e62`).** (1) Chat: `CustomerToken="_customerToken"` missing `@` on a string parameter → literal token sent → `/hubs/chat?customerToken=_customerToken` → ShopERP `/me` 401 → `HubException: Invalid customerToken`. (2) GPS: `StartGpsTracking(OrderId.ToString())` → Gateway `RecordLocation` (looks up `DeliveryTasks.Id`) logged `DeliveryTask … not found` → every ping discarded. (3) QR: `OnAfterRenderAsync` gated on `firstRender && _qr != null`; `_qr` loads async so the JS call never ran on the render with the canvas. Build 0 errors · Guard ALL PASSED · CD Multi-VPS SUCCESS. RV: deployed WASM has no `_customerToken` literal + has `_qrRendered`/`_deliveryTaskId`; chat `/me` 200 + history 200; `DeliveryTrackings` row written for real task id `f07c5f6e`; `vananQR.generate` drew 569 modules. Task card: `task_card_05_chat_gps_qr.md`. Temp `DevToken__Secret` used for RV then REMOVED (see `task_card_04_dev_otp_gate.md`).
* **2026-09-16 — KHACHLINK UX FIXES COMPLETE + DEPLOYED.** Fix 1 (`32d0bae8`): NearbyProducts GPS error — `pwa.js getCurrentPosition` rejected raw `GeolocationPositionError` → Blazor showed `[object GeolocationPositionError]`. Now rejects with proper `Error` + Vietnamese message mapped from `err.code` (1/2/3). `NearbyProducts.razor` guards raw object dumps. Fixes all 5 pages using `vananPWA.getCurrentPosition`. RV: pwa.js on production contains fix ✓ · `/community/nearby-products` 200 ✓. Fix 2 (`a6e712e3`): `Home.razor` line 453 `_searchMode` default `"store"` → `"product"` (nhu cầu tìm sản phẩm cao hơn). Directory app unchanged. Build 0 errors · Guard PASSED · CD Multi-VPS all 6 jobs SUCCESS.
* **2026-09-16 — COMMUNITY COMMERCE BATCH 2 (4 RV DEFECTS) COMPLETE + DEPLOYED + RV PASS.** Master plan: `docs/AI/tasks/community_commerce_fixes_batch2/master_plan.md`. Issue #1 (`73b0133a`): Gateway `IOrderWorkflowService` DI registration — `OrdersController` 400 "Operation is not valid" → 200/204. Issue #2 (`73b0133a` + `b9f0fc0a` + `67b91fe3`): `DeliveryWorkflowService` delegates to `OrderWorkflowService` + loyalty/stats decoupled + tenant context set → shipper delivered → order completed + 4 Outbox events Processed. Issue #3 (`0948658a`): Remove `Include(o.Customer)` from `GetByIdWithIncludesAsync` + `GetByIdWithIncludesIgnoreFiltersAsync` → public tracking 200 with CustomerId (was 404 due to `CryptographicException` on corrupt phone data). Issue #4 (`65ab0d3b`): `KhachLinkLayout.SubmitFreeOrderDirectAsync` reads `customer_id` + `customer_token` from localStorage + validates via `/api/customers/me` → CustomerId set in PG → chat works immediately after checkout. Build 0 errors · Guard ALL PASSED · 11/11 DeliveryWorkflowServiceTests PASS (incl. T11 delegation) · CI PASS · CD Multi-VPS PASS. RV: all 4 issues verified on production VPS.
* **2026-09-15 — ISSUE #175 FIX COMPLETE (pending commit/deploy).** Bug #1 shipper GPS: `NearbyOrders.razor` `GeolocationResult {Latitude,Longitude}` vs JS `vananPWA.getCurrentPosition` `{lat,lng}` mismatch → always 0,0 → "Không lấy được vị trí GPS" blocked all orders (Issue #3 GPS-mock masked it in Playwright). Same silent bug in `StoreFinder.razor`. Fix: `GpsPosition {Lat,Lng}` in both pages. Bug #2 salesman QR "Không thể tạo mã QR": `GetCompositeSalesmanQrAsync` returned null when `CommunityRole.SalesmanCode` NULL/empty (legacy rows; DB column allows NULL). Fix: `CommunityRole.EnsureSalesmanCode()/RegenerateSalesmanCode()` domain methods + `SalesmanService` backfill+persist (3-attempt unique-collision retry) + `SalesmanQR.razor` "Chọn sản phẩm" guidance for missing productId (nav "Mã QR của tôi" dead-end). Test T13 backfill added. Build 0 errors · Guard ALL PASSED · 13/13 SalesmanServiceTests PASS.
* **2026-09-15 — COMMUNITY COMMERCE ISSUES #1-4 COMPLETE + DEPLOYED + RV PASS (#1-2).** Issue #1 (`26b060ee`): OrderType DELIVERY — `Order.SetOrderType()` + CreateOrderCommand + Checkout.razor + 12 unit tests. RV: checkout → DELIVERY order → owner confirm → shipper sees order (distanceKm=28.56). Issue #2 (`369b2986`): NATS sync — `OrderSyncSubscriber` read OrderType from payload + `OrderService` add delivery fields to Outbox event. Debug: NATS sync WAS working (msgs=2), "empty logs" = red herring (Warning filter hides Information). RV: SQLite OrderType=DELIVERY + DeliveryAddress + ShippingFee + Lat/Lng. Issue #3 (`5b97bcf9`): GPS mock — `gps-mock.ts` helper (all property variants) + 3 e2e specs. Issue #4: X-Dev-OTP gate sealed — removed X-Dev-OTP from `/otp/send` + `/upgrade/send-otp` + added `POST /api/customer-identity/dev-token` (secret-gated via `X-Dev-Secret` + `DevToken:Secret` config). `CustomerTokenService.CreateLongLivedToken(365)`. 6/6 unit tests PASS. Pending: CD deploy + set `DEV_TOKEN_SECRET` env var on VPS.
* **2026-09-15 — PROJECT_STATE.MD ARCHIVE CLEANUP.** Reduced from 353 → ~190 lines. Moved: Section 2 PREVIOUS OBJECTIVE blocks (KhachLink Sprint 3, GTM W2) + Section 3 completed items + Section 4 completed items + Section 6 history (pre-2026-09-15) + Section 10 maintenance log (pre-2026-09-15) → `project_state_archive.md` (2918 → 2970 lines).
