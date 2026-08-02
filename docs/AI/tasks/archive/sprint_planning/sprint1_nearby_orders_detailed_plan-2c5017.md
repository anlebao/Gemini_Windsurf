# Sprint 1 Detailed Plan — Nearby Orders + Accept (v1.3: +CC-S1-T0 "delivering" status + Facebook UI + NavMenu) (v1.5: +CC-S1-T0c Customer Login Simplify)

TDD plan (10 test cases + 3 v1.3 cases), coding plan (4 sessions), API specs, Haversine formula, UI specs, **CC-S1-T0 Domain Modification (v1.3 NEW)**, **CC-S1-T0c Customer Login Simplify (v1.5 NEW)**.

> **v1.3 changes:**
> - **CC-S1-T0 (NEW task, FIRST in sprint):** Domain Modification — add `"delivering"` vào `OrderStatuses.Default[]` (Domain.cs:458-508) + add transition rules trong `OrderWorkflowService.IsTransitionValidAsync` (line 411-440). Status hiện: `OrderStatusId.Delivering` (Domain.cs:429) ĐÃ CÓ nhưng `Default[]` + transitions CHƯA có.
> - **Facebook login UI:** Add Facebook button vào `Login.razor` (controller đã có `SocialAuthController.cs`). **(v1.5: merged into CC-S1-T0c)**
> - **NavMenu.razor community tabs:** Add Nearby Orders + Wallet + Sales Dashboard tabs (conditional on CommunityRole).

> **v1.5 changes (NEW — CC-S1-T0c):**
> - **Customer login simplify:** Xóa SMS OTP khỏi Login.razor primary flow. Rewrite IdentityUpgradeModal từ OTP flow → 3 buttons (Google + Facebook + Guest=skip).
> - **Checkout flow không login chen ngang:** Khách đặt hàng trực tiếp (guest form có sẵn). Modal "Nâng cấp tài khoản" chỉ show SAU khi đơn hàng hoàn tất.
> - **OTP endpoints GIỮ NGUYÊN:** `/api/customer-identity/otp/*` + `/upgrade/*` không xóa — dùng cho Sprint 6 collaborator verification toggle.
> - **Facebook controller:** Add `GET /api/auth/facebook/login` + `/callback` (stub hoặc real OAuth). SocialAuthController hiện chỉ có Google.

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

## 0.5. CC-S1-T0c: CUSTOMER LOGIN SIMPLIFY (v1.5 NEW — SECOND task, after CC-S1-T0)

> **Không cần Domain Modification** — chỉ UI + Controller changes. Aligns v1.2 "SMS OTP OPTIONAL" cho customer + v1.5 UC-01 AC-01.10/AC-01.11.

### 0.5.1 Current state (verified 2026-07-29)
- **Login.razor** (5_WebApps/KhachLink/Pages/Login.razor): 3-step flow `Phone → Otp → Success`. Google button có (line 19-32). SĐT input + SendOtp (line 39-54). OTP input + VerifyOtp (line 56-82). OAuth callback handler (line 113-160).
- **IdentityUpgradeModal.razor** (5_WebApps/KhachLink/Components/): 3-step OTP flow `Intro → OtpSent → Success`. Buttons: "Nâng cấp ngay" (send OTP) → "Xác nhận OTP" → "Hoàn tất". Show sau checkout success (Checkout.razor:232-234, `_showLoyaltySignupModal=true`).
- **SocialAuthController.cs** (5_WebApps/ShopERP/Controllers/): chỉ có `GET /api/auth/google/login` + `GET /api/auth/google/callback`. **KHÔNG có Facebook.**
- **SocialAuthHttpService.cs** (5_WebApps/KhachLink/Services/Http/): có `SendUpgradeOtpAsync` + `VerifyUpgradeOtpAsync` (sẽ không còn dùng sau rewrite).
- **OTP endpoints** (CustomerIdentityController.cs): `/otp/send` + `/otp/verify` + `/upgrade/send-otp` + `/upgrade/verify-otp` — **GIỮ NGUYÊN** (Sprint 6 collaborator toggle).
- **Checkout.razor**: guest form có sẵn (line 124-186), `_isLoggedIn` check (line 285), `_showLoyaltySignupModal` (line 264). **KHÔNG sửa** — flow đã đúng (guest checkout → modal sau success).

### 0.5.2 Changes cần làm

**File 1: `5_WebApps/KhachLink/Pages/Login.razor` — xóa SMS OTP, giữ Google + thêm Facebook + Guest**
```
REMOVE:
- LoginStep.Otp enum value (giữ Phone→rename Choice, Success)
- _phone, _otp fields
- SendOtp() method
- VerifyOtp() method
- OnPhoneKeyUp, OnOtpKeyUp methods
- SĐT input form (line 39-54)
- OTP input form (line 56-82)
- CustomerIdentityResult class (nếu không còn dùng)

KEEP:
- LoginStep enum (Phone→Choice, Success)
- LoginStep.Success step UI
- Google button + LoginWithGoogle() method
- OAuth callback handler (OnAfterRenderAsync firstRender)
- RegisterDeviceFingerprintAsync (CC-S0-T3 wire-up)

ADD:
- Facebook button (redirect /api/auth/facebook/login)
- "Tiếp tục as Guest" button (NavigateTo "/")
```

**File 2: `5_WebApps/KhachLink/Components/IdentityUpgradeModal.razor` — REWRITE: 3 buttons thay OTP**
```razor
<VanAnModal Title="Nâng cấp tài khoản">
  <div class="text-center">
    <h4>Tích điểm thưởng + xem lịch sử đơn hàng</h4>
    <p class="text-muted">Đăng nhập để tích điểm và đổi quà hấp dẫn</p>
    
    <!-- Google button -->
    <VanAnButton OnClick="LoginWithGoogle">Đăng nhập với Google</VanAnButton>
    
    <!-- Facebook button -->
    <VanAnButton OnClick="LoginWithFacebook">Đăng nhập với Facebook</VanAnButton>
    
    <!-- Guest skip -->
    <VanAnButton Variant="Secondary" OnClick="OnDismiss">Bỏ qua</VanAnButton>
  </div>
</VanAnModal>

REMOVE:
- UpgradeStep enum (Intro, OtpSent, Success)
- _otp, _phoneSuffix, _successMessage, _loading, _cachedToken fields
- SendUpgradeOtp(), VerifyUpgradeOtp(), BackToIntro(), OnOtpKeyUp(), HandleUpgradeComplete() methods
- GetTokenAsync() helper
- SocialAuthHttpService inject (không còn dùng OTP methods)

KEEP:
- ShowModal, OnDismiss, OnUpgradeComplete parameters
- JSRuntime inject (cho redirect)

ADD:
- LoginWithGoogle() — redirect to /api/auth/google/login
- LoginWithFacebook() — redirect to /api/auth/facebook/login
- Inject NavigationManager + IHttpClientFactory (for gateway base URL)
```

**File 3: `5_WebApps/KhachLink/Services/Http/SocialAuthHttpService.cs` — xóa OTP methods**
```
REMOVE:
- SendUpgradeOtpAsync(token)
- VerifyUpgradeOtpAsync(token, otp)
- Related DTOs (UpgradeOtpResponse, etc. nếu không dùng chỗ khác)

KEEP:
- Các method khác (Google auth, profile, v.v.)
```

**File 4: `5_WebApps/ShopERP/Controllers/SocialAuthController.cs` — add Facebook endpoints**
```csharp
[HttpGet("facebook/login")]
public IActionResult FacebookLogin([FromQuery] string? redirectTo = null)
{
    // Stub: redirect to KhachLink login with warning (Facebook OAuth credentials chưa setup)
    // Sprint 7+ sẽ config real Facebook OAuth
    var khachLinkLoginUrl = _configuration["Google:KhachLinkLoginUrl"] ?? "http://localhost:5002/login";
    _logger.LogWarning("[FacebookAuth] Login stub — Facebook OAuth credentials not configured. Redirecting to login.");
    return Redirect($"{khachLinkLoginUrl}?error=facebook_not_configured&provider=facebook");
}

[HttpGet("facebook/callback")]
public async Task<IActionResult> FacebookCallback(...)
{
    // Stub — same pattern. Real implementation Sprint 7+.
}
```

### 0.5.3 Kịch bản Guest button (chi tiết)
```
1. Khách bấm [Tiếp tục as Guest] trên Login.razor
   → NavigateTo("/") (KHÔNG gọi API, KHÔNG tạo token)
   → localStorage KHÔNG có customer_token, customer_id
   → _isLoggedIn = false

2. Khách thêm hàng vào cart → vào Checkout.razor
   → OnAfterRenderAsync: đọc localStorage customer_token → null → _isLoggedIn = false
   → showGuestForm = true (form guest có sẵn — line 124-186)
   → KHÔNG pre-fill name/phone (không có profile)

3. Khách điền guestName + guestPhone + guestAddress → bấm [Đặt hàng]
   → SubmitGuestOrder (Checkout.razor:332)
   → Generate/reuse CustomerDeviceId (localStorage "customer_device_id")
   → customerIdForOrder = null (vì _isLoggedIn=false)
   → POST /api/orders { CustomerDeviceId, CustomerName, CustomerPhone, CustomerId: null, Items }

4. Order tạo thành công → _showLoyaltySignupModal = true (Checkout.razor:501)
   → IdentityUpgradeModal show với 3 buttons:
     - [Đăng nhập với Google] → OAuth redirect → login → link order to Customer
     - [Đăng nhập với Facebook] → (stub) redirect → login → link order
     - [Bỏ qua] → OnDismiss → order vẫn hợp lệ, tích điểm qua DeviceId fallback (Bug 6 fix)
```

### 0.5.4 Test cases (3 NEW — v1.5)
| # | Test Name | What It Verifies |
|---|---|---|
| T0c.1 | `Login_Page_NoOtpForm` (v1.5 NEW) | Login.razor render KHÔNG có SĐT input + OTP input. Chỉ có Google + Facebook + Guest buttons. |
| T0c.2 | `IdentityUpgradeModal_ShowsThreeButtons` (v1.5 NEW) | Modal render 3 buttons (Google + Facebook + "Bỏ qua"). KHÔNG có OTP input. |
| T0c.3 | `Otp_Endpoints_StillWork` (v1.5 NEW) | `POST /api/customer-identity/otp/send` + `/otp/verify` vẫn trả 200 (không xóa). Regression. |

### 0.5.5 Session assignment
**Session S1.5 (SECOND, after CC-S1-T0):** Implement CC-S1-T0c. 45 min task. Không cần Domain change.

> **v1.5 UPDATE 2026-07-29 — CC-S1-T0c COMPLETE + VPS VERIFIED:**
> - Commit `4e7d9507` on `main` (DEPLOYED + VERIFIED).
> - Login.razor REWRITE: xóa SĐT + OTP, UI mới Google + Facebook + "Tiếp tục as Guest".
> - IdentityUpgradeModal.razor REWRITE: 3 buttons thay OTP flow.
> - SocialAuthController.cs: +Facebook stub endpoints.
> - SocialAuthHttpService.cs: xóa OTP methods.
> - AuthorizationEnforcementTests.cs: +DeviceRegistrationController exempt.
> - Build 0 errors, 59 community tests PASS, 39/39 Architecture tests PASS.
> - VPS RV (2026-07-29): WASM binary verified `Guest`=10 matches + `Facebook`=1 match + `OTP`=0 (removed). API endpoints: OTP still 200 (kept), Facebook 302 (stub), Google 302, device register 401 (CC-S0-T3 regression), fingerprint JS 200. 3 curl-based "failures" were false negatives (Blazor WASM renders client-side, curl sees static HTML shell only).
> - OTP endpoints GIỮ NGUYÊN — Sprint 6 collaborator toggle dùng.

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

### Session S1: CC-S1-T0 Domain Modification (delivering status)
- Add "delivering" vào OrderStatuses.Default[] (Domain.cs)
- Add transition rules trong OrderWorkflowService.IsTransitionValidAsync
- 3 test cases (T0.1, T0.2, T0.3)
- `dotnet test` — all 3 pass

### Session S1.5: CC-S1-T0c Customer Login Simplify (v1.5 NEW)
- Rewrite Login.razor: xóa SĐT + OTP, giữ Google + thêm Facebook + Guest
- Rewrite IdentityUpgradeModal.razor: 3 buttons thay OTP flow
- Add Facebook stub endpoints trong SocialAuthController.cs
- Xóa OTP methods trong SocialAuthHttpService.cs
- 3 test cases (T0c.1, T0c.2, T0c.3)
- `dotnet build` + `dotnet test` — all pass

### Session S2: Service + Unit Tests (TDD)
- Write test file FIRST (10 test cases)
- Write `ICommunityOrderService` + `CommunityOrderService`
- Haversine implementation
- Mock IVanAnDbContext in tests
- `dotnet test` — all 10 pass

### Session S3: Gateway Controller + DI
- Create `CommunityController.cs` — GET nearby-orders, POST accept
- Auth: X-Customer-Token → resolve CustomerId (reuse ICustomerTokenService)
- Add `RequireCommunityRole` check (query CommunityRoles table)
- DI registration in `Gateway/Program.cs`
- `dotnet build` — fix errors
- Integration test: controller returns correct responses

### Session S4: KhachLink UI
- Create `CommunityHttpService.cs` — HTTP calls to Gateway
- Create `NearbyOrders.razor` — GPS button + list + accept button
- GPS: `IJSRuntime` invoke `navigator.geolocation.getCurrentPosition`
- UI Platform components: VanAnButton, VanAnCard, VanAnList
- Add NavMenu.razor community tabs (conditional on CommunityRole)
- DI registration in `KhachLink/Program.cs`
- `dotnet build` — fix errors

### Session S5: E2E Test + Final
- Write `community-nearby-orders.spec.ts`
- Test flow: login → nearby orders page → GPS → see list → accept → order detail
- `guard-check.ps1` pass
- Architecture tests pass
- OTP regression pass (endpoints still work)
- Login simplify regression (no OTP form, 3 buttons in modal)
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
| RV1-6 (v1.5) | Login page no OTP | `curl -sk https://diemthuong.khachvip.online/login` | HTML KHÔNG chứa "Gửi mã OTP" + có "Tiếp tục as Guest" |
| RV1-7 (v1.5) | OTP endpoints still work | `curl -X POST https://api.khachvip.online/api/customer-identity/otp/send -d '{"phoneNumber":"0901234567"}'` | 200 (không xóa) |
| RV1-8 (v1.5) | Facebook login endpoint | `curl -sk -o /dev/null -w '%{http_code}' https://api.khachvip.online/api/auth/facebook/login` | 302 (stub redirect) |
| RV1-9 (v1.5) | Checkout→modal flow | Playwright: guest checkout → order success → modal show 3 buttons | PASS |
