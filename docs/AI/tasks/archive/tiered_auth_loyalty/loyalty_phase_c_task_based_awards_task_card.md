# TASK CARD: LOYALTY-C - Task-Based Awards + Owner Config UI + Notification Rules

> **Updated 2026-07-24** — expanded from 14 SC to 18 SC. Added 3 workstreams (WS-A/B/C) after L-B code review revealed gaps: (1) L-A loyalty formula config is appsettings.json-only with NO admin UI, (2) customer mission tracking UI needs proof submission + completion history, (3) loyalty event notifications need per-tenant configurable rules.

## 1. GOAL & CONTEXT
- **Mục tiêu cốt lõi:** Build gamification framework — khách tích điểm qua nhiệm vụ (không chỉ mua hàng) + owner tự cấu hình công thức điểm + quy tắc thông báo. Hiện tại 0% complete (audit 2026-07-23).
- **Nghiệp vụ áp dụng (3 workstreams):**

### WS-A — Owner config UI cho công thức điểm thưởng (L-A gap fix)
- **Vấn đề:** `LoyaltyPointsConfig` (PointsRate, MinPointsPerOrder, MaxPointsPerOrder, AwardOnAllOrders) hiện chỉ config qua `appsettings.json` — owner KHÔNG thể tự sửa, phải dev commit + redeploy. Không per-tenant.
- **Fix:** Extend `ShopFeatureSettingsDto` + `ShopFeatureSettingsEntity` (per-tenant, DB-backed) với 4 field loyalty formula. Owner tự sửa qua `ShopFeatures.razor` UI. `OrderWorkflowService` đọc từ `IShopFeatureSettingsService` (per-tenant) với `IOptions<LoyaltyPointsConfig>` fallback (global default).
- **Benefit:** Owner tự chỉnh formula (vd: quán A 10%, quán B 15%), per-tenant override, không cần dev.

### WS-B — Task-based awards (gamification framework — CORE)
- **Cài app (PWA install)** — khách install PWA → +50 điểm (one-time).
- **Xác thực OTP** — khách upgrade Social → Verified via OTP → +100 điểm (one-time).
- **Nhập ngày sinh** — khách nhập birthday → +30 điểm (one-time) + sinh nhật hàng năm auto +100 điểm.
- **Share/like Facebook** — khách share campaign page lên Facebook → +20 điểm (daily cap 1 lần). Khách submit share URL → verify format → award.
- **Share/like TikTok** — khách share campaign page lên TikTok → +20 điểm (daily cap 1 lần). Khách submit share URL → verify format → award.
- Admin cấu hình nhiệm vụ + điểm thưởng + điều kiện (configurable per tenant).
- **Customer UI:** `/missions` page — danh sách nhiệm vụ + progress + complete button + proof submit form (share URL) + MissionCompletion history (completed missions + points awarded + date).

### WS-C — Quy tắc notification cho loyalty events
- **Vấn đề:** Hiện `PushNotificationService.SendLoyaltyPointsChangedNotificationAsync` (Phase 5) fire trên mọi AddPoints/SubtractPoints — generic, không có notification riêng cho mission completed / birthday bonus / redemption fulfilled / voucher expiring.
- **Fix:** 5 per-tenant toggles trong `ShopFeatureSettingsDto` + `VoucherExpiryNotifyHours` + mới `SendRedemptionFulfilledNotificationAsync` + `VoucherExpiryReminderJob` (HostedService daily).
- **Notification rules:**
  - Mission completed → push "Hoàn thành nhiệm vụ X, +Y điểm" (if `Notify_MissionCompleted=true`).
  - Birthday bonus → push "Chúc mừng sinh nhật +100 điểm" (if `Notify_BirthdayBonus=true`).
  - Redemption fulfilled → push "Voucher X đã được xác nhận" (if `Notify_RedemptionFulfilled=true`).
  - Redemption cancelled + refunded → push "Đổi điểm đã hủy, hoàn Y điểm" (if `Notify_RedemptionCancelled=true`).
  - Voucher expiring soon (T-{VoucherExpiryNotifyHours}h) → push "Voucher X sắp hết hạn" (if `Notify_VoucherExpiringSoon=true`).
- **UI:** `ShopFeatures.razor` thêm section "Thông báo điểm thưởng" với 5 toggles + expiry hours input.

## 2. ACTIVE WORKFLOW ROUTING
- **Target Workflow:** `.devin/workflows/newfeaturebuild.md`
- **Execution Mode:** ANALYZE → IMPLEMENT
- **Prerequisites (ALL COMPLETE 2026-07-24):**
  - ✅ Phase 5 Push Notification COMPLETE — push infra (PushNotificationService, NATS, outbox, PushNotificationDelivery) sẵn sàng.
  - ✅ Loyalty Phase A COMPLETE — configurable formula via `IOptions<LoyaltyPointsConfig>` (L-C WS-A sẽ mở rộng sang per-tenant DB).
  - ✅ Loyalty Phase B COMPLETE — RedemptionCatalogItem + RedemptionRecord + Voucher entities + RedemptionService (L-C WS-C sẽ thêm notification cho fulfill/cancel/expiry).
  - ✅ ShopFeatureSettings pattern tồn tại — `ShopFeatureSettingsDto` + `ShopFeatureSettingsEntity` + `ShopFeatureSettingsService` + `ShopFeatures.razor` (12 toggles hiện có, L-C sẽ thêm 4 formula + 5 notification = 9 field mới).

## 3. RELEVANT FILES (CONTEXT BOUNDARY)
- **Files được phép đọc/sửa:**

### WS-A — Owner config UI (loyalty formula per-tenant)
  - `1_Shared/Services/IShopFeatureSettingsService.cs` — thêm 4 field vào `ShopFeatureSettingsDto` (Loyalty_PointsRate, Loyalty_MinPointsPerOrder, Loyalty_MaxPointsPerOrder, Loyalty_AwardOnAllOrders)
  - `3_CoreHub/Infrastructure/Entities/ShopFeatureSettingsEntity.cs` — thêm 4 property + update `UpdateToggles` method signature
  - `3_CoreHub/Services/ShopFeatureSettingsService.cs` — update `ToDto`/`FromDto` mapping + `UpdateSettingsAsync` + `IsEnabledAsync` switch
  - `5_WebApps/ShopERP/Components/Pages/Settings/ShopFeatures.razor` — thêm section "Công thức điểm thưởng" (4 inputs: rate decimal, min int, max int nullable, award-on-all toggle)
  - `3_CoreHub/Services/OrderWorkflowService.cs` — đổi source loyalty formula từ `IOptions<LoyaltyPointsConfig>` (global) → `IShopFeatureSettingsService.GetSettingsAsync(tenantId)` (per-tenant) với IOptions fallback
  - `5_WebApps/ShopERP/Migrations/` — migration add 4 columns to ShopFeatureSettings table
  - `3_CoreHub/Infrastructure/Configurations/ShopFeatureSettingsConfiguration.cs` — map 4 new columns

### WS-B — Task-based awards (gamification core)
  - `1_Shared/Domain.cs` — thêm `Mission` entity + `MissionCompletion` entity + Customer fields (Birthday, PWAInstalledAt, OtpVerifiedAt, FacebookShareCount, TikTokShareCount)
  - `3_CoreHub/Infrastructure/Configurations/MissionConfiguration.cs` (NEW)
  - `3_CoreHub/Infrastructure/Configurations/MissionCompletionConfiguration.cs` (NEW)
  - `3_CoreHub/Domain/Repositories/IMissionRepository.cs` (NEW)
  - `3_CoreHub/Infrastructure/Repositories/MissionRepository.cs` (NEW)
  - `3_CoreHub/Services/IMissionService.cs` (NEW) + `1_Shared/Services/IMissionService.cs` (NEW — contract)
  - `3_CoreHub/Services/MissionService.cs` (NEW) — mission CRUD + complete mission + award points + check notification toggle
  - `5_WebApps/ShopERP/Controllers/MissionsController.cs` (NEW) — admin CRUD missions + customer complete mission + submit share URL proof
  - `2_Gateway/Controllers/MissionsController.cs` (NEW) — forward to ShopERP
  - `5_WebApps/ShopERP/Controllers/CustomerIdentityController.cs` — thêm award points khi OTP verify (line 79-85)
  - `5_WebApps/KhachLink/Services/PWAService.cs` — thêm award points khi PWA install (line 98-124, 234-239)
  - `5_WebApps/KhachLink/Pages/Profile.razor` — thêm birthday input + mission list + progress
  - `5_WebApps/KhachLink/Pages/Missions.razor` (NEW) — `/missions` page: danh sách nhiệm vụ + progress + complete button + share URL proof submit form + MissionCompletion history
  - `5_WebApps/KhachLink/wwwroot/js/pwa.js` — thêm social share functions (Facebook SDK share, TikTok share URL)
  - `5_WebApps/ShopERP/Components/Pages/Admin/MissionsAdmin.razor` (NEW) — admin mission management
  - `5_WebApps/ShopERP/Migrations/` — migration tạo 2 tables + alter Customer table (add 5 fields)
  - `6_Tests/` — unit + integration tests

### WS-C — Notification rules
  - `1_Shared/Services/IShopFeatureSettingsService.cs` — thêm 6 field vào `ShopFeatureSettingsDto` (Notify_MissionCompleted, Notify_BirthdayBonus, Notify_RedemptionFulfilled, Notify_RedemptionCancelled, Notify_VoucherExpiringSoon, VoucherExpiryNotifyHours)
  - `3_CoreHub/Infrastructure/Entities/ShopFeatureSettingsEntity.cs` — thêm 6 property + update `UpdateToggles`
  - `3_CoreHub/Services/ShopFeatureSettingsService.cs` — update mapping
  - `5_WebApps/ShopERP/Components/Pages/Settings/ShopFeatures.razor` — thêm section "Thông báo điểm thưởng" (5 toggles + expiry hours input)
  - `3_CoreHub/Services/PushNotificationService.cs` — thêm `SendRedemptionFulfilledNotificationAsync(customerId, voucherCode, productName)` + `SendVoucherExpiringSoonNotificationAsync(customerId, voucherCode, expiresAt)`
  - `3_CoreHub/Services/RedemptionService.cs` — `FulfillAsync` + `CancelAsync` thêm notification call (check toggle before push)
  - `3_CoreHub/Services/MissionService.cs` — `CompleteMissionAsync` thêm notification call (check Notify_MissionCompleted)
  - `3_CoreHub/Services/VoucherExpiryReminderJob.cs` (NEW) — HostedService daily, query vouchers expiring within VoucherExpiryNotifyHours → push reminder (check Notify_VoucherExpiringSoon)
  - `5_WebApps/ShopERP/Program.cs` — register VoucherExpiryReminderJob hosted service
  - `5_WebApps/ShopERP/Migrations/` — migration add 6 columns to ShopFeatureSettings (cùng migration WS-A)

- **Boundary Rules:**
  - **Domain modifications (2 entity mới + Customer fields THÊM):** `Mission`, `MissionCompletion` entity + Customer thêm `Birthday`, `PWAInstalledAt`, `OtpVerifiedAt`, `FacebookShareCount`, `TikTokShareCount`. Approved as part of feature plan.
  - KHÔNG sửa `LoyaltyRewards` entity (dùng `AddPointsAsync` có sẵn).
  - KHÔNG sửa `Order` entity.
  - KHÔNG sửa `RedemptionCatalogItem` / `RedemptionRecord` / `Voucher` entity (L-B entities — chỉ thêm notification call trong service layer).
  - Multi-tenancy: missions + loyalty formula + notification rules tenant-scoped (mỗi tenant cấu hình riêng).
  - **Customer entity modification:** THÊM fields (Birthday, PWAInstalledAt, etc.) — không sửa field hiện có. Cần migration alter table.
  - **ShopFeatureSettingsEntity modification:** THÊM 10 property (4 formula + 6 notification) — không sửa existing 12 toggles. Cần migration add columns.

## 4. TECHNICAL & REGULATORY CONSTRAINTS

### WS-A — Owner config UI (loyalty formula per-tenant)
- [ ] **ShopFeatureSettingsDto thêm 4 field:** `Loyalty_PointsRate` (decimal, default 0.1 = 10%), `Loyalty_MinPointsPerOrder` (int, default 10), `Loyalty_MaxPointsPerOrder` (int? nullable, default null = ∞), `Loyalty_AwardOnAllOrders` (bool, default true).
- [ ] **ShopFeatureSettingsEntity thêm 4 property** + update `UpdateToggles` method signature (thêm 4 param).
- [ ] **ShopFeatureSettingsService:** update `ToDto`/`FromDto` mapping + `UpdateSettingsAsync` pass 4 new fields + `IsEnabledAsync` switch case for new toggles.
- [ ] **OrderWorkflowService.HandleOrderCompletedAsync:** đổi source từ `IOptions<LoyaltyPointsConfig>` (global) → `IShopFeatureSettingsService.GetSettingsAsync(tenantId)` (per-tenant). Fallback: nếu tenant chưa config (entity null) → dùng `IOptions<LoyaltyPointsConfig>` default. Logic: `var settings = await _shopFeatureSettingsService.GetSettingsAsync(tenantId); var rate = settings.Loyalty_PointsRate > 0 ? settings.Loyalty_PointsRate : _loyaltyPointsConfig.PointsRate;` (tương tự cho min/max/award).
- [ ] **ShopFeatures.razor:** thêm section "Công thức điểm thưởng" — 4 controls: PointsRate (number input decimal, step 0.01, min 0, max 1), MinPointsPerOrder (number input int, min 0), MaxPointsPerOrder (number input int, nullable — để trống = ∞), AwardOnAllOrders (toggle switch).
- [ ] **Migration:** add 4 columns to `ShopFeatureSettings` table (Loyalty_PointsRate REAL, Loyalty_MinPointsPerOrder INTEGER, Loyalty_MaxPointsPerOrder INTEGER NULL, Loyalty_AwardOnAllOrders INTEGER).
- [ ] **Backward compat:** `IOptions<LoyaltyPointsConfig>` vẫn giữ làm global default (appsettings.json) — dùng khi tenant chưa config. Không xóa config hiện có.

### WS-B — Task-based awards (gamification core)
- [ ] **Mission entity:** TenantId, MissionType [PWAInstall/OtpVerify/BirthdayEntry/FacebookShare/TikTokShare/Custom], Title, Description, PointsReward, IsOneTime (bool), DailyCap (nullable int), IsActive, SortOrder, Config (JSON — mission-specific params).
- [ ] **MissionCompletion entity:** TenantId, CustomerId, MissionId, CompletedAt, PointsAwarded, Metadata (JSON — e.g., Facebook post URL, TikTok video URL).
- [ ] **Customer fields mới:** `DateTime? Birthday`, `DateTime? PWAInstalledAt`, `DateTime? OtpVerifiedAt`, `int FacebookShareCount`, `int TikTokShareCount`.
- [ ] **Mission completion flow:** Customer trigger mission → verify condition (e.g., PWA installed, OTP verified, birthday entered, share URL submitted) → check one-time/daily cap → `AddPointsAsync` → tạo MissionCompletion record → check `Notify_MissionCompleted` toggle → push notification (WS-C).
- [ ] **PWA install trigger:** `PWAService.HandleInstallStateChanged(true)` → call MissionService.CompleteMissionAsync(customerId, MissionType.PWAInstall).
- [ ] **OTP verify trigger:** `CustomerIdentityController.VerifyOtp` (line 79-85) → call MissionService.CompleteMissionAsync(customerId, MissionType.OtpVerify).
- [ ] **Birthday entry trigger:** Profile.razor birthday input save → MissionService.CompleteMissionAsync(customerId, MissionType.BirthdayEntry).
- [ ] **Birthday annual bonus (chốt 2026-07-23 — Auto scheduled job):** Scheduled job (daily, HostedService) check customers có birthday today → AddPointsAsync (100 points) + tạo MissionCompletion (MissionType.Custom, Metadata="Birthday bonus {year}"). Auto award, KHÔNG require customer claim. Check `Notify_BirthdayBonus` toggle → push.
- [ ] **Social share (chốt 2026-07-23 — Require share URL):** Khách click share button → JS open Facebook/TikTok share dialog → return share URL → submit to server → verify URL format (Facebook post URL / TikTok video URL pattern) → CompleteMissionAsync.
  - **Facebook share verification:** Require Facebook post URL (pattern `facebook.com/*/posts/*` hoặc `fb.com/*`). KHÔNG verify thật (Facebook KHÔNG có callback API). Filter URL rỗng/sai format.
  - **TikTok share verification:** Require TikTok video URL (pattern `tiktok.com/@*/*` hoặc `tiktok.com/*/video/*`). Tương tự Facebook.
  - **Daily cap:** Max 1 share reward per platform per day per customer (prevent abuse).
- [ ] **Admin UI (chốt 2026-07-23 — Per tenant):** MissionsAdmin.razor — CRUD missions (type, title, points, one-time/daily cap, active toggle). Missions configurable **per tenant** (mỗi tenant cấu hình nhiệm vụ + điểm thưởng riêng).
- [ ] **Customer proof submit form (SC15 NEW):** `/missions` page — Facebook/TikTok share missions hiển thị "Submit share URL" form (text input + submit button). Server validate URL format → reject if invalid → feedback error message. Accept → CompleteMissionAsync + show success.
- [ ] **MissionCompletion history (SC16 NEW):** `/missions` page — section "Nhiệm vụ đã hoàn thành" hiển thị list MissionCompletion (mission title, points awarded, completed date, metadata preview). Separate from loyalty points history in `/my-loyalty`.

### WS-C — Notification rules
- [ ] **ShopFeatureSettingsDto thêm 6 field:** `Notify_MissionCompleted` (bool, default true), `Notify_BirthdayBonus` (bool, default true), `Notify_RedemptionFulfilled` (bool, default true), `Notify_RedemptionCancelled` (bool, default true), `Notify_VoucherExpiringSoon` (bool, default true), `VoucherExpiryNotifyHours` (int, default 24).
- [ ] **ShopFeatureSettingsEntity thêm 6 property** + update `UpdateToggles`.
- [ ] **ShopFeatures.razor:** thêm section "Thông báo điểm thưởng" — 5 toggle switches + 1 number input (VoucherExpiryNotifyHours, min 1, max 168 = 7 days).
- [ ] **PushNotificationService:** thêm 2 method mới:
  - `SendRedemptionFulfilledNotificationAsync(customerId, voucherCode, productName)` — push "Voucher {code} đã được xác nhận — {productName}".
  - `SendVoucherExpiringSoonNotificationAsync(customerId, voucherCode, expiresAt)` — push "Voucher {code} sắp hết hạn lúc {expiresAt}".
- [ ] **RedemptionService.FulfillAsync:** sau mark Voucher.Used + Record.Fulfilled → check `Notify_RedemptionFulfilled` → call `SendRedemptionFulfilledNotificationAsync`.
- [ ] **RedemptionService.CancelAsync:** sau refund points + expire voucher → check `Notify_RedemptionCancelled` → push refund reason.
- [ ] **MissionService.CompleteMissionAsync:** sau AddPointsAsync → check `Notify_MissionCompleted` → call `SendLoyaltyPointsChangedNotificationAsync` với mission-specific reason (vd: "Hoàn thành nhiệm vụ: Cài app +50 điểm").
- [ ] **VoucherExpiryReminderJob (NEW HostedService):** daily run → query vouchers where `ExpiresAt <= DateTime.UtcNow.AddHours(VoucherExpiryNotifyHours)` AND `Status == Active` AND `ExpiresAt > DateTime.UtcNow` → for each → check `Notify_VoucherExpiringSoon` → call `SendVoucherExpiringSoonNotificationAsync`. Tenant-scoped (query per tenant).
- [ ] **Migration:** add 6 columns to `ShopFeatureSettings` (cùng migration WS-A — total 10 new columns).

## 5. SUCCESS CRITERIA (18)
- [ ] SC1: `Mission` entity + migration (ShopERP SQLite).
- [ ] SC2: `MissionCompletion` entity + migration.
- [ ] SC3: Customer entity thêm 5 fields (Birthday, PWAInstalledAt, OtpVerifiedAt, FacebookShareCount, TikTokShareCount) + migration alter table.
- [ ] SC4: `MissionService.CompleteMissionAsync(customerId, missionType)` — verify condition → check cap → AddPointsAsync → tạo MissionCompletion → check notification toggle.
- [ ] SC5: PWA install → auto-complete PWAInstall mission + award points.
- [ ] SC6: OTP verify → auto-complete OtpVerify mission + award points.
- [ ] SC7: Birthday entry → auto-complete BirthdayEntry mission + award points.
- [ ] SC8: Birthday annual bonus — scheduled job award 100 points on birthday + push if toggle on.
- [ ] SC9: Facebook share → submit share URL → verify format → complete FacebookShare mission (daily cap 1) + award points.
- [ ] SC10: TikTok share → submit share URL → verify format → complete TikTokShare mission (daily cap 1) + award points.
- [ ] SC11: KhachLink `Missions.razor` (`/missions`) — danh sách nhiệm vụ + progress + complete button + share URL proof submit form + MissionCompletion history.
- [ ] SC12: Profile.razor — birthday input field + save.
- [ ] SC13: ShopERP `MissionsAdmin.razor` — admin CRUD missions.
- [ ] SC14: `dotnet build VanAn.sln` PASS + `guard-check.ps1` PASS + tests PASS.
- [ ] SC15 (NEW — WS-B): Customer proof submit form — Facebook/TikTok share URL input + server validation + feedback (accept/reject).
- [ ] SC16 (NEW — WS-B): MissionCompletion history in `/missions` — list completed missions (title, points, date, metadata).
- [ ] SC17 (NEW — WS-A): Owner config UI for loyalty formula — `ShopFeatures.razor` section "Công thức điểm thưởng" (4 fields) + `OrderWorkflowService` reads per-tenant config with IOptions fallback + migration 4 columns.
- [ ] SC18 (NEW — WS-C): Notification rules — `ShopFeatures.razor` section "Thông báo điểm thưởng" (5 toggles + expiry hours) + `SendRedemptionFulfilledNotificationAsync` + `SendVoucherExpiringSoonNotificationAsync` + `VoucherExpiryReminderJob` HostedService + `RedemptionService`/`MissionService` notification calls + migration 6 columns.

**Implementation Date:** _TBD_
**Branch:** `main` (prerequisites all COMPLETE 2026-07-24: Phase 5 + L-A + L-B)

## 6. ACTIVE SKILLS (MAX 3)
- `domain-integrity-validation` — 2 entity mới + Customer fields, verify không phá existing
- `accounting-ui-implementation` — admin + customer UI pattern
- `test-system-upgrade` — TDD cho mission completion flow

## 7. AI HEALTH CHECK MATRIX (INITIAL)
- **Evidence Count:** 8 (from audit 2026-07-23)
- **Verified Facts:**
  - Fact 1: KHÔNG có Mission/Quest/Task entity/service/controller nào.
  - Fact 2: `PWAService.ShowInstallPromptAsync` (line 98-124) + `HandleInstallStateChanged` (line 234-239) REAL — nhưng KHÔNG gọi AddPointsAsync.
  - Fact 3: `CustomerIdentityController.VerifyOtp` (line 79-85) REAL — upgrade IdentityLevel nhưng KHÔNG award points.
  - Fact 4: Customer entity KHÔNG có Birthday/DateOfBirth field.
  - Fact 5: `TenantSettings.SocialLinksFb` + `SocialLinksTiktok` có (link page) — nhưng KHÔNG có share reward logic.
  - Fact 6: `LoyaltyRewardsService.AddPointsAsync` REAL — có thể reuse cho mission awards.
  - Fact 7: `ActivateCustomerAsync` (LoyaltyRewardsService:210) đã award 100 points welcome bonus — precedent cho trigger-based awards.
  - Fact 8: KHÔNG có admin UI cho tasks/missions.
- **Assumptions:**
  - A1: Facebook/TikTok share KHÔNG thể verify thật (no callback API) — trust-based verification.
  - A2: Scheduled job cho birthday annual bonus — cần background service (verify HostedService pattern trong codebase).
- **Open Questions:**
  - Q1: Social share verification → **Require share URL** (chốt 2026-07-23 — verify URL format, không verify thật, daily cap 1 per platform).
  - Q2: Birthday annual bonus → **Auto scheduled job** (chốt 2026-07-23 — HostedService daily check, không require claim).
  - Q3: Mission config → **Per tenant** (chốt 2026-07-23 — mỗi tenant cấu hình nhiệm vụ + điểm riêng).
- **Recommended Action:** ANALYZE HostedService pattern → IMPLEMENT (sau Phase 5.6 + L-A).

## 8. REVERSE IMPACT ANALYSIS
| File thay đổi | Reverse impact | Mitigation |
|---|---|---|
| Domain.cs (thêm 2 entity + Customer 5 fields) | THÊM entity + THÊM Customer fields (alter table) | Domain integrity validation + migration |
| PWAService.cs (thêm award on install) | Sửa existing — thêm call sau install success | Phase 5 COMPLETE — không còn conflict |
| Profile.razor (thêm birthday + mission list) | Sửa existing — thêm section | Phase 5 COMPLETE — không còn conflict |
| CustomerIdentityController.cs (thêm award on OTP) | Sửa existing — thêm call sau upgrade | None (Phase 5 không sửa file này) |
| pwa.js (thêm social share functions) | THÊM functions, không sửa existing | None |
| ShopFeatureSettingsEntity.cs (thêm 10 property) | Sửa existing — THÊM property + update UpdateToggles signature | Backward compat: UpdateToggles thêm param có default value — existing callers không break |
| ShopFeatureSettingsService.cs (thêm mapping) | Sửa existing — update ToDto/FromDto + UpdateSettingsAsync | Backward compat: DTO record — new field có default value |
| ShopFeatures.razor (thêm 2 section) | Sửa existing — THÊM 2 section (formula + notification) | None — additive, không sửa existing toggles |
| OrderWorkflowService.cs (đổi loyalty formula source) | Sửa existing — đổi từ IOptions → IShopFeatureSettingsService | Backward compat: IOptions fallback khi tenant chưa config. Test existing loyalty awards vẫn pass. |
| RedemptionService.cs (thêm notification call) | Sửa existing L-B — thêm push sau FulfillAsync/CancelAsync | None — additive, push fire-and-forget, không block fulfillment |
| PushNotificationService.cs (thêm 2 method) | Sửa existing — THÊM method | None — additive |
| New files (MissionService, controllers, pages, VoucherExpiryReminderJob) | New, no impact existing | Isolated |

## 9. TDD & E2E TESTING STRATEGY
- **Unit test (WS-B):** MissionService.CompleteMissionAsync — first completion → award points; one-time mission second completion → reject; daily cap exceeded → reject.
- **Unit test (WS-B):** PWA install trigger → CompleteMissionAsync(PWAInstall) → points awarded.
- **Unit test (WS-B):** OTP verify trigger → CompleteMissionAsync(OtpVerify) → points awarded.
- **Unit test (WS-B):** Birthday entry → CompleteMissionAsync(BirthdayEntry) → points awarded + Customer.Birthday updated.
- **Unit test (WS-B):** Birthday annual bonus — scheduled job → AddPointsAsync(100) on birthday.
- **Unit test (WS-B):** Facebook/TikTok share → submit URL → format validation (valid URL accept, invalid reject) → CompleteMissionAsync → daily cap enforcement.
- **Unit test (WS-B):** MissionCompletion history — query by customer returns list with title/points/date/metadata.
- **Unit test (WS-A):** OrderWorkflowService uses per-tenant formula — tenant with custom PointsRate=0.15 → award 15% (not default 10%). Tenant with no config → fallback to IOptions default.
- **Unit test (WS-A):** ShopFeatureSettingsService read/write 4 new loyalty formula fields round-trip.
- **Unit test (WS-C):** RedemptionService.FulfillAsync → check Notify_RedemptionFulfilled=true → push called. Toggle=false → push NOT called.
- **Unit test (WS-C):** VoucherExpiryReminderJob — voucher expiring in 24h + toggle on → push. Voucher expiring in 48h + NotifyHours=24 → NOT pushed yet. Voucher already expired → NOT pushed.
- **Integration test (WS-B):** Full mission flow — admin create mission → customer complete → points awarded → MissionCompletion record created → push notification sent (if toggle on).
- **Integration test (WS-A):** Owner change formula via ShopFeatures.razor → next order completes → points awarded using new formula (per-tenant).

## 10. JIT PLANNING + PURE EXECUTION
| Session | JIT Planning | Pure Execution |
|---|---|---|
| S1 | ANALYZE HostedService pattern + ShopFeatureSettings extension pattern | WS-A: ShopFeatureSettingsDto + Entity + Service + migration (4 loyalty formula columns) + OrderWorkflowService per-tenant formula + ShopFeatures.razor section + unit tests |
| S2 | WS-B domain design | Domain entities (Mission, MissionCompletion) + Customer 5 fields + EF configs + migration (2 tables + alter Customer) |
| S3 | WS-B service | MissionService + IMissionRepository + MissionRepository + unit tests |
| S4 | WS-B triggers | PWA install + OTP verify + birthday entry triggers + unit tests |
| S5 | WS-B social share | Facebook/TikTok share URL submit + format validation + daily cap + pwa.js + unit tests |
| S6 | WS-B birthday job + WS-C notification | Birthday annual bonus scheduled job + WS-C: ShopFeatureSettingsDto 6 notification fields + PushNotificationService 2 new methods + RedemptionService/MissionService notification calls + unit tests |
| S7 | WS-C expiry job | VoucherExpiryReminderJob HostedService + unit tests |
| S8 | WS-B/C controllers | ShopERP + Gateway MissionsController + integration tests |
| S9 | WS-B customer UI | KhachLink Missions.razor (list + progress + complete + proof submit + history) + Profile.razor birthday input + browser test |
| S10 | WS-B/C admin UI | ShopERP MissionsAdmin.razor + ShopFeatures.razor notification section |
| S11 | Full test suite + build + guard-check + RV | Test + RV report |

## 12. ESTIMATED EFFORT
- 11 sessions (was 9 — added 2 for WS-A config UI + WS-C notification rules). **NO BLOCKER** (Q1-Q3 đã chốt 2026-07-23, prerequisites all COMPLETE 2026-07-24).
- **Prerequisite:** Phase 5 + Loyalty Phase A + Loyalty Phase B COMPLETE (all done 2026-07-24).
- **Risk:** Social share verification require URL — vẫn có thể bị abuse (user submit URL fake). Mitigation: daily cap 1 per platform + admin can disable mission + URL format validation.
- **Risk (WS-A):** OrderWorkflowService formula change — existing loyalty award tests may break if they assert hardcoded 10%. Mitigation: IOptions fallback preserves default behavior when tenant has no config. Update tests to use per-tenant config mock.
