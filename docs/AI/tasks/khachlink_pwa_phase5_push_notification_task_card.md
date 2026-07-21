# TASK CARD: PWA-OFFLINE - Phase 5 - Push Notification + PWA Polish

## 1. GOAL & CONTEXT
- **Mục tiêu cốt lõi:** Wire push notification subscription vào UI + thêm Gateway push sending endpoint + verify VAPID key.
- **Nghiệp vụ áp dụng:** Khách nhận thông báo "Đơn hàng đã xác nhận" khi app đóng (Android). iOS 16.4+ hỗ trợ web push.

## 2. ACTIVE WORKFLOW ROUTING
- **Target Workflow:** `.devin/workflows/newfeaturebuild.md`
- **Execution Mode:** IMPLEMENT

## 3. RELEVANT FILES (CONTEXT BOUNDARY)
- **Files được phép đọc/sửa:**
  - `docs/AI/project_state.md`
  - `5_WebApps/KhachLink/wwwroot/js/pwa.js` (VAPID key line 156, subscribeToPush)
  - `5_WebApps/KhachLink/Pages/Profile.razor` (add notification toggle)
  - `2_Gateway/Controllers/PushController.cs` (NEW — subscribe + send endpoints)
  - `3_CoreHub/Services/PushNotificationService.cs` (NEW — WebPush sending)
  - `1_Shared/Domain.cs` (NEW entity: PushSubscription — **requires Tech Lead approval**)
  - `2_Gateway/Migrations/` (new migration for PushSubscriptions table)
- **Boundary Rules:**
  - **Hard Stop:** New Domain entity `PushSubscription` → require Tech Lead approval (Domain Modeling).
  - KHÔNG thay đổi Order/Accounting entities.
  - Push subscription tenant-scoped (multi-tenancy enforced).

## 4. TECHNICAL & REGULATORY CONSTRAINTS
- [ ] **VAPID key:** Verify key in pwa.js line 156 valid (or regenerate + update both client + server).
- [ ] **WebPush NuGet:** Add `WebPush` package to Gateway for sending push.
- [ ] **PushSubscription entity:** Tenant-scoped, fields: Endpoint, P256DHKey, AuthKey, CustomerId (optional).
- [ ] **Gateway endpoints:** `POST /api/push/subscribe` (store subscription), `POST /api/push/send` (SystemAdmin sends).
- [ ] **Auto-push trigger:** Order status change → push to customer (if subscribed).
- [ ] **iOS 16.4+:** Web push supported but requires app open (not background).

## 5. SUCCESS CRITERIA
- [ ] SC1: VAPID key verified (or regenerated + updated).
- [ ] SC2: `PushSubscription` entity added to Domain (Tech Lead approved).
- [ ] SC3: PG migration for `PushSubscriptions` table applied.
- [ ] SC4: `PushController.cs` — `POST /api/push/subscribe` stores subscription.
- [ ] SC5: `PushController.cs` — `POST /api/push/send` sends push (SystemAdmin).
- [ ] SC6: `PushNotificationService.cs` — WebPush sending logic.
- [ ] SC7: Profile.razor — "Cài đặt thông báo" toggle wired to `subscribeToPush()`.
- [ ] SC8: Order status change auto-triggers push to subscribed customer.
- [ ] SC9: `dotnet build VanAn.sln` PASS.
- [ ] SC10: RV Android Chrome: subscribe → close app → trigger push → notification received.
- [ ] SC11: RV iOS Safari 16.4+: subscribe → app open → trigger push → notification received.

**Implementation Date:** _TBD_
**Branch:** `feature/khachlink-wasm`

## 6. ACTIVE SKILLS (MAX 3)
- `domain-integrity-validation` — new PushSubscription entity
- `einvoice-integration` — N/A (placeholder, similar integration pattern)
- `pattern-based-fixing` — WebPush API pattern

## 7. AI HEALTH CHECK MATRIX (INITIAL)
- **Evidence Count:** 3
- **Verified Facts:**
  - Fact 1: pwa.js line 156 có hardcoded VAPID key (BJIeg2XokT35...)
  - Fact 2: pwa.js line 150 `subscribeToPush()` exists (not wired to UI)
  - Fact 3: Gateway KHÔNG có push notification endpoint (no PushController)
- **Assumptions:**
  - A1: VAPID key trong pwa.js chưa match server (chưa có server-side key).
  - A2: `WebPush` NuGet package available cho .NET 8.
- **Open Questions:**
  - Q1: VAPID key pair hiện tại có hợp lệ không? → regenerate để chắc.
  - Q2: PushSubscription nên gắn với CustomerId hay TenantId? → Cả hai (customer within tenant).
  - Q3: Auto-push on order status change — thêm vào OrderService.MarkPaidAsync? → Yes, nhưng KHÔNG sửa Domain.
- **Recommended Action:** Proceed to ANALYZE (Q1-Q3) → request Domain approval → IMPLEMENT.

## 8. REVERSE IMPACT ANALYSIS
| File thay đổi | Reverse impact | Mitigation |
|---|---|---|
| Domain.cs (new PushSubscription) | **Hard Stop** — require Tech Lead approval | Submit Domain Modeling proposal |
| Gateway PushController.cs | New controller, no impact existing | Isolated |
| Profile.razor | New toggle, no impact existing | Feature flag default OFF |

## 9. TDD & E2E TESTING STRATEGY
- **Unit test:** PushNotificationService — send push (mock WebPush client).
- **Integration test:** PushController — subscribe + send (mock push).
- **Manual RV:** Android Chrome real device → subscribe → push → verify notification.

## 10. JIT PLANNING + PURE EXECUTION
| Session | JIT Planning | Pure Execution |
|---|---|---|
| S1 | Regenerate VAPID + chốt PushSubscription entity design | Request Domain approval |
| S2 | Implement PushSubscription entity + migration (if approved) | Code + migration |
| S3 | Implement PushController + PushNotificationService | Code + unit test |
| S4 | Wire Profile.razor toggle + order status trigger | Code + RV Android |

## 12. ESTIMATED EFFORT
- 3-4 sessions. **BLOCKER:** Tech Lead approval for new Domain entity. Can parallel Phase 2-4.
