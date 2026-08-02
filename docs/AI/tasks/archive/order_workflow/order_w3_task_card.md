# TASK CARD — Order Lifecycle Wave 3: Payment Confirm UI (Admin + KhachLink)

> **Status:** 📋 PLANNING — awaiting user review
> **Prerequisite:** W2 merged (Admin Orders UI) · **Branch:** `feature/order-w3-payment-confirm-ui`
> **Estimated sessions:** 1-2
> **Gaps fixed:** G5 (QrPaymentModal không confirm payment), G6 (Không UI Admin confirm payment)

## Objective

**Option B (D2 approved):** Admin ShopERP manually xác nhận "Đã nhận tiền" sau khi kiểm tra tài khoản ngân hàng.

2 entry points:
1. **Admin ShopERP** — nút "Xác nhận đã nhận tiền" trong `Orders/Detail.razor` (W2) → `POST api/webhooks/payment`
2. **KhachLink** — nút "Tôi đã thanh toán" trong `QrPaymentModal.razor` → `POST api/webhooks/payment` (optional, khách tự báo)

Sau confirm → `OrderService.ConfirmPaymentAsync` → `PaymentStatus = "Paid"` + accounting entries → `OrderHub.PaymentConfirmed` broadcast (W0).

## Architecture Decision (D2, R6 RESOLVED)

- **Option B:** Manual confirm (không bank webhook tự động)
- **R6 RESOLVED:** Order có TenantId (kế thừa từ Product → Cart → Order). KhachLink có `order.TenantId.Value` sau khi tạo order → CÓ THỂ tự confirm payment
- **2 entry points (cả 2 đều tự confirm):**
  - **Admin ShopERP** — nút "Xác nhận đã nhận tiền" (admin verify bank rồi bấm)
  - **KhachLink** — nút "Tôi đã thanh toán" (khách tự bấm sau khi chuyển khoản) → tự confirm qua `POST api/webhooks/payment`
- **TenantId flow:** Product.TenantId → CartItem (cần thêm) → Order.TenantId → KhachLink đọc `order.TenantId.Value`

## Prerequisites (to verify in INVESTIGATE)

- [ ] W2 merged — `Orders/Detail.razor` exists with order detail
- [ ] W0 merged — `OrderService.ConfirmPaymentAsync` broadcasts `PaymentConfirmed`
- [ ] `2_Gateway/Controllers/WebhookController.cs:115-154` — `POST api/webhooks/payment` exists (AllowAnonymous)
- [ ] `3_CoreHub/Services/OrderService.cs:550-586` — `ConfirmPaymentAsync` exists (idempotent)
- [ ] `1_Shared/Domain.cs:1038-1047` — `Order.ConfirmPayment(transactionId, paymentMethod)` exists
- [ ] `5_WebApps/KhachLink/Components/QrPaymentModal.razor` — has QR display + "Đóng" button only
- [ ] `5_WebApps/KhachLink/Pages/Checkout.razor:138-153` — creates order, captures response (need to verify TenantId in response)
- [ ] `5_WebApps/KhachLink/Models/ProductDto.cs:6` — `TenantId` property exists (Product → Cart → Order chain)
- [ ] `1_Shared/Domain/CartItem.cs` — record does NOT have TenantId (gap — but Order has TenantId after creation)
- [ ] `PaymentConfirmRequest` DTO: `{ OrderId, TenantId, TransactionId }` (WebhookController.cs:212)

## Open Questions

| Q | Question | Default answer |
|---|----------|----------------|
| Q1 | KhachLink "Tôi đã thanh toán" có tự confirm không? | **YES** — KhachLink có `order.TenantId.Value` (Order kế thừa từ Product) → gửi đầy đủ `{OrderId, TenantId, TransactionId}` |
| Q2 | TransactionId cho manual confirm: auto-generate hay admin nhập? | Auto-generate: `MANUAL_{orderId}_{timestamp}` (admin) / `KL_{orderId}_{timestamp}` (KhachLink) |
| Q3 | Nút "Xác nhận nhận tiền" hiện khi nào? | Khi `PaymentStatus == "Pending"` (chưa paid) |
| Q4 | Cần thêm `TenantId` vào `CartItem`? | **YES** — hiện `CartItem` record (1_Shared/Domain/CartItem.cs) KHÔNG có TenantId. Cần thêm để KhachLink biết tenant khi tạo order. Tuy nhiên Order sau khi tạo đã có TenantId → OrderTracking/QrPaymentModal dùng `order.TenantId.Value` |

## Files to Modify (estimated 3 files)

| File | Action | Lines |
|------|--------|-------|
| `5_WebApps/ShopERP/Components/Pages/Orders/Detail.razor` | UPDATE — add "Xác nhận đã nhận tiền" button + confirm modal | +40 lines |
| `5_WebApps/KhachLink/Components/QrPaymentModal.razor` | UPDATE — add "Tôi đã thanh toán" button → self-confirm via `POST api/webhooks/payment` | +35 lines |
| `5_WebApps/KhachLink/Pages/Checkout.razor` | VERIFY — ensure `order.TenantId` is captured after order creation (for QrPaymentModal) | +5 lines |

## Detailed Task List

### W3-T1: Admin "Xác nhận đã nhận tiền" (ShopERP Orders/Detail.razor)

```razor
<!-- Orders/Detail.razor — add after order info section -->
@if (order?.PaymentStatus == "Pending")
{
    <VanAnButton Variant="ButtonVariant.Primary" OnClick="ConfirmPayment" data-testid="btn-confirm-payment">
        💰 Xác nhận đã nhận tiền
    </VanAnButton>
}

@code {
    private async Task ConfirmPayment()
    {
        var http = HttpClientFactory.CreateClient("shoperp");
        var request = new
        {
            OrderId = orderId,
            TenantId = GetTenantId(),
            TransactionId = $"MANUAL_{orderId}_{DateTime.UtcNow:yyyyMMddHHmmss}"
        };
        var response = await http.PostAsJsonAsync("api/webhooks/payment", request);
        if (response.IsSuccessStatusCode)
        {
            // Reload order to show PaymentStatus = "Paid"
            await LoadOrder();
        }
    }
}
```

**Note:** ShopERP calls its own `api/webhooks/payment` via YARP → Gateway → ShopERP OrdersController. Or call Gateway directly. Verify routing in INVESTIGATE.

### W3-T2: KhachLink "Tôi đã thanh toán" (QrPaymentModal.razor — self-confirm)

```razor
<!-- QrPaymentModal.razor — add to Footer, next to "Đóng" -->
@if (!string.IsNullOrEmpty(QrImageUrl))
{
    <VanAnButton Variant="ButtonVariant.Primary" OnClick="ConfirmPayment" data-testid="btn-confirm-payment">
        ✅ Tôi đã thanh toán
    </VanAnButton>
}

@code {
    [Parameter] public Guid TenantId { get; set; }  // NEW — passed from parent (Checkout/OrderTracking)

    private async Task ConfirmPayment()
    {
        // KhachLink CÓ TenantId (từ Order.TenantId.Value — Order kế thừa từ Product)
        var http = HttpClientFactory.CreateClient("gateway");
        var request = new
        {
            OrderId = Guid.Parse(OrderId),
            TenantId = TenantId,  // ← KhachLink có TenantId từ Order
            TransactionId = $"KL_{OrderId}_{DateTime.UtcNow:yyyyMMddHHmmss}"
        };
        var response = await http.PostAsJsonAsync("api/webhooks/payment", request);
        if (response.IsSuccessStatusCode)
        {
            // Payment confirmed → OrderHub.PaymentConfirmed broadcast (W0)
            // KhachLink OrderTracking sẽ detect PaymentStatus=Paid qua polling
            if (OnPaymentCompleted.HasDelegate)
                await OnPaymentCompleted.InvokeAsync();
        }
    }
}
```

**Parent component (Checkout.razor hoặc OrderTracking.razor) phải truyền TenantId:**
```razor
<QrPaymentModal IsOpen="@showQrModal"
                OrderId="@currentOrderId"
                TenantId="@currentTenantId"  ← NEW
                Amount="@orderTotal"
                OnPaymentCompleted="HandlePaymentCompleted" />
```

**Note:** `currentTenantId` lấy từ `order.TenantId.Value` sau khi tạo order thành công (Checkout.razor line 153).

### W3-T3: Verify Checkout.razor captures TenantId after order creation

```csharp
// Checkout.razor — after order creation success (line 153+)
// BEFORE: chỉ navigate to OrderTracking
// AFTER: capture TenantId from response, pass to QrPaymentModal

var response = await http.PostAsJsonAsync("api/orders", orderRequest);
if (response.IsSuccessStatusCode)
{
    var result = await response.Content.ReadFromJsonAsync<OrderCreationResponse>();
    currentOrderId = result?.Id ?? Guid.Empty;
    currentTenantId = result?.TenantId ?? Guid.Empty;  // ← Capture TenantId
    // Show QR payment modal with TenantId
    showQrModal = true;
}

// DTO for response
private record OrderCreationResponse(Guid Id, string Status, Guid TenantId);
```

### W3-T4: Build + verify

- `dotnet build VanAn.sln` — 0 errors
- Admin: open Order Detail → click "Xác nhận đã nhận tiền" → PaymentStatus = "Paid"
- KhachLink: open QrPaymentModal → click "Tôi đã thanh toán" → modal closes, admin gets notification
- SignalR: `PaymentConfirmed` event fires → Dashboard updates

## Verification Checklist

- [ ] Build 0 errors
- [ ] Admin "Xác nhận đã nhận tiền" button visible only when `PaymentStatus == "Pending"`
- [ ] Admin click → `POST api/webhooks/payment` → `PaymentStatus = "Paid"` + accounting entries generated
- [ ] KhachLink "Tôi đã thanh toán" → `POST api/webhooks/payment` với `{OrderId, TenantId, TransactionId}` → self-confirm
- [ ] KhachLink `QrPaymentModal` nhận `TenantId` parameter từ parent (Checkout/OrderTracking)
- [ ] `Checkout.razor` captures `TenantId` từ order creation response → truyền cho `QrPaymentModal`
- [ ] Idempotent: clicking twice → second call is noop (existing `ConfirmPaymentAsync` guard)
- [ ] SignalR `PaymentConfirmed` broadcast fires (W0 wiring) → ShopERP Dashboard + KhachLink polling detect
- [ ] UI Platform components used (VanAnButton)
- [ ] TransactionId format: `MANUAL_{orderId}_{timestamp}` (admin) / `KL_{orderId}_{timestamp}` (KhachLink)

## Rollback Plan

Remove "Xác nhận đã nhận tiền" button from Detail.razor. Remove "Tôi đã thanh toán" button from QrPaymentModal. Remove `payment-notify` endpoint. Build passes.
