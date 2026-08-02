# MASTER IMPLEMENTATION PLAN — KhachLink Improvements (PWA + QR Scanning + Product Personalization + Real-time Order Status)

> ⚠️ **SUPERSEDED (2026-06-29)**
> This plan has been merged into the unified roadmap:
> **`docs/AI/tasks/UNIFIED_ROADMAP_master_plan.md`** (Option C — Merged, Layer-ordered)
> Do NOT use this file for implementation. Use the unified plan instead.

**Created:** 2026-06-29
**Last Updated:** 2026-06-29
**Current Status:** ~~PLANNING~~ → SUPERSEDED
**Branch strategy:** See unified plan
**Execution principle:** See unified plan

---

## 0. EXECUTION RULES

### JIT Planning Strategy (Áp dụng cho mọi wave)
**Nguyên tắc cốt lõi:** KHÔNG code mò mẫm - Investigate trước, Implement sau

**Bước 1: INVESTIGATE & ANALYZE (Planning Phase)**
- Đọc và hiểu rõ hiện trạng implementation
- Đọc production code để hiểu logic nghiệp vụ hiện tại
- Identify gaps và requirements
- Lập detailed coding plan với specific steps
- Chốt approach trước khi viết bất kỳ dòng code nào
- Document assumptions, open questions, verified facts

**Bước 2: IMPLEMENT (Execution Phase)**
- Thực hiện viết code theo plan đã chốt ở Bước 1
- KHÔNG thay đổi approach khi đang implement (trừ khi phát hiện critical issue)
- Mỗi bước implement xong, test trên production/staging để verify
- Nếu test fail theo cách khác, DỪNG LẠI và quay lại Bước 1

**QUY TẮC SẮC (HARD RULES):**
- **KHÔNG sửa production code khi chưa hiểu rõ logic nghiệp vụ**
- **KHÔNG bypass existing features**
- **CHỈ sửa production code khi code production đang sai rõ ràng và verified**
- **LUÔN test trên real devices (Android Chrome, iOS Safari) cho PWA/QR features**
- **UI Platform components MUST be used - không bypass với custom HTML/CSS**

### Session protocol
1. **Mỗi session chỉ làm 1 wave** - không跳步
2. **Bắt đầu mỗi session:** Planning Phase (Investigate → Analyze → Plan)
3. **Sau khi plan chốt:** Execution Phase (Implement theo plan)
4. **Trước khi session end:** Test feature trên real devices, đảm bảo hoạt động
5. **Sau mỗi session:** Commit với message format `[WAVE X] Task description`
6. **Nếu feature không hoạt động:** DỪNG IMPLEMENT, quay lại Planning Phase, re-analyze
7. **Nếu phát hiện production code sai:** Document rõ, report, chờ approval trước khi sửa

### Branch protocol
```
main
  └── feature/khachlink-wave1-pwa-install (Wave 1)
      └── feature/khachlink-wave2-qr-scanning (Wave 2)
          └── feature/khachlink-wave3-product-personalization (Wave 3)
              └── feature/khachlink-wave4-order-status-realtime (Wave 4)
```
- Mỗi wave có branch riêng để dễ rollback
- Merge wave vào branch trước đó (cherry-pick hoặc rebase)
- Final merge vào main khi tất cả waves complete

### Hard rules (không violate)
- **UI Platform components MUST be used** - không bypass với custom HTML/CSS
- **PWA features MUST be tested on real devices** - không chỉ trên desktop browser
- **QR scanning MUST handle camera permissions properly** - iOS và Android khác nhau
- **Product personalization KHÔNG được làm chậm performance** - cache là bắt buộc
- **KHÔNG SỬA PRODUCTION CODE CHỈ ĐỂ BYPASS** - QUY TẮC SẮC
- **CHỈ SỬA PRODUCTION CODE KHI HIỂU RÕ LOGIC NGHIỆP VỤ** - QUY TẮC SẮC
- **KHÔNG CODE MÒ MẪM** - Luôn Planning trước, Implement sau

---

## 1. CURRENT ISSUES SUMMARY

### Issue 1: PWA Install Functionality
**Status:** ⚠️ PARTIAL IMPLEMENTATION
**Priority:** 1 (Quick Win)
**Estimated Time:** 1-2 hours

**Current State:**
- ✅ PWAService.cs exists with full implementation
- ✅ PWAInstallPrompt.razor exists (complete UI)
- ✅ manifest.json configured properly
- ✅ service-worker.js exists
- ❌ AppInstallPrompt.razor (duplicate, incomplete)
- ❌ VAPID key placeholder in pwa.js
- ❌ Service worker may not be properly initialized in App.razor
- ❌ Unclear which install prompt component is active

**Files:**
- `5_WebApps/KhachLink/Services/PWA/PWAService.cs`
- `5_WebApps/KhachLink/Components/PWA/PWAInstallPrompt.razor`
- `5_WebApps/KhachLink/Components/AppInstallPrompt.razor` (duplicate)
- `5_WebApps/KhachLink/wwwroot/js/pwa.js`
- `5_WebApps/KhachLink/wwwroot/manifest.json`
- `5_WebApps/KhachLink/wwwroot/service-worker.js`
- `5_WebApps/KhachLink/Components/App.razor`

### Issue 2: QR Code Scanning for Orders
**Status:** ❌ NOT IMPLEMENTED
**Priority:** 2 (New Feature)
**Estimated Time:** 1-2 days

**Current State:**
- ✅ QrPaymentModal.razor exists (display QR for payment only)
- ❌ NO QR scanning library (html5-qrcode, jsQR, BarcodeReader)
- ❌ NO camera access implementation
- ❌ NO QR scan-to-order functionality
- ❌ Products do not have QR codes generated

**Files:**
- `5_WebApps/KhachLink/Components/QrPaymentModal.razor` (payment only)
- `5_WebApps/KhachLink/Pages/Checkout.razor`

**Missing Components:**
- QR scanning library integration
- QRScanner.razor component (camera-based)
- QR code generation for products
- Scan-to-cart workflow
- Camera permission handling

### Issue 3: Product Personalization (Option C - Hybrid)
**Status:** ❌ NOT IMPLEMENTED
**Priority:** 3 (Enhancement)
**Estimated Time:** 2-3 days

**Current State:**
- ✅ Products loaded from ShopERP API (all active products)
- ✅ ProductHttpService.GetProductsAsync() working
- ✅ ProductsController returns product catalog
- ❌ NO personalization based on customer order history
- ❌ NO "Frequently Bought" section
- ❌ NO "Recently Viewed" tracking
- ❌ NO recommendation service

**Data Flow:**
```
KhachLink → ProductHttpService → Gateway → ShopERP → ProductsController → SQLite
```

**Current Behavior:**
- Shows ALL active products for tenant/shop
- No filtering based on customer preferences
- No order history analysis

**Required Enhancement:**
- Add "Frequently Bought" section based on order history
- Add "Recently Viewed" tracking
- Keep main product catalog (current behavior)
- Hybrid approach: global catalog + personalized sections

---

## 2. WAVE 1 — Fix PWA Install Functionality

**Branch:** feature/khachlink-wave1-pwa-install
**Estimated sessions:** 1
**Conflict risk:** LOW (chỉ cleanup duplicate components, config)
**Priority:** 1 (Quick Win - 1-2 hours)
**Task Card:** `docs/AI/tasks/wave1_pwa_install_fix_task_card.md`

### Tasks (sequential)
| # | Task ID | Task | Files | Task card | Status |
|---|---|---|---|---|---|
| 1 | W1-T1 | Remove duplicate AppInstallPrompt.razor | 5_WebApps/KhachLink/Components/AppInstallPrompt.razor | Keep PWAInstallPrompt.razor only | PENDING |
| 2 | W1-T2 | Ensure PWAInstallPrompt.razor is active in App.razor | 5_WebApps/KhachLink/Components/App.razor | Verify component is properly included | PENDING |
| 3 | W1-T3 | Configure VAPID key or disable push notifications | 5_WebApps/KhachLink/wwwroot/js/pwa.js | Replace placeholder or comment out push logic | PENDING |
| 4 | W1-T4 | Verify service worker registration in App.razor | 5_WebApps/KhachLink/Components/App.razor | Ensure service worker is registered on startup | PENDING |
| 5 | W1-T5 | Test PWA installation on real devices | Production (diemthuong.${VANAN_DOMAIN}) | Test on Android Chrome, iOS Safari | PENDING |

### Entry criteria
- [ ] Project builds successfully (`dotnet build`)
- [ ] Git status clean (no uncommitted changes)
- [ ] PWA components reviewed and understood
- [ ] Production access available for testing

### Exit criteria — ALL PASSED
- [ ] AppInstallPrompt.razor removed (duplicate eliminated)
- [ ] PWAInstallPrompt.razor is active in App.razor
- [ ] VAPID key configured or push notifications disabled
- [ ] Service worker properly registered
- [ ] PWA install prompt shows on mobile devices
- [ ] PWA installation completes successfully on Android Chrome
- [ ] PWA installation completes successfully on iOS Safari
- [ ] App runs in standalone mode after installation
- [ ] No new errors introduced
- [ ] Build: 0 errors

### Why first
- Quick win (1-2 hours)
- Low risk (cleanup + config)
- Foundation for other features
- Easy to validate on production

---

## 3. WAVE 2 — Implement QR Code Scanning

**Branch:** feature/khachlink-wave2-qr-scanning
**Estimated sessions:** 2-3
**Conflict risk:** MEDIUM (new feature, camera permissions)
**Priority:** 2 (New Feature - 1-2 days)
**Task Card:** `docs/AI/tasks/wave2_qr_scanning_task_card.md`

### Tasks (sequential)
| # | Task ID | Task | Files | Task card | Status |
|---|---|---|---|---|---|
| 1 | W2-T1 | Install and integrate html5-qrcode library | 5_WebApps/KhachLink/wwwroot/, 5_WebApps/KhachLink/5_WebApps/KhachLink.csproj | Add via CDN or npm | PENDING |
| 2 | W2-T2 | Create QRScanner.razor component | 5_WebApps/KhachLink/Components/QRScanner.razor | Camera access, QR detection, error handling | PENDING |
| 3 | W2-T3 | Add QR code generation to products | 5_WebApps/KhachLink/Services/QrCodeService.cs, ShopERP/Services/QrCodeService.cs | Generate unique QR per product | PENDING |
| 4 | W2-T4 | Implement scan-to-cart workflow | 5_WebApps/KhachLink/Pages/Scan.razor, CartService | Parse QR data, add to cart, navigate | PENDING |
| 5 | W2-T5 | Add camera permission handling | 5_WebApps/KhachLink/Components/QRScanner.razor | iOS và Android permissions | PENDING |
| 6 | W2-T6 | Add QR scanner to navigation | 5_WebApps/KhachLink/Components/Layout/NavMenu.razor | Add scan button to menu | PENDING |
| 7 | W2-T7 | Test QR scanning on real devices | Production (diemthuong.${VANAN_DOMAIN}) | Test on Android Chrome, iOS Safari | PENDING |

### Entry criteria
- [ ] Wave 1 merged to main
- [ ] Project builds successfully
- [ ] Git status clean
- [ ] html5-qrcode library research completed
- [ ] QR code format defined (product ID, shop ID)

### Exit criteria — ALL PASSED
- [ ] html5-qrcode library integrated
- [ ] QRScanner.razor component created and working
- [ ] QR codes generated for all products
- [ ] Scan-to-cart workflow functional
- [ ] Camera permissions handled properly on iOS
- [ ] Camera permissions handled properly on Android
- [ ] QR scanner accessible from navigation
- [ ] QR scanning works on Android Chrome
- [ ] QR scanning works on iOS Safari
- [ ] Scanned products added to cart correctly
- [ ] No new errors introduced
- [ ] Build: 0 errors

### Dependencies
- Wave 1 must complete first (PWA foundation)
- Camera permissions research (iOS vs Android differences)

---

## 4. WAVE 3 — Implement Product Personalization (Option C - Hybrid)

**Branch:** feature/khachlink-wave3-product-personalization
**Estimated sessions:** 3-4
**Conflict risk:** MEDIUM (new service, API changes)
**Priority:** 3 (Enhancement - 2-3 days)
**Task Card:** `docs/AI/tasks/wave3_product_personalization_task_card.md`

### Tasks (sequential)
| # | Task ID | Task | Files | Task card | Status |
|---|---|---|---|---|---|
| 1 | W3-T1 | Create CustomerRecommendationService in ShopERP | 3_CoreHub/Services/CustomerRecommendationService.cs | Query order history, calculate frequent products | PENDING |
| 2 | W3-T2 | Add personalized products API endpoint | 5_WebApps/ShopERP/Controllers/ProductsController.cs | New GET /api/products/recommended endpoint | PENDING |
| 3 | W3-T3 | Add recently viewed tracking | 3_CoreHub/Services/ProductViewTrackingService.cs | Track viewed products per customer | PENDING |
| 4 | W3-T4 | Update ProductHttpService for personalized data | 5_WebApps/KhachLink/Services/Http/ProductHttpService.cs | Add GetRecommendedProductsAsync() method | PENDING |
| 5 | W3-T5 | Add "Frequently Bought" section to Home.razor | 5_WebApps/KhachLink/Pages/Home.razor | Display personalized recommendations | PENDING |
| 6 | W3-T6 | Add "Recently Viewed" section to Home.razor | 5_WebApps/KhachLink/Pages/Home.razor | Display recently viewed products | PENDING |
| 7 | W3-T7 | Implement caching for recommendations | 3_CoreHub/Infrastructure/Caching/RecommendationCache.cs | Cache for 5-10 minutes to avoid performance impact | PENDING |
| 8 | W3-T8 | Test personalization with real customer data | Production (diemthuong.${VANAN_DOMAIN}) | Verify recommendations are accurate | PENDING |

### Entry criteria
- [ ] Wave 2 merged to main
- [ ] Project builds successfully
- [ ] Git status clean
- [ ] Customer order history data available
- [ ] Recommendation algorithm designed

### Exit criteria — ALL PASSED
- [ ] CustomerRecommendationService created
- [ ] Personalized products API endpoint working
- [ ] Recently viewed tracking functional
- [ ] ProductHttpService updated for personalized data
- [ ] "Frequently Bought" section displays correctly
- [ ] "Recently Viewed" section displays correctly
- [ ] Main product catalog still shows all products (not replaced)
- [ ] Caching implemented (no performance degradation)
- [ ] Recommendations accurate based on order history
- [ ] No new errors introduced
- [ ] Build: 0 errors

### Dependencies
- Wave 2 must complete first
- Customer order history must be available
- Recommendation algorithm must be defined (frequency-based, collaborative filtering, etc.)

---

## 5. WAVE 4 — Real-time Order Status Updates (Short Polling + Web Push)

**Branch:** feature/khachlink-wave4-order-status-realtime
**Estimated sessions:** 2-3
**Conflict risk:** LOW (không thay đổi existing business logic, chỉ thêm transport layer)
**Priority:** 4 (Scalability - 1-2 days)
**Task Card:** `docs/AI/tasks/wave4_order_status_realtime_task_card.md`

### Motivation: Tại sao KHÔNG dùng SignalR ở KhachLink

```
SignalR tại 10,000 users:
  - 10,000 persistent WebSocket connections
  - ~20GB RAM chỉ riêng connection state
  - CPU: heartbeat + reconnect overhead liên tục
  - Horizontal scaling: bắt buộc Redis backplane

Short Polling + Web Push tại 10,000 users:
  - 0 persistent connections
  - ~500MB RAM
  - CPU: thấp, stateless
  - Horizontal scaling: tự do, không cần Redis
```

**GrabFood, Gojek, Shopee Food** dùng pattern này cho order tracking.
**SignalR giữ lại** cho Kitchen Display (ShopERP) vì staff count << 10,000.

### Architecture

```
Customer đang mở tracking page:
  OrderTracking.razor ─ GET /api/orders/{id}/status (mỗi 5s) ─→ Gateway ─→ ShopERP ─→ SQLite/Cache
  ← nhận status mới, cập nhật UI ngay lập tức

Customer đóng app / minimise:
  ShopERP PushNotificationService ─→ Web Push API ─→ Service Worker
  Service Worker ─→ show notification "Đơn hàng #xxx đã sẵn sàng!"
  Customer tap notification ─→ mở OrderTracking.razor
```

### Tasks (sequential)
| # | Task ID | Task | Files | Status |
|---|---|---|---|---|
| 1 | W4-T1 | Generate + config VAPID keys | `5_WebApps/KhachLink/wwwroot/js/pwa.js`, `.env` production | PENDING |
| 2 | W4-T2 | Persist `Customer.PushSubscriptionJson` vào Domain + DB | `1_Shared/Domain.cs`, `3_CoreHub/Infrastructure/Configurations/CustomerConfiguration.cs` | PENDING |
| 3 | W4-T3 | Implement `PushNotificationService` (server-side sender) | `3_CoreHub/Services/PushNotificationService.cs`, `5_WebApps/ShopERP/Services/PushNotificationService.cs` | PENDING |
| 4 | W4-T4 | Hook PushNotificationService vào OrderWorkflow (khi status thay đổi) | `3_CoreHub/Services/OrderWorkflowService.cs` | PENDING |
| 5 | W4-T5 | Update `service-worker.js`: handle `push` event → show notification | `5_WebApps/KhachLink/wwwroot/service-worker.js` | PENDING |
| 6 | W4-T6 | Add `PeriodicTimer` polling loop vào `OrderTracking.razor` (5s interval) | `5_WebApps/KhachLink/Pages/OrderTracking.razor` | PENDING |
| 7 | W4-T7 | Add lightweight `/api/orders/{id}/status` endpoint (tránh load full order) | `5_WebApps/ShopERP/Controllers/OrdersController.cs`, `2_Gateway/Controllers/CustomerOrdersController.cs` | PENDING |
| 8 | W4-T8 | Disable SignalR từ KhachLink (giữ cho Kitchen ShopERP) | `5_WebApps/KhachLink/Program.cs`, KhachLink components | PENDING |
| 9 | W4-T9 | Load test polling: 10,000 concurrent GET /status | Load test script | PENDING |

### Entry criteria
- [ ] Wave 3 merged to main
- [ ] Project builds successfully
- [ ] Git status clean
- [ ] VAPID key pair generated (offline tool)
- [ ] Domain change approved (Customer.PushSubscriptionJson)

### Exit criteria — ALL PASSED
- [ ] VAPID keys configured in production
- [ ] `Customer.PushSubscriptionJson` persisted to DB
- [ ] `PushNotificationService` sends push when order status changes
- [ ] `service-worker.js` handles push event and shows notification
- [ ] `OrderTracking.razor` polls every 5s while page is open
- [ ] Polling pauses when page is hidden (visibilitychange event)
- [ ] `/api/orders/{id}/status` endpoint responds in < 50ms
- [ ] Web Push notification appears when customer closes app
- [ ] SignalR removed from KhachLink (no WebSocket connections)
- [ ] Load test: 10,000 concurrent polling requests handled without errors
- [ ] Build: 0 errors

### Performance Budget
| Metric | Target |
|--------|--------|
| Polling endpoint latency | < 50ms (p95) |
| Push notification delivery | < 2s from status change |
| Memory usage at 10,000 users | < 1GB (vs 20GB SignalR) |
| CPU overhead at 10,000 users | < 20% (1 core) |

### Dependencies
- Wave 3 must complete first
- Domain change (Customer.PushSubscriptionJson) requires approval
- VAPID key pair must be generated before implementation
- Production HTTPS required for Web Push (already configured)

---

## 6. CROSS-WAVE CONSIDERATIONS

### Testing Strategy
- **Wave 1:** Test PWA installation on real devices (Android Chrome, iOS Safari)
- **Wave 2:** Test QR scanning on real devices (camera permissions, QR detection)
- **Wave 3:** Test personalization with real customer data, verify performance
- **Wave 4:** Load test 10,000 concurrent polling; test Web Push on Android + iOS

### Performance Considerations
- **Wave 1:** Service worker caching (already implemented)
- **Wave 2:** QR scanning performance (camera initialization, detection speed)
- **Wave 3:** Recommendation caching (5-10 minute TTL) to avoid performance impact
- **Wave 4:** Short polling stateless — zero persistent connections, scales horizontally

### Security Considerations
- **Wave 1:** VAPID key for push notifications (if enabled)
- **Wave 2:** Camera permissions (user consent required)
- **Wave 3:** Customer data privacy (order history access)
- **Wave 4:** VAPID keys must be secret (server-side only), PushSubscription stored encrypted

### UI Platform Compliance
- **All waves:** MUST use UI Platform components (VanAnButton, VanAnCard, VanAnModal, etc.)
- **Wave 2:** QRScanner.razor should follow UI Platform patterns
- **Wave 3:** New sections in Home.razor should use VanAnCard, VanAnButton
- **Wave 4:** OrderTracking.razor polling status indicator should use VanAnSpinner

---

## 7. SUCCESS METRICS

### Wave 1 (PWA Install)
- PWA installation success rate > 90% on Android Chrome
- PWA installation success rate > 80% on iOS Safari
- Install prompt shows within 3 seconds of page load
- No errors in browser console related to PWA

### Wave 2 (QR Scanning)
- QR detection accuracy > 95% on good lighting
- QR detection time < 2 seconds
- Camera permission grant rate > 80%
- Scan-to-cart success rate > 95%

### Wave 3 (Product Personalization)
- Recommendation load time < 500ms (with caching)
- "Frequently Bought" click-through rate > 10%
- "Recently Viewed" click-through rate > 15%
- No performance degradation on main product catalog load

### Wave 4 (Real-time Order Status)
- Polling endpoint latency < 50ms (p95)
- Push notification delivery < 2s from status change
- Memory usage at 10,000 users < 1GB (vs ~20GB SignalR)
- Zero WebSocket connections from KhachLink
- Push notification shown to customer when app is closed

---

## 8. RISK MITIGATION

### Wave 1 Risks
| Risk | Impact | Mitigation |
|------|--------|------------|
| iOS Safari PWA limitations | HIGH | Test on real iOS device, fallback to web instructions |
| Service worker not registering | MEDIUM | Verify registration in App.razor, add error logging |
| VAPID key configuration | LOW | Document VAPID setup or disable push notifications |

### Wave 2 Risks
| Risk | Impact | Mitigation |
|------|--------|------------|
| Camera permission denied | HIGH | Clear permission request UI, fallback to manual product search |
| QR detection accuracy | MEDIUM | Test with various QR codes, add retry logic |
| iOS vs Android differences | HIGH | Research and implement platform-specific handling |

### Wave 3 Risks
| Risk | Impact | Mitigation |
|------|--------|------------|
| Performance degradation | HIGH | Implement caching, monitor query performance |
| Inaccurate recommendations | MEDIUM | Start with simple frequency-based algorithm, iterate |
| No customer order history | MEDIUM | Fallback to global catalog, show "New customer" message |

### Wave 4 Risks
| Risk | Impact | Mitigation |
|------|--------|------------|
| iOS Web Push not supported (iOS < 16.4) | HIGH | Polling is primary mechanism; Push is enhancement only |
| VAPID key rotation | MEDIUM | Document key rotation procedure; all subscriptions re-subscribe |
| SQLite query slow at high concurrency | HIGH | Add Redis cache for order status, TTL 30s |
| Domain change rejected | MEDIUM | Store PushSubscription in separate table (not Customer entity) |

---

## 9. FINAL DELIVERABLES

### Wave 1
- [ ] Clean PWA install implementation (no duplicates)
- [ ] Working PWA installation on Android Chrome
- [ ] Working PWA installation on iOS Safari
- [ ] Documentation for VAPID key setup

### Wave 2
- [ ] QRScanner.razor component
- [ ] QR code generation for products
- [ ] Scan-to-cart workflow
- [ ] Camera permission handling
- [ ] QR scanner in navigation menu

### Wave 3
- [ ] CustomerRecommendationService
- [ ] Personalized products API endpoint
- [ ] Recently viewed tracking
- [ ] "Frequently Bought" section
- [ ] "Recently Viewed" section
- [ ] Caching implementation

### Wave 4
- [ ] VAPID keys configured (production)
- [ ] Customer.PushSubscriptionJson persisted
- [ ] PushNotificationService (server-side push sender)
- [ ] service-worker.js push event handler
- [ ] OrderTracking.razor polling loop (5s, pauses when hidden)
- [ ] Lightweight /api/orders/{id}/status endpoint
- [ ] SignalR removed from KhachLink
- [ ] Load test results: 10,000 concurrent polling

---

## 10. APPROVAL & SIGN-OFF

**Pre-Implementation Approval:**
- [ ] Product Owner approves master plan
- [ ] Tech Lead reviews technical approach
- [ ] QA reviews test strategy

**Wave-by-Wave Sign-Off:**
- [ ] Wave 1: PWA Install — Approved ✅ / Rejected □
- [ ] Wave 2: QR Scanning — Approved ✅ / Rejected □
- [ ] Wave 3: Product Personalization — Approved ✅ / Rejected □
- [ ] Wave 4: Real-time Order Status (Polling + Web Push) — Approved ✅ / Rejected □

**Final Sign-Off:**
- [ ] All waves completed
- [ ] All features tested on production
- [ ] Documentation updated
- [ ] Code merged to main