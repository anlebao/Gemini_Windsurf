# Task Card #1: OrderType DELIVERY Support — Checkout UI + Command + Domain

> **Status:** PLANNED (awaiting implementation approval)
> **Priority:** P1 — chặn toàn bộ shipper delivery flow
> **Created:** 2026-09-15
> **Master plan:** `docs/AI/tasks/community_commerce_fixes/master_plan.md`
> **Prerequisite:** RV community commerce (2026-09-14) — 3 bugs đã fix, issue này còn lại
> **Effort:** 1-2 ngày (~8h)

## Problem

Checkout hardcode `OrderType = "TAKEAWAY"` (Checkout.razor:529). `CreateOrderCommand` không có field `OrderType` → bị bỏ qua. `Order.Create()` default `"DINEIN"`. `CommunityOrderService` filter `OrderType == "DELIVERY"` → không khớp đơn hàng nào → shipper không thấy đơn để nhận.

**Root cause chain:**
```
Checkout.razor:529     → OrderType = "TAKEAWAY" (hardcode, không có selector)
CreateOrderCommand.cs  → KHÔNG có OrderType field (field bị bỏ qua hoàn toàn)
Order.Create()         → default "DINEIN" (Domain.cs:1506)
CommunityOrderService  → .Where(o => o.OrderType == "DELIVERY") → 0 orders
```

**Evidence (RV 2026-09-14):**
- Shipper nearby-orders API trả `[]` cho đơn hàng tạo qua checkout
- Phải SQL INSERT trực tiếp `OrderType='DELIVERY'` để test shipper flow
- Shipper accept + pickup + deliver + complete workflow đều PASS khi có DELIVERY order

## Solution

Thêm OrderType selector vào Checkout UI + truyền qua Command → Domain:

1. **Checkout.razor**: Thêm OrderType selector (radio/dropdown: DINEIN / TAKEAWAY / DELIVERY). Khi DELIVERY → yêu cầu DeliveryAddress, hiển thị ShippingFee. Default = DINEIN.
2. **CreateOrderCommand.cs**: Thêm `public string? OrderType { get; set; }` + `DeliveryAddress`, `DeliveryLat`, `DeliveryLng`, `ShippingFee`
3. **PublicOrdersController.cs**: Truyền `OrderType = request.OrderType` vào CreateOrderCommand
4. **OrderService.cs**: Sau `Order.Create()`, gọi `order.SetOrderType(command.OrderType)` nếu không null
5. **Domain.cs Order entity**: Thêm `public void SetOrderType(string orderType)` — protected setter, validate against allowed values

## Scope Checklist

### Phase A — Domain + Command + Service (Day 1)
- [ ] A1: `1_Shared/Domain.cs` — Add `SetOrderType(string orderType)` method to Order entity (validate DINEIN/TAKEAWAY/DELIVERY, throw on invalid)
- [ ] A2: `3_CoreHub/Commands/CreateOrderCommand.cs` — Add `OrderType`, `DeliveryAddress`, `DeliveryLat`, `DeliveryLng`, `ShippingFee` fields
- [ ] A3: `2_Gateway/Controllers/PublicOrdersController.cs` — Pass `OrderType = request.OrderType` + delivery fields into CreateOrderCommand (line ~279)
- [ ] A4: `3_CoreHub/Services/OrderService.cs` — After `Order.Create()` (line ~849), call `order.SetOrderType(command.OrderType)` if not null + set delivery fields
- [ ] A5: `2_Gateway/Controllers/PublicOrdersController.cs` — Add `DeliveryAddress`, `DeliveryLat`, `DeliveryLng`, `ShippingFee` to `CheckoutOrderRequest` DTO
- [ ] A6: `dotnet build VanAn.sln` PASS

### Phase B — Checkout UI (Day 1-2)
- [ ] B1: `5_WebApps/KhachLink/Pages/Checkout.razor` — Add OrderType selector (radio buttons or dropdown)
- [ ] B2: When DELIVERY selected → show DeliveryAddress input (required) + ShippingFee display
- [ ] B3: Replace hardcoded `OrderType = "TAKEAWAY"` (line 529) with selected value
- [ ] B4: Pass `DeliveryAddress`, `DeliveryLat`, `DeliveryLng`, `ShippingFee` in orderRequest
- [ ] B5: `dotnet build VanAn.sln` PASS

### Phase C — Tests (Day 2)
- [ ] C1: Unit test — `Order.SetOrderType()` accepts valid values, rejects invalid
- [ ] C2: Unit test — `CreateOrderFromCommandAsync` with OrderType=DELIVERY creates order with correct type
- [ ] C3: Integration test — Checkout with DELIVERY → order appears in `CommunityOrderService.GetNearbyOrdersAsync()`
- [ ] C4: All existing tests PASS (no regression)

### Phase D — Deploy + RV (Day 2)
- [ ] D1: `dotnet build VanAn.sln -c Release` → 0 errors
- [ ] D2: CI pipeline PASS
- [ ] D3: CD pipeline PASS — deployed to production
- [ ] D4: RV — Checkout with DELIVERY → order created with OrderType=DELIVERY
- [ ] D5: RV — Shipper nearby-orders API returns the DELIVERY order
- [ ] D6: RV — Shipper accept + pickup + deliver + complete workflow PASS
- [ ] D7: RV — Existing DINEIN/TAKEAWAY checkout still works (no regression)

## Prerequisites

- 3 previous bug fixes deployed (commits `f39c8649`, `88f3496f`, `6fe17d31`)
- Test customers + roles exist in production DB (from RV 2026-09-14)
- `CommunityOrderService.GetNearbyOrdersAsync` filter `OrderType == "DELIVERY"` already in place

## Verification

1. **Build:** `dotnet build VanAn.sln -c Release` → 0 errors
2. **Unit tests:** `dotnet test` → all PASS
3. **Deploy:** CD pipeline → production
4. **RV checkout DELIVERY:**
   - Login as customer → checkout → select DELIVERY → enter address → place order
   - API: `GET /api/community/nearby-orders` → order appears with `orderType: "DELIVERY"`
5. **RV shipper flow:**
   - Login as shipper → nearby-orders → see DELIVERY order → accept → pickup → deliver → complete
6. **RV regression:**
   - Checkout with DINEIN (default) → order created with OrderType=DINEIN
   - Checkout with TAKEAWAY → order created with OrderType=TAKEAWAY

## Files to modify

| File | Change | Lines |
|---|---|---|
| `1_Shared/Domain.cs` | Add `SetOrderType()` method to Order | ~10 lines |
| `3_CoreHub/Commands/CreateOrderCommand.cs` | Add OrderType + delivery fields | ~5 lines |
| `2_Gateway/Controllers/PublicOrdersController.cs` | Pass OrderType + delivery fields to command | ~5 lines |
| `3_CoreHub/Services/OrderService.cs` | Call `order.SetOrderType()` after Create | ~5 lines |
| `5_WebApps/KhachLink/Pages/Checkout.razor` | Add OrderType selector + delivery fields | ~30 lines |

## Risks

| # | Risk | Mitigation |
|---|---|---|
| R1 | SetOrderType() vi phạm Domain purity | Chỉ protected setter + validation, không business logic — same pattern as SetCustomerNotes |
| R2 | Break existing TAKEAWAY orders | Default = DINEIN (current Domain default), TAKEAWAY still supported |
| R3 | DeliveryAddress/Lat/Lng not persisted | Check OrderConfiguration has column mapping (already exists per schema check) |
| R4 | ShippingFee calculation missing | Phase 1: client-side fixed fee. Phase 2: server-side calculation (defer) |

## Related

- Master plan: `docs/AI/tasks/community_commerce_fixes/master_plan.md`
- Task card #2: `docs/AI/tasks/community_commerce_fixes/task_card_02_nats_sync.md`
- Source: `5_WebApps/KhachLink/Pages/Checkout.razor` (line 529)
- Source: `3_CoreHub/Commands/CreateOrderCommand.cs`
- Source: `1_Shared/Domain.cs` (line 1506, 1618)
- Source: `3_CoreHub/Services/CommunityOrderService.cs` (line 28)
- RV script: `.devin/rv-cc-full-otp.js`
