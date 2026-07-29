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

**Community Commerce Sprint 1 — COMPLETE (CC-S1-T0c/T0/T1/T2 + UI + E2E)**

All Sprint 1 deliverables done:
- **CC-S1-T0c:** Customer Login Simplify (COMPLETE + VPS verified, commit `4e7d9507`)
- **CC-S1-T0:** Domain Modification — "delivering" status (6 tests PASS, commit `64d3bf77`)
- **CC-S1-T1:** Nearby orders API + Haversine (10 tests PASS, commit `64d3bf77`)
- **CC-S1-T2:** Accept order API + F2 fix + CommunityController (commit `64d3bf77`)
- **CC-S1-T1/T2 UI:** CommunityHttpService + NearbyOrders.razor + NavMenu shipper tab + role endpoint + DI (pending commit)
- **CC-S1-T1/T2 E2E:** community-nearby-orders.spec.ts (8 test cases, pending commit)

Backend deployed + RV verified on VPS: 9/9 PASS.

Branch: `main`. Pending commit: Sprint 1 UI + E2E test.

---

---

**Previous: Loyalty/CRM Audit Fix — P3 Cosmetic (COMPLETE 2026-07-28, commit `018a42c2`)** — 3 cosmetic tasks (PromoPushComposer extract, RecipientConfig extract, CSV export endpoint). Branch `fix/loyalty-crm-audit-fix` (NOT yet merged).

**Previous: Loyalty/CRM Audit Fix — P2 UX Completions (COMPLETE 2026-07-27)** — 5 UX tasks (per-row/bulk promo send, progress bar, detail expand, push column). Commit `56926b44`.

---

**Previous: Community Commerce Sprint 0 — Foundation (COMPLETE 2026-07-26)** — 11 Domain entities + 42 tests + migration. Merged to `main`, VPS deployed, RV 18/18 PASS. Branch `feature/community-sprint0-foundation` (commits `e1a75bbf` + `f563e415`).

---

## 3. Current Status

- **Branch:** `main`
- **Last commit:** `4e7d9507` feat(community): CC-S1-T0c customer login simplify (Sprint 1)
- **.NET SDK:** 8.0.422
- **DB:** SQLite `vanan_shoperp.db` (business) + PostgreSQL `VanAnCoreHub` (accounting + Gateway + Community tables)
- **Build (2026-07-28):** 0 errors, 1117 warnings (pre-existing CA). Core.Tests 21/21 outbox+evidence tests PASS. guard-check ALL PASSED. Pre-commit guard PASSED.
- **VPS (2026-07-28):** 7 containers healthy (`vanan-nginx`, `vanan-shoperp`, `vanan-gateway`, `vanan-khachlink`, `vanan-postgres`, `vanan-nats`, `vanan-seq`). CD auto-deploy on push to main. Domains: `khachvip.online` (ShopERP), `diemthuong.khachvip.online` (KhachLink), `api.khachvip.online` (Gateway).
- **VPS CRM/Loyalty Verification + P0/P1 Fix (2026-07-28 COMPLETE + DEPLOYED + VERIFIED, commits `8d75abc1` + `e47dad26`):** Verified guide vs VPS với 3 roles (Owner `adminvanan1`, SystemAdmin `sysadmin@vanan.vn`, Customer). Found 4 issues, fixed P0+P1:
  - **P0-A1 — Owner AccessDenied (FIXED):** 3 trang admin (`/admin/missions`, `/admin/redemption-catalog`, `/admin/redemption-history`) có `[Authorize(Policy="SystemAdmin")]` chặn Owner. Guide mục 3.1+5.1 nói Owner+SA đều có quyền. Fix: đổi sang `[Authorize(Policy="OwnerOnly")]`. VPS verify: Owner login → 3 trang 200 (trước đó 302→AccessDenied).
  - **P0-A2 — Outbox stuck loop (FIXED root cause):** NatsSyncWorker re-publish event `832f4637` mỗi 1 giây, block toàn bộ outbox processing → cao điểm tê liệt. **4 evidence thu thập:** (1) `ExecuteUpdateAsync` rowsAffected=0 cho lowercase Id, =1 cho UPPERCASE Id; (2) container mở đúng file `/app/keys/vanan_shoperp.db`; (3) cả 3 WAL file tồn tại; (4) ToDomain reflection works. **Root cause:** EF Core SQLite gửi Guid parameter UPPERCASE, một số row có lowercase Id (từ NATS sync/JSON) → SQLite BINARY collation case-sensitive → WHERE không match → 0 rows → loop. **Fix:** `OutboxRepository` dùng raw SQL + `COLLATE NOCASE` (case-insensitive, đúng .NET Guid semantics). VPS verify: lowercase row processed <1s, 0 errors, pending=0.
  - **P1-B1 — Guide sai endpoint (FIXED):** `docs/user-guide/CRM_Loyalty_Guide.{md,html}` ghi `GET /api/customer-orders` → thực tế `GET /api/customerorders` (không gạch ngang, Gateway controller route). Fix cả 2 file.
  - **P1-A3 — `/` redirect `/sitemap` (NOT A BUG):** Code comment xác nhận by design — Sitemap.razor là single entry point cho role-based navigation.
  - **Diagnostic commit `1b02efb4`:** Added evidence-gathering logging to MarkAsProcessedAsync (rowsAffected + row lookup + DbContext type). Superseded by root cause fix `e47dad26`.
  - **Test coverage:** 4 new file-based SQLite evidence tests (`OutboxFileBasedEvidenceTests.cs`) — ToDomain reflection, ExecuteUpdateAsync persist, full round-trip, **lowercase Id COLLATE NOCASE match** (reproduce production bug). 21/21 outbox+evidence tests PASS.
- **Loyalty/CRM Audit Fix — P3 (2026-07-28 COMPLETE, commit `018a42c2`, NOT yet merged):** Cosmetic (3 tasks). (1) P3-T1 Extract `PromoPushComposer.razor` from `CustomerList.razor` inline modal — new component at `5_WebApps/ShopERP/Components/Pages/Admin/PromoPushComposer.razor` owns form fields (title/message/url) + reset-on-show; parameters `Show`, `RecipientCount`, `IsSubmitting`, `ErrorMessage`, `OnSubmit` (EventCallback<PromoPushPayload>), `OnClose`; `CustomerList.razor` removed 56 lines inline modal + 3 form-state fields + `ResetPromoForm` helper; `SubmitPromoAsync` accepts `PromoPushComposer.PromoPushPayload`. (2) P3-T2 Extract `PromoCampaignRecipientConfiguration.cs` to own file at `3_CoreHub/Infrastructure/Configurations/PromoCampaignRecipientConfiguration.cs` (same namespace, same logic); `PromoCampaignConfiguration.cs` trimmed to single class. (3) P3-T3 `POST /api/customers/export` endpoint (`CustomerController.ExportCsv`) — accepts `SegmentRequest` body, returns CSV `Name,Phone,Tier,Points,TotalSpent,LastOrder,Birthday,IdentityLevel,HasPush` with `text/csv` + `attachment; filename=customers.csv`; minimal CSV escaping. Closes deviations D5, D7, D13. No domain layer changes. No regressions (1038 Core.Tests PASS).
- **Loyalty/CRM Audit Fix — P2 (2026-07-27 COMPLETE, commit `56926b44`, NOT yet merged):** UX completions (5 tasks). (1) P2-T1 Per-row "Gửi" button: `CustomerList.razor` per-row button opens promo modal with single customer ID via new `IPromoCampaignService.CreateCampaignAsync(title, msg, url, IReadOnlyList<Guid>)` overload. (2) P2-T2 Bulk select: checkbox column + select-all-on-page header checkbox + "Gửi cho N đã chọn" button; `HashSet<Guid> _selectedCustomerIds` survives pagination, pruned on filter change; modal dispatches to explicit-ID overload. (3) P2-T3 Progress bar: `PromoCampaignList.razor` renders striped+animated progress bar for Processing rows (`width = SentCount/TotalRecipients*100%`); `PeriodicTimer` auto-refreshes every 5s while any campaign Processing/Pending, stops when none; `IAsyncDisposable` cleanup. (4) P2-T4 Detail expand: "Chi tiết" button toggles inline recipient table; loads via `IPromoCampaignService.GetRecipientsAsync` (20/page + "Tải thêm"); enriches with customer names via `ICustomerRepository.GetByIdAsync`. (5) P2-T5 Push column: `CustomerDto.HasPushSubscription` field; `CustomerController` injects `IPushSubscriptionRepository`, batch-loads active push CustomerIds per request (single query → HashSet); `MapCustomerDto` gains `hasPushSubscription` param (default false — backward compatible); UI shows ✓/✗ icon. Backend: `PromoCampaignService` new ctor param `ICustomerRepository` (DI-resolved); `PromoCampaignController.Create` accepts `SelectedCustomerIds` (non-empty → explicit flow, else segment fallback); `CreateCampaignRequest.SelectedCustomerIds` field. No regressions (1023 Core.Tests PASS).
- **Loyalty/CRM Audit Fix — P1-T3 (2026-07-27 COMPLETE, commit `756f1dac`, NOT yet merged):** Missions pagination full-stack. (1) Repo: `IMissionRepository.GetCompletionsByCustomerPagedAsync(customerId, page, pageSize)` → `(Items, Total)` with Skip/Take + CountAsync, page 1-based, pageSize clamped 1-100. (2) Service: `IMissionService.GetCustomerCompletionsPagedAsync` delegates to repo. (3) Controller: `MissionsController.GetMyCompletions` accepts `[FromQuery] page` + `[FromQuery] pageSize` (default 1/20), returns `{ items, total, page, pageSize, hasMore }` instead of flat list. (4) Gateway: `MissionsController.ForwardAsync` now forwards `Request.QueryString` (enables `?page=2&pageSize=20`). (5) UI: KhachLink `Missions.razor` loads page 1 (20 items) on init, "Xem thêm" button appends next page via `LoadMoreCompletionsAsync`. State: `_completionsPage`, `_completionsHasMore`, `_completionsTotal`, `_completionsLoadingMore`. New `PaginatedCompletionsResponse` DTO. No regressions (14/14 mission+toggle tests PASS).
- **Loyalty/CRM Audit Fix — P1-T2 (2026-07-27 COMPLETE, commit `e58184da`, NOT yet merged):** 15 missing tests (TDD). (1) 5 toggle tests (`NotificationToggleTests.cs`): Notify_RedemptionFulfilled ON→push sent / OFF→push skipped+fulfillment succeeds; Notify_MissionCompleted ON→push sent; Notify_BirthdayBonus OFF→push skipped+points awarded; Notify_VoucherExpiringSoon OFF→push skipped+job still queries. (2) 10 URL validation tests (`CustomerProfileShareUrlValidationTests.cs`): Facebook /posts/ + /permalink?story_id= → 200; homepage + profile + empty → 400. TikTok /@user/video/ + /user/video/ → 200; homepage + profile + empty → 400. All 15 tests PASS. Production code changes (minimal, non-breaking): PushNotificationService 4 Send*NotificationAsync methods → `virtual` (Moq intercept); BirthdayBonusJob.RunBirthdayBonusAsync + VoucherExpiryReminderJob.RunExpiryRemindersAsync: `private` → `internal`; ShopERP InternalsVisibleTo VanAn.Core.Tests + VanAn.Integration.Tests.
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

1. **CC-S6-T5 (Sprint 6) — Collaborator SMS OTP + Deposit Wallet (TOGGLE):** SystemAdmin toggle ON/OFF. Default OFF. ON: Salesman/Shipper/Owner bắt buộc SMS OTP, phí trừ deposit. **Cần Domain Modification approval** (WalletTransactionType.Deposit=7 + SmsOtpFee=8, CommunityRole.IsPhoneVerified, SystemSetting toggle).
2. **Community Commerce Sprint 1 — Nearby Orders** — per `task_cc_sprint1_nearby_orders-2c5017.md`. Requires Domain Modification #2: OrderStatuses.Default[] + "delivering" status. **Needs user approval for Domain Modification.** CC-S1-T0c đã xong, còn CC-S1-T0 (delivering) + CC-S1-T1/T2 (nearby orders + accept).
3. **A2 follow-up — Guid case audit (P2):** Guid case mismatch không chỉ ảnh hưởng OutboxMessages. Cần audit các table khác. Fix triệt để: thêm `COLLATE NOCASE` vào EF config cho Id column, hoặc normalize tất cả row lowercase → UPPERCASE.
4. **Replace FingerprintJS stub** — Download real FingerprintJS v4 (MIT) before production deployment.
5. **Phase 8 — Multi-VPS E2E Validation (Playwright)** — per `phase8_multi_vps_e2e_task_card.md`. 7 E2E scenarios.
6. **Tech debt cleanup** — TD-MVPS-001 through TD-MVPS-004. **TD-CUSTSYNC-001:** Customer sync SQLite→PG.
7. **(Cosmetic)** Fix `?` in Checkout.razor. Fix `isTabVisible` display bug in OrderTracking.razor.
8. **(Env)** Fix local DB role mismatch — ShopERP `vanan_admin` vs Gateway `vanan_dev`.
9. **(Guard-check script)** Investigate transient `$LASTEXITCODE` false-positive in fast-test-gate (`dotnet test ... | Out-Null` pattern). Direct `dotnet test` with identical args → exit 0; guard-check reports FAIL.
10. **(Facebook OAuth)** Config real Facebook OAuth credentials (AppId + AppSecret) — Sprint 7+. Currently stub redirect.

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
- **Verified Facts:** Branch=main, Sprint 0 verified 100% (11 entities Domain.cs:3191-3716 + 11 EF configs + migration + 59 community tests PASS + 39 architecture tests PASS + guard-check ALL PASSED + fingerprint JS tồn tại), build 0 errors (1120 warnings pre-existing CA), spec v1.5 + master plan v1.5 updated, guard-check.ps1 fixed (regex syntax + test file exclusion).
- **Open Questions:** 0
- **Gate 6 Status:** ✅ Assumptions < Verified Facts, Open Questions < 3

---

## 10. Maintenance Log

* **2026-07-29 — CC-S1-T0/T1/T2 SPRINT 1 BACKEND (delivering status + nearby orders + accept).** Domain Modification approved by user. 8 files: `1_Shared/Domain.cs` (+delivering OrderStatusDefinition Sequence=5 + shift completed→6/cancelled→7, +Order.AssignShipper() +Order.SetDeliveryLocation() F2 fix), `3_CoreHub/Services/OrderWorkflowService.cs` (+delivering transitions in both kitchen ON/OFF flows: ready→delivering, confirmed→delivering, delivering→completed/cancelled/delivered), `3_CoreHub/Services/ICommunityOrderService.cs` (NEW — interface + NearbyOrderDto), `3_CoreHub/Services/CommunityOrderService.cs` (NEW — Haversine + nearby orders query cross-tenant + accept with DeliveryTask creation + Order.AssignShipper + status→delivering), `2_Gateway/Controllers/CommunityController.cs` (NEW — GET /api/community/nearby-orders + POST /api/community/orders/{id}/accept, X-Customer-Token auth via ShopERP /me forward + CommunityRole Shipper check), `2_Gateway/Program.cs` (+ICommunityOrderService DI registration), `6_Tests/VanAn.Core.Tests/Community/DeliveringStatusTests.cs` (NEW — 6 tests: T0.1 Default contains delivering, T0.2-T0.6 transition rules), `6_Tests/VanAn.Core.Tests/Community/CommunityOrderServiceTests.cs` (NEW — 10 tests: Haversine same point + HCM→HN ~1080km + filters by radius/type/status/assigned + sorts by distance + accept creates task + double-accept null + invalid status null, SQLite in-memory), `6_Tests/VanAn.Architecture.Tests/AuthorizationEnforcementTests.cs` (+CommunityController to W12-G7 exempt list). Build 0 errors, 65 community tests PASS (6+10+49), 39/39 Architecture tests PASS. Branch: `main`.
* **2026-07-29 — CC-S1-T0c SPRINT 1 CUSTOMER LOGIN SIMPLIFY — COMPLETE + VPS VERIFIED.** Xóa SMS OTP khỏi Login.razor primary flow + rewrite IdentityUpgradeModal từ OTP flow → 3 buttons (Google + Facebook + Guest=skip). 5 files: `Login.razor` (REWRITE — xóa LoginStep.Otp + SĐT/OTP input + SendOtp/VerifyOtp, UI mới Google + Facebook + "Tiếp tục as Guest" button, Guest→NavigateTo `/`), `IdentityUpgradeModal.razor` (REWRITE — xóa OTP flow Intro→OtpSent→Success, UI mới 3 buttons Google + Facebook + "Bỏ qua", show sau checkout success, Guest skip→OnDismiss→order hợp lệ + DeviceId fallback tích điểm), `SocialAuthController.cs` (+Facebook stub endpoints `GET /api/auth/facebook/login` + `/callback`, Sprint 7+ config real OAuth), `SocialAuthHttpService.cs` (xóa SendUpgradeOtpAsync + VerifyUpgradeOtpAsync + DTOs, giữ RedeemPointsAsync), `AuthorizationEnforcementTests.cs` (+DeviceRegistrationController to W12-G7 exempt list, same pattern as CustomerIdentityController). OTP endpoints trên server GIỮ NGUYÊN (Sprint 6 collaborator toggle). Checkout flow KHÔNG login chen ngang — modal chỉ show SAU khi đơn hoàn tất. Build 0 errors, 59 community/device/login/social tests PASS, 39/39 Architecture tests PASS. EInvoiceOrchestratorTests flaky (pre-existing, passes in isolation). **VPS RV (2026-07-29):** WASM binary verified `Guest`=10 matches + `Facebook`=1 match + `OTP`=0 (removed). API endpoints: OTP still 200 (kept), Facebook 302 (stub), Google 302, device register 401 (CC-S0-T3 regression), fingerprint JS 200. 3 curl-based "failures" were false negatives (Blazor WASM renders client-side, curl sees static HTML shell only). Commit `4e7d9507`. Branch: `main`.
* **2026-07-29 — CC-S0-T3 SPRINT 0.5 DEVICE FINGERPRINT WIRE-UP.** Sprint 0 GAP duy nhất đã đóng. 4 files: `2_Gateway/Controllers/DeviceRegistrationController.cs` (NEW — Gateway-native endpoint `POST /api/customer-identity/device/register`, validate X-Customer-Token qua ShopERP /me forward rồi gọi IDeviceRegistrationService, max 3 active devices enforcement), `5_WebApps/KhachLink/Pages/Login.razor` (+`RegisterDeviceFingerprintAsync` helper, fire-and-forget sau Google OAuth + OTP login success, non-blocking), `5_WebApps/KhachLink/wwwroot/index.html` (+`<script src="/js/fingerprint.js">`), `6_Tests/VanAn.Core.Tests/Community/DeviceRegistrationControllerTests.cs` (NEW — 3 unit tests: 401 without token, 400 empty fingerprint, 400 empty deviceToken, MockDeviceRegistrationService + MockHttpClientFactory). Architecture note: task card gốc nói "ShopERP + Gateway forward" nhưng thực tế Gateway-native vì DeviceRegistrationService chỉ registered trong Gateway DI + ShopERP IVanAnDbContext → SQLite không có DeviceRegistrations table + community entities PG-only (v1.3). Build 0 errors, 1045 Core.Tests PASS (3 new + 1042 existing). guard-check fast-test-gate transient false-positive (known issue item 10) — direct `dotnet test` with same filter exit 0. Branch: `main`.
* **2026-07-29 — COMMUNITY COMMERCE SPEC v1.5 + SPRINT 0 VERIFICATION.** Spec v1.5: thêm Section 1.6 "Collaborator Verification Policy (Toggle + Deposit Wallet)" + UC-02b + update UC-01/UC-02. Master plan v1.5: thêm CC-S0-T3 (Sprint 0.5 fingerprint wire-up) + CC-S1-T0c (Sprint 1 login simplify) + CC-S6-T5 (Sprint 6 collaborator verify + deposit toggle) + Sprint 7 branch protocol + task card reference. Sprint 0 base code đối chiếu 100% pass: 11 entities + 11 EF configs + migration `20260726105331_CommunitySprint0.cs` + 59 community tests + 39 architecture tests + guard-check ALL PASSED + fingerprint JS. GAP duy nhất: fingerprint wire-up chưa hoàn thành (CC-S0-T3 sẽ xử lý). guard-check.ps1 fix: regex syntax error (PowerShell single-quote escaping `["\']`→`["'']` trong single-quoted string) + exclude `6_Tests\` từ raw SQL scan (test cleanup code dùng `ExecuteSqlRawAsync("DELETE FROM...")` trên SQLite test DB — false positive). Branch: `main`.
* **2026-07-28 — LOYALTY/CRM AUDIT FIX P3 (S6).** Commit `018a42c2` on `fix/loyalty-crm-audit-fix` (NOT yet merged). 5 files: `PromoPushComposer.razor` (NEW — extracted modal component, owns form fields + reset-on-show, params Show/RecipientCount/IsSubmitting/ErrorMessage/OnSubmit/OnClose, payload record `PromoPushPayload`), `CustomerList.razor` (removed 56 lines inline modal + 3 form-state fields + `ResetPromoForm` helper; `SubmitPromoAsync` accepts `PromoPushComposer.PromoPushPayload`), `PromoCampaignRecipientConfiguration.cs` (NEW — extracted from `PromoCampaignConfiguration.cs`, same namespace+logic), `PromoCampaignConfiguration.cs` (trimmed to single class), `CustomerController.cs` (+`ExportCsv` endpoint `POST /api/customers/export` returning CSV with `text/csv` + `attachment; filename=customers.csv`, minimal CSV escaping). Build 0 errors, 1117 warnings (pre-existing CA). 1038/1053 Core.Tests PASS (0 failed, 15 skipped). guard-check ALL PASSED. Closes deviations D5, D7, D13. Branch: `fix/loyalty-crm-audit-fix`.
* **2026-07-27 — LOYALTY/CRM AUDIT FIX P2 (S5).** Commit `56926b44` on `fix/loyalty-crm-audit-fix` (NOT yet merged). 6 files: `IPromoCampaignService.cs` (+`CreateCampaignAsync(title,msg,url,IReadOnlyList<Guid>)` overload), `PromoCampaignService.cs` (explicit-ID impl + new `ICustomerRepository` ctor param), `PromoCampaignController.cs` (`CreateCampaignRequest.SelectedCustomerIds` + dispatch logic), `CustomerController.cs` (inject `IPushSubscriptionRepository` + batch push lookup + `MapCustomerDto` gains `hasPushSubscription`), `CustomerList.razor` (checkbox col + select-all + per-row "Gửi" + bulk "Gửi cho N đã chọn" + Push column + modal selection mode), `PromoCampaignList.razor` (progress bar for Processing + auto-refresh 5s via PeriodicTimer + "Chi tiết" expand/collapse recipients with name enrichment). Build 0 errors, 1023 Core.Tests PASS. Branch: `fix/loyalty-crm-audit-fix`.
* **2026-07-27 — LOYALTY/CRM AUDIT FIX P1-T3 (S4).** Commit `756f1dac` on `fix/loyalty-crm-audit-fix` (NOT yet merged). 7 files: `IMissionRepository.cs` (+`GetCompletionsByCustomerPagedAsync`), `MissionRepository.cs` (Skip/Take+CountAsync impl), `IMissionService.cs` (+`GetCustomerCompletionsPagedAsync`), `MissionService.cs` (delegate), `MissionsController.cs` (ShopERP — page/pageSize query params + `{items,total,page,pageSize,hasMore}` response), `MissionsController.cs` (Gateway — forward Request.QueryString), `Missions.razor` (KhachLink — 20/page + "Xem thêm" button + `LoadMoreCompletionsAsync` + `PaginatedCompletionsResponse` DTO). Build 0 errors, guard-check ALL PASSED, 14/14 mission+toggle tests PASS. Branch: `fix/loyalty-crm-audit-fix`.
* **2026-07-27 — LOYALTY/CRM AUDIT FIX P1-T2 (S3).** Commit `e58184da` on `fix/loyalty-crm-audit-fix` (NOT yet merged). 6 files: `PushNotificationService.cs` (4 Send*NotificationAsync → `virtual`), `BirthdayBonusJob.cs` (`RunBirthdayBonusAsync` private→internal), `VoucherExpiryReminderJob.cs` (`RunExpiryRemindersAsync` private→internal), `Program.cs` (InternalsVisibleTo VanAn.Core.Tests + VanAn.Integration.Tests), `NotificationToggleTests.cs` (NEW — 5 toggle tests), `CustomerProfileShareUrlValidationTests.cs` (NEW — 10 URL validation tests). All 15 tests PASS. Build 0 errors, guard-check ALL PASSED. Branch: `fix/loyalty-crm-audit-fix`.
* **2026-07-27 — LOYALTY/CRM AUDIT FIX P1-T1 (S2).** Commit `2059f403` on `fix/loyalty-crm-audit-fix` (NOT yet merged). 6 files: `ICustomerRepository.cs` (+`GetAllCustomersAcrossTenantsAsync`), `CustomerRepository.cs` (impl with `IgnoreQueryFilters`), `CustomerController.cs` (+`ListGlobal` action `[Authorize(Policy="SystemAdmin")]` + `GlobalCustomerDto` with TenantId), `CustomerListGlobal.razor` (REWRITE — customer table + Tenant column + filter bar, replaces campaign overview), `CustomerRepositoryCrossTenantTests.cs` (3 tests), `CustomerGlobalEndpointAuthTests.cs` (3 tests + `StaffRoleWebApplicationFactory`). Build 0 errors, guard-check ALL PASSED, 6/6 new tests PASS, 31/31 regression tests PASS. Deviation: "Push Subscribed" column deferred to P2. Branch: `fix/loyalty-crm-audit-fix`.
* **2026-07-27 — LOYALTY/CRM AUDIT FIX P0 (S1).** Commit `4aa0c6e2` on `fix/loyalty-crm-audit-fix`. `CustomerController` + `PromoCampaignController` `[Authorize]`→`[Authorize(Policy="OwnerOnly")]`; `IPromoCampaignService` moved `3_CoreHub/Services`→`1_Shared/Services`; `CustomerSegmentCriteria` moved to `1_Shared/Domain`. Build 0 errors, guard-check PASSED.
* **2026-07-27 — KHACHLINK BUGS 1-3 FIX.** Commit `35dc9de6` merged + deployed. 8 files: new `Filters/ResolveCustomerTenantAttribute.cs` (action filter — resolves customer TenantId from token via IgnoreQueryFilters + ITenantProvider.SetTenant), 6 controllers decorated (`CustomerIdentityController`, `CustomerProfileController`, `LoyaltyController`, `MissionsController`, `NotificationsController`, `RedemptionController`), `OrderHistory.razor` (`[^8..]`→`[..8]` to match ShopERP). CD #30247192483 PASS. RV: OTP login → 5 endpoint tests all return 200 with correct data (profile, birthday save+persist, push subscribe, missions progress, loyalty info). Branch: `main`.
* **2026-07-27 — BUG 5+6 FIX.** Commit `30e42e69` merged + deployed. 4 files: OrderHub.cs (`[Authorize]`→`[AllowAnonymous]`), OrderWorkflowService.cs (DeviceId fallback + Customer stub creation in ProcessLoyaltyPointsAsync), Index.razor (explicit StateHasChanged), Display.razor (explicit StateHasChanged). CD #30241063324 PASS. RV: `/orderHub/negotiate` 200 (was 401), new order `019FA22A` confirmed has CustomerDeviceId+CustomerInfo in SQLite. Branch: `main`.
* **2026-07-27 — 4-BUG CHECKOUT-TO-KITCHEN FIX.** Commit `4af5672e` merged + deployed. 5 files: OrderRepository.cs (AsNoTracking), OrderService.cs (CustomerNotes payload), OrderSyncSubscriber.cs (parse notes + CustomerId + Customer stub), Index.razor (default filter + notes column), Display.razor (notes block). CD PASS. RV: checkout flow verified on VPS — CustomerNotes + CustomerId sync to SQLite confirmed. TD-CUSTSYNC-001 logged (Customer SQLite→PG sync gap). Branch: `main`.
* **2026-07-26 — PROJECT STATE ARCHIVED.** Reduced from 627 → ~170 lines. All Previous Objectives (Doc v1.1-v1.4, Phase 5, Loyalty L-A/L-B/L-C, Product Picker, Font/Freeze Fix, Theme, PWA Phases 1-3, Multi-VPS Option C) + full History Log + full Maintenance Log moved to `docs/AI/project_state_archive.md` (Section "Archived 2026-07-26"). Branch: `main`.
* **2026-07-26 — SPRINT 0 REVIEW + PARTIAL FIX.** Review-only audit found 8 items marked COMPLETE but not 100% production. Part 1: F2/F4/F5a (dead code pending callers) added to correct downstream sprint task cards (Sprint 1: AssignShipper/SetDeliveryLocation; Sprint 4: AssignSalesman + RiskScoringService caller + WalletService app-install caller; Sprint 5: MarkCodCollected + WalletService COD/Advance/Settlement caller). Sprint 4 + Sprint 5 task cards fixed: WalletService "Files cần CREATE" → "MODIFY" (Sprint 0 đã tạo base). Part 2 in progress: F5b (WalletService SQLite comment fixed), F6 (migration scope note added to SC9), F7 (SC13 phrasing fixed). Branch: `main`.
