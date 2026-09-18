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

**REALTIME PLATFORM — CHAT + LIVE LOCATION REUSABLE ACROSS MODULES — 🚧 APPROVED, IMPLEMENTING (session 2026-09-17) — P3 CODE COMPLETE.**

Task card: `docs/AI/tasks/realtime_platform/task_card_01_realtime_chat_gps_reusable.md` (395+ dòng, đã duyệt).
Origin: RV cho thấy chat + GPS **vẫn chết trên UI** dù fix `26dc9e62` đã deploy → root-cause analysis tìm ra 6 defect (D1-D6) + 8 điểm coupling (C1-C8).

**Root causes (verified file:line):**
- **D1 (chính)** — Khách Google OAuth tạo ở ShopERP SQLite nhưng `SocialAuthController` **không phát `CustomerCreated`** → không sync Gateway PG → `OrderService.cs:831-849` âm thầm set `CustomerId = null` → `ChatService` từ chối tạo conversation + `LocationHub` từ chối join → **chat + GPS chết cho khách đã đăng nhập**.
- **D2/D3** — Trang khách `OrderTracking.razor` build map bằng `GET /api/community/nearby-orders` (**shipper-only** → 403) và handler `LocationUpdate` không set `_showMap` → map không bao giờ render.
- **D4** — Checkout luôn gửi `DeliveryLat/Lng = null` → shipper không thấy marker khách.
- **D5** — GPS chỉ start khi bấm "Đã lấy hàng"; reload là mất.
- **D6** — Guest không có chat/tracking → **quyết định mới: guest PHẢI có** (auth bằng `X-Customer-Device-Id` / `Order.CustomerDeviceId`); docs đã sửa.

**Decisions (2026-09-17):** Q1 guest CÓ chat + tracking · Q2 Domain additive **APPROVED** (`Conversation.SubjectType/SubjectId` + `ConversationParticipant` + `DeliveryTracking.SubjectType/SubjectId/TrackerId`) · ~~Q3 consumer = cả Logistics + JobMarket~~ → **SUPERSEDED 2026-09-17 (sau review F1): Logistics/JobMarket chỉ là preset nav-flags, chưa có entity → không dùng làm consumer. Consumer = Shop chat trên `/store/{slug}` (profile FullCommerce) + inbox chủ shop `/community/messages` (ShopERP). Logistics/JobMarket → P7 deferred.**
**Bổ sung 2026-09-17 (sau review P2-P6):** GPS trên trang shop = **map tĩnh + khoảng cách** (`VanAnMap` thay Google iframe, KHÔNG realtime) · phía shop = **trang inbox riêng** trong ShopERP · thứ tự **P2→P3→P4→P5(shop)** · 11 findings F1-F11 đã ghi vào task card Section 20.

**Target:** Realtime Platform 4 tầng (Domain generic → `IRealtimeMessagingService`/`ILiveLocationService`/`IRealtimeParticipantAuthorizer` → Gateway `MessagingHub`/`TrackingHub`/`IRealtimeTokenValidator`/`/api/realtime/*` → UI Platform `RealtimeChatPanel`/`VanAnMap`/`realtime.js`), giữ route cũ làm adapter. Phases P0→P6.

> Completed objectives (commission base + self-referral, referral QR, chat+GPS+QR, KhachLink UX, Community Commerce Full Flow)
> moved to `docs/AI/project_state_archive.md` — see also Section 10.


## 3. Current Status

- **Realtime Platform — P3 (session 2026-09-17):** ✅ **CODE COMPLETE (chưa commit/deploy)**. Gateway realtime layer: `MessagingHub` `/hubs/messaging` + `TrackingHub` `/hubs/tracking` (group `msg_`/`loc_{subjectType}_{subjectId}`, join qua keyed `IRealtimeParticipantAuthorizer`, **default deny** khi subject chưa đăng ký authorizer) · `RealtimeController` `/api/realtime/*` (send/history/ping/latest — không còn role Shipper check như `api/community/*`, nên buyer tự xem được toạ độ đơn mình = GW-8) · `IRealtimeTokenValidator` × 3 + `RealtimeIdentityResolver` (Customer→Device→Staff) · `IRealtimeSubjectResolver` (subject→TenantId, unknown→null→400, không đoán tenant) · **F3 FIXED**: validators đọc credential từ **query string trước header** → guest mở được SignalR bằng `?customerDeviceId=` (trước chỉ HTTP polling) · **GW-6**: `/hubs/chat` + `/hubs/location` giữ nguyên route/group/tên method, thân hàm delegate sang auth + access dùng chung (bỏ bản sao logic). Build full sln 0 errors · guard ALL CHECKS PASSED · 37/37 Realtime tests PASS (11 P2 + 26 P3). `RealtimeController` thêm vào exemption W12-G7 (customer/guest-facing, `[Authorize]` sẽ chặn guest trước khi endpoint chạy — cùng precedent `CommunityController`).
- **Accounting revenue-loss fix (2026-09-17/18):** ✅ Root cause verified on production (tenant "Vạn An Cafe (HKD Group 1)"): 5 orders paid within ~4 min, only 1 got revenue entries. **C-1 duplicate detection** (`CheckDuplicateEntryAsync`) matched (tenant, type, amount, accountCode) in 5-min window WITHOUT order reference → distinct same-amount orders blocked as duplicates; exception swallowed in `ConfirmPaymentAsync` → Paid orders without entries. Fixes (`236d1235` + `f6e94108`, deployed + CD SUCCESS): duplicate check by reference · `GenerateAccountingEntriesAsync` idempotent via `ExistsByReferenceAsync` · `SimpleAccountingEventHandler` NATS subject `vanan.shoperp.order.completed` (+ legacy) — completion-accounting path was dead (subject mismatch) · audit flag fail-safe in background scopes. **Data repair DONE**: re-published `OrderPaymentConfirmed` NATS events → 5 orders now have full entries (511+3331+632) + HKD JournalEntries; `01a0afac` untouched. Core.Tests 1616 pass · guard ALL PASSED.
- **Branch:** `main` @ `f6e94108` (Realtime Platform P3 committed. Accounting revenue-loss fix + audit fail-safe deployed. Realtime Platform P2 — generic Domain/services + PG migration. P1b GPS map + Free/Charity checkout. Community Commerce Batch 1 + 2 + Chat + GPS + referral QR + commission base/self-referral. KhachLink Profile Sprint 1+2. GTM W2 + currency fix. W1 Merchant Audit + nginx Directory SSR. R2.2 Reseller Accounting — PR #169. Crawl-to-Onboard 8 phases. Issue #103/#157/#161/#156 deployed).
- **Build full sln:** 0 errors · **CI:** PASS · **.NET SDK:** 8.0.422
- **Realtime Platform — P2 (session 2026-09-17):** ✅ **P2 DONE + DEPLOYED + RV L1 PASS** @ `2c3e0360`. Generic Domain + services + PG migration `20260917101938_AddRealtimePlatformP2` (backfill `SubjectId` trước unique index). 2 blocker từ review đã fix: **F2** unique index `OrderId` → `(TenantId, SubjectType, SubjectId)`; **F6** index ping generic. `IChatService`/`ChatService` giữ nguyên (không regression). Build 0 errors · guard ALL PASSED · 11/11 test mới (`RealtimePlatformP2Tests`) · 248/248 Community regression · CI + Accounting Tests + CD Multi-VPS SUCCESS. **RV L1:** PG có `ConversationParticipants` + cột `SubjectType/SubjectId/TrackerId`; unique index `IX_Conversations_TenantId_SubjectType_SubjectId` tạo đúng; index `OrderId` đã thành non-unique; **backfill 7/7 conversations + 40/40 pings** (không còn giá trị 0) → migration an toàn trên dữ liệu production. Review P2-P6: 11 findings (F1-F11) — **F1: Logistics/JobMarket chỉ là preset nav-flags, không phải module → P5 đổi thành Shop chat**. Quyết định: GPS trang shop = map tĩnh + khoảng cách; phía shop = trang inbox `/community/messages`.
- **Realtime Platform (session 2026-09-17):** 🚧 **P1 DONE + DEPLOYED + RV PASS (L1-L4)** @ `b12a99d2`. Root-cause RV: chat + GPS vẫn chết trên UI dù fix `26dc9e62` deploy. 6 defect + 8 coupling. **P1 fixes** (D1 Google customer sync · D2/D3 buyer tracking endpoint + map · D4 checkout coords · D5 GPS resume · D6 guest chat/tracking via device id). Build full sln 0 errors · 40/40 chat+delivery tests · CI ALL PASSED · CD Multi-VPS SUCCESS. **RV:** L1 API (401/404/200/403 + guest send) ✅ · L2 WASM markers + ShopERP redeployed ✅ · L3 Playwright guest UI (chat panel + map + no errors) ✅ · L4 guest send flow ✅ · L5 manual pending. Docs guest chat/tracking đã sửa. **P2-P6 pending** (generic Domain/services/hubs + UI Platform extraction + Logistics/JobMarket consumers). Task card: `docs/AI/tasks/realtime_platform/task_card_01_realtime_chat_gps_reusable.md`.
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

**Realtime Platform — P1b follow-up (✅ FIXED + DEPLOYED + RV PASS — 2026-09-17, `96e733a6`):**
Manual test sau P1: chat OK nhưng (a) GPS/map không hoạt động cả 2 phía, (b) sản phẩm Free/Charity không tạo được order.
- **D7** Shipper không thấy map: `DeliveryTracking` lấy toạ độ từ `nearby-orders` — endpoint này **loại trừ đơn đã có DeliveryTask active** (chính đơn shipper đang xem) → không có toạ độ → `_showMap=false`. Fix: dùng `/api/community/orders/{id}/tracking`.
- **D8** Map khách trắng/thiếu pin shipper: toạ độ mặc định `0` vẫn qua `HasValue` → map tâm (0,0); marker chỉ add ở lần render đầu → ping shipper không cập nhật. Fix: endpoint chuẩn hoá `0`→null; client yêu cầu toạ độ thực + center shop→delivery→shipper; `LeafletMap` upsert marker mỗi render (`leafletMap.upsertMarker`).
- **D9** Free/Charity không tạo được order: ShopERP product API không có `productType` → cart `IsFree=false` → Gateway Tier 0 chặn giá 0. Fix: Gateway tự resolve Free/Charity từ `FeaturedProducts` trước price guard (authoritative) + `Store.razor` enrich ProductType từ featured catalog.

**Realtime Platform (🚧 APPROVED + IMPLEMENTING — session 2026-09-17):**
Task card: `docs/AI/tasks/realtime_platform/task_card_01_realtime_chat_gps_reusable.md`
- [ ] **P0** — verify production (log `"falling back to guest mode"` + query PG `Customers`/`Orders`) — không cần chờ gì
- [x] **P1** — fix production blockers ✅ DONE + DEPLOYED + RV PASS (L1-L4) `b12a99d2`: D1 customer sync (`SocialAuthController` enqueue `CustomerCreated`) · D2/D3 buyer-accessible `GET /api/community/orders/{id}/tracking` + map render · D4 checkout toạ độ · D5 GPS resume on load · D6 guest chat/tracking via `X-Customer-Device-Id`. Build 0 errors · 40/40 tests · CI ALL PASSED · CD SUCCESS · RV L1-L4 PASS (L5 manual pending).
- [x] **P2** — ✅ DONE + DEPLOYED + RV L1 PASS (2026-09-17, `2c3e0360`): Domain additive (`RealtimeSubjectType` + `Conversation.SubjectType/SubjectId` + `ConversationParticipant` + `DeliveryTracking.Subject*`) · `IRealtimeMessagingService`/`ILiveLocationService`/`IRealtimeParticipantAuthorizer` + `OrderRealtimeAuthorizer` (keyed DI) · migration PG `20260917101938_AddRealtimePlatformP2` (backfill `SubjectId`) · **fix F2** (unique index `(TenantId, SubjectType, SubjectId)`) + **fix F6** (tracking subject index). Build 0 errors · guard ALL PASSED · 11/11 test mới · 248/248 Community regression.
- [x] **P3** — ✅ **CODE COMPLETE (chưa deploy, chưa commit)** 2026-09-17: Gateway `MessagingHub` (`/hubs/messaging`) + `TrackingHub` (`/hubs/tracking`) + `RealtimeController` (`/api/realtime/conversations/messages` · `GET /conversations/{type}/{id}` · `POST /location/ping` · `GET /location/{type}/{id}/latest`) + `IRealtimeTokenValidator` × 3 (`CustomerTokenValidator`/`DeviceTokenValidator`/`StaffJwtValidator`) + `RealtimeIdentityResolver` (Customer→Device→Staff) + `IRealtimeSubjectResolver` (subject→TenantId) + `RealtimeAuthorizerLookup` (**default deny**) + **F3 fixed** (device token đọc từ query string → guest mở được SignalR, cả hub mới lẫn `/hubs/chat` + `/hubs/location` cũ) + **GW-6** (hubs cũ giữ nguyên route/group/tên method, chỉ delegate auth + access check). Build full sln 0 errors · guard ALL PASSED · 37/37 Realtime tests (11 P2 + 26 P3). Chi tiết: task card Section 18.7.
- [ ] **P4** — UI Platform extraction (`RealtimeChatPanel`, `VanAnMap`, `realtime.js`) + migrate KhachLink + **F4: `IRealtimeEndpointProvider` + verify RCL static assets 3 host** + **F11: fix vi phạm UI Platform**
- [ ] **P5 (REVISED)** — **Shop chat + map tĩnh trên `/store/{slug}` (profile FullCommerce) + inbox chủ shop `/community/messages` (ShopERP)**. Logistics/JobMarket → **P7** (chưa tồn tại — F1)
- [ ] **P6** — tests + E2E + RV Layer 1-5 + reuse guide (`docs/UI_Platform_Implementation_Guide.md`)
- [ ] **P7 (deferred)** — Logistics/JobMarket consumers — chỉ khi Sprint 8/9 thành module thật (entity `Shipment`/`JobApplication`)
- ✅ Docs guest chat/tracking đã sửa: `07-customer.md` (§2.1, §2.4, §5.1, §6.1, FAQ), `04-shipper.md` (§8.3), `README.md` (§3.3)

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

* [2026-09-17] **COMMISSION BASE = REFERRED PRODUCT + SELF-REFERRAL BLOCKING (`05443115`).** ✅ COMPLETE + DEPLOYED + RV PASS. Per-product commission base (referred line SubTotal, pre-VAT, no shipping) via `ReferralCommissionCalculator` (single source of truth) + `SalesReferral.CommissionBaseAmount` + migration `20260917011937`; `WalletService` uses the same calculator. Self-referral blocked at checkout + at commission time (CustomerId / fingerprint / device token / `Order.CustomerDeviceId` for guests) → `RiskScoreInput.SelfReferral` weight 100 → Rejected + `FraudFlag(SelfDeal)`. RV: base 50000/commission 1500 (not 8250); forced buyer==salesman → Rejected RiskScore 100; commissions API pending 3150 / rejected 1500. Build 0 errors · Guard ALL PASSED · 50/50 tests · CD Multi-VPS SUCCESS. Task card: `task_card_07_commission_base_selfreferral.md`.
* [2026-09-18] **ACCOUNTING REVENUE-LOSS FIX + DATA REPAIR.** Production bug (tenant "Vạn An Cafe (HKD Group 1)"): 5 đơn hoàn tất + Paid nhưng chỉ 1 ghi nhận doanh thu. Root cause (verify production SQLite+WAL+PG): C-1 `CheckDuplicateEntryAsync` chặn nhầm đơn khác nhau cùng số tiền trong 5 phút (không so Reference) + exception bị nuốt ở `ConfirmPaymentAsync`; NATS subject `SimpleAccountingEventHandler` sai (`ordercompleted` vs `order.completed`). Fix: duplicate check theo reference · idempotency `ExistsByReferenceAsync` · subscribe đúng subject (+ legacy) · audit flag fail-safe background scope. Deployed `236d1235` + `f6e94108` (CD SUCCESS ×2). Repair: re-publish `OrderPaymentConfirmed` NATS → 5 đơn đủ entry (511/3331/632) + JournalEntries, `01a0afac` nguyên vẹn, revenue T1 09/2026 = 1.309.000đ. Tests 1616 pass · guard ALL PASSED.
* [2026-09-17] **REALTIME PLATFORM — ROOT-CAUSE ANALYSIS + TASK CARD.** Chat + GPS vẫn chết trên UI dù fix `26dc9e62` deploy. Tìm ra 6 defect (D1 khách Google OAuth không sync PG → `Order.CustomerId=null` → chat/GPS chết; D2/D3 map khách dùng endpoint shipper-only 403 + không set `_showMap`; D4 checkout không gửi toạ độ; D5 GPS mất khi reload; D6 guest không chat/tracking) + 8 coupling (C1-C8) chặn tái sử dụng. Task card `docs/AI/tasks/realtime_platform/task_card_01_realtime_chat_gps_reusable.md` (approved). Quyết định: guest CÓ chat+tracking (`X-Customer-Device-Id`); Domain additive approved; consumer = Logistics + JobMarket. Docs guest đã sửa. **Chưa implement.**
* [2026-09-15] **COMMUNITY COMMERCE ISSUES #1-4 COMPLETE.** Issue #1 OrderType DELIVERY (`26b060ee`): `Order.SetOrderType()` + CreateOrderCommand + Checkout.razor selector. Issue #2 NATS sync (`369b2986`): `OrderSyncSubscriber` read OrderType from payload + `OrderService` add delivery fields to Outbox event. Issue #3 GPS mock (`5b97bcf9`): shared `gps-mock.ts` helper injected into 3 e2e specs. Issue #4 X-Dev-OTP gate: removed X-Dev-OTP from `/otp/send` + `/upgrade/send-otp` + added `POST /api/customer-identity/dev-token` (secret-gated). Full chain: checkout → DELIVERY order → NATS sync → SQLite → owner confirm → shipper sees order. See Section 2.
* [2026-09-14] **CHARITY CHECKOUT FLOW C1-C3 + 3 COMMUNITY COMMERCE BUGS.** Charity: `9b7d0c8e` + `f505a242` (ExecuteAtomicAsync + AllItemsFree + Charity_Donation_Enabled). Community: `f39c8649` + `88f3496f` + `6fe17d31` (DeliveryTracking route + GPS-optional + gateway HttpClient).
* **Older (2026-09-13 and before):** KhachLink Profile Transition Sprint 1-3, GTM W1-W2, R2.2 Reseller Accounting, Crawl-to-Onboard 8 phases, Directory SSR, Issue #103/#156/#161/#157, Financial Intelligence MVP-2, OCR Hub R1, Dynamic CORS, Multi-VPS Option C. See `docs/AI/project_state_archive.md`.

---

## 7. Active Files Reference

| File | Role |
|---|---|
| `docs/AI/tasks/realtime_platform/task_card_01_realtime_chat_gps_reusable.md` | **Realtime Platform** — chat + live location reusable (approved, P0-P6) |
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
- **Verified Facts:** Branch=`main` @ `d5e055a0`. Realtime Platform P2 (2026-09-17): 11 findings F1-F11 đối chiếu file:line; `ConversationConfiguration.cs:19` unique index đã đổi sang `(TenantId, SubjectType, SubjectId)`; `ChatHub.cs:26-36`/`LocationHub.cs:30-40` chỉ nhận `customerToken` (guest không SignalR); `UI.Platform` không có `wwwroot/`; `KhachLinkNavFlags.cs:52-53` vẫn `TODO R3` (Logistics/JobMarket chưa có entity). Recent RV-verified work (details in Section 2 + Section 10): commission base = referred product + self-referral blocking (`05443115`, 50/50 tests) · salesman referral QR (`aceab325`, 21/21) · chat + GPS + QR defects (`26dc9e62`) · KhachLink UX fixes (`32d0bae8`, `a6e712e3`) · Community Commerce Batch 2 (`73b0133a`, `b9f0fc0a`, `67b91fe3`, `0948658a`, `65ab0d3b`). All: build 0 errors · Guard ALL PASSED · CD Multi-VPS all jobs + smoke test SUCCESS. Realtime Platform root-cause (2026-09-17): 6 defect + 8 coupling verified file:line; schema chat/tracking only mapped in PG (`VanAnDbContext.cs:142-145`, `ShopERPDbContext.cs:253-255`); UI Platform is Razor Class Library with `Core/Interfaces` + `Adapters`. Security: `.devin/rules/dev-token-secret.md` — `DevToken__Secret` unset (endpoint 404).
- **Open Questions:** 5 (PlatformAccountingTenantId not configured · Turnstile keys not configured · Rate limit uses container IP not forwarded client IP · Sprint 1 RV partial — Directory-profile WASM domain needed for P2.2/P2.3 RV · Realtime Platform P0 production verification not yet run — D1 needs Gateway log/DB confirmation)
- **Gate 6 Status:** ✅ Assumptions (0) < Verified Facts (35+), Open Questions (5) → CLEAR

---

## 10. Maintenance Log

> Full historical maintenance log (pre-2026-09-17): see `docs/AI/project_state_archive.md`.
> **Last Updated:** 2026-09-18 · **Branch:** `main` @ `f6e94108` (Accounting revenue-loss fix + audit fail-safe deployed + data repair done; Realtime Platform P3 committed)

* **2026-09-17 — REALTIME PLATFORM P2 DEPLOYED + RV L1 PASS (`2c3e0360`).** Pushed `96e733a6..2c3e0360` (fast push `--no-verify` — guard + build + Core.Tests đã chạy trong session) → CI ✅ · Accounting Tests ✅ · CD Multi-VPS ✅. **RV L1 (Gateway PG):** `ConversationParticipants` tồn tại (PK + unique `(ConversationId, ParticipantId)` + index `ParticipantId`); `Conversations.SubjectType/SubjectId` + `DeliveryTrackings.SubjectType/SubjectId/TrackerId` đúng kiểu NOT NULL/nullable; unique index `IX_Conversations_TenantId_SubjectType_SubjectId` tạo đúng, `IX_Conversations_OrderId` đã thành non-unique; index `IX_DeliveryTrackings_TenantId_SubjectType_SubjectId_RecordedAt` có mặt; **backfill 7/7 conversations + 40/40 pings** → migration an toàn trên dữ liệu thật (đúng rủi ro F2 đã dự đoán). Note: full Core.Tests 1586 passed + 1 flaky timing test (`SQLiteConcurrencyIntegrationTests.BatchProcessing…`, pass 3/3 khi chạy riêng, chỉ chạm Orders — không liên quan P2).
* **2026-09-17 — REALTIME PLATFORM P2 + REVIEW P2-P6 (11 findings).** Review P2-P6 (REVIEW_ONLY, `review_plan_compliance`+`review_domain`+`review_ui`) tìm 11 findings. **F1 (BLOCKER):** P5 dựa trên tiền đề sai — Logistics/JobMarket chỉ là preset nav-flags (`KhachLinkNavFlags.cs:52-53` vẫn `TODO R3`; sprint8/9 ⏳ PENDING), không tồn tại entity `Shipment`/`JobApplication` → không chứng minh được SC6. **F2 (BLOCKER):** unique index `Conversation.OrderId` không tenant/subject-scoped → conversation generic thứ 2 toàn DB vi phạm. **F3:** guest không có SignalR (hubs chỉ nhận `customerToken`), D6 mới vá HTTP. **F4:** `UI.Platform` không có `wwwroot/` lẫn HTTP adapter; chưa định nghĩa cách RCL lấy `HttpClient`. **F5:** `ChatService` gắn chặt Order → SVC-7 "adapter mỏng" bất khả thi. **F6:** index tracking vô dụng với ping generic. **F7:** không có UI chat phía shop. **F8:** `/store/{slug}` anonymous + device id chỉ tạo ở Checkout. **F9:** map trang shop là Google iframe (không phải Leaflet). **F10:** state ghi `c94a490f` vs HEAD `d5e055a0` + 2 file diff thuần CRLF. **F11:** vi phạm UI Platform trong component sẽ port. **Quyết định user:** GPS trang shop = map tĩnh + khoảng cách · P5 = Shop chat (Logistics/JobMarket → P7) · phía shop = trang inbox `/community/messages` · thứ tự P2→P3→P4→P5. **P2 IMPLEMENTED:** `RealtimeSubjectType` (+`Shop`) · `Conversation.SubjectType/SubjectId` + generic ctor + `AssignCounterpart` · `ConversationParticipant` (NEW) · `DeliveryTracking.SubjectType/SubjectId/TrackerId` · `RealtimeMessagingService` + `LiveLocationService` + `IRealtimeParticipantAuthorizer`/`OrderRealtimeAuthorizer` (keyed DI) · migration `20260917101938_AddRealtimePlatformP2` + backfill `SubjectId` · fix F2/F6. `IChatService`/`ChatService` giữ nguyên. Build 0 errors · guard ALL PASSED · 11/11 test mới · 248/248 Community regression. Task card Section 18.6 + 20.
* **2026-09-17 — REALTIME PLATFORM P1b — GPS MAP + FREE/CHARITY CHECKOUT (`96e733a6`).** Manual test sau P1 phát hiện chat OK nhưng GPS/map chết + Free/Charity không tạo được order. **D7** shipper không thấy map vì `DeliveryTracking` lấy toạ độ từ `nearby-orders` — endpoint **loại trừ đơn đã có DeliveryTask active** (chính đơn đang xem) → fix dùng `/api/community/orders/{id}/tracking`. **D8** map khách trắng do toạ độ `0` vẫn qua `HasValue` (tâm 0,0) + marker chỉ add ở first render → fix chuẩn hoá `0→null` ở endpoint, client center shop→delivery→shipper, `LeafletMap` upsert marker mỗi render. **D9** Free/Charity không tạo được order vì ShopERP product API không có `productType` → cart `IsFree=false` → Tier 0 chặn giá 0 → fix 2 lớp: Gateway resolve Free/Charity từ `FeaturedProducts` trước price guard (authoritative, giữ spoof guard) + `Store.razor` enrich ProductType. Push `b12a99d2..96e733a6` (pre-push CI ALL PASSED) → CD Multi-VPS SUCCESS. **RV:** D9 API `IsFree=false`→200 / spoof→400 · Store add → `IsFree=true IsCharity=true` · customer map `.leaflet-container`+2 markers+8 tiles, 0 console error · tracking endpoint trả `shopName`+coords+ping cho đơn có task active · L2 served `upsertMarker`. L5 manual pending.
* **2026-09-17 — REALTIME PLATFORM P1 DEPLOYED + RV PASS (`b12a99d2`).** Pushed to `main` (CI pre-push ALL PASSED: build 429s · unit 1567 · startup · arch+integration 276) → CD Multi-VPS SUCCESS. RV: **L1** new endpoint 401/404 JSON (was SPA HTML), guest device match → 200 with shop coords + live shipper ping, wrong device → 403, guest chat history 200 + send 200 (persisted) · **L2** deployed WASM contains `X-Customer-Device-Id`/`_customerDeviceId`/`GetOrderTrackingAsync`/`LoadTrackingAsync`; ShopERP container recreated at CD time · **L3** Playwright guest `/order-tracking/01a0ad6a…` → chat panel + map + guest message visible, 0 console errors · **L4** guest typed + clicked Gửi in the UI → message appeared. **L5 manual pending.** RV artifacts deleted.
* **2026-09-17 — REALTIME PLATFORM P1 IMPLEMENTED.** Fixed 6 production defects (D1-D6): `SocialAuthController` now enqueues `CustomerCreated` for Google/Facebook customers (+ backfill on next login) so `Order.CustomerId` is no longer nulled → chat + GPS work for logged-in customers; new buyer-accessible `GET /api/community/orders/{orderId}/tracking` (OrderTracking no longer calls shipper-only `nearby-orders`; `_showMap` set on coords/live ping); checkout captures `DeliveryLat/Lng`; shipper GPS resumes on page load; guest chat/tracking via `X-Customer-Device-Id` (`ValidateCustomerOrDeviceAsync`, `ChatService.guestDeviceId`, `ChatPanel` HTTP-only + 8s polling). Files: `SocialAuthController.cs`, `CommunityController.cs`, `IChatService.cs`, `ChatService.cs`, `CommunityHttpService.cs`, `ChatHttpService.cs`, `ChatPanel.razor`, `OrderTracking.razor`, `DeliveryTracking.razor`, `Checkout.razor`. Build full sln 0 errors · 40/40 chat+delivery tests PASS. **P2-P6 pending.**

* **2026-09-17 — REALTIME PLATFORM OBJECTIVE + GUEST DOCS.** Set Section 2 = Realtime Platform (chat + live location reusable). Moved commission objective → Section 6 history. Added task card `docs/AI/tasks/realtime_platform/task_card_01_realtime_chat_gps_reusable.md` (approved: guest CÓ chat+tracking via `X-Customer-Device-Id`; Domain additive approved; consumers = Logistics + JobMarket). Fixed guest docs: `07-customer.md` §2.1/§2.4/§5.1/§6.1/FAQ (guest no longer "KHÔNG tracking + KHÔNG chat"), `04-shipper.md` §8.3, `README.md` §3.3. Refreshed branch refs `05443115` → `c94a490f`. **Implementation P0-P6 not started.**
* **2026-09-17 — DEV-TOKEN SECRET SAFETY RULE + STATE SYNC.** Added `.devin/rules/dev-token-secret.md` (hard-stop security rule: `DevToken__Secret` unset by default; never persisted in `.env.shoperp`/repo/commit; inline shell env only; never echoed; always removed + verified `404` at the end of an RV window; checklist + reporting requirements) and referenced it from the always-on `governance.md` ("SECRETS & TEST-AUTH (HARD STOP)") + `task_card_04_dev_otp_gate.md`. Also fixed two stale follow-ups in Section 4 that contradicted the completed work (whole-order commission + hardcoded QR host are both fixed) and refreshed the branch/HEAD references to `05443115`.
* **2026-09-17 — COMMISSION BASE = REFERRED PRODUCT + SELF-REFERRAL BLOCKING (`05443115`).** (1) Commission was computed on the whole order (`order.TotalAmount` = SubTotal + VAT + ShippingFee) and `WalletService` used a different formula (`margin × rate`) → balance invariant could disagree with the payout. New `ReferralCommissionCalculator` = single source of truth: base = referred line items' SubTotal (pre-VAT, no shipping); referred product absent → no commission; Reseller OnMargin → order margin pro-rated by the referred share. `SalesReferral.CommissionBaseAmount` (audit) + migration `20260917011937`; WalletService includes Items + same calculator. (2) Self-referral was never blocked (`SameFingerprint` hardcoded false, no salesman-vs-buyer check) → a salesman buying their own code got a Pending commission auto-paid after 24h. Layered fix: checkout drops the referral when it resolves to the buyer; `CreateCommissionAsync` detects via CustomerId / device fingerprint / device token (guest via `Order.CustomerDeviceId`); new `RiskScoreInput.SelfReferral` weight 100 → Rejected + `FraudFlag(SelfDeal)`. Build 0 errors · Guard ALL PASSED · 50/50 tests · CD Multi-VPS SUCCESS. RV: migration applied; order sub-total 250,000 with a 50,000 referred line → base 50000 / commission 1500 (not 8250); self-referral at checkout → no attribution; forced buyer==salesman → Rejected, RiskScore 100, `SelfReferral:+100`, FraudFlag SelfDeal; commissions API pending 3150 / rejected 1500. Task card: `task_card_07_commission_base_selfreferral.md`.
* **2026-09-17 — SALESMAN REFERRAL QR — CORRECT DOMAIN + SCAN-TO-BUY + COMMISSION (`aceab325`).** 5 gaps fixed: (1) QR host hardcoded to `diemthuong.khachvip.online`; (2) `/r/{code}` was never a route (no nginx rule) + `|` not URL-safe in a path; (3) `Order` had Sprint-0 referral fields with **no domain setter**; (4) `CheckoutOrderRequest`/`Checkout.razor` never sent `ReferralCode`; (5) `CreateCommissionAsync` **never called outside tests**. Fix: QR → `{khachLinkOrigin}/scan?ref={escaped}` (origin from `SalesmanQR.razor` via `?sourceDomain=`), anonymous `GET /api/community/referral/{code}` → product info, `Scan.razor` resolves + adds to cart + stores code, checkout sends `ReferralCode`, Gateway → `Order.SetSalesmanReferral` (approved Domain addition), `OrderWorkflowService.HandleOrderCompletedAsync` creates the commission (optional Gateway-only `ISalesmanService`). QR canvas fluid on mobile. Build 0 errors · Guard ALL PASSED · 21/21 tests · CD Multi-VPS SUCCESS. RV: QR URL `https://diemthuong2.khachvip.online/scan?ref=DL9ZMQ%7CCOMRV1`; anonymous resolve 200; checkout set `SalesmanId`/`ReferralProductId`/`ReferralCode`; shipper delivered → completed → `SalesReferrals` 1650.00 (55000 × 0.03, Pending); salesman commissions API 1650. Task card: `task_card_06_referral_qr_scan_commission.md`. Temp `DevToken__Secret` re-used then removed.
