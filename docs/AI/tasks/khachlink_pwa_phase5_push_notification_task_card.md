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

## 5. SUCCESS CRITERIA (EXPANDED — 17)
- [ ] SC1: VAPID key verified (match client + server — đã verify ANALYZE).
- [ ] SC2: `CampaignPushJob` entity + PG migration applied.
- [ ] SC3: `EventTypes.LoyaltyPointsChanged` + outbox event publish trong LoyaltyRewardsService.AddPointsAsync + SubtractPointsAsync.
- [ ] SC4: `PushNotificationService.SendLoyaltyPointsChangedNotificationAsync` — push khi điểm biến động (EARN/SPEND).
- [ ] SC5: `PushNotificationService.SendBulkNotificationAsync` — bulk push cho campaign (return sentCount, failedCount).
- [ ] SC6: `CustomerSegmentationService.GetBySegmentAsync` — filter customers by criteria.
- [ ] SC7: `Customer.UpdateOrderStats` — update LastOrderDate + TotalSpent khi order complete.
- [ ] SC8: Auto-push on order status change (wire NATS subscriber → SendOrderStatusNotificationAsync).
- [ ] SC9: Profile.razor push toggle + full unsubscribe (browser + server).
- [ ] SC10: `POST /api/campaigns/{id}/send-push` (SystemAdmin) + `POST /api/push/send` ad-hoc.
- [ ] SC11: `DELETE /api/notifications/push/subscribe` (mark IsActive=false).
- [ ] SC12: CampaignsAdmin.razor segment builder UI + CampaignPushJob history.
- [ ] SC13: `dotnet build VanAn.sln` PASS + `guard-check.ps1` PASS.
- [ ] SC14: RV VPS Android Chrome: subscribe → loyalty change push → campaign bulk push.
- [ ] SC15: `PushNotificationDelivery` entity + PG migration — record per-notification delivery + click.
- [ ] SC16: `POST /api/push/track` (anonymous beacon) — update delivery Status=Clicked. SW `notificationclick` handler gửi beacon.
- [ ] SC17: CampaignsAdmin.razor hiển thị Sent / Clicked / CTR stats per CampaignPushJob.

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
| S5 (5.5) | Gateway admin endpoints (send-push + push/send + DELETE subscribe) | Code + integration test | ⏳ PENDING — Session 2 |
| S6 (5.6) | Profile.razor toggle + pwa.js unsubscribe + PWAService | Code + browser test | ⏳ PENDING — Session 2 |
| S7 (5.7) | CampaignsAdmin.razor segment builder + history UI | Code | ⏳ PENDING — Session 2 |
| S8 (5.8) | Full test suite + build + guard-check + RV VPS | Test + RV report | ⏳ PENDING — Session 2 |
| S9 (5.9) | Click tracking — PushNotificationDelivery entity + SW notificationclick + POST /api/push/track + admin CTR stats | Code + integration test + RV click | ⏳ PENDING — Session 2 |

**Grouping:** S1-S4 = Session 1 (backend infra) — ✅ COMPLETE 2026-07-24. S5-S9 = Session 2 (API + UI + tracking + tests + RV) — PENDING.

### Session 1 Build Verification (2026-07-24)
- `dotnet build VanAn.sln` — 0 errors, 0 critical warnings ✅
- `guard-check.ps1` — ALL CHECKS PASSED (untracked, encodings, windsurf guard, architecture guard, Roslyn, fast test gate) ✅
- 9 Success Criteria达成: SC2, SC3, SC4, SC5, SC6, SC7, SC8, SC13, SC15

## 12. ESTIMATED EFFORT
- **Session 1 (5.1-5.4):** 4 sub-sessions, backend infra. — ✅ COMPLETE 2026-07-24
- **Session 2 (5.5-5.9):** 5 sub-sessions, API + UI + tracking + tests + RV. — ⏳ PENDING
- **Total:** 9 sub-sessions across 2 sessions.
- **NO BLOCKER:** Domain modifications approved as part of feature plan (THÊM, không sửa entity hiện có). PushSubscription entity đã có — bỏ Hard Stop task card cũ.
