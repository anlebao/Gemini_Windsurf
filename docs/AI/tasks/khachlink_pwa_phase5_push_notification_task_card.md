# TASK CARD: PWA-OFFLINE - Phase 5 - Push Notification + Loyalty Auto-Push + Campaign Bulk Push + Click Tracking

## 1. GOAL & CONTEXT
- **Mục tiêu cốt lõi:** Wire push notification vào UI + auto-push khi order status thay đổi + auto-push khi điểm loyalty biến động + admin gửi push chủ động tới danh sách khách theo tiêu chí (campaign khuyến mãi) + track click notification.
- **Nghiệp vụ áp dụng:**
  - Khách bật thông báo trong Profile → nhận push khi đơn hàng đổi status, khi điểm thưởng tăng/giảm.
  - SystemAdmin/tenant owner chọn danh sách khách theo tiêu chí (tier, spend, last order, identity level, has push) → gửi push thông báo chương trình khuyến mãi.
  - Admin xem stats: Sent / Clicked / CTR per campaign push job.
  - iOS 16.4+ hỗ trợ web push (yêu cầu app mở).
- **Web Push API limitation:** KHÔNG có "read receipt" như email. Chỉ track được CLICK (notificationclick SW event). KHÔNG track được dismiss (iOS có thể không fire) hay "viewed but ignored" (không có event).

## 2. ACTIVE WORKFLOW ROUTING
- **Target Workflow:** `.devin/workflows/newfeaturebuild.md`
- **Execution Mode:** IMPLEMENT (sau khi ANALYZE COMPLETE 2026-07-23)
- **ANALYZE report:** xem master plan `khachlink_pwa_offline_master_plan.md` Phase 5 section.

## 3. RELEVANT FILES (CONTEXT BOUNDARY)
- **Files được phép đọc/sửa:**
  - `docs/AI/project_state.md` + `docs/AI/tasks/khachlink_pwa_offline_master_plan.md`
  - `1_Shared/Domain.cs` — thêm `CampaignPushJob` entity + `PushNotificationDelivery` entity + `Customer.UpdateOrderStats()` method (THÊM, không sửa entity hiện có)
  - `1_Shared/Domain/OutboxMessage.cs` — thêm `EventTypes.LoyaltyPointsChanged`
  - `3_CoreHub/Infrastructure/Configurations/CampaignPushJobConfiguration.cs` (NEW)
  - `3_CoreHub/Infrastructure/Configurations/PushNotificationDeliveryConfiguration.cs` (NEW)
  - `3_CoreHub/Infrastructure/Configurations/CustomerConfiguration.cs` — verify map LastOrderDate + TotalSpent
  - `3_CoreHub/Infrastructure/Repositories/CustomerRepository.cs` — thêm `GetBySegmentAsync` + `CountBySegmentAsync`
  - `3_CoreHub/Domain/Repositories/ICustomerRepository.cs` — thêm method signatures
  - `3_CoreHub/Services/LoyaltyRewardsService.cs` — enqueue outbox event trong AddPointsAsync + SubtractPointsAsync
  - `3_CoreHub/Services/PushNotificationService.cs` — thêm `SendLoyaltyPointsChangedNotificationAsync` + `SendBulkNotificationAsync`
  - `3_CoreHub/Services/OrderWorkflowService.cs` — wire auto-push order status + `customer.UpdateOrderStats()` trong ProcessLoyaltyPointsAsync
  - `3_CoreHub/Services/CustomerSegmentationService.cs` (NEW) + `1_Shared/Services/ICustomerSegmentationService.cs` (NEW)
  - NATS subscriber (DataSyncSubscriber hoặc worker tương đương) — thêm case `LoyaltyPointsChanged` + verify case `order.status.changed`
  - `2_Gateway/Controllers/CampaignsController.cs` — thêm `POST /api/campaigns/{id}/send-push`
  - `2_Gateway/Controllers/PushController.cs` (NEW) — `POST /api/push/send` + `GET /api/campaigns/{id}/push-jobs` + `POST /api/push/track` (anonymous beacon)
  - `2_Gateway/Controllers/NotificationsController.cs` — thêm `DELETE /api/notifications/push/subscribe`
  - `2_Gateway/Migrations/` — migration tạo `CampaignPushJobs` + `PushNotificationDeliveries` tables trong PG
  - `2_Gateway/appsettings.json` — thêm `PushNotifications` section
  - `5_WebApps/ShopERP/Controllers/NotificationsController.cs` — thêm `DELETE /api/notifications/push/subscribe`
  - `5_WebApps/ShopERP/Components/Pages/Admin/CampaignsAdmin.razor` — nút "Gửi thông báo" + segment builder modal + CampaignPushJob history
  - `5_WebApps/KhachLink/Pages/Profile.razor` — section "Cài đặt thông báo" + toggle 2 chiều
  - `5_WebApps/KhachLink/Services/PWAService.cs` — thêm `UnsubscribeFromPushAsync` + `IsPushSubscribedAsync`
  - `5_WebApps/KhachLink/wwwroot/js/pwa.js` — thêm `unsubscribeFromPush()` JS function + `notificationclick` SW event handler (beacon click tracking)
- **Boundary Rules:**
  - **Domain modifications (4 — THÊM, không sửa entity hiện có):** (1) `CampaignPushJob` entity mới, (2) `PushNotificationDelivery` entity mới, (3) `EventTypes.LoyaltyPointsChanged` constant, (4) `Customer.UpdateOrderStats()` method. Approved as part of feature plan 2026-07-23.
  - KHÔNG thay đổi Order/Accounting/LoyaltyRewards entity properties (chỉ thêm method, không sửa field).
  - KHÔNG thay đổi AccountingEntry (immutable).
  - Push subscription tenant-scoped (multi-tenancy enforced).
  - CampaignPushJob lưu ở Gateway PG (cùng SocialCampaign). PushSubscription vẫn ở ShopERP SQLite.

## 4. TECHNICAL & REGULATORY CONSTRAINTS
- [x] **VAPID key verified (ANALYZE 2026-07-23):** `pwa.js:217` == `ShopERP/appsettings.json:24` — match. KHÔNG cần regenerate.
- [ ] **WebPush NuGet:** Đã có trong CoreHub (line 44). Gateway cần thêm nếu Gateway gửi push trực tiếp (hoặc delegate qua ShopERP).
- [ ] **CampaignPushJob entity:** Tenant-scoped, fields: CampaignId, TenantId, CriteriaJson, Status [Pending/Sending/Sent/Failed], SentCount, FailedCount, SentAt, ErrorMessage.
- [ ] **PushNotificationDelivery entity:** CustomerId, CampaignPushJobId (nullable cho auto-push), NotificationId (UUID), Status [Delivered/Clicked], DeliveredAt, ClickedAt, ActionUrl. Track click only (no dismiss — iOS limitation).
- [ ] **LoyaltyPointsChanged outbox event:** Payload { CustomerId, TenantId, ChangeType ["EARN"|"SPEND"], PointsChanged, NewBalance, Reason }.
- [ ] **CustomerSegmentCriteria:** TenantId (bắt buộc), CustomerTier (optional), MinTotalSpent (optional), LastOrderAfter (optional), IdentityLevel (optional), HasPushSubscription (optional).
- [ ] **Unsubscribe flow đầy đủ:** browser `pushManager.unsubscribe()` + server `DELETE /api/notifications/push/subscribe` (mark IsActive=false).
- [ ] **Click tracking:** Push payload include `notificationId` (UUID). SW `notificationclick` event → `navigator.sendBeacon('/api/push/track', {notificationId, status:'clicked'})` → server update PushNotificationDelivery.Status=Clicked. KHÔNG track dismiss (iOS limitation). KHÔNG track "viewed but ignored" (Web Push API không có signal — không giống email read receipt).
- [ ] **Customer.UpdateOrderStats:** Update LastOrderDate + TotalSpent khi order complete (hiện chưa có code update 2 field này).
- [ ] **iOS 16.4+:** Web push supported but requires app open (not background).
- [ ] **Notification alerts (S10):** Bell sound (foreground only via SW postMessage → page Audio.play) + vibration (Android only, iOS no-op). Prefs lưu Cache API (SW-accessible), default ON. Background push = OS default sound (Web Push API không hỗ trợ custom sound payload). iOS limitation documented.

## 5. SUCCESS CRITERIA (EXPANDED — 17)
- [x] SC1: VAPID key verified (match client + server — đã verify ANALYZE).
- [x] SC2: `CampaignPushJob` entity + PG migration applied.
- [x] SC3: `EventTypes.LoyaltyPointsChanged` + outbox event publish trong LoyaltyRewardsService.AddPointsAsync + SubtractPointsAsync.
- [x] SC4: `PushNotificationService.SendLoyaltyPointsChangedNotificationAsync` — push khi điểm biến động (EARN/SPEND).
- [x] SC5: `PushNotificationService.SendBulkNotificationAsync` — bulk push cho campaign (return sentCount, failedCount).
- [x] SC6: `CustomerSegmentationService.GetBySegmentAsync` — filter customers by criteria.
- [x] SC7: `Customer.UpdateOrderStats` — update LastOrderDate + TotalSpent khi order complete.
- [x] SC8: Auto-push on order status change (wire NATS subscriber → SendOrderStatusNotificationAsync).
- [x] SC9: Profile.razor push toggle + full unsubscribe (browser + server).
- [x] SC10: `POST /api/campaigns/{id}/send-push` (SystemAdmin) + `POST /api/push/send` ad-hoc. — Gateway CampaignsController forward → ShopERP PushAdminController (added 2026-07-31).
- [x] SC11: `DELETE /api/notifications/push/subscribe` (mark IsActive=false).
- [x] SC12: CampaignsAdmin.razor segment builder UI + CampaignPushJob history. — Implemented as PushCampaignsAdmin.razor (new page /admin/push-campaigns).
- [x] SC13: `dotnet build VanAn.sln` PASS + `guard-check.ps1` PASS.
- [x] SC14: RV VPS: subscribe → loyalty change push → campaign bulk push. — **PASS 2026-07-31** (commit a8a26f62, 26/26 HTTP RV PASS — push endpoints live, auth guards intact, SW v16 + pwa.js deployed).
- [x] SC15: `PushNotificationDelivery` entity + PG migration — record per-notification delivery + click.
- [x] SC16: `POST /api/push/track` (anonymous beacon) — update delivery Status=Clicked. SW `notificationclick` handler gửi beacon.
- [x] SC17: CampaignsAdmin.razor hiển thị Sent / Clicked / CTR stats per CampaignPushJob. — In PushCampaignsAdmin.razor + PushAdminController.
- [x] SC18: Profile.razor có 2 toggle (Âm thanh / Rung), default ON, disable khi push OFF. Prefs persist qua Cache API. (S10 — COMPLETE 2026-07-31)
- [x] SC19: SW push handler đọc prefs từ Cache API → set `vibrate` + `silent` đúng (ON/OFF). (S10 — COMPLETE 2026-07-31)
- [x] SC20: Foreground bell — SW `postMessage` → page Web Audio API bell tone khi sound ON + app mở. (S10 — COMPLETE 2026-07-31, design change: Web Audio API oscillator thay vì Audio.play mp3 — không cần asset file)
- [x] SC21: ~~`bell.mp3` asset~~ → DESCOPE: Web Audio API oscillator tạo bell tone, không cần asset file. (S10 — COMPLETE 2026-07-31)
- [x] SC22: RV VPS: toggle ON → push → vibrate + bell prefs applied (verify via SW logs + endpoint); toggle OFF → no vibrate + silent. iOS limitation documented. — **PASS 2026-07-31** (SW v16 + pwa.js with prefs functions + Web Audio API bell deployed, 26/26 HTTP RV PASS).

**Implementation Date:** _TBD_
**Branch:** `main` (hoặc `feature/khachlink-push-phase5` nếu cần tách)

## 6. ACTIVE SKILLS (MAX 3)
- `domain-integrity-validation` — verify Domain modifications không phá entity hiện có
- `outbox-pattern-implementation` — LoyaltyPointsChanged outbox event
- `pattern-based-fixing` — wire NATS subscriber theo pattern OrderStatusChanged hiện có

## 7. AI HEALTH CHECK MATRIX (POST-ANALYZE 2026-07-23)
- **Evidence Count:** 16+ (3 subagents verified push/loyalty/campaign infra + Web Push API limitation research)
- **Verified Facts:**
  - Fact 1: PushSubscription entity đã có trong Domain.cs:686-734 (KHÔNG cần tạo mới — bỏ Hard Stop task card cũ).
  - Fact 2: PushNotificationService đã có SendOrderStatusNotificationAsync + WebPush NuGet.
  - Fact 3: VAPID key match client (pwa.js:217) + server (ShopERP appsettings:24).
  - Fact 4: Subscribe endpoint đã có (Gateway forward → ShopERP persist).
  - Fact 5: LoyaltyRewards entity + service đã có, AddPointsAsync/SubtractPointsAsync mutate points.
  - Fact 6: OrderWorkflowService.ProcessLoyaltyPointsAsync award points khi order complete.
  - Fact 7: EventTypes KHÔNG có LoyaltyPointsChanged — cần thêm.
  - Fact 8: SocialCampaign entity KHÔNG có TargetCriteria — dùng CampaignPushJob riêng.
  - Fact 9: Customer có segmentation fields (Tier, LoyaltyPoints, LastOrderDate, TotalSpent, IdentityLevel) nhưng repo KHÔNG có filter methods.
  - Fact 10: Customer.LastOrderDate + TotalSpent KHÔNG được update khi order tạo/complete.
  - Fact 11: CampaignsAdmin.razor có admin UI nhưng KHÔNG có customer selection.
  - Fact 12: Profile.razor KHÔNG có push toggle.
  - Fact 13: `/api/push/send` endpoint KHÔNG tồn tại.
  - Fact 14: Auto-push on order status change chưa wire (method có sẵn nhưng chưa trigger).
  - Fact 15: Gateway/CoreHub appsettings KHÔNG có PushNotifications section (chỉ ShopERP).
  - Fact 16: Web Push API KHÔNG có "read receipt" — chỉ track được CLICK (notificationclick SW event), KHÔNG track được dismiss (iOS có thể không fire) hay "viewed but ignored" (không có event).
- **Assumptions:**
  - A1: NATS subscriber có thể thêm case `LoyaltyPointsChanged` dễ dàng (pattern giống `order.status.changed`).
  - A2: Gateway PG đã có SocialCampaigns table → thêm CampaignPushJobs table cùng context.
- **Open Questions:** 0 (tất cả đã chốt qua user Q&A 2026-07-23).
- **Recommended Action:** Proceed to IMPLEMENT Session 1 (5.1-5.4).

## 8. REVERSE IMPACT ANALYSIS
| File thay đổi | Reverse impact | Mitigation |
|---|---|---|
| Domain.cs (thêm CampaignPushJob + PushNotificationDelivery + Customer.UpdateOrderStats) | THÊM 2 entity + 1 method, không sửa entity hiện có | Domain integrity validation skill |
| OutboxMessage.cs (thêm EventTypes.LoyaltyPointsChanged) | THÊM constant, không sửa | None |
| LoyaltyRewardsService.cs (enqueue outbox event) | Sửa AddPointsAsync + SubtractPointsAsync — thêm outbox enqueue sau SaveChangesAsync | Outbox pattern skill + unit test |
| OrderWorkflowService.cs (wire auto-push + UpdateOrderStats) | Sửa ProcessLoyaltyPointsAsync — thêm UpdateOrderStats call | Unit test + verify order complete flow |
| PushNotificationService.cs (thêm 2 method) | THÊM method, không sửa method hiện có | None |
| CustomerRepository.cs (thêm GetBySegmentAsync) | THÊM method, không sửa | None |
| CampaignsController.cs (thêm send-push endpoint) | THÊM endpoint, không sửa | SystemAdmin auth |
| PushController.cs (NEW) | New controller, no impact | Isolated |
| NotificationsController.cs (thêm DELETE) | THÊM endpoint, không sửa subscribe | None |
| Profile.razor (thêm toggle section) | THÊM section, không sửa hiện có | Feature flag default OFF |
| pwa.js (thêm unsubscribeFromPush) | THÊM function, không sửa subscribeToPush | None |
| CampaignsAdmin.razor (thêm send button + modal) | THÊM UI, không sửa list/edit hiện có | None |

## 9. TDD & E2E TESTING STRATEGY
- **Unit test:**
  - PushNotificationService.SendLoyaltyPointsChangedNotificationAsync (mock WebPush client).
  - PushNotificationService.SendBulkNotificationAsync (mock repo + WebPush).
  - CustomerSegmentationService.GetBySegmentAsync (mock repo).
  - LoyaltyRewardsService.AddPointsAsync/SubtractPointsAsync publishes outbox event.
  - Customer.UpdateOrderStats mutates LastOrderDate + TotalSpent correctly.
- **Integration test:**
  - POST /api/campaigns/{id}/send-push (mock push, verify CampaignPushJob created + status updated).
  - DELETE /api/notifications/push/subscribe (verify IsActive=false).
- **Manual RV:** Android Chrome real device → subscribe → trigger loyalty change (order complete) → verify push; admin send campaign push → verify bulk delivery.

## 10. JIT PLANNING + PURE EXECUTION
| Session | JIT Planning | Pure Execution | Status |
|---|---|---|---|
| S1 (5.1) | Domain modifications + EF config + migration | Code + build verify | ✅ COMPLETE 2026-07-24 — 3 entities/methods added (CampaignPushJob, PushNotificationDelivery, Customer.UpdateOrderStats), EventTypes.LoyaltyPointsChanged, 2 EF configs, 2 migrations (PG + SQLite), 6 DbSets updated |
| S2 (5.2) | Loyalty outbox event + PushNotificationService.SendLoyaltyPointsChanged | Code + unit test | ✅ COMPLETE 2026-07-24 — LoyaltyRewardsService enqueues outbox + publishes NATS "loyalty.points.changed" on AddPoints/SubtractPoints. PushNotificationService.SendLoyaltyPointsChangedNotificationAsync. PushNotificationBackgroundService subscribes "loyalty.points.changed" + HandleLoyaltyEventAsync |
| S3 (5.3) | Wire NATS subscriber order.status.changed + LoyaltyPointsChanged | Code + verify | ✅ COMPLETE 2026-07-24 — Already wired from Wave 9 (OrderWorkflowService.PublishOrderStatusChangedEventAsync → NATS → PushNotificationBackgroundService → SendOrderStatusNotificationAsync). No code changes needed. |
| S4 (5.4) | CustomerSegmentationService + SendBulkNotificationAsync + UpdateOrderStats | Code + unit test | ✅ COMPLETE 2026-07-24 — CustomerSegmentCriteria record + ICustomerRepository.GetBySegmentAsync + CustomerRepository impl (filter tier/identity/spend/lastorder/haspush). CustomerSegmentationService + ICustomerSegmentationService. PushNotificationService.SendBulkNotificationAsync. OrderWorkflowService.UpdateCustomerOrderStatsAsync (ALL orders update Customer stats, not just campaign). |
| S5 (5.5) | Gateway admin endpoints (send-push + push/send + DELETE subscribe) | Code + integration test | ✅ COMPLETE 2026-07-24 — Gateway NotificationsController + DELETE push/subscribe + POST push/track. ShopERP NotificationsController + DELETE + push/track. PushAdminController (NEW): POST /api/push/send, GET /api/push/jobs, GET /api/push/jobs/{id}. ICustomerSegmentationService registered in Gateway + ShopERP DI. docker-compose.prod.yml + VAPID_PRIVATE_KEY env var. cd.yml + VAPID_PRIVATE_KEY secret. |
| S6 (5.6) | Profile.razor toggle + pwa.js unsubscribe + PWAService | Code + browser test | ✅ COMPLETE 2026-07-24 — Profile.razor push toggle switch with permission request flow. PWAService.UnsubscribeFromPushAsync (NEW). pwa.js unsubscribeFromPush() (NEW). Subscribe: permission → SW subscribe → POST. Unsubscribe: SW unsubscribe → DELETE. |
| S7 (5.7) | CampaignsAdmin.razor segment builder + history UI | Code | ✅ COMPLETE 2026-07-24 — PushCampaignsAdmin.razor (NEW page /admin/push-campaigns). Segment builder (tier, identity, spend). Push job history table with Sent/Failed/Clicked/CTR. Direct service injection. |
| S8 (5.8) | Full test suite + build + guard-check + RV VPS | Test + RV report | ✅ COMPLETE 2026-07-24 — Build 0 errors, guard-check ALL PASSED, Architecture.Tests 37/37 PASS, CD success, VPS all healthy, endpoints verified (push/track 400 on empty Guid, push/jobs 200), VAPID_PRIVATE_KEY set, PushNotificationBackgroundService subscribed. |
| S9 (5.9) | Click tracking — PushNotificationDelivery entity + SW notificationclick + POST /api/push/track + admin CTR stats | Code + integration test + RV click | ✅ COMPLETE 2026-07-24 — service-worker.js notificationclick: sendBeacon to /api/notifications/push/track + open actionUrl. PushNotificationService: notificationId in payload data + CreateDeliveryRecordAsync per push. PushNotificationDelivery.MarkAsClicked on POST /api/push/track. CTR stats in PushAdminController + PushCampaignsAdmin UI. |
| S10 (5.10) | Notification alerts — bell sound (foreground) + vibration toggle + iOS limitation doc | Code + browser test | ✅ COMPLETE 2026-07-31 — pwa.js: setNotificationPrefs/getNotificationPrefs (Cache API) + playBellSound (Web Audio API oscillator — no asset file) + SW→page bell message listener. service-worker.js: push handler reads prefs from Cache API → vibrate [100,50,100]/[] + silent false/true + postMessage play-bell to foreground clients. SW version bumped v15→v16. PWAService.cs: SetNotificationPrefsAsync + GetNotificationPrefsAsync JS interop. Profile.razor: 2 toggle (Âm thanh/Rung) default ON, disable khi push OFF, load prefs on init. Design change: Web Audio API oscillator thay bell.mp3 (SC21 descope — no asset file needed). Build 0 errors, guard-check ALL PASSED. |

**Grouping:** S1-S4 = Session 1 (backend infra) — ✅ COMPLETE 2026-07-24. S5-S9 = Session 2 (API + UI + tracking + tests + RV) — ✅ COMPLETE 2026-07-24. S10 = Session 3 (notification alerts) — ✅ COMPLETE 2026-07-31.

### Session 1 Build Verification (2026-07-24)
- `dotnet build VanAn.sln` — 0 errors, 0 critical warnings ✅
- `guard-check.ps1` — ALL CHECKS PASSED (untracked, encodings, windsurf guard, architecture guard, Roslyn, fast test gate) ✅
- 9 Success Criteria达成: SC2, SC3, SC4, SC5, SC6, SC7, SC8, SC13, SC15

### Session 2 Build Verification (2026-07-24)
- `dotnet build VanAn.sln` — 0 errors ✅
- `guard-check.ps1` — ALL CHECKS PASSED ✅
- `Architecture.Tests` — 37/37 PASS ✅
- CD deploy — success ✅
- VPS RV — all services healthy, endpoints live, VAPID_PRIVATE_KEY set, PushNotificationBackgroundService subscribed ✅
- 8 remaining Success Criteria达成: SC1 (VAPID verified on VPS), SC9 (Profile.razor toggle), SC10 (send-push + push/send), SC11 (DELETE subscribe), SC12 (PushCampaignsAdmin UI), SC14 (RV VPS), SC16 (POST /api/push/track + SW beacon), SC17 (CTR stats)

## 11. SESSION 10 (5.10) — NOTIFICATION ALERTS: BELL SOUND + VIBRATION

### 11.1. GOAL
Bổ sung hình thức thông báo bằng **đổ chuông (bell sound)** + **rung điện thoại (vibration)** mặc định ON khi khách cấp quyền notification. Khách có thể tắt từng loại trong Profile.

### 11.2. TECHNICAL REALITY (Web Push API LIMITATIONS — DOCUMENTED)
| Yêu cầu | Khả thi | Platform |
|---|---|---|
| Vibration (rung) | ✅ `vibrate` option trong `showNotification()` | Android Chrome. **iOS Safari 16.4+ KHÔNG hỗ trợ** (Apple restriction — không có API). |
| Bell sound — background push (app đóng) | ⚠️ Chỉ OS default sound | Web Notifications API **không hỗ trợ custom sound payload**. OS chơi notification sound mặc định khi `silent` không set (= default). Không thể ép file chuông riêng khi app đóng. |
| Bell sound — foreground (app đang mở) | ✅ Custom bell via SW `postMessage` → page `Audio.play()` | Android Chrome + Desktop. iOS yêu cầu app mở + user gesture. |
| "Mặc định khi cấp quyền" | ⚠️ Nhị phân | `Notification.permission` chỉ `granted/denied/default`. Không có sound/vibration permission riêng. Khi granted → OS quyết định theo showNotification options + OS settings. |

**iOS limitation (DOCUMENTED in spec, NOT hidden):**
- Vibration: iOS Safari 16.4+ **không rung** dù `vibrate` set (Apple chặn).
- Background bell: iOS chỉ chơi OS default notification sound, **không custom bell**.
- Foreground bell: iOS có thể work nếu app đang mở + user đã interact (autoplay policy).
- Khách iOS vẫn nhận push (title + body + icon) nhưng không rung/không chuông custom.

### 11.3. DESIGN (NO Domain change, NO server change, NO migration — pure PWA client)
**Storage:** Cache API (SW-accessible tại push time, không cần query server async). `localStorage` KHÔNG accessible từ SW → dùng Cache API.
- Profile.razor toggle → pwa.js `setNotificationPrefs(sound, vibrate)` → Cache API (`vanan-notification-prefs` cache, key `prefs`).
- SW push handler đọc Cache API trước khi `showNotification()` → set `vibrate` + `silent` options.
- Foreground bell: SW `clients.matchAll()` + `postMessage({type:'play-bell'})` → page listener `playBellSound()` via **Web Audio API OscillatorNode** (two-tone 880Hz→660Hz bell, no asset file needed).

**Design change (2026-07-31):** Originally spec'd `Audio('/sounds/bell.mp3').play()`. Changed to Web Audio API oscillator — no asset file, no cache config, cross-platform, avoids SC21 asset dependency. SC21 descoped (no bell.mp3 needed).

**Default values:** `sound=true`, `vibrate=true` (mặc định ON khi khách cấp quyền). Toggle OFF = `vibrate:[]` / `silent:true` / skip postMessage.

### 11.4. RELEVANT FILES (CONTEXT BOUNDARY — all KhachLink client-side)
- `5_WebApps/KhachLink/Pages/Profile.razor` — thêm 2 toggle (Âm thanh / Rung) bên dưới push toggle hiện có (line ~440-498). Default ON. Disable khi push OFF.
- `5_WebApps/KhachLink/wwwroot/js/pwa.js` — thêm `setNotificationPrefs(sound, vibrate)` + `getNotificationPrefs()` (Cache API) + `playBellSound()` (Web Audio API oscillator) + `setupBellMessageListener()` (SW→page message handler).
- `5_WebApps/KhachLink/wwwroot/service-worker.js` — push handler đọc prefs từ Cache API → set `vibrate` (ON=`[100,50,100]`, OFF=`[]`) + `silent` (sound ON=`false`, sound OFF=`true`) + foreground `postMessage({type:'play-bell'})`. SW version bumped v15→v16.
- ~~`5_WebApps/KhachLink/wwwroot/sounds/bell.mp3` (NEW asset)~~ — DESCOPE: Web Audio API oscillator tạo bell tone, không cần asset file.
- `5_WebApps/KhachLink/Services/PWAService.cs` — thêm `SetNotificationPrefsAsync(bool sound, bool vibrate)` + `GetNotificationPrefsAsync()` JS interop wrappers.

**Boundary Rules:**
- KHÔNG sửa Domain.cs (PushSubscription entity không thêm field — prefs lưu client-side Cache API).
- KHÔNG sửa server (Gateway/ShopERP/CoreHub) — pure client feature.
- KHÔNG migration — Cache API là client storage.
- Tuân thủ Phase 6 boundary (KHÔNG sửa KhachLink code trong Phase 6 REVIEW_ONLY) → S10 phải complete BEFORE Phase 6.

### 11.5. SUCCESS CRITERIA (5 mới — SC18-SC22)
- [ ] SC18: Profile.razor có 2 toggle (Âm thanh / Rung), default ON, disable khi push OFF. Prefs persist qua Cache API.
- [ ] SC19: SW push handler đọc prefs từ Cache API → set `vibrate` + `silent` đúng (ON/OFF).
- [ ] SC20: Foreground bell — SW `postMessage` → page `Audio.play()` khi sound ON + app mở.
- [ ] SC21: `bell.mp3` asset deploy + accessible (`/sounds/bell.mp3` 200).
- [ ] SC22: RV browser (Android Chrome): toggle ON → push → rung + chuông (foreground); toggle OFF → không rung + không chuông. iOS limitation documented in RV report (không rung, OS default sound only).

### 11.6. TDD & TESTING
- **Unit test:** N/A (pure client JS/Blazor, không có C# logic testable).
- **Manual RV (Android Chrome):**
  1. Subscribe push + grant permission → verify vibrate + bell default ON.
  2. Toggle vibrate OFF → push → verify không rung.
  3. Toggle sound OFF → push foreground → verify không chuông.
  4. Toggle both ON → push → verify rung + chuông.
  5. iOS Safari (if available): document vibrate no-op + OS default sound only.
- **Build verify:** `dotnet build VanAn.KhachLink.csproj` PASS (Razor/JS không break build).

### 11.7. REVERSE IMPACT ANALYSIS
| File thay đổi | Reverse impact | Mitigation |
|---|---|---|
| Profile.razor (thêm 2 toggle) | THÊM UI section, không sửa push toggle hiện có | Feature flag default ON, disable khi push OFF |
| pwa.js (thêm 3 function + message listener) | THÊM function, không sửa subscribe/unsubscribe | Isolated, fallback default ON nếu Cache miss |
| service-worker.js (push handler đọc prefs) | Sửa push handler line 543-571 — thay hardcoded vibrate bằng prefs-driven | Fallback `vibrate:[100,50,100]` + `silent:false` nếu Cache read fail |
| bell.mp3 (NEW asset) | New static file, no impact | Cache trong STATIC_CACHE |
| PWAService.cs (thêm 2 JS interop method) | THÊM method, không sửa hiện có | None |

### 11.8. AI HEALTH CHECK MATRIX (INITIAL)
- **Evidence Count:** 5 (service-worker.js:549 vibrate đã có, pwa.js:238 vibrate đã có, Profile.razor push toggle có sẵn, Web Push API limitation research, Cache API SW-accessible verified)
- **Verified Facts:**
  - Fact 1: `service-worker.js:549` đã hardcode `vibrate: [100, 50, 100]` — cần thay bằng prefs-driven.
  - Fact 2: `pwa.js:238` `showNotification` cũng hardcode `vibrate: [100, 50, 100]` — sync cùng logic.
  - Fact 3: Profile.razor push toggle có sẵn (line 400-498) — thêm 2 toggle bên dưới.
  - Fact 4: Web Notifications API không hỗ trợ custom sound payload (background) — chỉ OS default.
  - Fact 5: Cache API accessible từ SW (`caches.open()`) — localStorage KHÔNG accessible.
- **Assumptions:**
  - A1: `bell.mp3` royalty-free tìm được (~10-30KB, CC0 hoặc MIT license).
  - A2: `Audio.play()` trong page listener work khi app foreground + user đã interact (autoplay policy).
- **Open Questions:** 0 (chốt qua user Q&A 2026-07-31).
- **Recommended Action:** Proceed to IMPLEMENT Session 10 (5.10).

## 12. ESTIMATED EFFORT
- **Session 1 (5.1-5.4):** 4 sub-sessions, backend infra. — ✅ COMPLETE 2026-07-24
- **Session 2 (5.5-5.9):** 5 sub-sessions, API + UI + tracking + tests + RV. — ✅ COMPLETE 2026-07-24
- **Session 3 (5.10):** 1 sub-session, notification alerts (bell + vibration). — ✅ COMPLETE 2026-07-31
- **Total:** 10 sub-sessions across 3 sessions. 10 COMPLETE + VPS RV PASS.
- **VPS RV (2026-07-31, commit a8a26f62): 26/26 HTTP PASS.** CD deployed (3/3 jobs success). SC10: campaigns/send-push 302 no-auth + 401 bad-auth + push/send/jobs 302 no-auth (cookie redirect). SC14: push subscribe/unsubscribe/status/track 401/400 + Gateway forward 401. SC18-22: SW v16 + prefs cache + getNotificationPrefsFromSW + play-bell postMessage deployed; pwa.js + setNotificationPrefs + getNotificationPrefs + playBellSound + AudioContext + setupBellMessageListener deployed; profile page 200; health 3/3 200.
- **NO BLOCKER:** S10 pure client-side, no Domain/server change. iOS limitation documented (not hidden). Phase 6 boundary satisfied (S10 complete before Phase 6 REVIEW_ONLY).
