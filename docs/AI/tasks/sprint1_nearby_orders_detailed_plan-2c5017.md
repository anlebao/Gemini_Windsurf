# Sprint 1 Detailed Plan — Nearby Orders + Accept (v1.3: +CC-S1-T0 "delivering" status + Facebook UI + NavMenu)

TDD plan (10 test cases + 3 v1.3 cases), coding plan (4 sessions), API specs, Haversine formula, UI specs, **CC-S1-T0 Domain Modification (v1.3 NEW)**.

> **v1.3 changes:**
> - **CC-S1-T0 (NEW task, FIRST in sprint):** Domain Modification — add `"delivering"` vào `OrderStatuses.Default[]` (Domain.cs:458-508) + add transition rules trong `OrderWorkflowService.IsTransitionValidAsync` (line 411-440). Status hiện: `OrderStatusId.Delivering` (Domain.cs:429) ĐÃ CÓ nhưng `Default[]` + transitions CHƯA có.
> - **Facebook login UI:** Add Facebook button vào `Login.razor` (controller đã có `SocialAuthController.cs`).
> - **NavMenu.razor community tabs:** Add Nearby Orders + Wallet + Sales Dashboard tabs (conditional on CommunityRole).

---

## 0. CC-S1-T0: "delivering" STATUS DOMAIN MODIFICATION (v1.3 NEW — FIRST task)

> **⚠️ DOMAIN MODIFICATION — requires user approval per governance.md.**
> Thay đổi: `1_Shared/Domain.cs` (OrderStatuses.Default[]) + `3_CoreHub/Services/OrderWorkflowService.cs` (IsTransitionValidAsync).

### 0.1 Current state (verified 2026-07-26)
- `OrderStatusId.Delivering` (Domain.cs:429) — **EXISTS** as constant
- `OrderStatuses.Default[]` (Domain.cs:458-508) — **6 statuses ONLY**: pending, confirmed, preparing, ready, completed, cancelled. **NO "delivering".**
- `OrderWorkflowService.IsTransitionValidAsync` (line 411-440) — has "delivered" in transitions but **NO "delivering"**:
  - `["ready"] = ["completed", "cancelled", "delivered"]` — KHÔNG có "delivering"
  - `["delivered"] = ["completed", "cancelled"]` — exists
  - **No `["delivering"]` key at all**

### 0.2 Changes cần làm

**File 1: `1_Shared/Domain.cs` (OrderStatuses.Default[])**
```csharp
// Add AFTER "ready" (Sequence=4), BEFORE "completed" (shift to 6):
new OrderStatusDefinition
{
    Id = new OrderStatusId("delivering"),
    DisplayName = "Đang giao",
    Sequence = 5,
    IsActive = true,
    RequiresInventoryDeduction = false
},
// Shift: completed → Sequence=6, cancelled → Sequence=7
```

**File 2: `3_CoreHub/Services/OrderWorkflowService.cs` (IsTransitionValidAsync)**
```csharp
// In normal kitchen flow (line 430-439), add "delivering":
["ready"] = ["completed", "cancelled", "delivered", "delivering"], // add delivering
["delivering"] = ["completed", "cancelled", "delivered"],           // NEW key
// Keep existing: ["delivered"] = ["completed", "cancelled"]

// In kitchen bypass flow (line 416-425), add "delivering":
["ready"] = ["completed", "cancelled", "delivered", "delivering"], // add delivering
["delivering"] = ["completed", "cancelled", "delivered"],           // NEW key
```

### 0.3 Test cases (3 NEW — v1.3)
| # | Test Name | What It Verifies |
|---|---|---|
| T0.1 | `OrderStatuses_Default_Contains_Delivering` (v1.3 NEW) | `OrderStatuses.Default` array contains "delivering" with Sequence=5 |
| T0.2 | `IsTransitionValid_Ready_To_Delivering_ReturnsTrue` (v1.3 NEW) | `ready` → `delivering` is valid |
| T0.3 | `IsTransitionValid_Delivering_To_Delivered_ReturnsTrue` (v1.3 NEW) | `delivering` → `delivered` is valid |

### 0.4 Session assignment
**Session S1 (FIRST):** Implement CC-S1-T0 before any other Sprint 1 work. 30 min task.

---

## 1. API SPECIFICATIONS

### 1.1 GET /api/community/nearby-orders
```
Query: lat (double), lng (double), radiusKm (int, default 5)
Header: X-Customer-Token: {token}
Auth: CustomerToken → resolve CustomerId → check CommunityRole(Shipper, Active)
Response 200: [
  {
    "orderId": "guid",
    "shopName": "string",
    "shopLat": 10.8,
    "shopLng": 106.7,
    "deliveryAddress": "string",
    "deliveryLat": 10.81,  // nullable
    "deliveryLng": 106.71, // nullable
    "totalAmount": 150000,
    "status": "ready",
    "distanceKm": 2.3
  }
]
Response 401: Missing/invalid token
Response 403: Customer doesn't have Shipper role
```

### 1.2 POST /api/community/orders/{orderId}/accept
```
Header: X-Customer-Token: {token}
Auth: CustomerToken → resolve CustomerId → check CommunityRole(Shipper, Active)
Response 200: { "deliveryTaskId": "guid", "orderId": "guid", "status": "Assigned" }
Response 409: Order already assigned or not in accept-able status
Response 404: Order not found
```

---

## 2. SERVICE SPECIFICATIONS

### 2.1 ICommunityOrderService
```csharp
public interface ICommunityOrderService
{
    Task<List<NearbyOrderDto>> GetNearbyOrdersAsync(double lat, double lng, int radiusKm, Guid shipperId);
    Task<DeliveryTask?> AcceptOrderAsync(Guid orderId, Guid shipperId);
}
```

### 2.2 CommunityOrderService Implementation
- `GetNearbyOrdersAsync`: Query Orders WHERE OrderType=DELIVERY AND Status IN (confirmed, ready) AND NOT EXISTS DeliveryTask(active). Join TenantSettings for shop lat/lng. Calculate Haversine distance. Filter by radiusKm. Sort by distance.
- `AcceptOrderAsync`: Check order exists + status. Check no active DeliveryTask. Create DeliveryTask. Set Order.ShipperId. Save with transaction.

### 2.3 Haversine Formula
```csharp
private static double CalculateHaversineKm(double lat1, double lng1, double lat2, double lng2)
{
    const double R = 6371; // Earth radius km
    var dLat = (lat2 - lat1) * Math.PI / 180;
    var dLng = (lng2 - lng1) * Math.PI / 180;
    var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
            Math.Cos(lat1 * Math.PI / 180) * Math.Cos(lat2 * Math.PI / 180) *
            Math.Sin(dLng / 2) * Math.Sin(dLng / 2);
    var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
    return R * c;
}
```

---

## 3. TDD PLAN (10 TEST CASES)

| # | Test Name | File | What It Verifies |
|---|---|---|---|
| 1 | `Haversine_SamePoint_ReturnsZero` | CommunityOrderServiceTests | Distance = 0 for same coords |
| 2 | `Haversine_KnownDistance_ReturnsCorrect` | CommunityOrderServiceTests | HCM to HN ~1080km |
| 3 | `GetNearbyOrders_FiltersByRadius` | CommunityOrderServiceTests | Orders outside radius excluded |
| 4 | `GetNearbyOrders_OnlyDeliveryType` | CommunityOrderServiceTests | DINEIN orders excluded |
| 5 | `GetNearbyOrders_OnlyConfirmedOrReady` | CommunityOrderServiceTests | Draft/completed excluded |
| 6 | `GetNearbyOrders_ExcludesAssigned` | CommunityOrderServiceTests | Orders with active DeliveryTask excluded |
| 7 | `GetNearbyOrders_SortsByDistance` | CommunityOrderServiceTests | Closest first |
| 8 | `AcceptOrder_CreatesDeliveryTask` | CommunityOrderServiceTests | DeliveryTask created, Order.ShipperId set |
| 9 | `AcceptOrder_AlreadyAssigned_ReturnsNull` | CommunityOrderServiceTests | Second accept returns null |
| 10 | `AcceptOrder_InvalidStatus_ReturnsNull` | CommunityOrderServiceTests | Draft order → null |

---

## 4. CODING PLAN — SESSION BREAKDOWN

### Session S1: Service + Unit Tests (TDD)
- Write test file FIRST (10 test cases)
- Write `ICommunityOrderService` + `CommunityOrderService`
- Haversine implementation
- Mock IVanAnDbContext in tests
- `dotnet test` — all 10 pass

### Session S2: Gateway Controller + DI
- Create `CommunityController.cs` — GET nearby-orders, POST accept
- Auth: X-Customer-Token → resolve CustomerId (reuse ICustomerTokenService)
- Add `RequireCommunityRole` check (query CommunityRoles table)
- DI registration in `Gateway/Program.cs`
- `dotnet build` — fix errors
- Integration test: controller returns correct responses

### Session S3: KhachLink UI
- Create `CommunityHttpService.cs` — HTTP calls to Gateway
- Create `NearbyOrders.razor` — GPS button + list + accept button
- GPS: `IJSRuntime` invoke `navigator.geolocation.getCurrentPosition`
- UI Platform components: VanAnButton, VanAnCard, VanAnList
- DI registration in `KhachLink/Program.cs`
- `dotnet build` — fix errors

### Session S4: E2E Test + Final
- Write `community-nearby-orders.spec.ts`
- Test flow: login → nearby orders page → GPS → see list → accept → order detail
- `guard-check.ps1` pass
- Architecture tests pass
- OTP regression pass
- Update `project_state.md`

---

## 5. UI SPEC — NearbyOrders.razor

```
@page "/community/nearby-orders"
- Header: "Đơn hàng gần bạn"
- GPS button: "Dùng vị trí của tôi" → getCurrentPosition
- Radius selector: 2km / 5km / 10km (default 5km)
- List items:
  - Shop name + distance badge
  - Delivery address
  - Total amount + status badge
  - "Nhận đơn" button (VanAnButton Primary)
- Empty state: "Không có đơn hàng trong khu vực"
- Loading state: spinner
- Error state: "Không lấy được vị trí. Vui lòng bật GPS."
```

---

## 6. VPS VERIFICATION (Sprint 1)

| # | Test | Command | Expected |
|---|---|---|---|
| RV1-1 | Nearby orders API | `curl -H 'X-Customer-Token: {token}' 'https://{VPS}/api/community/nearby-orders?lat=10.8&lng=106.7&radiusKm=5'` | 200 + JSON array |
| RV1-2 | Accept order | `curl -X POST -H 'X-Customer-Token: {token}' 'https://{VPS}/api/community/orders/{id}/accept'` | 200 + DeliveryTask |
| RV1-3 | Double accept | `curl -X POST -H 'X-Customer-Token: {token2}' .../orders/{id}/accept` | 409 Conflict |
| RV1-4 | E2E Playwright | `npx playwright test community-nearby-orders.spec.ts` | PASS |
| RV1-5 | DB check | `psql -c "SELECT * FROM \"DeliveryTasks\" WHERE \"ShipperId\" IS NOT NULL"` | ≥1 row |
