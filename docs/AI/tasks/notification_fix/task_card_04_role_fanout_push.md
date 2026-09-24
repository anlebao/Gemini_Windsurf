# Task Card NF-4: Role fan-out push — shipper / salesman / owner

> **Status:** ✅ IMPLEMENTED 2026-09-24 — owner đơn-mới + salesman-completed + assigned-shipper-cancelled. Shipper "đơn mới cần nhận" (khu vực/eligible) DEFER phase sau — plan giữ dưới. D2 = Option A (fan-out ở consumer). D3 = push-only. Build 0 errors · 8 fan-out unit tests PASS.
>
> **Implementation notes (khác sketch ban đầu — đơn giản hơn, đã verify code):**
> - `assignedShipperId` lấy từ **`Order.ShipperId`** (đã có sẵn trên entity) — KHÔNG cần query `DeliveryTask` (PG-only, `ShopERPDbContext` Ignore). ShipperId == CustomerId của shipper (xác nhận `CommunityOrderService.AcceptOrderAsync`).
> - Payload `order.status.changed` (generic push subject) += `salesmanId`, `assignedShipperId`, `orderType`; payload outbox `vanan.shoperp.order.status.changed` += `salesmanId`, `assignedShipperId` (dedup + sync shipper về SQLite).
> - `DataSyncSubscriber.SyncOrderStatusAsync` (Gateway): sau khi save PG, nếu `completed`/`cancelled` mà payload thiếu role id → resolve từ `order.SalesmanId`/`order.ShipperId` (đã load) → republish `order.status.changed` với `rolesOnly: true` (buyer không bị push lặp).
> - `OrderSyncSubscriber` (ShopERP): sync `SalesmanId`/`ReferralProductId`/`ReferralCode` (SetSalesmanReferral) + `assignedShipperId` (AssignShipper) vào SQLite → các event publish SAU ĐÓ từ ShopERP tự mang role ids.
> - Owner đơn-mới: `OrderService` created payload += `OwnerCustomerId` (resolve `Tenants` PG, best-effort try/catch) → `OrderSyncSubscriber` push owner qua `SendBulkNotificationAsync` (fallback tra SQLite `Tenants`).
> - **Strict parse — KHÔNG TryParse-stub** (user directive): optional Guid parse qua `GetGuid()` — malformed → throw → event bị reject + log error rõ ràng, không nuốt lặng thành "no recipient". `Guid.Empty` = sentinel "no recipient".
> - `PushNotificationService.SendOrderStatusNotificationAsync` + `SendBulkNotificationAsync` → `virtual` (Moq); `HandleEventAsync` private → internal.
> - actionUrl: owner `/` (KhachLink PWA home — owner subscribe qua KhachLink, ShopERP không có PWA) · salesman `/community/sales-dashboard` · shipper `/community/active-deliveries`.
> **Priority:** P1 — feature gap: 3 vai trò không có kênh notify nào
> **Created:** 2026-09-24
> **Master plan:** `docs\AI\tasks\notification_fix\master_plan.md`
> **Depends on:** TC-01 (subscribe pipeline phải sống trước)
> **Effort:** 1-2 ngày (tuỳ scope duyệt)

## Problem

| Vai trò | Sự kiện cần biết | Hiện trạng |
|---|---|---|
| Shipper | Đơn `ready`/mới cần giao trong khu vực; đơn của mình bị hủy | Poll `nearby-orders` thủ công — không push |
| Salesman | Conversion (order completed có `SalesmanId` của mình); commission status | Không có gì |
| Owner | Đơn mới từ KhachLink; (tuỳ chọn) status change | Chỉ có `OrderCreated` SignalR nếu đang mở ShopERP UI — không push ngoài app |
| Buyer | Status changes | TC-01+TC-03 fix |

`SendOrderStatusNotificationAsync` chỉ target `order.CustomerId` (`PushNotificationService.cs:78`). Không fan-out.

## Root cause (design gap — VERIFIED)

- Payload `order.status.changed` (`OrderWorkflowService.cs:963-971`) chỉ chứa `orderId/tenantId/customerId/oldStatus/newStatus` — không role recipients.
- `PushSubscriptions` keyed theo `CustomerId` — shipper/salesman/owner đều là Customer có role (`CommunityRoles`) → có thể reuse cùng pipeline nếu resolve được customerId của họ.
- DeliveryTask (PG, Gateway) chứa `AssignedShipperId`; `Order.SalesmanId` trên order; owner = `Tenant.OwnerCustomerId` (đã có cột — migration `AddTenantOwnerCustomerId`).

## Scope cần duyệt (DECISIONS)

**D1 — Sự kiện notify per role (chọn subset):**
| Role | Event đề xuất | Ghi chú |
|---|---|---|
| Owner | `order.created` (đơn mới) | Giá trị cao nhất, dễ nhất |
| Shipper | `newStatus=ready` + `orderType=DELIVERY` + chưa có DeliveryTask (đơn cần nhận) | **DEFER phase sau** — plan bên dưới |
| Shipper (assigned) | đơn mình bị `cancelled` | Đơn giản — resolve từ DeliveryTask |
| Salesman | `completed` với `order.SalesmanId == me` | Đơn giản — field có sẵn |
| Buyer | (đã cover TC-01/03) | — |

**D2 — Điểm fan-out:**
- **Option A (đề xuất): fan-out ở consumer (ShopERP `PushNotificationBackgroundService`).** Payload mở rộng thêm `salesmanId`, `ownerCustomerId`, `orderType`. Handler tra `PushSubscriptions` theo từng role-customerId → gọi `SendOrderStatusNotificationAsync` với body riêng per role. Đơn giản, không thêm DB read phía Gateway.
- **Option B: fan-out ở publisher** — Gateway resolve roles trước khi publish (cần thêm PG query). Không khuyến nghị: coupling chặt.
- **Shipper "đơn mới cần nhận"** (D1-row2) — **DEFERRED, plan sẵn cho phase sau:**
  - Trigger: `newStatus=ready` + `orderType=DELIVERY` + chưa có `DeliveryTask` active.
  - Recipients: `CommunityRoles` active `IsShipper=true` của tenant (SQLite `CommunityRoles` hoặc PG — verify nơi sync); optional filter khoảng cách nếu shipper có last-known GPS (Haversine như `CommunityOrderService` nearby-orders).
  - Anti-spam: dedup per (orderId, shipperId), chỉ gửi 1 lần khi order vào `ready` lần đầu.
  - Implementation: consumer-side fan-out (giống Option A) — resolve eligible shipper list trong ShopERP scope rồi gửi push từng người.

**D3 — Kênh:** chỉ Web Push (subscription sẵn từ TC-01), hay thêm SMS qua `EsmsNotificationService` cho owner (đơn mới)? SMS tốn phí — **APPROVED: push-only phase này.**

## Implementation sketch (sau khi duyệt D1-D3)

1. `PublishOrderStatusChangedEventAsync` payload += `salesmanId`, `ownerCustomerId` (đọc từ `order` — SalesmanId có sẵn; owner cần Tenant lookup hoặc để consumer tra `Tenant.OwnerCustomerId` trong SQLite).
2. `PushNotificationBackgroundService.HandleEventAsync` — sau khi gửi buyer, fan-out:
   - `salesmanId` + `newStatus=completed` → push salesman "Đơn giới thiệu #{id} hoàn thành — hoa hồng sắp về"
   - `ownerCustomerId` → push owner "Đơn #{id} → {status}" (hoặc chỉ `order.created`)
   - `assignedShipperId` (resolve DeliveryTask trong SQLite? — **verify DeliveryTask có trong ShopERPDbContext không**; nếu chỉ ở PG → gửi shipper-cancelled qua Gateway path) + `newStatus=cancelled` → push shipper
3. Order-created event (`vanan.cloud.order.created.{shopInstanceId}` → `OrderSyncSubscriber`) → publish push cho owner (đơn mới) — subject mới hoặc reuse handler.
4. Message templates per role (tiếng Việt) + `actionUrl` phù hợp (`/community/owner-panel`, `/my-deliveries`, …).

## Tests

- [ ] Unit `HandleEventAsync` fan-out: payload có salesmanId + completed → gọi push đúng customerId; không có → chỉ buyer.
- [ ] Role-targeted body/URL đúng per role.
- [ ] Không push khi role-customerId == buyer-customerId (owner tự mua — tránh duplicate).

## Acceptance

- [ ] Owner (đã bật push) nhận notification "đơn mới" khi khách checkout — kể cả khi không mở ShopERP
- [ ] Salesman nhận push khi đơn giới thiệu completed
- [ ] Assigned shipper nhận push khi đơn bị hủy
- [ ] RV production: từng role verify trên thiết bị thật
