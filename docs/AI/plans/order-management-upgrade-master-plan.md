# MASTER PLAN — NÂNG CẤP QUẢN LÝ ĐƠN HÀNG (Issue #195)

> **Nguồn:** GitHub issue #195 (OPEN). Verify codebase 2026-10-19 — tất cả yêu cầu đều confirmed chưa có.
> **Mode:** ANALYZE → IMPLEMENT (`newfeaturebuild.md`). **Chờ user approval trước khi implement.**
> **Skills dự kiến:** `order-workflow-unified`, `outbox-pattern-implementation`, `domain-integrity-validation`.

## 0. Phạm vi & verify hiện trạng

| # | Yêu cầu | Hiện trạng (verified) |
|---|---|---|
| 0.1 | Trạng thái/hành động đơn động theo tenant/nhóm SP; SystemAdmin config được số lượng + thứ tự | Transitions **hardcode** `OrderWorkflowService.IsTransitionValidAsync` (2 biến thể theo `Kitchen_Workflow_Enabled`). Không có cơ chế config. |
| 0.2 | Booking→Order chỉ cần 2 hành động: Thanh toán + Hoàn thành | Đơn từ booking đi chung workflow bếp. Link: `Order.TrackingCode = PublicBookingCode` (BookingService.cs:30). |
| 1.1 | Chi tiết đơn: hiện tên + SĐT khách | `Order.CustomerInfo` (OwnsOne, FullName/PhoneNumber) **đã có sẵn trong DB** — chỉ thiếu map DTO + render. |
| 1.2 | Bố cục lại + Timeline hiển thị trạng thái hiện tại + trạng thái tiếp theo | Timeline tĩnh 5 bước, không highlight current, không gợi ý next, thiếu delivered/cancelled, không timestamp. |
| 2.1 | List: default filter "chờ xác nhận + hôm nay" | Date đã default `today`; status default `"ALL"` (cần đổi → `pending`). |
| 2.2 | Xử lý theo lô (chọn nhiều đơn → 1 hành động) | Không có selection/bulk action — chỉ per-row. |
| 2.3 | Cột tên + SĐT khách | `OrderListItem` không có field khách. |
| 2.4 | Phân trang | Load nguyên dải ngày (tới 365 ngày) vào memory — không paging. |

## 1. Quyết định cần user chốt (Q)

- **Q1 — Granularity config (0.1):** scope `từng sản phẩm` phức tạp (đơn nhiều SP thuộc nhiều nhóm → flow nào thắng?). Đề xuất: **system → tenant → product-category (nếu có), KHÔNG tới từng SP** trong phase này. Chốt?
- **Q2 — Timeline timestamps (1.2):** Order không có per-status history (chỉ OrderDate/CompletedAt). Chọn: (a) thêm `OrderStatusHistory` table (migration PG + ghi tại TransitionStatusAsync — accurate, có audit) hoặc (b) chỉ hiển thị current/next không timestamp (nhẹ hơn). Đề xuất **(a)** — giá trị audit cao.
- **Q3 — Booking order nhận diện (0.2):** qua `TrackingCode` (booking code) hay thêm `Order.SourceType` ("pos"|"checkout"|"booking")? Đề xuất **thêm `SourceType`** (explicit, không phụ thuộc format code) — Domain additive, Gate 5.
- **Q4 — Lưu flow config:** PG-only (pattern FeaturedProduct — SystemAdmin qua Gateway admin API, ShopERP đọc qua `*ApiClient`, cache per-tenant). Chốt?
- **Q5 — Batch action (2.2):** giới hạn action theo lô = transition hợp lệ chung của các đơn được chọn (intersection) + confirm-payment. Kết quả per-order: success/fail từng đơn, không all-or-nothing. Chốt?

## 2. Design

### 2.1 Domain (1_Shared/Domain.cs — Gate 5, cần approve)
- `OrderFlowProfile` (entity, PG-only — pattern FeaturedProduct): `Id`, `Scope` (System|Tenant|Category), `TenantId?`, `CategoryKey?`, `IsActive`.
- `OrderFlowStep` (child): `Code` (string — "pending"/"confirmed"/"thanh_toan"/custom), `DisplayName`, `SortOrder`, `IsTerminal`, `AllowedNext` (JSON list codes hoặc bảng con `OrderFlowTransition`).
- `OrderStatusHistory` (entity): `OrderId`, `FromStatus`, `ToStatus`, `ChangedBy`, `ChangedAt`, `Reason` — append-only.
- `Order.SourceType` (string?, nullable additive — "pos"|"checkout"|"booking"|null legacy).

### 2.2 Services (3_CoreHub)
- `IOrderFlowService` — resolve profile (tenant → system fallback), trả `List<FlowAction>` cho 1 order (current + allowed next, bỏ qua step bị tắt). Cache in-memory per tenant (invalidate on config save).
- `OrderWorkflowService.IsTransitionValidAsync` — consult `IOrderFlowService` khi có profile; fallback hardcode hiện tại khi không có config (backward compatible, zero regression).
- `IOrderService` += `GetOrdersPagedAsync(tenantId, status?, dateRange, page, pageSize)` → `(items, total)` — repo-level Skip/Take + Count.
- `IOrderService` += `TransitionBatchAsync(orderIds, newStatus, tenantId)` → per-order result `{OrderId, Ok, Error}`.
- `TransitionStatusAsync` ghi `OrderStatusHistory` (nếu Q2=a).

### 2.3 Gateway (2_Gateway)
- `AdminOrderFlowController` (`/api/admin/order-flow` — SystemAdmin JWT): CRUD profiles + steps, activate/deactivate, reorder.

### 2.4 UI (5_WebApps/ShopERP — UI Platform 100%)
- `/admin/order-flow` — SystemAdmin config page (list profiles → step editor: thêm/sửa/xóa/reorder/toggle).
- `Orders/Detail.razor` — thêm khối Khách hàng (CustomerInfo.FullName/Phone + fallback Customer nav prop); timeline render từ flow service: step đã qua (check xanh + timestamp nếu Q2a), current (highlight), next possible (mờ + arrow).
- `Orders/Index.razor` — default `pending`+`today`; checkbox column + bulk action bar (ẩn khi 0 chọn); cột Khách hàng; pagination control (page size 20/50).

### 2.5 Data flow
```
Config: PG (OrderFlowProfile/Step) — SystemAdmin → Gateway admin API
Runtime: ShopERP OrderFlowService → Gateway tenant API → PG (cache 60s)
Order history: OrderStatusHistory — ghi ở scope transition chạy (SQLite ShopERP + PG Gateway — cả 2, sync qua outbox status event)
```

## 3. Phases

| Phase | Nội dung | Gate |
|---|---|---|
| P1 | Domain entities + EF config + migrations (PG `OrderFlowProfile/Step`, `OrderStatusHistory` + SQLite ShopERP mirror cho history; `Order.SourceType` cả 2) | Gate 5 — cần user approve |
| P2 | Services: OrderFlowService + paged query + batch transition + history write + tests | — |
| P3 | Gateway admin API + ShopERP ApiClient | — |
| P4 | UI: admin config page + Detail.razor rework | Gate 4 — E2E spec bắt buộc |
| P5 | UI: Index.razor (filter/batch/column/paging) + tests + E2E | Gate 4 |
| P6 | Deploy + RV L1-L4 (markers, routes, flow thật: config → order actions đổi; booking→order 2 actions) | RV rule |

## 4. Rủi ro & ràng buộc

- **Backward compat:** không có profile → giữ nguyên transition map hiện tại (kitchen on/off). Không migrate data cũ.
- **Kitchen flow:** `Kitchen_Workflow_Enabled` vẫn là bypass nhanh; flow config là lớp trên — nếu tenant có profile riêng thì profile thắng (ghi rõ trong code + docs).
- **Batch transition:** mỗi đơn 1 transaction riêng (không all-or-nothing); log per-order; UI hiện summary "x/y thành công".
- **Single-identity:** entities mới tuân thủ checklist (ctor set `Id = BusinessKey.Value`, `builder.Ignore(VO)`, query theo `e.Id`).
- **Không đụng:** AccountingEntry (immutable), payment flow, NATS subjects hiện hữu.

## 5. Test plan

- Core.Tests: OrderFlowService resolution (system→tenant→fallback), transition validation theo profile, batch partial-fail, history write.
- ShopERP.Tests: bUnit Detail (customer info, timeline current/next), Index (default filter, pagination, batch bar).
- E2E production: `/orders` default pending+today; chọn 2 đơn → xác nhận lô; detail hiện SĐT; admin tạo profile 2-step cho tenant → đơn tenant đó chỉ còn Thanh toán + Hoàn thành.
