# MASTER FIX PLAN — KhachLink All Gaps (W1-W4 + W17 + W18 Tech Debt)

**Created:** 2026-07-06
**Source:** Tổng hợp từ 3 review:
  1. `wave2_qr_scanning_task_card.md` (W2 QR Scanning)
  2. `khachlink_improvements_master_plan.md` (W1-W4 — đã SUPERSEDED)
  3. `KHACHLINK_RETENTION_PLAN.md` (W17 Retention + W18 Tech Debt)

**Status:** ✅ IMPLEMENTED — BATCH 1-7 COMPLETE, BATCH 8 DEFERRED
**Mode:** IMPLEMENT (approved 2026-07-06)
**Build:** 0 errors after all batches
**Tests:** 11/11 QRCode tests PASS

---

## 0. EXECUTION RULES

- Tuân thủ `.devin/rules/governance.md` (Hard Stops, Domain Protection, UI Platform)
- Mỗi fix batch: build 0 errors + guard-check.ps1 PASS bắt buộc
- Không tạo file mới nếu không cần thiết — ưu tiên edit file hiện có
- Domain.cs: KHÔNG sửa (W18-TD1 đã resolve qua separate table, không cần thêm field)
- Branch: làm trên branch riêng `fix/khachlink-all-gaps` (không commit thẳng main)

---

## 1. GAP INVENTORY (verified bằng code inspection)

### Tier CRITICAL — Workflow đứt / Service unreachable

| # | Gap | Wave | File | Evidence |
|---|---|---|---|---|
| C1 | `IQrCodeService` không đăng ký DI, không có Controller endpoint sinh QR | W2 | `3_CoreHub/Services/QrCodeService.cs`, `5_WebApps/ShopERP/Services/QrCodeService.cs` | grep Program.cs → 0 match. Scanner quét được nhưng hệ thống không sinh QR → SC3 sai |
| C2 | `CustomerRecommendationService` không đăng ký DI | W3 | `3_CoreHub/Services/CustomerRecommendationService.cs` | Inject vào `ProductsController` line 17 via primary constructor → runtime throw khi gọi `/api/products/recommended` |
| C3 | `PushNotificationService` không đăng ký DI | W4 | `3_CoreHub/Services/PushNotificationService.cs` | grep Program.cs → 0 match. Service tồn tại nhưng unreachable |
| C4 | `PushNotificationService.SubscribeToNatsAsync()` là STUB (chỉ log, không subscribe NATS thật) | W4 | line 171-187 | Comment thừa nhận "would be implemented as a BackgroundService... For Session 3, we'll implement a simple subscription hook" |

### Tier HIGH — Task ghi COMPLETE nhưng chưa làm

| # | Gap | Wave | File | Evidence |
|---|---|---|---|---|
| H1 | SignalR chưa remove khỏi KhachLink (W4-T8) | W4 | `KhachLink/Program.cs` line 112 `AddSignalR()`, `Hubs/DashboardHub.cs`, `RealTimeDashboard.razor` line 512-534 | Exit criteria "SignalR removed from KhachLink" chưa đạt |
| H2 | `IdentityUpgradeModal.razor` dead code — không component nào reference | W17 | `Components/IdentityUpgradeModal.razor` | grep `*.razor` for "IdentityUpgradeModal" → 0 match. `OnUpgrade` EventCallback declared nhưng không ai wire |
| H3 | `AddFromQrCodeAsync` dead code — `Scan.razor` gọi `AddItemAsync` thay vì method này | W2 | `CartService.cs` line 95-100 | Method identical với `AddItemAsync` (duplicate logic) |

### Tier MEDIUM — Security / Supply chain / TDD

| # | Gap | Wave | File | Evidence |
|---|---|---|---|---|
| M1 | `html5-qrcode` CDN không pin version + không SRI integrity | W2 | `App.razor` line 13 `<script src="https://unpkg.com/html5-qrcode">` | Floating range → supply-chain risk |
| M2 | Không unit test cho QR/Recommendation/Push | W2/W3/W4 | `6_Tests/` | grep `*Test*.cs` for QRCodePayload/QrCodeService/CustomerRecommendation/PushNotification → 0 match |
| M3 | Hardcode fallback menu trong service-worker.js (demo data) | W1 | `service-worker.js` line 99-108 `{id:1, name:'Trà sữa', price:25000}` | Offline fallback path vi phạm "no hardcode" |
| M4 | `LoyaltyController.CalcTier` hardcode tier rules (W18-TD3 vẫn valid) | W17/W18 | `LoyaltyController.cs` line 57-65 | Switch expression hardcode, không config-driven |
| M5 | `GoogleMaps.razor` + `StoreFinder.razor` duplicate (W18-TD5) | W17/W18 | 2 file riêng cho 1 chức năng | Cần merge |

### Tier LOW — Doc debt / Known limitation

| # | Gap | Wave | File | Evidence |
|---|---|---|---|---|
| L1 | `InvalidateAllCache()` no-op (IMemoryCache không support pattern removal) | W3 | `CustomerRecommendationService.cs` line 134-139 | Known limitation, chỉ log |
| L2 | Master plan W1-W4 SUPERSEDED nhưng section 9/10 deliverables không update | — | `khachlink_improvements_master_plan.md` line 470-521 | Toàn bộ `[ ]` |
| L3 | Branch protocol không tuân thủ (commit thẳng main) | — | git log | Không có branch `feature/khachlink-wave*` |
| L4 | KhachLinkLayout.razor inject `ShopConfigHttpService` (concrete) thay vì `IShopConfigService` (interface) | W17-T9 | `KhachLinkLayout.razor` line 8 | Deviation nhẹ, vẫn hoạt động |

---

## 2. FIX BATCHES (theo thứ tự ưu tiên + dependency)

### BATCH 1 — DI Registration (C1, C2, C3) — 30 phút

**Mục tiêu:** 3 service unreachable → reachable, không đổi logic

| Task | File | Action |
|---|---|---|
| 1.1 | `3_CoreHub/Extensions/ServiceCollectionExtensions.cs` (hoặc `Program.cs` của host) | `services.AddScoped<IQrCodeService, QrCodeService>();` |
| 1.2 | `5_WebApps/ShopERP/Program.cs` | `services.AddScoped<IShopQrCodeService, ShopQrCodeService>();` |
| 1.3 | `5_WebApps/ShopERP/Program.cs` (hoặc CoreHub extension) | `services.AddScoped<CustomerRecommendationService>();` |
| 1.4 | `5_WebApps/ShopERP/Program.cs` | `services.AddScoped<PushNotificationService>();` (cần VAPID config section + env var) |

**Verify:**
- `dotnet build VanAn.sln` → 0 errors
- Test runtime: gọi `GET /api/products/recommended?customerId=...&tenantId=...` → không throw DI exception
- Test runtime: resolve `PushNotificationService` từ service provider → không throw

**Risk:** `PushNotificationService` constructor throw nếu `VAPID_PRIVATE_KEY` env var missing → cần config fallback cho dev environment ( đọc từ `appsettings.Development.json` nếu env var null)

---

### BATCH 2 — QR Generation Endpoint (C1 hoàn chỉnh) — 1-2 giờ

**Mục tiêu:** Sinh QR thật cho product → scanner có QR để quét

| Task | File | Action |
|---|---|---|
| 2.1 | `5_WebApps/ShopERP/Controllers/ProductsController.cs` | Thêm `GET /api/products/{id}/qr` trả `FileResult` PNG (gọi `_qrCodeService.GenerateProductQRCode(id, tenantId)`) |
| 2.2 | `2_Gateway/Controllers/ProductsController.cs` (hoặc tạo mới nếu chưa có) | Forward `GET /api/products/{id}/qr` → ShopERP |
| 2.3 | `5_WebApps/ShopERP/Pages/Products/Index.razor` (hoặc admin product list) | Thêm button "Tải QR" link tới endpoint |
| 2.4 | Tạo `5_WebApps/KhachLink/Services/QrCodeService.cs` (client-side HTTP service) | Hoặc **XÓA** khỏi task card section 3 (planning leftover) |

**Verify:**
- `curl GET /api/products/{id}/qr` → trả PNG image, Content-Type `image/png`
- Mở PNG → QR scan được bằng app scanner
- Scan → `QRCodePayload.FromJson` parse đúng → add to cart

---

### BATCH 3 — Push NATS Subscriber thật (C4) — 2-3 giờ

**Mục tiêu:** Thay stub `SubscribeToNatsAsync` bằng `IHostedService` subscribe thật

| Task | File | Action |
|---|---|---|
| 3.1 | `3_CoreHub/Services/PushNotificationBackgroundService.cs` (CREATE) | `class PushNotificationBackgroundService : IHostedService` — subscribe NATS `order.status.changed`, gọi `PushNotificationService.SendOrderStatusNotificationAsync` |
| 3.2 | `5_WebApps/ShopERP/Program.cs` | `services.AddHostedService<PushNotificationBackgroundService>();` |
| 3.3 | `3_CoreHub/Services/PushNotificationService.cs` line 171-187 | Xóa `SubscribeToNatsAsync` stub (đã thay bằng BackgroundService) HOẶC keep nhưng mark `[Obsolete]` |
| 3.4 | Verify NATS event flow | `OrderWorkflowService.PublishOrderStatusChangedEventAsync` → NATS → BackgroundService → `SendOrderStatusNotificationAsync` → WebPush |

**Verify:**
- Trigger order status change → push notification đến subscriber đã subscribe
- Log: "Push notifications sent: N/M for OrderId:..."

**Dependency:** BATCH 1.4 (PushNotificationService DI) phải done trước

---

### BATCH 4 — Remove SignalR from KhachLink (H1) — 1 giờ

**Mục tiêu:** KhachLink zero WebSocket connections (exit criteria W4)

| Task | File | Action |
|---|---|---|
| 4.1 | `5_WebApps/KhachLink/Program.cs` line 112 | Xóa `builder.Services.AddSignalR();` |
| 4.2 | `5_WebApps/KhachLink/Program.cs` line 176 | Xóa `MapHub<DashboardHub>` (nếu có) |
| 4.3 | `5_WebApps/KhachLink/Hubs/DashboardHub.cs` | XÓA file |
| 4.4 | `5_WebApps/KhachLink/Services/Dashboard/RealTimeDashboardService.cs` | Refactor sang HTTP polling (giống `OrderTracking.razor`) HOẶC xóa nếu không dùng |
| 4.5 | `5_WebApps/KhachLink/Components/Dashboard/RealTimeDashboard.razor` line 512-534 | Thay `HubConnection` bằng `PeriodicTimer` polling 10s |

**Verify:**
- `dotnet build` → 0 errors
- KhachLink runtime: không còn WebSocket connection trong browser Network tab
- Dashboard vẫn update (via polling)

**Risk:** Staff dashboard có thể cần realtime更强 → giữ SignalR chỉ cho ShopERP Kitchen Display, KhachLink staff dùng polling

---

### BATCH 5 — Cleanup Dead Code (H2, H3) — 30 phút

| Task | File | Action |
|---|---|---|
| 5.1 | `5_WebApps/KhachLink/Components/IdentityUpgradeModal.razor` | Wire vào `Home.razor` hoặc `CartDrawer.razor` (hiện sau đơn đầu tiên) HOẶC xóa nếu W17-T1 login flow đã đủ |
| 5.2 | `5_WebApps/KhachLink/Services/CartService.cs` line 95-100 | Xóa `AddFromQrCodeAsync` (dead code, duplicate `AddItemAsync`) HOẶC sửa `Scan.razor` line 116 gọi nó thay `AddItemAsync` |

**Verify:**
- `dotnet build` → 0 errors
- Grep `AddFromQrCodeAsync` → 0 reference (nếu xóa) HOẶC 1 reference từ Scan.razor (nếu wire)

---

### BATCH 6 — Security & Supply Chain (M1, M3) — 30 phút

| Task | File | Action |
|---|---|---|
| 6.1 | `5_WebApps/KhachLink/Components/App.razor` line 13 | Pin version: `<script src="https://unpkg.com/html5-qrcode@2.3.8/dist/html5-qrcode.min.js" integrity="sha384-..." crossorigin="anonymous"></script>` (tính SRI hash) |
| 6.2 | `5_WebApps/KhachLink/wwwroot/service-worker.js` line 99-108 | Xóa hardcode fallback menu, thay bằng `Response(JSON.stringify({error:'Offline mode'}), {status:503})` |

**Verify:**
- Browser Network tab: html5-qrcode load từ pinned version
- Offline mode: trả 503 thay vì demo data

---

### BATCH 7 — Unit Tests (M2) — 2-3 giờ

| Task | File | Action |
|---|---|---|
| 7.1 | `6_Tests/VanAn.Unit.Tests/Services/QrCodeServiceTests.cs` (CREATE) | Test `GenerateProductQRCode` trả byte[] non-empty, header PNG (`\x89PNG`) |
| 7.2 | `6_Tests/VanAn.Unit.Tests/DTOs/QRCodePayloadTests.cs` (CREATE) | Test `ToJson/FromJson` round-trip, null/invalid JSON, timestamp expiry |
| 7.3 | `6_Tests/VanAn.Unit.Tests/Services/CustomerRecommendationServiceTests.cs` (CREATE) | Test frequency algorithm với mock OrderItems, cache hit/miss, empty history fallback |
| 7.4 | `6_Tests/VanAn.Unit.Tests/Services/PushNotificationServiceTests.cs` (CREATE) | Mock `IPushSubscriptionRepository` + `WebPushClient`, verify payload format, VAPID details |

**Verify:**
- `dotnet test` → all new tests PASS
- Coverage cho 4 service/DTO critical path

---

### BATCH 8 — Tech Debt W18 (M4, M5, L1) — DEFERRED (cần approval riêng)

| Task | File | Action | Note |
|---|---|---|---|
| 8.1 | `5_WebApps/ShopERP/Controllers/LoyaltyController.cs` line 57-65 | Move `CalcTier`/`GetNextTierThreshold` sang config section `Loyalty:Tiers` | W18-TD3 |
| 8.2 | `5_WebApps/KhachLink/Components/GoogleMaps.razor` + `Pages/StoreFinder.razor` | Merge 2 component thành 1 | W18-TD5 |
| 8.3 | `3_CoreHub/Services/CustomerRecommendationService.cs` line 134-139 | `InvalidateAllCache` — track cache keys trong `ConcurrentBag<string>` để remove thật | L1 |

---

### BATCH 9 — Doc Cleanup (L2, L3) — 15 phút

| Task | File | Action |
|---|---|---|
| 9.1 | `docs/AI/tasks/khachlink_improvements_master_plan.md` | Update section 9/10 checkbox `[x]` cho item đã done, HOẶC thêm note "See MASTER_FIX_PLAN_all_gaps.md" |
| 9.2 | `docs/AI/project_state.md` | Update Maintenance Log: branch `fix/khachlink-all-gaps`, list batch completed |

---

## 3. EXECUTION ORDER & DEPENDENCIES

```
BATCH 1 (DI) ─┬─→ BATCH 2 (QR endpoint) ─→ BATCH 5 (cleanup H3)
              ├─→ BATCH 3 (NATS subscriber)
              └─→ BATCH 4 (SignalR remove) ─→ BATCH 5 (cleanup H2)

BATCH 6 (security) — độc lập
BATCH 7 (tests) — sau BATCH 1-5
BATCH 8 (tech debt) — DEFERRED
BATCH 9 (doc) — cuối cùng
```

**Total effort estimate:** BATCH 1-7 ≈ 1-2 ngày làm việc
**BATCH 8:** defer (cần approval riêng theo governance "no scope expansion")

---

## 4. VERIFICATION GATES

Sau mỗi batch:
- [ ] `dotnet build VanAn.sln` → 0 errors
- [ ] `guard-check.ps1` → PASS
- [ ] Không regression (test liên quan vẫn PASS)
- [ ] Commit với format: `[FIX-BATCH-N] <mô tả>`

Sau tất cả batch 1-7:
- [ ] `dotnet test` → all PASS
- [ ] Manual smoke test: QR scan end-to-end, recommendation API, push notification, dashboard polling
- [ ] Update `docs/AI/project_state.md` section 11

---

## 5. RISK & MITIGATION

| Risk | Mitigation |
|---|---|
| `PushNotificationService` throw khi VAPID env var missing ở dev | Fallback đọc `appsettings.Development.json`, hoặc wrap DI registration với `try/catch` + log warning |
| Remove SignalR break staff dashboard | Refactor `RealTimeDashboard.razor` sang polling TRƯỚC khi xóa SignalR |
| `CustomerRecommendationService` DI conflict (CoreHub vs ShopERP scope) | Đăng ký ở `ShopERP/Program.cs` (host), không phải CoreHub (class lib) |
| NATS subscriber fail silently | Log error + health check endpoint `GET /api/health/push` |

---

## 6. APPROVAL

- [ ] User approve master fix plan
- [ ] User approve branch `fix/khachlink-all-gaps`
- [ ] User approve BATCH 8 (tech debt) hay defer
- [ ] Mode switch: REVIEW_ONLY → IMPLEMENT

**Pending user decision.** Không sửa code cho đến khi approve.
