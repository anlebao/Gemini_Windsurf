# TASK CARD: KhachLink Full Flow — Wave 1 — Payment Flow + Kitchen UI + Polling 3s

## 1. GOAL & CONTEXT
- **Mục tiêu cốt lõi:** (1) UI chọn payment method (Cash/Transfer) + Processing Bar cho cash + dual status bars cho transfer. (2) ShopERP kitchen transition buttons ở Order Detail. (3) KhachLink status name fix + polling 3s. (4) Kitchen flow bypass khi toggle OFF.
- **Nghiệp vụ áp dụng:** Section 4 (Giai đoạn 2) của `Tai_lieu_yeu_cau_nghiep_vu_Khachlink.md` v1.2
- **Status:** ✅ COMPLETE — IMPLEMENT + Live RV PASS
- **Branch:** `feature/khachlink-flow-wave1-payment-kitchen-ui`
- **Dependency:** Wave 0 COMPLETE ✅ (toggle infrastructure sẵn sàng)
- **Last commit:** `0748a63` [KL WAVE 1] Payment flow + kitchen UI + polling 3s + kitchen bypass
- **Live RV:** RV1-RV10 ALL PASS (2026-07-11, Docker+ShopERP 5003+KhachLink 5002+Gateway 5001)

---

## 2. ACTIVE WORKFLOW ROUTING
- **Target Workflow:** `newfeaturebuild.md` (ANALYZE → IMPLEMENT)
- **Execution Mode:** ANALYZE → IMPLEMENT
- **Current Phase:** Wave 1 of 5
- **Dependency:** Wave 0 (toggle infrastructure — `IShopFeatureSettingsService`)

---

## 3. RELEVANT FILES (CONTEXT BOUNDARY)

### Files được phép đọc/sửa
- `docs/AI/project_state.md` (Bắt buộc đọc đầu phiên)
- `docs/AI/tasks/khachlink_full_flow_master_plan.md` (READ — master plan)
- `docs/MVP_Product/Tai_lieu_yeu_cau_nghiep_vu_Khachlink.md` (READ — requirements v1.2)

### Files cần CREATE
- `5_WebApps/KhachLink/Components/ProcessingBar.razor` — Processing Bar component cho cash payment

### Files cần MODIFY (KhachLink client)
- `5_WebApps/KhachLink/Pages/Checkout.razor` — thêm UI chọn Tiền mặt/Chuyển khoản
- `5_WebApps/KhachLink/Components/QrPaymentModal.razor` — thêm dual status bars
- `5_WebApps/KhachLink/Pages/OrderTracking.razor` — fix status name `processing`→`preparing`, polling 3s, ẩn kitchen statuses khi toggle OFF
- `5_WebApps/KhachLink/Services/CheckoutFlowState.cs` — wire PaymentMethod vào checkout logic

### Files cần MODIFY (ShopERP server)
- `5_WebApps/ShopERP/Components/Pages/Orders/Detail.razor` — thêm nút "Bắt đầu làm" (→preparing), "Sẵn sàng" (→ready), "Hoàn tất" (→completed)
- `3_CoreHub/Services/OrderWorkflowService.cs` — kitchen bypass khi `Kitchen_Workflow_Enabled` = OFF (cho phép confirmed→completed trực tiếp)

### Files READ ONLY (investigate patterns)
- `5_WebApps/ShopERP/Components/Pages/Orders/Index.razor` — existing "Xác nhận" button pattern
- `3_CoreHub/Services/IOrderWorkflowService.cs` — transition API
- `5_WebApps/ShopERP/Controllers/OrderWorkflowController.cs` — API endpoint
- `UI.Platform/Components/VanAnButton.razor` — button component
- `UI.Platform/Components/VanAnCard.razor` — card component

### Boundary Rules
- KHÔNG sửa `1_Shared/Domain.cs` — dùng existing status transitions
- KHÔNG tạo UI custom HTML/CSS — dùng UI Platform components
- KHÔNG inject `IShopFeatureSettingsService` (CoreHub) vào KhachLink — dùng `ShopFeatureSettingsHttpService`
- Kitchen bypass: chỉ thêm logic trong `OrderWorkflowService.IsTransitionValidAsync` — khi toggle OFF, cho phép `confirmed→completed`

---

## 4. TECHNICAL CONSTRAINTS
- [ ] **Domain Protection:** KHÔNG sửa Domain.cs — dùng existing `OrderStatusId` + state machine
- [ ] **UI Platform:** Mọi UI mới MUST dùng VanAnButton, VanAnCard — KHÔNG custom HTML
- [ ] **KhachLink HTTP-only:** Toggle check qua `ShopFeatureSettingsHttpService`, KHÔNG inject CoreHub service
- [ ] **Polling 3s:** `OrderTracking.razor` polling interval đổi từ 5-10s → 3s (tất cả statuses)
- [ ] **Status name fix:** `OrderTracking.razor` line 270: `"processing"` → `"preparing"` (sync với domain)
- [ ] **Kitchen bypass:** Khi `Kitchen_Workflow_Enabled` = OFF, `OrderWorkflowService` cho phép `confirmed→completed` trực tiếp (bỏ qua preparing, ready)
- [ ] **DI Checklist:** Nếu thêm service mới vào KhachLink → (1) DI trong Program.cs, (2) assertion trong KhachLinkStartupTests

---

## 5. SUCCESS CRITERIA
- [ ] **SC1:** Checkout.razor hiển thị 2 option Tiền mặt/Chuyển khoản (radio buttons hoặc toggle)
- [ ] **SC2:** Cash flow hiển thị ProcessingBar component → gửi request ShopERP
- [ ] **SC3:** Transfer flow (QrPaymentModal) hiển thị 2 thanh trạng thái: "Xử lý đơn hàng" + "Chờ thanh toán"
- [ ] **SC4:** ShopERP Order Detail có nút "Bắt đầu làm" (→preparing) khi status=confirmed
- [ ] **SC5:** ShopERP Order Detail có nút "Sẵn sàng" (→ready) khi status=preparing
- [ ] **SC6:** ShopERP Order Detail có nút "Hoàn tất" (→completed) khi status=ready
- [ ] **SC7:** Kitchen bypass: khi toggle OFF, `confirmed→completed` transition valid
- [ ] **SC8:** KhachLink OrderTracking dùng `"preparing"` (không phải `"processing"`)
- [ ] **SC9:** Polling interval = 3s
- [ ] **SC10:** KhachLink OrderTracking ẩn kitchen statuses (preparing, ready) khi toggle OFF
- [ ] **SC11:** Build: 0 errors
- [ ] **SC12:** guard-check.ps1 pass
- [ ] **SC13:** Architecture Tests 38/38 pass
- [ ] **SC14:** Live Runtime Verification PASS (RV1-RV10 trong §9 Post-IMPLEMENT) — boot ShopERP+KhachLink+Docker, test HTTP/UI thực tế

---

## 6. DETAILED IMPLEMENTATION

### 6.1. ANALYZE Phase (trước khi code)

**Cần investigate:**
1. **Checkout.razor hiện tại:** Đọc full file để hiểu layout, flow, guest form structure. Xác định chỗ thêm payment method selector.
2. **QrPaymentModal.razor hiện tại:** Đọc full file để hiểu QR display, loading spinner, "Tôi đã thanh toán" button. Xác định chỗ thêm 2 status bars.
3. **OrderTracking.razor hiện tại:** Đọc lines 260-390 để hiểu status timeline, polling logic, interval config.
4. **Orders/Detail.razor hiện tại:** Đọc full file để hiểu action buttons, ConfirmOrder method, timeline display.
5. **OrderWorkflowService.cs:** Đọc `IsTransitionValidAsync` (lines 241-256) để hiểu state machine. Xác định chỗ thêm bypass logic.
6. **ShopFeatureSettingsHttpService:** Confirm API path `shoperp/api/shop/settings/features?tenantId={id}` hoạt động.
7. **UI Platform:** Check VanAnButton variants (primary, secondary, success, warning, danger) cho kitchen buttons.

### 6.2. Payment Method Selection (W1-T1)

**File:** `5_WebApps/KhachLink/Pages/Checkout.razor`

Thêm UI chọn payment method trước nút "Đặt hàng":
```razor
<div class="payment-method-selector" data-testid="payment-method-selector">
    <h3>Hình thức thanh toán</h3>
    <div class="payment-options">
        <label class="payment-option">
            <input type="radio" name="paymentMethod" value="cash" @bind="checkoutState.PaymentMethod" />
            <span>💵 Tiền mặt</span>
        </label>
        <label class="payment-option">
            <input type="radio" name="paymentMethod" value="transfer" @bind="checkoutState.PaymentMethod" />
            <span>🏦 Chuyển khoản (VietQR)</span>
        </label>
    </div>
</div>
```

**Behavior:**
- `PaymentMethod = "cash"` → hiển thị ProcessingBar → gửi order → redirect OrderTracking
- `PaymentMethod = "transfer"` → mở QrPaymentModal → QR display + dual status bars

### 6.3. Processing Bar Component (W1-T2)

**File:** `5_WebApps/KhachLink/Components/ProcessingBar.razor` (NEW)

```razor
<div class="processing-bar" data-testid="processing-bar">
    <VanACard>
        <div class="processing-content">
            <VanASpinner />
            <h3>Đang gửi đơn hàng...</h3>
            <p>Đơn hàng của bạn đang được gửi tới quán. Vui lòng chờ trong giây lát.</p>
            <div class="progress-steps">
                <span class="step active">📋 Gửi đơn</span>
                <span class="step">☕ Pha chế</span>
                <span class="step">✅ Hoàn thành</span>
            </div>
        </div>
    </VanACard>
</div>
```

**Parameters:**
- `bool IsVisible` — show/hide
- `EventCallback OnComplete` — callback khi request thành công

**Behavior:** Hiển thị trong 2-3 giây (thời gian gửi request), sau đó redirect sang OrderTracking.

### 6.4. Dual Status Bars for Transfer (W1-T3)

**File:** `5_WebApps/KhachLink/Components/QrPaymentModal.razor`

Thay single loading spinner bằng 2 thanh trạng thái:
```razor
<div class="dual-status-bars" data-testid="dual-status-bars">
    <div class="status-bar">
        <label>Xử lý đơn hàng</label>
        <div class="progress-track">
            <div class="progress-fill @orderProcessingClass"></div>
        </div>
        <span class="status-text">@orderProcessingStatus</span>
    </div>
    <div class="status-bar">
        <label>Chờ thanh toán</label>
        <div class="progress-track">
            <div class="progress-fill @paymentStatusClass"></div>
        </div>
        <span class="status-text">@paymentStatusText</span>
    </div>
</div>
```

**States:**
- Order processing: `pending` (chờ) → `confirmed` (đã nhận) → `preparing` (đang làm) → `ready` (sẵn sàng)
- Payment: `pending` (chờ thanh toán) → `paid` (đã thanh toán)

### 6.5. ShopERP Kitchen Transition Buttons (W1-T4, W1-T5)

**File:** `5_WebApps/ShopERP/Components/Pages/Orders/Detail.razor`

Thêm buttons sau nút "Xác nhận đơn hàng" existing:

```razor
@if (order.Status?.Value == "confirmed")
{
    <VanAButton Variant="primary" OnClick="StartPreparing" data-testid="btn-start-preparing">
        ⏳ Bắt đầu làm
    </VanAButton>
}

@if (order.Status?.Value == "preparing")
{
    <VanAButton Variant="success" OnClick="MarkReady" data-testid="btn-mark-ready">
        ✅ Sẵn sàng giao
    </VanAButton>
}

@if (order.Status?.Value == "ready")
{
    <VanAButton Variant="primary" OnClick="CompleteOrder" data-testid="btn-complete">
        ✔️ Hoàn tất
    </VanAButton>
}
```

**Methods:** Gọi `PUT api/orderworkflow/{orderId}/status` với `{ Status = "preparing" }` / `"ready"` / `"completed"`.

**Kitchen bypass:** Khi `Kitchen_Workflow_Enabled` = OFF, ẩn nút "Bắt đầu làm" và "Sẵn sàng", chỉ hiển thị "Hoàn tất" khi status=confirmed.

### 6.6. Kitchen Flow Bypass (W1-T6)

**File:** `3_CoreHub/Services/OrderWorkflowService.cs`

Modify `IsTransitionValidAsync` (lines 241-256):

```csharp
public async Task<bool> IsTransitionValidAsync(OrderStatusId currentStatus, OrderStatusId newStatus)
{
    // Check kitchen toggle
    bool kitchenEnabled = await _settingsService.IsEnabledAsync(
        _currentTenantId, nameof(ShopFeatureSettingsDto.Kitchen_Workflow_Enabled));

    if (!kitchenEnabled)
    {
        // Bypass kitchen: allow confirmed→completed directly
        var bypassTransitions = new Dictionary<string, List<string>>
        {
            ["pending"] = ["confirmed", "cancelled", "completed"],
            ["confirmed"] = ["completed", "cancelled"],
            ["completed"] = [],
            ["cancelled"] = []
        };
        return bypassTransitions.GetValueOrDefault(currentStatus.Value, [])?.Contains(newStatus.Value) ?? false;
    }

    // Normal kitchen flow
    var validTransitions = new Dictionary<string, List<string>>
    {
        ["pending"] = ["preparing", "cancelled", "completed"],
        ["preparing"] = ["ready", "cancelled", "completed"],
        ["ready"] = ["completed", "cancelled"],
        ["completed"] = [],
        ["cancelled"] = []
    };
    return validTransitions.GetValueOrDefault(currentStatus.Value, [])?.Contains(newStatus.Value) ?? false;
}
```

**Note:** Cần inject `IShopFeatureSettingsService` vào `OrderWorkflowService` constructor. Investigate existing constructor để xác định cách thêm dependency.

### 6.7. KhachLink Status Name Fix + Polling 3s (W1-T7, W1-T8, W1-T9)

**File:** `5_WebApps/KhachLink/Pages/OrderTracking.razor`

**W1-T7: Status name fix (line 270):**
```razor
// BEFORE:
new { Id = "processing", DisplayName = "Đang pha chế", Sequence = 3 },
// AFTER:
new { Id = "preparing", DisplayName = "Đang chế biến", Sequence = 3 },
```

**W1-T8: Polling interval 3s (lines 377-388):**
```csharp
// BEFORE: adaptive 5-10s
// AFTER: fixed 3s for all non-final statuses
"pending" => 3,
"confirmed" => 3,
"preparing" => 3,
"ready" => 3,
"completed" => 0,
"delivered" => 0,
"cancelled" => 0
```

**W1-T9: Hide kitchen statuses when toggle OFF:**
```razor
@inject ShopFeatureSettingsHttpService FeatureSettings

// In OnInitializedAsync:
var settings = await FeatureSettings.GetSettingsAsync(tenantId);
kitchenEnabled = settings?.Kitchen_Workflow_Enabled ?? true;

// In timeline rendering:
@if (kitchenEnabled)
{
    // Show all statuses including preparing, ready
}
else
{
    // Show only: pending, confirmed, completed (skip preparing, ready)
}
```

---

## 7. AI HEALTH CHECK MATRIX

### Pre-ANALYZE
- **Evidence Count:** 5
- **Verified Facts:**
  - Fact 1: `Checkout.razor` có `CheckoutFlowState.PaymentMethod` field nhưng không có UI (subagent A)
  - Fact 2: `QrPaymentModal.razor` có single loading spinner (lines 22-26), không có dual status bars (subagent A)
  - Fact 3: `OrderTracking.razor` line 270 dùng `"processing"` thay vì `"preparing"` (subagent C)
  - Fact 4: `OrderTracking.razor` lines 377-388: polling 5-10s adaptive (subagent C)
  - Fact 5: `Orders/Detail.razor` chỉ có nút "Xác nhận" (pending→confirmed), thiếu nút preparing/ready/completed (subagent B)
- **Assumptions:**
  - Assumption 1: `OrderWorkflowService` constructor có thể inject `IShopFeatureSettingsService` (Cần verify)
  - Assumption 2: `CheckoutFlowState.PaymentMethod` là string field (Cần verify type)
- **Open Questions:**
  - Q1: `OrderWorkflowService` constructor hiện tại inject gì? Có cần thêm `IShopFeatureSettingsService`?
  - Q2: `CheckoutFlowState.PaymentMethod` là string hay enum?
- **Gate check:** Assumptions (2) < Verified Facts (5) → ✅ OK để proceed IMPLEMENT sau khi verify Q1-Q2

---

## 8. REVERSE IMPACT ANALYSIS
| File thay đổi | Reverse impact | Mitigation |
|---|---|---|
| `Checkout.razor` (thêm payment selector) | Low — additive UI | None |
| `ProcessingBar.razor` (new) | No impact — new component | None |
| `QrPaymentModal.razor` (dual status bars) | Low — UI enhancement | None |
| `OrderTracking.razor` (status fix + 3s + toggle) | Medium — polling behavior change | Test polling không quá aggressive |
| `Detail.razor` (kitchen buttons) | Low — additive buttons | None |
| `OrderWorkflowService.cs` (bypass logic) | Medium — state machine change | Architecture test sẽ catch regression |

---

## 9. EXECUTION CHECKLIST

### ANALYZE Phase
- [ ] Read `Checkout.razor` full — understand layout, flow
- [ ] Read `QrPaymentModal.razor` full — understand QR display
- [ ] Read `OrderTracking.razor` lines 260-390 — timeline + polling
- [ ] Read `Orders/Detail.razor` full — action buttons
- [ ] Read `OrderWorkflowService.cs` constructor + `IsTransitionValidAsync`
- [ ] Read `CheckoutFlowState.cs` — verify PaymentMethod type
- [ ] Update Health Check Matrix

### IMPLEMENT Phase
- [ ] W1-T1: Checkout payment method selector
- [ ] W1-T2: ProcessingBar component + cash flow
- [ ] W1-T3: QrPaymentModal dual status bars
- [ ] W1-T4: Detail.razor "Bắt đầu làm" + "Sẵn sàng" buttons
- [ ] W1-T5: Detail.razor "Hoàn tất" button
- [ ] W1-T6: OrderWorkflowService kitchen bypass
- [ ] W1-T7: OrderTracking status name fix
- [ ] W1-T8: OrderTracking polling 3s
- [ ] W1-T9: OrderTracking hide kitchen statuses when toggle OFF
- [ ] W1-T10: Build + guard-check.ps1 + Architecture Tests

### Post-IMPLEMENT
- [ ] Commit: `[KL WAVE 1] Payment flow + kitchen UI + polling 3s`
- [ ] Update `project_state.md` (if user requests)

### Live Runtime Verification (MANDATORY — see Wave 0 lesson)
> Static checks (build + architecture tests + guard-check) KHÔNG đảm bảo runtime works.
> Phải boot app + test HTTP/UI thực tế trước khi mark wave COMPLETE.

**Prerequisites:**
- [ ] Docker Desktop running (PostgreSQL 5432 + NATS 4222)
- [ ] ShopERP started on http://localhost:5003 (watch logs: migration applied + seed OK)
- [ ] KhachLink started on http://localhost:5002 (PWA loads)
- [ ] DevLogin admin trên ShopERP + customer token trên KhachLink

**RV tests (all MUST pass):**
- [ ] **RV1 — Cash payment flow:** KhachLink Checkout → chọn "Tiền mặt" → ProcessingBar hiển thị → API `POST /api/orders/{id}/confirm-payment` trả 200 → Order status `confirmed`
- [ ] **RV2 — Transfer payment flow:** KhachLink Checkout → chọn "Chuyển khoản" → QrPaymentModal hiển thị dual status bars (Xử lý đơn + Chờ thanh toán) → API confirm trả 200
- [ ] **RV3 — Kitchen buttons (toggle ON):** ShopERP Order Detail → nút "Bắt đầu làm" (pending→preparing) → nút "Sẵn sàng" (preparing→ready) → API transition 200
- [ ] **RV4 — Kitchen bypass (toggle OFF):** Set `Kitchen_Workflow_Enabled=false` qua PUT API → KhachLink OrderTracking KHÔNG hiển thị kitchen statuses (preparing/ready) → Order auto-skip confirmed→delivered
- [ ] **RV5 — Polling 3s:** Mở OrderTracking → inspect Network tab → polling interval = 3000ms (không 5s/10s)
- [ ] **RV6 — Status name fix:** KhachLink OrderTracking hiển thị `preparing` (không `processing`) — match ShopERP status
- [ ] **RV7 — EF Migration:** Nếu có entity change → `dotnet ef migrations add` + verify `MigrateAsync()` log áp dụng migration mới (không `no such table` error)
- [ ] **RV8 — LINQ translation:** Mọi query mới dùng direct property comparison (KHÔNG `EF.Property<Guid>` hay `.Value` accessor cho TenantId) — verify không `InvalidOperationException: LINQ expression could not be translated`
- [ ] **RV9 — UI Platform:** Checkout + OrderTracking + Detail.razor dùng VanAForm/VanACard/VanAButton (no custom HTML) — grep HTML source
- [ ] **RV10 — Persist:** Sau khi transition status, refresh page → status giữ nguyên (DB persist OK)
