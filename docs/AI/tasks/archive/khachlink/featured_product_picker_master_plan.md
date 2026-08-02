# Featured Product Picker Refactor — Master Plan

> **Status:** APPROVED → IMPLEMENT (2026-07-23)
> **Created:** 2026-07-23
> **Workflow:** `newfeaturebuild.md` (ANALYZE → IMPLEMENT)
> **Active skills:** `domain-integrity-validation`, `ui-platform-migration`
> **Branch:** `main`

---

## 1. Use Case & Business Design

### Problem
Sysadmin tạo FeaturedProduct bằng cách **nhập tay ProductId (GUID)** trong `FeaturedProducts.razor` (<ref_file file="C:\VibeCoding\Gemini_Windsurf\5_WebApps\ShopERP\Components\Pages\Admin\FeaturedProducts.razor" /> lines 105-108). Không có validation product có tồn tại hay thuộc tenant đã chọn.

Hậu quả:
- ProductId sai / không tồn tại / cross-tenant → vẫn tạo featured product thành công.
- Khi customer đặt hàng, `OrderSyncSubscriber` auto-create **stub product** trong SQLite tenant (<ref_snippet file="C:\VibeCoding\Gemini_Windsurf\5_WebApps\ShopERP\Services\OrderSyncSubscriber.cs" lines="178-199" />) với description = "Synced from Gateway".
- Tenant owner mở ShopERP thấy **product lạ tự xuất hiện** — rủi ro ẩn, không minh bạch.

### Solution (user-approved 2026-07-23)
1. **Product picker thay nhập tay GUID:** Sysadmin chọn product từ dropdown list được load trực tiếp từ `ShopERPDbContext.Products` (local SQLite), filter theo tenant đã chọn.
2. **Khi chọn product → auto-fill snapshot:** `DisplayName`, `DisplayPrice`, `VatRate` được snapshot từ Product entity (không cho sửa Price + VatRate).
3. **Cho phép sửa `DisplayName`** (marketing name, có thể khác Product.Name).
4. **Cho phép sửa `DisplayDescription` + `ImageUrl`** (marketing fields).
5. **Nút "Refresh from Product":** Khi owner đổi giá Product, sysadmin bấm nút để re-snapshot `DisplayPrice` + `VatRate` từ Product hiện tại. `DisplayName`/`Description`/`ImageUrl` giữ nguyên (không ghi đè marketing content).
6. **Không sync FeaturedProduct xuống ShopERP** (giữ design hiện tại — PG-only entity).
7. **Xóa stub products đã tồn tại** trong SQLite (description = "Synced from Gateway") — cleanup một lần.

### Out of scope
- Không sửa `OrderSyncSubscriber` auto-create stub (giữ làm safety net — chỉ log WARNING + alert khi trigger).
- Không sửa conflict #2 (2 luồng update status) — task riêng.
- Không thêm cơ chế live price refresh (chỉ manual "Refresh from Product").

---

## 2. Reverse Impact Analysis + TDD Plan

### Files to modify

| File | Layer | Change |
|---|---|---|
| `5_WebApps/ShopERP/Components/Pages/Admin/FeaturedProducts.razor` | UI | Replace ProductId GUID input → product picker dropdown; auto-fill snapshot; lock Price/VatRate fields; add "Refresh from Product" button |
| `2_Gateway/Controllers/FeaturedProductsController.cs` | API | Add validation: reject Create if ProductId not in PG (cross-check via existing Tenants/Orders — see note); OR keep validation client-side only |
| `5_WebApps/ShopERP/Services/FeaturedProductApiClient.cs` | Service | Add `RefreshFromProductAsync(id)` method (calls Gateway PUT with snapshot from local Product) |
| `3_CoreHub/Services/ProductService.cs` (or new helper) | Service | Expose `GetProductForSnapshotAsync(productId, tenantId)` returning `{ Name, Price, VatRate }` — used by Refresh button |

### Files NOT modified
- `1_Shared/Domain/FeaturedProduct.cs` — entity đã có đủ fields. Không cần Domain change.
- `OrderSyncSubscriber.cs` — giữ auto-create stub làm safety net (chỉ log WARNING rõ hơn).
- `ShopERPDbContext.cs` — không cần migration mới cho FeaturedProduct (PG-only).

### Cleanup (one-time)
- SQL script `DELETE FROM Products WHERE Description = 'Synced from Gateway'` (chỉ chạy trên VPS, không trong migration — vì đây là data cleanup, không phải schema change). **Cần verify không có OrderItem reference** trước khi xóa (FK constraint). Nếu có OrderItem → giữ stub, chỉ đánh dấu `IsActive = false` để ẩn khỏi UI.

### TDD Plan
- **Unit test:** `FeaturedProductsController.Create` reject khi ProductId không thuộc tenant đã chọn (nếu thêm server-side validation).
- **Unit test:** `RefreshFromProduct` cập nhật DisplayPrice + VatRate, KHÔNG ghi đè DisplayName/Description/ImageUrl.
- **Razor component test (bypass):** Manual verify — chọn product → auto-fill đúng; bấm Refresh → price cập nhật; lock field không cho sửa.
- **Integration test:** Tạo featured product với ProductId thật → checkout → order sync → KHÔNG tạo stub product mới trong SQLite.

---

## 3. Detailed Coding Plan + Namespace Strategy

### Phase A: UI Layer (`FeaturedProducts.razor`)
1. Inject `IProductRepository` (đã có trong DI — `Program.cs` line 212) hoặc `ShopERPDbContext` trực tiếp.
2. Thêm state `_products` (List<Product>) — load khi tenant thay đổi.
3. Replace input `fp-productid` → `<select>` dropdown hiển thị `Product.Name (Price)` cho product active của tenant đã chọn.
4. `@onchange` handler: khi chọn product → set `_form.ProductId`, `_form.DisplayPrice = product.Price`, `_form.VatRate = product.VatRate`, `_form.DisplayName = product.Name` (default, user có thể sửa sau).
5. Lock `DisplayPrice` + `VatRate` input: `disabled="true"` (read-only display, hint: "Giá/VAT lấy từ Product. Bấm 'Refresh from Product' để cập nhật.").
6. Thêm button "Refresh from Product" — chỉ hiện ở edit mode — gọi `RefreshFromProductAsync`.
7. Khi tenant thay đổi (create mode) → reload `_products` list.

### Phase B: Service Layer
1. `FeaturedProductApiClient.RefreshFromProductAsync(Guid featuredId, Guid productId, Guid tenantId)`:
   - Query local `ShopERPDbContext.Products` để lấy `Price` + `VatRate`.
   - Gọi Gateway `PUT /api/v1/featured-products/{id}` với `UpdateFeaturedProductRequest` giữ nguyên `DisplayName`/`Description`/`ImageUrl`/`SortOrder`/`IsActive` từ featured hiện tại, chỉ override `DisplayPrice` + `VatRate` từ Product.
2. Không cần thêm endpoint Gateway mới — dùng `Update` hiện có.

### Phase C: Cleanup stub products
1. Script SQL (chạy thủ công trên VPS, KHÔNG trong migration):
   ```sql
   -- Identify stubs
   SELECT Id, Name, TenantId, Description FROM Products WHERE Description = 'Synced from Gateway';
   -- Check FK references
   SELECT p.Id, p.Name, COUNT(oi.Id) AS OrderItemCount
   FROM Products p
   LEFT JOIN OrderItems oi ON oi.ProductId = p.Id
   WHERE p.Description = 'Synced from Gateway'
   GROUP BY p.Id, p.Name;
   -- Safe delete (no OrderItem references):
   DELETE FROM Products WHERE Description = 'Synced from Gateway'
     AND Id NOT IN (SELECT DISTINCT ProductId FROM OrderItems WHERE ProductId IS NOT NULL);
   -- Has OrderItem references → deactivate instead of delete:
   UPDATE Products SET IsActive = 0 WHERE Description = 'Synced from Gateway'
     AND Id IN (SELECT DISTINCT ProductId FROM OrderItems WHERE ProductId IS NOT NULL);
   ```

### Namespace strategy
- Không thêm namespace mới. Tất cả thay đổi trong namespace hiện có (`VanAn.ShopERP.Services`, `VanAn.Gateway.Controllers`).

---

## 4. Review & Approval (User)

### Decisions (chốt 2026-07-23)
1. **Server-side validation:** Client-side only. ShopERP picker đảm bảo ProductId hợp lệ. Gateway tin tưởng client. Không phá Option C.
2. **Cleanup timing:** SAU khi deploy fix. Deploy → verify trên VPS → chạy cleanup script.
3. **Picker UX:** Dropdown `<select>` đơn giản (dùng UI Platform component nếu có).

### Approval gate
- [x] User confirm 3 open questions above
- [ ] User approve Phase A + B + C scope
- [ ] User approve "no Domain layer change" (verify FeaturedProduct entity đã đủ fields)
- [x] User approve Section 5 (Order Status Conflict Fix) scope + decisions

---

## 5. Order Status Conflict Fix (added 2026-07-23)

### Problem (verified in earlier ANALYZE)
ShopERP có **3+ luồng update order status khác nhau**, mỗi luồng dùng service/state-machine khác nhau → conflict:

| # | Caller | Service | Validation | Outbox sync | Issue |
|---|---|---|---|---|---|
| A | `Kitchen/Display.razor` (TransitionTo, DeliverOrder) | `OrderWorkflowService.TransitionStatusAsync` | ✅ | ✅ | OK |
| B | `Orders/Index.razor` (ConfirmOrder) | `OrderService.UpdateOrderStatusAsync` | ❌ | ✅ | Set "confirmed" — KHÔNG có trong state machine `OrderWorkflowService` normal flow → order kẹt |
| C | `Orders/Detail.razor` (TransitionOrderStatus, ConfirmOrder) | `OrderWorkflowService.TransitionStatusAsync` | ✅ | ✅ | OK |
| D | `OrdersController.UpdateOrderStatus` (API) | `OrderService.UpdateOrderStatusAsync` | ✅ controller-level | ✅ | State machine sai (chỉ 4 trạng thái) |
| E | `OrderWorkflowController.TransitionStatus` (API) | `OrderWorkflowService.TransitionStatusAsync` | ✅ | ✅ | OK |
| F | `KitchenService.UpdateItemStatusAsync` (item-level) | **Direct entity mutation** | ❌ | ❌ | Set "ready" KHÔNG sync về Gateway → KhachLink không thấy "ready" |
| G | `Gateway/OrdersController.UpdateOrderStatus` | `OrderService.UpdateOrderStatusAsync` | ✅ controller-level | ✅ | State machine sai (chỉ 4 trạng thái) |
| H | `DataSyncSubscriber.SyncOrderStatusAsync` (NATS receiver) | **Direct entity mutation** | ❌ | N/A (receiver) | OK — đây là sink, không phải source |

### 3 Conflicts cụ thể
1. **Hai state machine khác nhau:** `OrderService.IsTransitionValidAsync` (<ref_snippet file="C:\VibeCoding\Gemini_Windsurf\3_CoreHub\Services\OrderService.cs" lines="461-467" />) chỉ có 4 trạng thái (Pending/Processing/Completed/Cancelled). `OrderWorkflowService.IsTransitionValidAsync` (<ref_snippet file="C:\VibeCoding\Gemini_Windsurf\3_CoreHub\Services\OrderWorkflowService.cs" lines="340-350" />) có 7 trạng thái. Order set "confirmed" qua `OrderService` → kẹt vì `OrderWorkflowService` normal flow không có transition từ "confirmed".

2. **`KitchenService.UpdateItemStatusAsync` bypass workflow:** (<ref_snippet file="C:\VibeCoding\Gemini_Windsurf\3_CoreHub\Services\KitchenService.cs" lines="119-123" />) gọi trực tiếp `order.UpdateOrderStatus(OrderStatusId.Ready)` trên entity, không qua `OrderWorkflowService.TransitionStatusAsync` → không enqueue Outbox event → status "ready" KHÔNG sync về Gateway PG → KhachLink OrderTracking không thấy "ready".

3. **`OrderService.UpdateOrderStatusAsync` không validate internally:** (<ref_snippet file="C:\VibeCoding\Gemini_Windsurf\3_CoreHub\Services\OrderService.cs" lines="339-385" />) gọi `order.UpdateOrderStatus()` trực tiếp, không gọi `IsTransitionValidAsync`. Validation chỉ ở controller layer. Khi Razor page gọi trực tiếp service (bypass controller) → không có validation.

### Solution (user-approved direction 2026-07-23)

#### Fix 1: Unify all status updates via `OrderWorkflowService.TransitionStatusAsync`
- **Deprecate** `OrderService.UpdateOrderStatusAsync` cho order status (mark `[Obsolete]`, redirect internal call sang `OrderWorkflowService`).
- **Refactor callers:**
  - `Orders/Index.razor` ConfirmOrder (line 338) → `OrderWorkflowService.TransitionStatusAsync(orderId, new OrderStatusId("confirmed"), reason)`.
  - `OrdersController.UpdateOrderStatus` (line 89) → delegate sang `OrderWorkflowService.TransitionStatusAsync`.
  - `Gateway/OrdersController.UpdateOrderStatus` (line 188) → delegate sang `OrderWorkflowService.TransitionStatusAsync`.
- **Giữ `OrderService.UpdateOrderStatusAsync`** tạm thời (deprecated, không xóa) để tránh break tests + external callers. Log warning khi gọi.

#### Fix 2: `KitchenService.UpdateItemStatusAsync` delegate sang `OrderWorkflowService`
- Khi tất cả OrderItem completed → thay vì `order.UpdateOrderStatus(OrderStatusId.Ready)` trực tiếp, gọi `OrderWorkflowService.TransitionStatusAsync(orderId, OrderStatusId.Ready, reason)`.
- Inject `IOrderWorkflowService` vào `KitchenService` constructor (nullable — null trong test scope).
- Hậu quả: status "ready" được enqueue Outbox → sync về Gateway → KhachLink thấy "ready". ✅
- Edge case: nếu `IOrderWorkflowService` null (test) → fallback direct mutation (giữ behavior cũ cho test).

#### Fix 3: `Orders/Index.razor` ConfirmOrder dùng `OrderWorkflowService` + thêm "confirmed" vào state machine
- **Quyết định:** THÊM "confirmed" vào state machine normal flow (không bỏ — "confirmed" có semantic riêng: order đã được owner xác nhận, chờ bếp nhận).
- **State machine mới (kitchen ON):**
  ```
  pending → [preparing, confirmed, cancelled, completed]
  confirmed → [preparing, cancelled, completed]    // ← THÊM: confirmed có thể chuyển sang preparing
  preparing → [ready, cancelled, completed]
  ready → [completed, cancelled, delivered]
  delivered → [completed, cancelled]
  completed → []
  cancelled → []
  ```
- **Lý do giữ "confirmed":**
  - `Orders/Detail.razor` line 239 đã gọi `TransitionOrderStatus("confirmed")` — UI flow hiện tại owner confirm → bếp nhận.
  - `Kitchen/Display.razor` line 226 đã query cả "pending" + "confirmed" cho column "Chờ tiếp nhận" (Bug 4 fix 2026-07-18).
  - `OmnichannelOrderService` line 969: `cancellableStatuses = ["pending", "confirmed"]` — confirmed là trạng thái có thể cancel.
  - `Payment.razor` line 183 comment: POS payment → "preparing" (skip "confirmed" vì staff đã confirm payment). → "confirmed" là cho manual owner confirm, "preparing" là cho auto-confirm (POS).
  - KhachLink `OrderTracking.razor` line 417: hiển thị "Đã xác nhận" cho customer.
  - Tests + docs đã reference "confirmed" (15+ files).
  - **Bỏ "confirmed" sẽ phá nhiều chỗ → giữ + thêm transition ra.**

### Files to modify (Section 5)

| File | Layer | Change |
|---|---|---|
| `3_CoreHub/Services/OrderWorkflowService.cs` | Service | Thêm "confirmed" vào `validTransitions` (kitchen ON + OFF) |
| `3_CoreHub/Services/OrderService.cs` | Service | Mark `UpdateOrderStatusAsync` `[Obsolete("Use OrderWorkflowService.TransitionStatusAsync")]`; redirect internal logic sang `OrderWorkflowService` nếu có thể (nullable inject) |
| `3_CoreHub/Services/KitchenService.cs` | Service | Inject `IOrderWorkflowService?`; khi all items completed → delegate sang `TransitionStatusAsync(orderId, Ready)` thay vì direct mutation; fallback direct nếu null |
| `5_WebApps/ShopERP/Components/Pages/Orders/Index.razor` | UI | ConfirmOrder → `OrderWorkflowService.TransitionStatusAsync` thay vì `OrderService.UpdateOrderStatusAsync` |
| `5_WebApps/ShopERP/Controllers/OrdersController.cs` | API | UpdateOrderStatus → delegate sang `OrderWorkflowService.TransitionStatusAsync` |
| `2_Gateway/Controllers/OrdersController.cs` | API | UpdateOrderStatus → delegate sang `OrderWorkflowService.TransitionStatusAsync` |

### Files NOT modified (Section 5)
- `1_Shared/Domain.cs` — `OrderStatusId.Confirmed` đã có (line 426). Không cần Domain change.
- `Orders/Detail.razor` — đã dùng `OrderWorkflowService.TransitionStatusAsync` (OK).
- `Kitchen/Display.razor` — đã dùng `OrderWorkflowService.TransitionStatusAsync` (OK).
- `Payment.razor` — đã dùng `OrderWorkflowService.TransitionStatusAsync` (OK).
- `DataSyncSubscriber` (cả 2 Gateway + ShopERP) — đây là NATS receiver (sink), không phải source. Direct mutation OK.

### TDD Plan (Section 5)
- **Unit test:** `OrderWorkflowService.IsTransitionValidAsync(Confirmed, Preparing)` → true (sau fix).
- **Unit test:** `OrderWorkflowService.IsTransitionValidAsync(Confirmed, Cancelled)` → true.
- **Unit test:** `KitchenService.UpdateItemStatusAsync` khi all items completed → gọi `OrderWorkflowService.TransitionStatusAsync(orderId, Ready)` (verify via mock).
- **Unit test:** `KitchenService.UpdateItemStatusAsync` khi `IOrderWorkflowService` null → fallback direct mutation (giữ behavior cũ).
- **Integration test:** Order flow `pending → confirmed → preparing → ready → completed` qua `OrderWorkflowService` → Outbox event cho mỗi transition → KhachLink thấy tất cả status.
- **Regression test:** `Orders/Index.razor` ConfirmOrder → order status = "confirmed", KHÔNG kẹt (có thể transition tiếp sang "preparing").

### Risk assessment (Section 5)
- **Risk 1:** Deprecated `OrderService.UpdateOrderStatusAsync` có thể break tests hiện tại (15+ reference trong docs, 1 trong `OrderServiceTests.cs`).
  - **Mitigation:** Giữ method, mark `[Obsolete]` only. Không xóa. Tests cũ vẫn pass.
- **Risk 2:** `KitchenService` inject `IOrderWorkflowService` có thể tạo circular dependency (OrderWorkflowService → ? → KitchenService?).
  - **Mitigation:** Check DI graph. `OrderWorkflowService` dependencies: `IOrderRepository`, `ISocialCampaignService`, `ILoyaltyRewardsService`, `ICustomerRepository`, `INatsEventPublisher`, `IShopFeatureSettingsService`, `IOutboxRepository`, `IOrderNotificationService`. Không có `IKitchenService` → không circular.
- **Risk 3:** Thêm "confirmed" vào state machine có thể cho phép transition không mong muốn.
  - **Mitigation:** Chỉ thêm `confirmed → [preparing, cancelled, completed]`. Không thêm `confirmed → ready` (skip preparing). Test kỹ.

### Decisions (chốt 2026-07-23)
1. **`OrderService.UpdateOrderStatusAsync`:** Deprecated `[Obsolete]` + giữ method. Tests cũ vẫn pass, migration dần. Log warning khi gọi.
2. **"confirmed" trong state machine kitchen OFF (bypass) mode:** Giữ `["confirmed"] = ["completed", "cancelled", "delivered"]` (line 330). Kitchen ON thêm `["confirmed"] = ["preparing", "cancelled", "completed"]`. Kitchen OFF không có "preparing" → không thêm.

---

## 6-7. IMPLEMENT (pending approval)

Will execute after user approves Section 4 + Section 5.
