# TASK CARD — Order Lifecycle Wave 2: Admin Orders UI (List + Confirm + Detail)

> **Status:** 📋 PLANNING — awaiting user review
> **Prerequisite:** W0 merged (SignalR wiring) · **Branch:** `feature/order-w2-admin-orders-ui`
> **Estimated sessions:** 2-3
> **Gap fixed:** G1 (Không UI "Xác nhận đơn" trong ShopERP)

## Objective

Tạo trang `/orders` (list) + `/orders/{orderId}` (detail) trong ShopERP Blazor. Admin có thể:
1. Xem danh sách đơn hàng (filter theo status, date)
2. Bấm "Xác nhận đơn" (pending → confirmed)
3. Xem chi tiết 1 đơn + timeline trạng thái
4. Real-time update khi OrderHub broadcast `OrderStatusChanged`

## Architecture Decision

- **UI Platform:** Dùng VanAnTable, VanAnButton, VanAnCard — KHÔNG custom HTML/CSS
- **Auth:** `[Authorize]` + `GetTenantId()` pattern (existing in OrdersController)
- **Real-time:** Subscribe `OrderHub` via JS interop (SignalR client) — staff only (5-20 connections)
- **API calls:** `GET api/orders?status=xxx` (list) + `PUT api/orders/{id}/status` (confirm) + `GET api/orders/{id}` (detail)

## Prerequisites (to verify in INVESTIGATE)

- [ ] W0 merged — `OrderHub` broadcasts `OrderStatusChanged`
- [ ] `5_WebApps/ShopERP/Controllers/OrdersController.cs` — `GET api/orders` (line 107), `GET api/orders/{id}` (line 49), `PUT api/orders/{id}/status` (line 66)
- [ ] `5_WebApps/ShopERP/Components/VanADashboard.razor:393` — `ShowAllOrders()` navigates to `/orders` (page doesn't exist yet)
- [ ] `5_WebApps/ShopERP/Components/VanADashboard.razor:350` — `HandleViewDetails` navigates to `/orders/{id}` (page doesn't exist yet)
- [ ] UI Platform components available: VanAnTable, VanAnButton, VanAnCard, VanAnModal
- [ ] SignalR client JS available in ShopERP (check `_Layout.cshtml` or `Pages/Shared`)

## Open Questions

| Q | Question | Default answer |
|---|----------|----------------|
| Q1 | Trang Orders đặt ở `Components/Pages/Orders/` hay `Pages/Orders/`? | `Components/Pages/Orders/` (Blazor interactive) |
| Q2 | Dùng Blazor Server interactive hay MVC? | Blazor Server interactive (`@rendermode InteractiveServer`) |
| Q3 | SignalR client: JS interop hay .NET client? | JS interop (consistent with Kitchen/Index.cshtml pattern) |
| Q4 | Filter: dropdown status + date picker? | Dropdown status + "Hôm nay" / "7 ngày" / "Tất cả" buttons |

## Files to Create/Modify (estimated 4 files)

| File | Action | Lines |
|------|--------|-------|
| `5_WebApps/ShopERP/Components/Pages/Orders/Index.razor` | CREATE — list page `@page "/orders"` | +150 lines |
| `5_WebApps/ShopERP/Components/Pages/Orders/Detail.razor` | CREATE — detail page `@page "/orders/{orderId:guid}"` | +120 lines |
| `5_WebApps/ShopERP/Components/Pages/Orders/Index.razor.css` | CREATE — scoped CSS (optional, prefer UI Platform) | +20 lines |
| `5_WebApps/ShopERP/Components/Pages/Sitemap.razor` | UPDATE — add link "Danh sách đơn hàng" → `/orders` | +5 lines |

## Detailed Task List

### W2-T1: Create `Orders/Index.razor` — list page

Features:
- `@page "/orders"` + `@rendermode InteractiveServer` + `@attribute [Authorize]`
- Inject `IHttpClientFactory` (call `api/orders?status=xxx`)
- Filter dropdown: Tất cả / Chờ xác nhận (pending) / Đã xác nhận (confirmed) / Đang làm (preparing) / Sẵn sàng (ready) / Hoàn thành (completed)
- Date filter: Hôm nay / 7 ngày / Tất cả
- VanAnTable với columns: Mã đơn, Thời gian, Trạng thái (badge), Tổng tiền, Hành động
- Nút "Xác nhận" (chỉ hiện khi status=pending) → `PUT api/orders/{id}/status` body `{status: "confirmed"}`
- Nút "Chi tiết" → navigate `/orders/{id}`
- SignalR: `connection.on("OrderStatusChanged", ...)` → refresh list
- SignalR: `connection.on("PaymentConfirmed", ...)` → refresh list (update payment badge)

### W2-T2: Create `Orders/Detail.razor` — detail page

Features:
- `@page "/orders/{orderId:guid}"` + `@rendermode InteractiveServer` + `@attribute [Authorize]`
- Inject `IHttpClientFactory` (call `GET api/orders/{id}`)
- Hiển thị: mã đơn, trạng thái (badge), thời gian, tổng tiền, payment status
- Timeline trạng thái (pending → confirmed → preparing → ready → completed) — giống KhachLink OrderTracking
- List OrderItems: tên sản phẩm, số lượng, đơn giá, thành tiền, kitchen status
- Nút "Xác nhận đơn" (nếu pending) → `PUT api/orders/{id}/status` confirmed
- Nút "Xác nhận nhận tiền" (nếu PaymentStatus=Pending) → W3 sẽ thêm
- Nút "Quay lại" → `/orders`
- SignalR: `connection.on("OrderStatusChanged", ...)` → reload detail

### W2-T3: Add Sitemap link

```razor
<!-- Sitemap.razor — add to card "Quản lý Đơn Hàng" -->
<a href="/orders" class="sitemap-link" data-testid="link-orders-list">
    <span class="sitemap-link-icon">📋</span>
    <span>Danh sách đơn hàng</span>
</a>
```

### W2-T4: Build + verify

- `dotnet build VanAn.sln` — 0 errors
- Navigate `/orders` renders list (manual test)
- Navigate `/orders/{valid-guid}` renders detail
- Click "Xác nhận" → status changes to confirmed
- SignalR: open 2 tabs, confirm in 1 → other tab updates

## Verification Checklist

- [ ] Build 0 errors
- [ ] `/orders` page renders with VanAnTable (UI Platform compliance)
- [ ] `/orders/{id}` page renders with order details + timeline
- [ ] "Xác nhận" button calls `PUT api/orders/{id}/status` with `{status: "confirmed"}`
- [ ] SignalR `OrderStatusChanged` event refreshes list real-time
- [ ] `[Authorize]` attribute present (auth required)
- [ ] `GetTenantId()` used for tenant filtering (multi-tenancy)
- [ ] Sitemap has link to `/orders`
- [ ] No custom HTML/CSS — all UI Platform components
- [ ] Dashboard "View All Orders" button now works (navigates to `/orders`)

## Rollback Plan

Delete `Orders/Index.razor`, `Orders/Detail.razor`, `Orders/Index.razor.css`. Revert Sitemap. Build passes (Dashboard buttons still 404 — pre-existing state).
