# TASK CARD: Community Commerce — Sprint 1 — Shipper Nearby Orders + Accept + Customer Login Simplify (v1.5)

## 1. GOAL & CONTEXT
- **Mục tiêu cốt lõi:** (A) Shipper thấy đơn DELIVERY trong bán kính 5km + nhận đơn (accept) với concurrency safety. (B) **v1.5 NEW (CC-S1-T0c):** Customer login simplify — xóa SMS OTP khỏi Login.razor primary flow, rewrite IdentityUpgradeModal thành 3 buttons (Google + Facebook + Guest=skip).
- **Nghiệp vụ áp dụng:** UC-03 (Nearby Orders) + UC-04 (Accept Order) + **UC-01 v1.5 (Customer login simplify)** từ requirements spec.
- **Status:** NOT STARTED
- **Branch:** `feature/community-sprint1-nearby-orders`

---

## 2. ACTIVE WORKFLOW ROUTING
- **Target Workflow:** `newfeaturebuild.md` (ANALYZE → IMPLEMENT)
- **Execution Mode:** IMPLEMENT
- **Current Phase:** Sprint 1 of 7
- **Dependency:** Sprint 0 COMPLETE (entities + migration applied)

---

## 3. RELEVANT FILES (CONTEXT BOUNDARY)

### Files cần CREATE
- `2_Gateway/Controllers/CommunityController.cs` — nearby orders + accept endpoints
- `3_CoreHub/Services/ICommunityOrderService.cs` — interface
- `3_CoreHub/Services/CommunityOrderService.cs` — Haversine + accept logic
- `5_WebApps/KhachLink/Services/Http/CommunityHttpService.cs` — HTTP client
- `5_WebApps/KhachLink/Pages/NearbyOrders.razor` — Shipper nearby orders page
- `6_Tests/VanAn.Core.Tests/CommunityOrderServiceTests.cs` — unit tests
- `6_Testing/e2e-tests/community-nearby-orders.spec.ts` — E2E test

### Files cần MODIFY
- `2_Gateway/Program.cs` — DI registration for CommunityOrderService
- `5_WebApps/KhachLink/Program.cs` — DI registration for CommunityHttpService
- `1_Shared/Domain.cs` — Order: add `AssignShipper()` method (F2 fix — ShipperId field có từ Sprint 0 nhưng chưa có domain method để set)
- `1_Shared/Domain.cs` — Order: add `SetDeliveryLocation(double lat, double lng)` method (F2 fix — DeliveryLat/DeliveryLng fields có từ Sprint 0 nhưng chưa có domain method để set. Cần set khi shipper accept đơn DELIVERY)
- **v1.3 NEW — Domain Modification (CC-S1-T0):** `1_Shared/Domain.cs` — `OrderStatuses.Default[]` add `"delivering"` OrderStatusDefinition (Sequence=5, DisplayName="Đang giao", RequiresInventoryDeduction=false) + shift "completed"→Sequence=6, "cancelled"→Sequence=7. **Status hiện:** `OrderStatusId.Delivering` (Domain.cs:429) ĐÃ TỒN TẠI nhưng `OrderStatuses.Default[]` (Domain.cs:458-508) CHỈ có 6 trạng thái — KHÔNG có "delivering". Cần add.
- **v1.3 NEW — Domain Modification (CC-S1-T0):** `3_CoreHub/Services/OrderWorkflowService.cs` — `IsTransitionValidAsync` (line 411-440) add "delivering" vào validTransitions: `["ready"] = ["completed", "cancelled", "delivered", "delivering"]` + `["delivering"] = ["completed", "cancelled", "delivered"]`. **Status hiện:** transitions có "delivered" nhưng KHÔNG có "delivering" → shipper accept đơn `ready` không thể chuyển sang `delivering`.
- **v1.3 NEW — UI Modification:** `5_WebApps/KhachLink/Components/Layout/NavMenu.razor` — add community tabs (Nearby Orders, Wallet, Sales Dashboard) cho shipper/salesman role. Conditional display based on CommunityRole.

### Files cần MODIFY — v1.5 NEW (CC-S1-T0c: Customer Login Simplify)
- **`5_WebApps/KhachLink/Pages/Login.razor`** — xóa SMS OTP khỏi primary flow: remove `LoginStep.Otp` enum value, remove `_phone`/`_otp` fields, remove `SendOtp()` + `VerifyOtp()` methods, remove SĐT input form + OTP input form. Giữ `LoginStep.Phone` (rename→`LoginStep.Choice`) + `LoginStep.Success`. UI mới: Google button + Facebook button + "Tiếp tục as Guest" button (NavigateTo `/`). OAuth callback handler giữ nguyên (Google token từ URL query). **Lưu ý:** KHÔNG xóa OTP endpoints (`/api/customer-identity/otp/*`) — giữ cho collaborator verification (Sprint 6 toggle).
- **`5_WebApps/KhachLink/Components/IdentityUpgradeModal.razor`** — REWRITE: thay OTP flow (Intro→OtpSent→Success) bằng 3 buttons layout. Modal title "Nâng cấp tài khoản" giữ. Body mới: Google button (redirect `/api/auth/google/login`) + Facebook button (redirect `/api/auth/facebook/login`) + "Bỏ qua" button (OnDismiss). Xóa `SendUpgradeOtp` + `VerifyUpgradeOtp` + `_otp`/`_phoneSuffix`/`_upgradeStep` state. Giữ `ShowModal`/`OnDismiss`/`OnUpgradeComplete` params. **Kịch bản:** Modal show sau khi đơn hàng hoàn tất (Checkout.razor `_showLoyaltySignupModal=true`) → khách chọn 1 trong 3: Google/Facebook (login + link order) hoặc Guest (skip, order vẫn hợp lệ, tích điểm qua DeviceId fallback).
- **`5_WebApps/KhachLink/Services/Http/SocialAuthHttpService.cs`** — xóa `SendUpgradeOtpAsync` + `VerifyUpgradeOtpAsync` methods (không còn dùng sau khi IdentityUpgradeModal rewrite). Giữ các method khác.
- **`5_WebApps/ShopERP/Controllers/SocialAuthController.cs`** — add `GET /api/auth/facebook/login` + `GET /api/auth/facebook/callback` (Facebook OAuth flow, tương tự Google). **Status hiện:** chỉ có Google login/callback. Facebook controller CHƯA có (spec v1.3 AC-01.2 yêu cầu). Nếu Facebook OAuth credentials chưa setup → tạo stub redirect với warning log (Sprint 7+ sẽ config real credentials).

### Files READ ONLY
- `2_Gateway/Controllers/OrdersController.cs` — controller pattern reference
- `3_CoreHub/Services/OrderWorkflowService.cs` — workflow pattern reference
- `5_WebApps/KhachLink/Pages/StoreFinder.razor` — GPS + radius search pattern
- `2_Gateway/Program.cs` — auth policy patterns

### Boundary Rules
- KHÔNG sửa OrderWorkflowService — chỉ thêm mới CommunityOrderService
- KHÔNG tạo SignalR hub trong Sprint 1 — Sprint 2
- Nearby orders query: Gateway PG (Orders source of truth theo Option C)
- Accept: tạo DeliveryTask + set Order.ShipperId (optimistic concurrency)
- KhachLink: HTTP only, không inject DbContext

---

## 4. TECHNICAL & REGULATORY CONSTRAINTS
- [ ] **Cross-tenant query:** Nearby orders query KHÔNG filter by TenantId (shipper thấy đơn từ nhiều shop)
- [ ] **Concurrency:** Double-accept → 409 Conflict. DB unique index trên DeliveryTask(OrderId) WHERE Status IN (Assigned, PickedUp, OutForDelivery)
- [ ] **Auth:** `X-Customer-Token` header — resolve CustomerId → check CommunityRole(Shipper, Active)
- [ ] **Haversine:** Distance calculation trong C# (không push xuống SQL)
- [ ] **UI Platform:** NearbyOrders.razor dùng VanAnButton, VanAnCard — không custom HTML
- [ ] **GPS:** `navigator.geolocation` getCurrentPosition (1 lần, không continuous)

---

## 5. SUCCESS CRITERIA
- [ ] **SC1:** GET `/api/community/nearby-orders?lat={lat}&lng={lng}&radiusKm=5` trả đơn DELIVERY status `confirmed`/`ready` trong bán kính
- [ ] **SC2:** Mỗi đơn có: shopName, deliveryAddress, totalAmount, status, distanceKm
- [ ] **SC3:** Sort theo distanceKm tăng dần
- [ ] **SC4:** POST `/api/community/orders/{orderId}/accept` tạo DeliveryTask + set Order.ShipperId + Order.SetDeliveryLocation() (F2 fix — Sprint 0 tạo ShipperId/DeliveryLat/DeliveryLng fields nhưng thiếu domain methods)
- [ ] **SC5:** Double-accept → 409 Conflict
- [ ] **SC6:** Accept đơn đã Delivered/Failed → 409
- [ ] **SC7:** KhachLink NearbyOrders page hiển thị list + nút "Nhận đơn"
- [ ] **SC8:** Unit tests ≥8 cases pass (Haversine, accept, concurrency, auth check)
- [ ] **SC9:** `dotnet build` 0 errors + `guard-check.ps1` pass
- [ ] **SC10:** E2E test: shipper login → nearby → accept → order detail
- [ ] **SC11:** Architecture tests pass
- [ ] **SC12:** OTP login regression pass (OTP endpoints vẫn hoạt động — không xóa)
- [ ] **SC13 (v1.5 NEW — CC-S1-T0c):** Login.razor KHÔNG còn SĐT input + OTP input + SendOtp/VerifyOtp methods. Chỉ có Google button + Facebook button + "Tiếp tục as Guest" button.
- [ ] **SC14 (v1.5 NEW — CC-S1-T0c):** IdentityUpgradeModal hiển thị 3 buttons (Google + Facebook + "Bỏ qua") thay vì OTP flow. Modal show sau checkout success (Checkout.razor `_showLoyaltySignupModal=true`).
- [ ] **SC15 (v1.5 NEW — CC-S1-T0c):** Guest button → NavigateTo `/` (không token, không CustomerId). Checkout vẫn hoạt động (form guest có sẵn). Order history qua `CustomerDeviceId`. Tích điểm qua DeviceId fallback (Bug 6 fix).
- [ ] **SC16 (v1.5 NEW — CC-S1-T0c):** Facebook login endpoint `GET /api/auth/facebook/login` tồn tại (stub hoặc real OAuth flow). UI button redirect đúng.
- [ ] **SC17 (v1.5 NEW — CC-S1-T0c):** OTP endpoints `/api/customer-identity/otp/send` + `/otp/verify` + `/upgrade/send-otp` + `/upgrade/verify-otp` VẪN trả 200 (không xóa — giữ cho Sprint 6 collaborator verification toggle).
- [ ] **SC18 (v1.5 NEW — CC-S1-T0c):** Checkout flow KHÔNG có login chen ngang — khách đặt hàng trực tiếp, modal "Nâng cấp tài khoản" chỉ show SAU khi đơn hàng hoàn tất.

**Branch:** `feature/community-sprint1-nearby-orders`

---

## 6. ACTIVE SKILLS (MAX 3)
- `domain-integrity-validation` — DeliveryTask creation, Order.ShipperId assignment
- `accounting-ui-implementation` — KhachLink UI patterns
- `build-error-analysis` — Fix API/controller errors

---

## 7. AI HEALTH CHECK MATRIX (INITIAL)
- **Evidence Count:** 8
- **Verified Facts:**
  - Fact 1: `OrdersController.cs` — pattern: [Authorize(Policy="RequireTenantAccess")], tenantId from claims
  - Fact 2: `OrderWorkflowService.TransitionStatusAsync` — status transition pattern
  - Fact 3: `StoreFinder.razor` — GPS via `IJSRuntime`, radius filter pattern
  - Fact 4: `Order.OrderType` includes "DELIVERY"
  - Fact 5: `OrderStatusId` has "confirmed", "ready", "delivering" states
  - Fact 6: `TenantSettings` has Latitude/Longitude for shop location
  - Fact 7: `CustomerIdentityController` — X-Customer-Token header pattern
  - Fact 8: Sprint 0 entities (DeliveryTask, CommunityRole) exist in Domain.cs
- **Assumptions:**
  - Nearby orders query PG directly (Gateway has IVanAnDbContext)
  - Shipper auth via X-Customer-Token (same as customer auth)
- **Open Questions:**
  - Q1: Nearby orders cần join TenantSettings để lấy shop lat/lng — có performance issue không? (Likely OK cho PoC <500 orders)
  - Q2: Accept endpoint cần check shipper's CommunityRole — query CommunityRoles table?
- **Recommended Action:** PROCEED — Assumptions (2) < Facts (8), Open Questions (2) < 3
