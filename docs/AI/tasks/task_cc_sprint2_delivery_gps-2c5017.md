# TASK CARD: Community Commerce — Sprint 2 — Delivery Workflow + GPS Tracking

## 1. GOAL & CONTEXT
- **Mục tiêu cốt lõi:** Shipper cập nhật trạng thái giao hàng (PickedUp → OutForDelivery → Delivered/Failed) + GPS location real-time qua SignalR.
- **Nghiệp vụ áp dụng:** UC-05 (Delivery status) + UC-06 (GPS tracking) từ requirements spec.
- **Status:** NOT STARTED
- **Branch:** `feature/community-sprint2-delivery-gps`

---

## 2. ACTIVE WORKFLOW ROUTING
- **Target Workflow:** `newfeaturebuild.md` (ANALYZE → IMPLEMENT)
- **Execution Mode:** IMPLEMENT
- **Current Phase:** Sprint 2 of 7
- **Dependency:** Sprint 1 COMPLETE (nearby orders + accept working)

---

## 3. RELEVANT FILES (CONTEXT BOUNDARY)

### Files cần CREATE
- `2_Gateway/Hubs/LocationHub.cs` — SignalR hub for GPS tracking
- `3_CoreHub/Services/IDeliveryWorkflowService.cs` — interface
- `3_CoreHub/Services/DeliveryWorkflowService.cs` — state machine + location update
- `5_WebApps/KhachLink/Services/LocationTrackingService.cs` — GPS polling service
- `5_WebApps/KhachLink/Pages/DeliveryTracking.razor` — shipper delivery page
- `5_WebApps/KhachLink/Pages/OrderTracking.razor` — customer tracking page
- `5_WebApps/KhachLink/Components/LeafletMap.razor` — interactive map component
- `5_WebApps/KhachLink/wwwroot/js/leaflet.js` — Leaflet JS interop
- `6_Tests/VanAn.Core.Tests/DeliveryWorkflowServiceTests.cs`
- `6_Testing/e2e-tests/community-delivery-flow.spec.ts`

### Files cần MODIFY
- `2_Gateway/Controllers/CommunityController.cs` — add pickup/delivering/delivered/failed/location endpoints
- `2_Gateway/Program.cs` — DI + SignalR hub mapping
- `5_WebApps/KhachLink/Program.cs` — DI for LocationTrackingService
- `5_WebApps/KhachLink/Pages/NearbyOrders.razor` — link to DeliveryTracking page after accept
- `3_CoreHub/Services/OrderWorkflowService.cs` — hook: DeliveryTask.Delivered → Order.Completed

### Files READ ONLY
- `2_Gateway/Hubs/OrderHub.cs` — SignalR hub pattern
- `2_Gateway/Hubs/KitchenHub.cs` — SignalR group pattern
- `3_CoreHub/Services/OrderWorkflowService.cs` — TransitionStatusAsync pattern
- `5_WebApps/KhachLink/Components/GoogleMaps.razor` — map component reference (replace with Leaflet)

### Boundary Rules
- KHÔNG sửa Domain.cs — DeliveryTask state machine đã có từ Sprint 0
- KHÔNG tạo Chat hub — Sprint 3
- GPS polling 10s interval, chỉ khi tab active
- Leaflet.js (open-source) thay Google Maps iframe
- Order.Completed khi DeliveryTask.Delivered — qua OrderWorkflowService hook
- SignalR LocationHub: shipper push → customer subscribe order_{orderId}

---

## 4. TECHNICAL & REGULATORY CONSTRAINTS
- [ ] **State machine:** DeliveryTask transitions validate trong Domain (Sprint 0) — service chỉ gọi methods
- [ ] **SignalR:** LocationHub — join group `order_{orderId}`, push `LocationUpdate` event
- [ ] **GPS polling:** 10s interval, `navigator.geolocation.watchPosition` hoặc `setInterval` + `getCurrentPosition`
- [ ] **Leaflet:** CDN import, không npm install (PWA Blazor WASM)
- [ ] **Order sync:** Delivered → OrderWorkflowService.TransitionStatusAsync(completed) — qua NATS hoặc direct
- [ ] **UI Platform:** DeliveryTracking + OrderTracking dùng VanAnButton, VanAnCard

---

## 5. SUCCESS CRITERIA
- [ ] **SC1:** POST `/api/community/orders/{id}/pickup` → DeliveryTask.PickedUp, timestamp set
- [ ] **SC2:** POST `/api/community/orders/{id}/delivering` → DeliveryTask.OutForDelivery
- [ ] **SC3:** POST `/api/community/orders/{id}/delivered` → DeliveryTask.Delivered + Order.Completed
- [ ] **SC4:** POST `/api/community/orders/{id}/failed` → DeliveryTask.Failed + reason
- [ ] **SC5:** POST `/api/community/location/update` → DeliveryTracking append + SignalR push
- [ ] **SC6:** LocationHub SignalR: shipper join → customer receive location update
- [ ] **SC7:** LeafletMap component hiển thị marker (shop + shipper + customer)
- [ ] **SC8:** GPS polling 10s khi tab active, dừng khi Delivered/Failed
- [ ] **SC9:** Unit tests ≥8 cases pass
- [ ] **SC10:** `dotnet build` 0 errors + `guard-check.ps1` pass
- [ ] **SC11:** E2E test: accept → pickup → delivering → delivered
- [ ] **SC12:** Architecture tests pass

**Branch:** `feature/community-sprint2-delivery-gps`

---

## 6. ACTIVE SKILLS (MAX 3)
- `domain-integrity-validation` — DeliveryTask state machine validation
- `accounting-ui-implementation` — KhachLink UI + Leaflet integration
- `build-error-analysis` — SignalR + JS interop errors

---

## 7. AI HEALTH CHECK MATRIX (INITIAL)
- **Evidence Count:** 7
- **Verified Facts:**
  - Fact 1: `OrderHub.cs` — SignalR hub pattern: Groups.AddToGroupAsync, group naming `Shop_{id}`
  - Fact 2: `KitchenHub.cs` — same pattern, group naming `shop_{id}`, `order_{id}`
  - Fact 3: `OrderWorkflowService.TransitionStatusAsync` — validates transition, updates order, saves
  - Fact 4: DeliveryTask entity (Sprint 0) has MarkPickedUp/MarkOutForDelivery/MarkDelivered/MarkFailed
  - Fact 5: `GoogleMaps.razor` — iframe embed (replace with Leaflet)
  - Fact 6: `StoreFinder.razor` — JS interop for geolocation
  - Fact 7: `Order.UpdateOrderStatus(status)` — method to set status
- **Assumptions:**
  - Leaflet.js via CDN works in Blazor WASM
  - SignalR from Blazor WASM works (already used for OrderHub)
- **Open Questions:**
  - Q1: Order.Completed trigger — direct call OrderWorkflowService hay qua NATS event?
  - Q2: GPS polling — `watchPosition` hay `setInterval(getCurrentPosition, 10000)`?
- **Recommended Action:** PROCEED — Assumptions (2) < Facts (7), Open Questions (2) < 3
