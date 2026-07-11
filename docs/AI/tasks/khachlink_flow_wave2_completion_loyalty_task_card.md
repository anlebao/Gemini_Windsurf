# TASK CARD: KhachLink Full Flow — Wave 2 — Completion + Loyalty + Customer Confirm

## 1. GOAL & CONTEXT
- **Mục tiêu cốt lõi:** (1) KhachLink nút "Xác nhận đã nhận hàng". (2) Loyalty flow bypass khi toggle OFF. (3) PWA disable cho logged-in users. (4) Accounting sync bypass khi toggle OFF.
- **Nghiệp vụ áp dụng:** Section 4 (Giai đoạn 3) của `Tai_lieu_yeu_cau_nghiep_vu_Khachlink.md` v1.2
- **Status:** ⬜ NOT STARTED
- **Branch:** `feature/khachlink-flow-wave2-completion-loyalty`
- **Dependency:** Wave 1 COMPLETE (kitchen UI + polling 3s + status fix)

---

## 2. ACTIVE WORKFLOW ROUTING
- **Target Workflow:** `newfeaturebuild.md` (ANALYZE → IMPLEMENT)
- **Execution Mode:** ANALYZE → IMPLEMENT
- **Current Phase:** Wave 2 of 5
- **Dependency:** Wave 0 (toggle infrastructure) + Wave 1 (kitchen UI, status fix)

---

## 3. RELEVANT FILES (CONTEXT BOUNDARY)

### Files được phép đọc/sửa
- `docs/AI/project_state.md` (Bắt buộc đọc đầu phiên)
- `docs/AI/tasks/khachlink_full_flow_master_plan.md` (READ — master plan)
- `docs/MVP_Product/Tai_lieu_yeu_cau_nghiep_vu_Khachlink.md` (READ — requirements v1.2)

### Files cần MODIFY (KhachLink client)
- `5_WebApps/KhachLink/Pages/OrderTracking.razor` — thêm nút "Xác nhận đã nhận hàng" khi status=ready/delivered
- `5_WebApps/KhachLink/Components/IdentityUpgradeModal.razor` — ẩn khi `Loyalty_Program_Enabled` = OFF
- `5_WebApps/KhachLink/Components/PWA/PWAInstallPrompt.razor` — disable cho logged-in users

### Files cần MODIFY (ShopERP/CoreHub server)
- `5_WebApps/ShopERP/Controllers/OrdersController.cs` hoặc `OrderWorkflowController.cs` — thêm `POST /api/orders/{id}/confirm-received`
- `3_CoreHub/Services/OrderWorkflowService.cs` — thêm transition `ready→delivered` (customer confirm)
- `3_CoreHub/Services/OrderService.cs` — `ConfirmPaymentAsync` skip `GenerateAccountingEntriesAsync` khi `Accounting_Sync_Enabled` = OFF

### Files READ ONLY (investigate patterns)
- `1_Shared/Domain.cs` — check existing `Order.MarkAsCompleted()`, status transitions, `OrderStatusId.Delivering`
- `3_CoreHub/Services/IOrderService.cs` — existing methods
- `5_WebApps/KhachLink/Pages/Login.razor` — check login state detection (localStorage token)
- `5_WebApps/KhachLink/Services/CartService.cs` — localStorage pattern for logged-in check

### Boundary Rules
- KHÔNG sửa `1_Shared/Domain.cs` — dùng existing `OrderStatusId.Delivering` hoặc `Completed`
- KHÔNG tạo UI custom HTML/CSS — dùng UI Platform components
- KHÔNG inject CoreHub services vào KhachLink — dùng HTTP service
- Customer confirm: dùng status transition `ready→delivered` (existing `OrderStatusId.Delivering`) hoặc `ready→completed` — investigate domain trước

---

## 4. TECHNICAL CONSTRAINTS
- [ ] **Domain Protection:** KHÔNG sửa Domain.cs — dùng existing status transitions
- [ ] **UI Platform:** Mọi UI mới MUST dùng VanAnButton, VanAnCard
- [ ] **KhachLink HTTP-only:** Toggle check qua `ShopFeatureSettingsHttpService`
- [ ] **Loyalty bypass:** Khi `Loyalty_Program_Enabled` = OFF, ẩn IdentityUpgradeModal + OTP + PWA prompt, show "Cảm ơn quý khách"
- [ ] **Accounting bypass:** Khi `Accounting_Sync_Enabled` = OFF, `ConfirmPaymentAsync` skip `GenerateAccountingEntriesAsync`
- [ ] **PWA disable:** Khi user đã đăng nhập (có customer token trong localStorage), không show PWAInstallPrompt

---

## 5. SUCCESS CRITERIA
- [ ] **SC1:** KhachLink OrderTracking hiển thị nút "Xác nhận đã nhận hàng" khi status=ready
- [ ] **SC2:** API `POST /api/orders/{id}/confirm-received` hoạt động (transition ready→delivered hoặc ready→completed)
- [ ] **SC3:** Khi `Loyalty_Program_Enabled` = OFF, IdentityUpgradeModal không hiển thị, show "Cảm ơn quý khách"
- [ ] **SC4:** Khi `Loyalty_Program_Enabled` = OFF, OTP flow + PWA prompt không hiển thị
- [ ] **SC5:** PWAInstallPrompt không hiển thị cho user đã đăng nhập (có customer token)
- [ ] **SC6:** Khi `Accounting_Sync_Enabled` = OFF, `ConfirmPaymentAsync` không gọi `GenerateAccountingEntriesAsync`
- [ ] **SC7:** Build: 0 errors
- [ ] **SC8:** guard-check.ps1 pass
- [ ] **SC9:** Architecture Tests pass

---

## 6. DETAILED IMPLEMENTATION

### 6.1. ANALYZE Phase (trước khi code)

**Cần investigate:**
1. **Domain status:** Đọc `1_Shared/Domain.cs` lines 422-433 — check `OrderStatusId.Delivering` và `OrderStatusId.Completed`. Xác định transition `ready→delivered` hay `ready→completed` cho customer confirm.
2. **OrderTracking.razor:** Đọc lines 440-464 — hiểu `IdentityUpgradeModal` trigger logic. Xác định chỗ thêm nút "Xác nhận đã nhận hàng".
3. **IdentityUpgradeModal.razor:** Đọc full file — hiểu khi nào hiển thị, cách ẩn khi toggle OFF.
4. **PWAInstallPrompt.razor:** Đọc lines 272-277 — hiểu dismiss flag logic. Xác định chỗ thêm logged-in check.
5. **Login.razor:** Đọc lines 155-157 — hiểu customer token storage (localStorage key name).
6. **OrderService.cs:** Đọc `ConfirmPaymentAsync` (lines 574-617) — xác định chỗ thêm accounting toggle check.
7. **OrderWorkflowService.cs:** Check `IsTransitionValidAsync` — `ready→delivered` có valid không? Nếu không, cần thêm.

### 6.2. Customer Confirm Receipt (W2-T1, W2-T2, W2-T3)

**W2-T1: KhachLink nút "Xác nhận đã nhận hàng"**

**File:** `5_WebApps/KhachLink/Pages/OrderTracking.razor`

Thêm button khi status=ready:
```razor
@if (order?.Status?.Value == "ready")
{
    <VanAButton Variant="success" OnClick="ConfirmReceived" data-testid="btn-confirm-received">
        ✅ Xác nhận đã nhận hàng
    </VanAButton>
}
```

**Method:** Gọi API `POST shoperp/api/orders/{orderId}/confirm-received` qua HTTP service.

**W2-T2: API endpoint**

**File:** `5_WebApps/ShopERP/Controllers/OrderWorkflowController.cs`

```csharp
[HttpPost("{orderId:guid}/confirm-received")]
public async Task<ActionResult> ConfirmReceived(Guid orderId)
{
    Order? order = await _workflowService.TransitionStatusAsync(orderId, OrderStatusId.Delivering, null);
    if (order == null) return NotFound();
    return Ok();
}
```

**Note:** Investigate xem `ready→delivered` có valid trong state machine không. Nếu không, thêm vào `validTransitions`.

**W2-T3: Domain check**

Đọc `1_Shared/Domain.cs` — nếu `ready→delivered` chưa trong state machine, thêm vào `OrderWorkflowService.IsTransitionValidAsync`:
```csharp
["ready"] = ["completed", "delivered", "cancelled"],
["delivered"] = ["completed", "cancelled"],
```

### 6.4. Loyalty Bypass (W2-T4)

**File:** `5_WebApps/KhachLink/Pages/OrderTracking.razor`

```razor
@inject ShopFeatureSettingsHttpService FeatureSettings

// In OnInitializedAsync:
var settings = await FeatureSettings.GetSettingsAsync(tenantId);
loyaltyEnabled = settings?.Loyalty_Program_Enabled ?? true;

// In render:
@if (order?.Status?.Value == "delivered" || order?.Status?.Value == "completed")
{
    @if (loyaltyEnabled)
    {
        <IdentityUpgradeModal />
    }
    else
    {
        <VanAAlert Variant="success" Message="Cảm ơn quý khách! 🙏" />
    }
}
```

### 6.5. PWA Disable for Logged-in Users (W2-T5)

**File:** `5_WebApps/KhachLink/Components/PWA/PWAInstallPrompt.razor`

```csharp
// In OnInitializedAsync:
// Check if user is logged in (customer token in localStorage)
string? customerToken = await JS.InvokeAsync<string>("localStorage.getItem", "customer_token");
isLoggedIn = !string.IsNullOrEmpty(customerToken);

// In render:
@if (!isLoggedIn && !pwaDismissed)
{
    // Show PWA install prompt
}
```

### 6.6. Accounting Sync Bypass (W2-T6)

**File:** `3_CoreHub/Services/OrderService.cs`

Trong `ConfirmPaymentAsync` (lines 574-617), thêm toggle check trước `GenerateAccountingEntriesAsync`:

```csharp
// Existing line 607:
// await GenerateAccountingEntriesAsync(order, ...);

// New:
bool accountingEnabled = await _settingsService.IsEnabledAsync(
    order.TenantId.Value, nameof(ShopFeatureSettingsDto.Accounting_Sync_Enabled));
if (accountingEnabled)
{
    await GenerateAccountingEntriesAsync(order, ...);
}
else
{
    _logger.LogInformation("Accounting sync disabled for tenant {TenantId} — skipping entry generation", order.TenantId.Value);
}
```

**Note:** Cần inject `IShopFeatureSettingsService` vào `OrderService` constructor. Investigate existing constructor.

---

## 7. AI HEALTH CHECK MATRIX

### Pre-ANALYZE
- **Evidence Count:** 4
- **Verified Facts:**
  - Fact 1: `OrderTracking.razor` không có nút customer confirm (subagent A)
  - Fact 2: `IdentityUpgradeModal` hiển thị khi order delivered (lines 11-14, 444-451) (subagent A)
  - Fact 3: `PWAInstallPrompt.razor` có dismiss flag localStorage (lines 272-277) nhưng không check login state (subagent A)
  - Fact 4: `OrderService.ConfirmPaymentAsync` (lines 574-617) gọi `GenerateAccountingEntriesAsync` line 607 (subagent B)
- **Assumptions:**
  - Assumption 1: `OrderStatusId.Delivering` tồn tại trong domain (verified — subagent C)
  - Assumption 2: `ready→delivered` transition có thể cần thêm vào state machine (Cần verify)
- **Open Questions:**
  - Q1: `ready→delivered` có valid trong state machine hiện tại không?
  - Q2: `OrderService` constructor hiện tại inject gì? Có cần thêm `IShopFeatureSettingsService`?
  - Q3: Customer token localStorage key name là gì? (`customer_token`? — verify từ Login.razor)
- **Gate check:** Assumptions (2) < Verified Facts (4) → ✅ OK để proceed IMPLEMENT sau khi verify Q1-Q3

---

## 8. REVERSE IMPACT ANALYSIS
| File thay đổi | Reverse impact | Mitigation |
|---|---|---|
| `OrderTracking.razor` (confirm button + loyalty bypass) | Medium — UI behavior change | Test cả 2 cases (toggle ON/OFF) |
| `IdentityUpgradeModal.razor` (hide when toggle OFF) | Low — conditional render | None |
| `PWAInstallPrompt.razor` (login check) | Low — additive condition | None |
| `OrderWorkflowController.cs` (confirm-received endpoint) | Low — new endpoint | None |
| `OrderWorkflowService.cs` (ready→delivered transition) | Low — additive transition | None |
| `OrderService.cs` (accounting bypass) | Medium — business logic change | Test accounting sync ON/OFF |

---

## 9. EXECUTION CHECKLIST

### ANALYZE Phase
- [ ] Read `1_Shared/Domain.cs` lines 422-433 — verify `OrderStatusId.Delivering` + state machine
- [ ] Read `OrderTracking.razor` lines 440-464 — IdentityUpgradeModal trigger
- [ ] Read `IdentityUpgradeModal.razor` full
- [ ] Read `PWAInstallPrompt.razor` lines 272-277
- [ ] Read `Login.razor` lines 155-157 — customer token localStorage key
- [ ] Read `OrderService.cs` constructor + `ConfirmPaymentAsync`
- [ ] Read `OrderWorkflowService.cs` — `ready→delivered` valid?
- [ ] Update Health Check Matrix

### IMPLEMENT Phase
- [ ] W2-T1: OrderTracking "Xác nhận đã nhận hàng" button
- [ ] W2-T2: API confirm-received endpoint
- [ ] W2-T3: State machine ready→delivered (if needed)
- [ ] W2-T4: Loyalty bypass (hide IdentityUpgradeModal, show "Cảm ơn")
- [ ] W2-T5: PWA disable for logged-in users
- [ ] W2-T6: Accounting sync bypass in ConfirmPaymentAsync
- [ ] W2-T7: Build + guard-check.ps1 + Architecture Tests

### Post-IMPLEMENT
- [ ] Commit: `[KL WAVE 2] Customer confirm + loyalty bypass + accounting bypass`
- [ ] Update `project_state.md` (if user requests)
