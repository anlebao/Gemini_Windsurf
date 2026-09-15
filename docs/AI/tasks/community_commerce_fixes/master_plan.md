# Community Commerce Fixes — Master Plan

**Created:** 2026-09-15
**Status:** PLANNED (awaiting implementation approval)
**Branch target:** `main`
**Source:** RV community commerce full flow (2026-09-14) — OTP bypass test phát hiện 4 issues chặn production flow

## Problem

### Hiện trạng (verify 2026-09-14 qua OTP bypass RV)

Community commerce flow đã implement đầy đủ (salesman QR, customer checkout, owner confirm, shipper delivery, chat, GPS tracking). Tuy nhiên, RV production test phát hiện 4 issues chặn end-to-end flow:

```
Luồng mong muốn:
Salesman tạo QR → Customer scan + đặt hàng (DELIVERY) → Owner confirm → Shipper nhận + giao → Customer track + chat

Luồng thực tế (RV 2026-09-14):
✅ Salesman QR API works (qrUrl + compositeCode generated)
❌ Checkout hardcode TAKEAWAY → order không bao giờ là DELIVERY → shipper không thấy đơn
❌ Order không sync NATS → ShopERP → owner không thấy đơn để confirm
⚠️ GPS headless → map không render (test-only, không ảnh hưởng production)
⚠️ X-Dev-OTP unconditional → security risk (nhưng cần cho bypass test)
```

### Root cause analysis

| # | Issue | Root cause | Layer | Production impact |
|---|---|---|---|---|
| 1 | OrderType hardcode TAKEAWAY | `Checkout.razor:529` hardcode + `CreateOrderCommand` thiếu field `OrderType` + `Order.Create()` không nhận orderType param | UI → Command → Domain | **CAO** — chặn toàn bộ shipper flow |
| 2 | NATS sync không đến ShopERP | Outbox event published OK (Status=2, RoutingKey đúng), nhưng ShopERP `OrderSyncSubscriber` không log gì → subscriber không nhận event | Infra/Network | **TRUNG BÌNH** — owner không confirm đơn online |
| 3 | GPS headless Playwright | Playwright không có GPS thật, Blazor WASM JS interop không trigger mock geolocation | Test tooling | **THẤP** — chỉ ảnh hưởng RV, không ảnh hưởng production |
| 4 | X-Dev-OTP unconditional | `CustomerIdentityController.cs:63` set `X-Dev-OTP` header mà không gate `IsDevelopment()` | Security | **CAO** (security) — nhưng **chưa fix** vì cần cho bypass test |

### Evidence (RV 2026-09-14)

**Issue #1 — OrderType chain:**
```
Checkout.razor:529  → OrderType = "TAKEAWAY" (hardcode)
CreateOrderCommand  → không có OrderType field (bỏ qua)
Order.Create()      → default "DINEIN"
CommunityOrderService:28 → .Where(o => o.OrderType == "DELIVERY") → 0 orders
```

**Issue #2 — NATS sync evidence:**
```
PostgreSQL OutboxMessages:
  EventType=OrderCreated, Status=2 (processed), RoutingKey=9e94f876-... (đúng)
ShopERP SHOP_INSTANCE_ID=9e94f876-... (khớp)
ShopERP OrderSyncSubscriber logs: TRỐNG (không có "connected to NATS" hay error nào)
```

**Issue #3 — GPS headless:**
```
Playwright context.setGeolocation({lat, lng}) + permissions: ['geolocation']
→ Blazor WASM vananPWA.getCurrentPosition() return null
→ DeliveryTracking page (đã fix commit 88f3496f) không block, chỉ ẩn map
```

**Issue #4 — X-Dev-OTP:**
```csharp
// CustomerIdentityController.cs:63
Response.Headers["X-Dev-OTP"] = otp;  // ← KHÔNG gate IsDevelopment()
// Comment line 51 nói "In dev (IsDevelopment)" nhưng code không có gate
```

## Solution

### Issue #1: OrderType DELIVERY support (P1 — fix trước)

Thêm OrderType selector vào Checkout UI + truyền qua Command → Domain:

```
Checkout.razor → OrderType selector (DINEIN/TAKEAWAY/DELIVERY)
  → CreateOrderCommand.OrderType (new field)
  → PublicOrdersController truyền vào command
  → OrderService.CreateOrderFromCommandAsync → order.SetOrderType()
  → Order entity: SetOrderType() method (protected setter)
  → CommunityOrderService filter OrderType == "DELIVERY" → match
```

### Issue #2: NATS sync debug + fix (P2)

Debug theo thứ tự:
1. Check ShopERP SQLite: `SELECT COUNT(*) FROM Orders` — đơn hàng đã đến chưa?
2. Nếu đến → UI filter issue (ShopERP Orders/Index.razor default filter)
3. Nếu chưa → check NATS connectivity từ ShopERP → Gateway
4. Nếu NATS OK → thêm structured logging + restart OrderSyncSubscriber

### Issue #3: GPS mock cho Playwright (P3 — test-only)

Inject mock JS function trước khi navigate:
```javascript
await page.addInitScript(() => {
    window.vananPWA = window.vananPWA || {};
    window.vananPWA.getCurrentPosition = () =>
        Promise.resolve({ Lat: 10.966, Lng: 106.594 });
});
```
Không đổi production code.

### Issue #4: X-Dev-OTP gate (P4 — DEFER, ghi nhận)

**Chưa fix** — cần X-Dev-OTP cho bypass test. Ghi nhận để fix khi:
- Có alternative test auth mechanism (dev token endpoint, staging env)
- Hoặc khi security review yêu cầu

Fix plan (khi cần):
```csharp
// Inject IHostEnvironment _env vào constructor
if (_env.IsDevelopment())
    Response.Headers["X-Dev-OTP"] = otp;
```

## Phases

### Phase 1 — OrderType DELIVERY (Issue #1) — P1
- Task card: `task_card_01_order_type.md`
- 4 files sửa, ~30 dòng
- Build + test + deploy + RV

### Phase 2 — NATS sync debug + fix (Issue #2) — P2
- Task card: `task_card_02_nats_sync.md`
- Debug infra trước, fix code sau
- Có thể là network/firewall issue, không phải code

### Phase 3 — GPS mock Playwright (Issue #3) — P3
- Task card: `task_card_03_gps_mock.md`
- 1 file test sửa, 0 file production
- Nhanh, low-risk

### Phase 4 — X-Dev-OTP gate (Issue #4) — P4 DEFER
- Task card: `task_card_04_dev_otp_gate.md`
- **Chưa implement** — ghi nhận để fix sau khi có alternative test auth
- 1 file sửa, 3 dòng khi implement

## Hard stops (governance)

- **Domain PURE** — `SetOrderType()` chỉ là protected setter, không thêm business logic
- **No new .csproj** — sửa existing files
- **Gateway = Order Creator + Routed Async Delivery (Option C)**
- **Multi-tenancy enforced**
- **KhachLink boundary** — chỉ HTTP via Gateway, không query DB
- **X-Dev-OTP fix DEFER** — không fix cho đến khi có alternative test auth

## Risks

| # | Risk | Mitigation |
|---|---|---|
| R1 | OrderType selector break existing TAKEAWAY orders | Default = DINEIN (current default), TAKEAWAY vẫn support |
| R2 | SetOrderType() vi phạm Domain purity | Chỉ protected setter, không business logic — same pattern as SetCustomerNotes |
| R3 | NATS issue là network/firewall, không phải code | Debug infra trước, không fix code mù |
| R4 | GPS mock không match production behavior | Chỉ cho RV, production user có GPS thật |
| R5 | Fix X-Dev-OTP break bypass test | DEFER — chỉ fix khi có alternative |

## Related

- Task card #1: `docs/AI/tasks/community_commerce_fixes/task_card_01_order_type.md`
- Task card #2: `docs/AI/tasks/community_commerce_fixes/task_card_02_nats_sync.md`
- Task card #3: `docs/AI/tasks/community_commerce_fixes/task_card_03_gps_mock.md`
- Task card #4: `docs/AI/tasks/community_commerce_fixes/task_card_04_dev_otp_gate.md`
- RV script: `.devin/rv-cc-full-otp.js`
- Previous commits: `f39c8649`, `88f3496f`, `6fe17d31` (3 bugs đã fix trong RV 2026-09-14)
- Source: `5_WebApps/KhachLink/Pages/Checkout.razor`, `3_CoreHub/Commands/CreateOrderCommand.cs`, `1_Shared/Domain.cs`
