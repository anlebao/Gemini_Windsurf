# TASK CARD: KhachLink Improvements - Wave 4 - Real-time Order Status (Short Polling + Web Push)

## 1. GOAL & CONTEXT
- **Mục tiêu cốt lõi:** Thay thế SignalR ở KhachLink bằng Short Polling + Web Push Notifications để hỗ trợ scale đến 10,000+ concurrent users mà không tốn tài nguyên persistent connections.
- **Nghiệp vụ áp dụng:** Sau khi khách hàng đặt đơn, họ cần biết trạng thái đơn hàng thay đổi (Pending → Đã nhận → Bếp đang làm → Sẵn sàng giao). Có 2 trường hợp: (1) app đang mở → polling 5s cập nhật UI, (2) app đóng → Web Push notification báo khách hàng.

## 2. ACTIVE WORKFLOW ROUTING
- **Target Workflow:** `newfeaturebuild.md` (scalability enhancement - multi-session)
- **Execution Mode:** ANALYZE → IMPLEMENT

## 3. RELEVANT FILES (CONTEXT BOUNDARY)
- **Files được phép đọc/sửa:**
  - `docs/AI/project_state.md` (Bắt buộc đọc đầu phiên)
  - `5_WebApps/KhachLink/Pages/OrderTracking.razor` (UPDATE - add polling loop)
  - `5_WebApps/KhachLink/wwwroot/js/pwa.js` (UPDATE - replace VAPID placeholder)
  - `5_WebApps/KhachLink/wwwroot/service-worker.js` (UPDATE - add push event handler)
  - `5_WebApps/KhachLink/Program.cs` (UPDATE - remove SignalR registration)
  - `1_Shared/Domain.cs` (UPDATE - add Customer.PushSubscriptionJson, nếu Domain phase approved)
  - `3_CoreHub/Infrastructure/Configurations/CustomerConfiguration.cs` (UPDATE - map PushSubscriptionJson)
  - `3_CoreHub/Services/PushNotificationService.cs` (CREATE)
  - `3_CoreHub/Services/OrderWorkflowService.cs` (UPDATE - hook push on status change)
  - `5_WebApps/ShopERP/Controllers/OrdersController.cs` (UPDATE - add /status lightweight endpoint)
  - `2_Gateway/Controllers/CustomerOrdersController.cs` (UPDATE - forward /status endpoint)
  - `5_WebApps/KhachLink/Services/PWA/PWAService.cs` (UPDATE - complete push subscription flow)
- **Boundary Rules (Nghiêm cấm):**
  - KHÔNG sửa Kitchen Display SignalR (KitchenHub.cs) — Kitchen cần real-time, staff count thấp
  - KHÔNG thay đổi business logic của OrderWorkflowService, chỉ hook notification
  - KHÔNG store VAPID private key trong code/git — chỉ environment variable
  - KHÔNG làm polling khi tab không active (visibilitychange phải pause polling)
  - KHÔNG bỏ polling khi Web Push không available (polling là primary mechanism)

## 4. TECHNICAL & REGULATORY CONSTRAINTS (HARDENING GATES)
- [ ] **Scalability:** Zero persistent WebSocket connections từ KhachLink sau wave này
- [ ] **Security:** VAPID private key NEVER in source code — environment variable only
- [ ] **Graceful Degradation:** Polling phải hoạt động ngay cả khi push notification bị từ chối
- [ ] **Battery Friendliness:** Polling PHẢI pause khi `document.visibilityState === 'hidden'`
- [ ] **Domain Integrity:** Customer.PushSubscriptionJson thêm vào Domain chỉ khi phase Domain approved, fallback là separate table nếu không approved
- [ ] **iOS Compatibility:** Web Push chỉ hoạt động trên iOS 16.4+ installed PWA — document limitation, không block shipping

## 5. SUCCESS CRITERIA (ĐO LƯỜNG ĐƯỢC)
- [ ] **SC1:** VAPID keys generated và configured trong production environment variables
- [ ] **SC2:** `Customer.PushSubscriptionJson` persisted to DB (hoặc separate PushSubscription table)
- [ ] **SC3:** `PushNotificationService.SendOrderStatusAsync()` gửi push khi order status thay đổi
- [ ] **SC4:** `service-worker.js` handles `push` event và shows notification với order info
- [ ] **SC5:** `OrderTracking.razor` polls `GET /api/orders/{id}/status` mỗi 5 giây
- [ ] **SC6:** Polling tự pause khi tab hidden (`visibilitychange` event), resume khi visible lại
- [ ] **SC7:** `/api/orders/{id}/status` endpoint trả về `{ orderId, status, updatedAt }` trong < 50ms
- [ ] **SC8:** Web Push notification hiện ra khi customer đóng app sau khi đặt hàng
- [ ] **SC9:** SignalR imports và registration removed khỏi KhachLink (Program.cs, components)
- [ ] **SC10:** Load test: 10,000 concurrent GET /status requests xử lý không lỗi
- [ ] **SC11:** Build: 0 errors
- [ ] **SC12:** Integration tests still pass (144/144 PASS)

**Implementation Date:** 2026-06-29
**Branch:** feature/khachlink-wave4-order-status-realtime

## 6. ACTIVE SKILLS (MAX 3)
- `domain-integrity-validation` — Verify Customer entity modification is safe
- `sqlite-concurrency-analysis` — Ensure /status endpoint không gây lock contention tại high concurrency
- `ui-platform-compliance-review` — Ensure polling status indicator dùng UI Platform components

## 7. AI HEALTH CHECK MATRIX (INITIAL)
- **Evidence Count:** 10
- **Verified Facts:**
  - Fact 1: `OrderTracking.razor` hiện tại KHÔNG có polling — load 1 lần khi OnParametersSetAsync (verified đọc file)
  - Fact 2: `pwa.js` có `subscribeToPush()` nhưng VAPID là placeholder `YOUR_VAPID_PUBLIC_KEY` (line 114)
  - Fact 3: `NotificationsController.cs` ShopERP: log-only, chưa persist PushSubscription (W17 deferred)
  - Fact 4: `service-worker.js` tồn tại nhưng KHÔNG có `push` event handler (verified grep)
  - Fact 5: `KitchenHub.cs` dùng SignalR cho Kitchen Display — KHÔNG được xóa
  - Fact 6: `KhachLink/Program.cs` có SignalR registration (lines 115, 180)
  - Fact 7: `OrderWorkflowService` đã có `TransitionStatusAsync()` — cần hook vào đây
  - Fact 8: `Customer.PushSubscriptionJson` field được ghi nhận là deferred to Wave 18 trong code comment
  - Fact 9: Production đã có HTTPS (nginx config verified) — Web Push cần HTTPS
  - Fact 10: NATS đã có trong hệ thống — có thể dùng làm event bus phía server để trigger push
- **Assumptions:**
  - VAPID keys cần generate lần đầu (có thể dùng web-push CLI hoặc online tool)
  - iOS Web Push chỉ work trên iOS 16.4+ khi PWA installed — acceptable limitation
  - SQLite có thể handle 2,000 req/s (10,000 users × poll/5s) nếu query đơn giản + cache
  - Customer entity change cần approval vì là Domain layer change
- **Open Questions:**
  - Q1: VAPID keys lưu ở đâu trong production? (environment variable hay Docker secret?)
  - Q2: Customer.PushSubscriptionJson add vào Domain hay tạo separate PushSubscription table?
  - Q3: Polling interval 5s hay 10s cho production? (battery vs latency tradeoff)
- **Recommended Action:** Start với W4-T6 (polling loop) trước vì zero dependency — ngay lập tức có giá trị, sau đó mới làm push notifications

## 8. REVERSE IMPACT ANALYSIS
| File thay đổi | Reverse impact | Mitigation |
|---|---|---|
| `OrderTracking.razor` (add PeriodicTimer) | Component sẽ có IAsyncDisposable — cần implement disposal đúng | Implement `DisposeAsync()`, cancel timer on dispose |
| `OrderWorkflowService.cs` (hook push) | Nếu push service throws, order workflow bị ảnh hưởng | Wrap push call in try/catch, log and continue |
| `Customer.PushSubscriptionJson` (Domain) | Migration cần, test suite cần update | Add nullable field, EF migration, integration test update |
| `service-worker.js` (add push handler) | Service worker update cần browser to re-register | `skipWaiting()` + `clients.claim()` đã có sẵn |
| `KhachLink/Program.cs` (remove SignalR) | Nếu có component nào vẫn dùng HubConnection sẽ break | Grep toàn bộ KhachLink components trước khi remove |
| `OrdersController.cs` (add /status endpoint) | Thêm public endpoint — cần AllowAnonymous hoặc token auth | Dùng X-Customer-Token header (consistent với Wave 17 pattern) |

## 9. TDD & E2E TESTING STRATEGY
- **Unit Tests:**
  - `PushNotificationService`: mock web push library, verify payload
  - `OrderTracking.razor` polling: verify timer starts/stops on visibility change
- **Integration Tests:**
  - `/api/orders/{id}/status` endpoint: verify returns correct status
  - Push subscription persistence: verify Customer.PushSubscriptionJson saved
  - Order status change → push triggered: mock PushNotificationService
- **Load Tests:**
  - 10,000 concurrent GET /api/orders/{id}/status
  - Verify p95 latency < 50ms
  - Verify no connection errors
- **Manual Tests:**
  - Web Push notification on Android Chrome (push when app closed)
  - Web Push notification on iOS Safari 16.4+ PWA
  - Polling UI updates correctly when status changes
  - Polling pauses when tab hidden, resumes when visible
- **Test boundary:**
  - Unit tests: PushNotificationService, polling timer logic
  - Integration tests: /status endpoint, push subscription
  - Load tests: k6 script for 10,000 concurrent polling
  - Manual: Web Push behavior on real devices

## 10. JIT PLANNING + PURE EXECUTION (MICRO-PHASES)

### Chiến lược thực thi: JIT Planning + Pure Execution

**Nguyên tắc Wave 4:** Start with HIGH VALUE, ZERO DEPENDENCY task (polling loop) first, then build push notifications incrementally.

**Lý do polling trước push:**
- Polling mang lại giá trị ngay lập tức (customer thấy status update)
- Polling không cần VAPID, không cần Domain change, không cần service worker update
- Push là enhancement — nếu bị block bởi Domain approval, polling vẫn đủ

### Micro-phase breakdown cho Wave 4

| Session | JIT Planning (chốt gì) | Pure Execution (viết gì) |
|---|---|---|
| **S1** | - Read OrderTracking.razor fully để hiểu current load pattern<br>- Read OrdersController.cs để biết existing endpoints<br>- Plan `/status` lightweight endpoint schema<br>- Plan polling loop implementation với `PeriodicTimer` | - Add `GET /api/orders/{id}/status` endpoint (ShopERP + Gateway)<br>- Add `PeriodicTimer` polling loop vào `OrderTracking.razor`<br>- Add `visibilitychange` event handler (pause/resume polling)<br>- Add polling status indicator UI (VanAnSpinner) |
| **S2** | - Research VAPID key generation (web-push CLI)<br>- Plan PushSubscriptionJson storage: Domain field hay separate table?<br>- Plan PushNotificationService interface<br>- Decide: WebPush .NET library (WebPush-NetCore5?) | - Generate VAPID keys (document procedure)<br>- Create `PushNotificationService` với WebPush library<br>- Update `pwa.js` với real VAPID public key<br>- Update `service-worker.js` với `push` event handler |
| **S3** | - Plan Customer.PushSubscriptionJson Domain change (hoặc separate table)<br>- Plan NotificationsController update (persist subscription)<br>- Plan OrderWorkflowService hook | - Add PushSubscriptionJson to Customer (Domain change với approval)<br>- Update NotificationsController: persist subscription (không chỉ log)<br>- Hook `PushNotificationService` vào `OrderWorkflowService.TransitionStatusAsync()`<br>- EF Core migration |
| **S4** | - Plan SignalR removal from KhachLink<br>- Plan load test scenarios (k6 script)<br>- Plan integration test updates | - Grep + remove SignalR từ KhachLink components<br>- Remove SignalR registration từ `Program.cs`<br>- Write load test script (k6 hoặc wrk)<br>- Update integration tests<br>- Final verification: build 0 errors, 144/144 tests pass |

### Rules
- **Polling trước push** — deliver value incrementally
- **VAPID private key NEVER in git** — environment variable only
- **Polling MUST pause when tab hidden** — battery friendliness không thương lượng
- **KHÔNG xóa KitchenHub.cs** — Kitchen cần real-time, chỉ remove khỏi KhachLink
- **Wrap push call in try/catch** — push failure KHÔNG được làm fail order workflow

## 11. ESTIMATED EFFORT
- 1-2 days total
- 3-4 sessions theo JIT Planning
- **BLOCKER 1:** Domain change (Customer.PushSubscriptionJson) cần approval — có fallback: separate PushSubscription table
- **BLOCKER 2:** VAPID keys cần generate và deploy vào production environment variables
- **NOTE:** Polling (S1) có thể ship độc lập trước push (S2-S4) — giảm risk