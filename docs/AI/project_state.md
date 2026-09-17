# Project State

> **Mục đích:** Single Source of Truth cho AI về trạng thái dự án. BẮT BUỘC đọc đầu mỗi phiên.
> **Archived:** 2026-07-24 + 2026-08-03 + 2026-08-09 + 2026-08-23 + 2026-09-06 + 2026-09-15 + 2026-09-17 — All completed objectives + full history/maintenance log moved to `docs/AI/project_state_archive.md`

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

> Completed objectives (referral QR, chat+GPS+QR, KhachLink UX, Community Commerce Full Flow)
> moved to `docs/AI/project_state_archive.md` — see also Section 10.


## 3. Current Status

- **Branch:** `main` @ `05443115` (Community Commerce Batch 1 + 2 + Chat + GPS + referral QR + commission base/self-referral. KhachLink Profile Sprint 1+2. GTM W2 + currency fix. W1 Merchant Audit + nginx Directory SSR. R2.2 Reseller Accounting — PR #169. Crawl-to-Onboard 8 phases. Issue #103/#157/#161/#156 deployed).
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

**Completed groups (Salesman Referral QR · Chat + GPS + QR · KhachLink UX Fixes · Community Commerce Full Flow)**
moved to `docs/AI/project_state_archive.md` — see also Section 10.

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
- **Verified Facts:** Branch=`main` @ `05443115`. Recent RV-verified work (details in Section 2 + Section 10): commission base = referred product + self-referral blocking (`05443115`, 50/50 tests) · salesman referral QR (`aceab325`, 21/21) · chat + GPS + QR defects (`26dc9e62`) · KhachLink UX fixes (`32d0bae8`, `a6e712e3`) · Community Commerce Batch 2 (`73b0133a`, `b9f0fc0a`, `67b91fe3`, `0948658a`, `65ab0d3b`). All: build 0 errors · Guard ALL PASSED · CD Multi-VPS all jobs + smoke test SUCCESS. Security: `.devin/rules/dev-token-secret.md` — `DevToken__Secret` unset (endpoint 404).
- **Open Questions:** 4 (PlatformAccountingTenantId not configured · Turnstile keys not configured · Rate limit uses container IP not forwarded client IP · Sprint 1 RV partial — Directory-profile WASM domain needed for P2.2/P2.3 RV)
- **Gate 6 Status:** ✅ Assumptions (0) < Verified Facts (30+), Open Questions (4) → CLEAR

---

## 10. Maintenance Log

> Full historical maintenance log (pre-2026-09-17): see `docs/AI/project_state_archive.md`.

* **2026-09-17 — DEV-TOKEN SECRET SAFETY RULE + STATE SYNC.** Added `.devin/rules/dev-token-secret.md` (hard-stop security rule: `DevToken__Secret` unset by default; never persisted in `.env.shoperp`/repo/commit; inline shell env only; never echoed; always removed + verified `404` at the end of an RV window; checklist + reporting requirements) and referenced it from the always-on `governance.md` ("SECRETS & TEST-AUTH (HARD STOP)") + `task_card_04_dev_otp_gate.md`. Also fixed two stale follow-ups in Section 4 that contradicted the completed work (whole-order commission + hardcoded QR host are both fixed) and refreshed the branch/HEAD references to `05443115`.
* **2026-09-17 — COMMISSION BASE = REFERRED PRODUCT + SELF-REFERRAL BLOCKING (`05443115`).** (1) Commission was computed on the whole order (`order.TotalAmount` = SubTotal + VAT + ShippingFee) and `WalletService` used a different formula (`margin × rate`) → balance invariant could disagree with the payout. New `ReferralCommissionCalculator` = single source of truth: base = referred line items' SubTotal (pre-VAT, no shipping); referred product absent → no commission; Reseller OnMargin → order margin pro-rated by the referred share. `SalesReferral.CommissionBaseAmount` (audit) + migration `20260917011937`; WalletService includes Items + same calculator. (2) Self-referral was never blocked (`SameFingerprint` hardcoded false, no salesman-vs-buyer check) → a salesman buying their own code got a Pending commission auto-paid after 24h. Layered fix: checkout drops the referral when it resolves to the buyer; `CreateCommissionAsync` detects via CustomerId / device fingerprint / device token (guest via `Order.CustomerDeviceId`); new `RiskScoreInput.SelfReferral` weight 100 → Rejected + `FraudFlag(SelfDeal)`. Build 0 errors · Guard ALL PASSED · 50/50 tests · CD Multi-VPS SUCCESS. RV: migration applied; order sub-total 250,000 with a 50,000 referred line → base 50000 / commission 1500 (not 8250); self-referral at checkout → no attribution; forced buyer==salesman → Rejected, RiskScore 100, `SelfReferral:+100`, FraudFlag SelfDeal; commissions API pending 3150 / rejected 1500. Task card: `task_card_07_commission_base_selfreferral.md`.
* **2026-09-17 — SALESMAN REFERRAL QR — CORRECT DOMAIN + SCAN-TO-BUY + COMMISSION (`aceab325`).** 5 gaps fixed: (1) QR host hardcoded to `diemthuong.khachvip.online`; (2) `/r/{code}` was never a route (no nginx rule) + `|` not URL-safe in a path; (3) `Order` had Sprint-0 referral fields with **no domain setter**; (4) `CheckoutOrderRequest`/`Checkout.razor` never sent `ReferralCode`; (5) `CreateCommissionAsync` **never called outside tests**. Fix: QR → `{khachLinkOrigin}/scan?ref={escaped}` (origin from `SalesmanQR.razor` via `?sourceDomain=`), anonymous `GET /api/community/referral/{code}` → product info, `Scan.razor` resolves + adds to cart + stores code, checkout sends `ReferralCode`, Gateway → `Order.SetSalesmanReferral` (approved Domain addition), `OrderWorkflowService.HandleOrderCompletedAsync` creates the commission (optional Gateway-only `ISalesmanService`). QR canvas fluid on mobile. Build 0 errors · Guard ALL PASSED · 21/21 tests · CD Multi-VPS SUCCESS. RV: QR URL `https://diemthuong2.khachvip.online/scan?ref=DL9ZMQ%7CCOMRV1`; anonymous resolve 200; checkout set `SalesmanId`/`ReferralProductId`/`ReferralCode`; shipper delivered → completed → `SalesReferrals` 1650.00 (55000 × 0.03, Pending); salesman commissions API 1650. Task card: `task_card_06_referral_qr_scan_commission.md`. Temp `DevToken__Secret` re-used then removed.
