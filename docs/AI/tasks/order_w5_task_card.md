# TASK CARD — Order Lifecycle Wave 5: Tests + Sitemap Links

> **Status:** 📋 PLANNING — awaiting user review
> **Prerequisite:** W0-W4 merged · **Branch:** `feature/order-w5-tests-sitemap`
> **Estimated sessions:** 1-2
> **Gaps fixed:** None (verification + integration layer)

## Objective

1. Thêm Sitemap links cho Orders + Kitchen
2. Unit tests cho SignalR broadcast (W0)
3. Unit tests cho Kitchen → Ready transition (W1)
4. Integration tests cho payment confirm flow (W3)
5. bUnit tests cho Orders UI (W2)
6. End-to-end build verification

## Prerequisites (to verify in INVESTIGATE)

- [ ] W0-W4 all merged
- [ ] `6_Tests/VanAn.Core.Tests/` — existing test patterns
- [ ] `6_Tests/VanAn.Integration.Tests/` — existing integration test patterns
- [ ] `6_Tests/VanAn.ShopERP.Tests/` — existing bUnit test patterns
- [ ] `5_WebApps/ShopERP/Components/Pages/Sitemap.razor` — existing sitemap

## Files to Create/Modify (estimated 6 files)

| File | Action | Lines |
|------|--------|-------|
| `5_WebApps/ShopERP/Components/Pages/Sitemap.razor` | UPDATE — add Orders + Kitchen links | +10 lines |
| `6_Tests/VanAn.Core.Tests/Services/OrderWorkflowServiceTests.cs` | ADD — test NotifyOrderStatusChangedAsync called after TransitionStatusAsync | +40 lines |
| `6_Tests/VanAn.Core.Tests/Services/KitchenServiceTests.cs` | ADD — test OrderStatus=Ready when all items completed | +30 lines |
| `6_Tests/VanAn.Integration.Tests/PaymentConfirmTests.cs` | ADD — test POST api/webhooks/payment → PaymentStatus=Paid + accounting entries | +50 lines |
| `6_Tests/VanAn.ShopERP.Tests/Components/OrdersIndexTests.cs` | ADD — bUnit test Orders/Index.razor renders list + confirm button | +40 lines |
| `6_Tests/VanAn.ShopERP.Tests/Components/OrdersDetailTests.cs` | ADD — bUnit test Orders/Detail.razor renders detail + payment confirm button | +30 lines |

## Detailed Task List

### W5-T1: Sitemap links

```razor
<!-- Sitemap.razor — add to "Quản lý Đơn Hàng" card -->
<a href="/orders" class="sitemap-link" data-testid="link-orders-list">
    <span class="sitemap-link-icon">📋</span>
    <span>Danh sách đơn hàng</span>
</a>
<a href="/Kitchen" class="sitemap-link" data-testid="link-kitchen-display">
    <span class="sitemap-link-icon">🍳</span>
    <span>Màn hình bếp</span>
</a>
```

### W5-T2: Unit test — OrderWorkflowService broadcasts SignalR

```csharp
// Test: TransitionStatusAsync calls NotifyOrderStatusChangedAsync with correct old/new status
// Test: NotifyOrderStatusChangedAsync NOT called when transition fails (invalid)
// Test: NotifyOrderStatusChangedAsync NOT called when order not found
// Mock: IOrderNotificationService
```

### W5-T3: Unit test — KitchenService transitions to Ready

```csharp
// Test: All OrderItems Completed → Order.UpdateOrderStatus(Ready) called
// Test: Partial items Completed → Order.UpdateOrderStatus NOT called
// Test: NotifyOrderStatusChangedAsync called with "ready" when all items completed
// Test: MarkAsCompleted NOT called (reserved for customer pickup)
// Mock: IVanAnDbContext, IOrderNotificationService
```

### W5-T4: Integration test — Payment confirm flow

```csharp
// Test: POST api/webhooks/payment with valid orderId/tenantId/transactionId → 200 OK
// Test: Order.PaymentStatus == "Paid" after confirm
// Test: Accounting entries generated (Revenue + COGS)
// Test: Idempotent — second call returns 200 without duplicate entries
// Test: Invalid orderId → 404
// Test: Missing transactionId → 400
// Use: WebApplicationFactory<GatewayProgram>
```

### W5-T5: bUnit test — Orders UI

```csharp
// Test: Orders/Index.razor renders VanAnTable with order rows
// Test: "Xác nhận" button visible only for pending orders
// Test: Click "Xác nhận" → calls HttpClient.PutAsJsonAsync with correct status
// Test: Orders/Detail.razor renders order info + timeline
// Test: "Xác nhận đã nhận tiền" button visible only when PaymentStatus=Pending
// Use: bUnit TestContext + Mock<IHttpClientFactory>
```

### W5-T6: Full build + all tests

- `dotnet build VanAn.sln` — 0 errors
- `dotnet test VanAn.sln` — all tests pass
- `guard-check.ps1` — pass

## Verification Checklist

- [ ] Build 0 errors
- [ ] All tests pass (existing + new)
- [ ] Sitemap has links: Orders (`/orders`), Kitchen (`/Kitchen`)
- [ ] Unit test: OrderWorkflowService broadcasts after status change
- [ ] Unit test: KitchenService transitions to Ready when all items completed
- [ ] Integration test: Payment confirm → PaymentStatus=Paid + accounting entries
- [ ] bUnit test: Orders/Index renders list + confirm button
- [ ] bUnit test: Orders/Detail renders detail + payment confirm button
- [ ] guard-check.ps1 pass
- [ ] No regression in existing tests

## End-to-End Flow Verification (manual or automated)

1. Khách đặt hàng trên KhachLink → `POST api/orders` → `OrderHub.NewOrderReceived` → ShopERP Dashboard shows new order
2. Admin opens `/orders` → sees new order (pending) → clicks "Xác nhận" → status=confirmed → `OrderHub.OrderStatusChanged` → Dashboard updates
3. Kitchen opens `/Kitchen` → sees order items → clicks "Hoàn thành" on all items → `OrderStatus=Ready` → `OrderHub.OrderStatusChanged`
4. Admin opens `/orders/{id}` → sees PaymentStatus=Pending → clicks "Xác nhận đã nhận tiền" → `PaymentStatus=Paid` + accounting entries → `OrderHub.PaymentConfirmed`
5. KhachLink OrderTracking: polling detects status changes (5-10s delay) → timeline updates → when delivered, IdentityUpgradeModal shows
6. Polling stops when order completed (no more requests)
