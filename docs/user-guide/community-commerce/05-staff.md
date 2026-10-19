# HƯỚNG DẪN STAFF — NHÂN VIÊN QUẦY/POS

> **Đối tượng:** Nhân viên quầy / POS của tenant (cửa hàng F&B) — role `Staff` trong UserRole enum.
> **Đăng nhập:** Tài khoản Staff tại `https://app2.khachvip.online` (ShopERP Blazor Server, cookie auth, role = Staff).
> **Phạm vi:** Tenant của mình — xử lý order tại quầy, POS, đặt lịch nhanh, hỗ trợ settlement.
>
> **Cập nhật 2026-10-19:** Đồng bộ route thực tế (`/orders`, `/pos`, `/pos/payment/{id}`, `/booking/create`, `/kitchen`) + UserRole mới + order flow configurable (#195).

---

## MỤC LỤC

1. [Tổng quan vai trò](#1-tổng-quan-vai-trò)
2. [Order Management — nhận + confirm đơn](#2-order-management--nhận--confirm-đơn)
3. [POS — bán hàng tại quầy (Dine-in/Takeaway)](#3-pos--bán-hàng-tại-quầy-dine-intakeaway)
4. [Order status transitions (Staff perspective)](#4-order-status-transitions-staff-perspective)
5. [Hỗ trợ Settlement (Marketplace vs Reseller)](#5-hỗ-trợ-settlement-marketplace-vs-reseller)
6. [Coordination với Kitchen + Shipper](#6-coordination-với-kitchen--shipper)
7. [FAQ](#7-faq)

---

## 1. TỔNG QUAN VAI TRÒ

Staff là nhân viên quầy/POS của tenant — xử lý order từ customer (delivery + dine-in + takeaway), điều phối kitchen, hỗ trợ settlement với shipper/Vạn An.

**UserRole enum (tenant RBAC — KHÔNG phải CommunityRole):**
- Owner=1 (chủ cửa hàng)
- StoreKeeper=2 (thủ kho)
- Guard=3 (bảo vệ)
- **Staff=4** (nhân viên quầy/POS)
- Masterchef=5 (bếp trưởng)
- ChiefAccountant=6 (kế toán trưởng — 2026-10-09)
- DebtAccountant=7 (kế toán công nợ — 2026-10-09)

> Menu "Vận hành" của Staff gồm: **POS** `/pos` · **Đặt lịch nhanh** `/booking/create` · **Bếp** `/kitchen` · **Đơn hàng** `/orders` — tất cả `Policy = "StaffOrAbove"`.

> **Lưu ý:** Shipper/Salesman KHÔNG nằm trong UserRole enum — họ là CommunityRole (cross-tenant, tách biệt). Staff là role tenant-scoped.

**Kiến trúc data flow:**
- Orders trên **Gateway PG** (source of truth) → NATS routed → ShopERP SQLite (replica cho POS/kitchen display).
- Staff thao tác qua ShopERP Admin (Blazor Server, cookie auth).

---

## 2. ORDER MANAGEMENT — NHẬN + CONFIRM ĐƠN

### 2.1. Truy cập

Đăng nhập Staff → **`/orders`** (Orders/Index.razor — default filter `pending` + `today`, có bulk select/action bar, server-side pagination, booking-aware action labels). Chi tiết đơn: `/orders/{id}`.

### 2.2. Order types

| OrderType | Flow |
|---|---|
| DELIVERY | Customer đặt qua KhachLink → shipper giao |
| DINEIN | Customer ăn tại quán → Staff tạo order POS |
| TAKEAWAY | Customer mang đi → Staff tạo order POS |

### 2.3. Nhận đơn Delivery (từ KhachLink)

1. Customer đặt hàng qua KhachLink → Order created trên Gateway PG (status=`confirmed` hoặc `pending`).
2. NATS routed → ShopERP SQLite (replica).
3. Staff thấy order mới trong Order Management (real-time qua OrderHub SignalR).
4. Staff **confirm** order (nếu status=pending) → status=`confirmed` → kitchen nhận (/orderHub push).
5. Kitchen chuẩn bị → status=`preparing` → xong → status=`ready`.
6. Shipper thấy đơn `ready` trong Nearby Orders → accept → status=`delivering`.
7. Shipper delivered → status=`completed`.

### 2.4. Tạo order POS (Dine-in/Takeaway)

1. Staff mở **`/pos`** → chọn products từ menu (tap items).
2. Cart hiện items/qty/price/total → nhập customer info (tên, SĐT — optional).
3. Bấm **"Tạo đơn"** → `IOrderService.CreateOrderFromCommandAsync` (atomic outbox) → Order `SourceType="pos"` tạo trong ShopERP → sync lên Gateway PG.
4. Redirect **`/pos/payment/{orderId}`** → customer trả tại quầy (cash/card/QR) → Staff confirm payment.
5. Kitchen nhận qua `/orderHub` push (OrderCreated).

### 2.5. Đặt lịch nhanh (Booking tại quầy)

Staff tạo booking hộ khách tại **`/booking/create`** (POS-style: catalog → ngày/giờ slot → staff → thông tin khách → tạo). Vào hàng đợi `/booking/queue` như booking khách tự đặt.

---

## 3. POS — BÁN HÀNG TẠU QUẦY (DINE-IN/TAKEAWAY)

### 3.1. POS flow

1. Staff mở **`/pos`** (POS/Create.razor) → chọn products (tap vào menu items).
2. Cart hiển thị: items, qty, price, total.
3. Bấm **"Tạo đơn"** → Order `SourceType="pos"` created.
4. Redirect **`/pos/payment/{orderId}`** (POS/Payment.razor) → customer thanh toán:
   - **Cash:** Staff nhập amount nhận → tính thối.
   - **QR:** Customer quét QR → Staff confirm nhận tiền.
5. Payment confirmed → kitchen nhận (`/orderHub` push).
6. In bill — trang `/orders/{id}/print` (PrintBill.razor).

### 3.2. POS vs Delivery

| Khía cạnh | POS (Dine-in/Takeaway) | Delivery |
|---|---|---|
| Source | Staff tạo | Customer đặt qua KhachLink |
| Payment | Tại quầy (cash/card/QR) | COD (shipper thu) hoặc online |
| Shipper | Không cần | Cần shipper accept + giao |
| Kitchen | /orderHub push | /orderHub push |

---

## 4. ORDER STATUS TRANSITIONS (STAFF PERSPECTIVE)

```
pending → confirmed → preparing → ready → delivering → completed
                                                       ↘ cancelled
```

> **Cập nhật (#195):** Flow **configurable per-tenant/per-source** (`OrderFlowProfile`) — booking/POS có thể dùng flow rút gọn. Các action trên trang `/orders` render động theo `AllowedNext` của flow đang áp dụng; mọi transition ghi `OrderStatusHistory`.

| Status | Ai chuyển | Staff action |
|---|---|---|
| pending | Staff | Confirm đơn (nếu cần) |
| confirmed | Staff/Kitchen | Kitchen nhận tự động |
| preparing | Kitchen | (Staff chỉ xem) |
| ready | Kitchen | Staff thông báo shipper / customer đến lấy |
| delivering | Shipper | (Staff chỉ xem) |
| completed | Shipper (delivery) / Staff (POS) | Đơn xong |
| cancelled | Staff/Owner | Hủy đơn + hoàn tiền (nếu đã thanh toán) |

### 4.1. Staff confirm order

- Delivery: confirm → kitchen nhận.
- POS: tạo + confirm 1 bước (payment confirmed luôn).

### 4.2. Cancel order

- Staff/Owner có quyền cancel.
- Nếu đã thanh toán (POS) → hoàn tiền (Reversal / refund).
- Nếu COD (delivery) → thông báo shipper không giao.

---

## 5. HỖ TRỢ SETTLEMENT (MARKETPLACE VS RESELLER)

### 5.1. Marketplace — Shipper ↔ Shop trực tiếp

1. Shipper thu COD từ customer.
2. Shipper confirm COD → Settlement record tạo.
3. **Staff/Owner xác nhận nhận tiền** từ shipper → `WalletTransaction(Settlement)` -amount shipper, +amount shop.
4. Nếu shipper ứng tiền (AdvancePayment): Staff xác nhận nhận advance → `WalletTransaction(AdvancePayment)` +amount shop.

### 5.2. Reseller — Tất cả qua Vạn An wallet

1. Shipper thu COD → confirm → Vạn An nhận (6 transactions).
2. Settlement: Vạn An wallet → Shop wallet (`WalletTransaction.Settlement)`).
3. **Staff/Owner xác nhận nhận tiền** từ Vạn An wallet.
4. AdvancePayment: Vạn An ứng trước → Staff xác nhận nhận advance.
5. Staff không deal trực tiếp với shipper về tiền — tất cả qua Vạn An.

### 5.3. Staff role trong settlement

- Staff **xác nhận nhận tiền** (confirm settlement) — không tự tạo transaction.
- Owner có quyền cao hơn (xem full settlement history, approve/reject).
- Nếu sai → Reversal pattern (admin tạo, không Staff).

---

## 6. COORDINATION VỚI KITCHEN + SHIPPER

### 6.1. Với Kitchen

- Staff tạo/confirm order → **`/orderHub` push** real-time (events `OrderCreated`/`OrderStatusChanged`/`PaymentConfirmed` — SignalR, host trên ShopERP) → bếp thấy order mới trên `/kitchen`.
- Kitchen `ready` → Staff thấy status update → thông báo shipper/customer.
- Nếu đổi món / hủy → Staff update order → hub push update.

### 6.2. Với Shipper

- Staff KHÔNG trực tiếp assign shipper — shipper tự accept từ Nearby Orders.
- Staff thấy shipper nào accept (Order.ShipperId).
- Staff hỗ trợ shipper: đưa hàng, confirm pickup (nếu cần), chat qua KhachLink (nếu Staff cũng có customer account).

### 6.3. Với Customer (Delivery)

- Staff không chat trực tiếp (chat là Customer ↔ Shipper).
- Staff liên hệ customer qua SĐT (nếu cần) — xem order detail.
- Customer tracking shipper qua KhachLink (GPS + map).

---

## 7. FAQ

**Q: Staff có thể kích hoạt role Shipper/Salesman không?**
A: KHÔNG trực tiếp. Staff là tenant role. Nếu Staff cũng là Customer trên KhachLink + đạt điều kiện → System Admin kích hoạt CommunityRole (tách biệt với tenant role).

**Q: POS có hỗ trợ Reseller mode không?**
A: POS (Dine-in/Takeaway) không phụ thuộc mode — customer trả trực tiếp tại quầy. Mode chỉ affect Delivery COD flow.

**Q: Staff có thể hủy đơn không?**
A: CÓ (nếu Owner cho phép). Cancel → hoàn tiền nếu đã thanh toán. Thông báo shipper không giao (nếu delivery).

**Q: Đơn Delivery từ KhachLink, Staff cần làm gì?**
A: Confirm đơn (nếu status=pending) → kitchen nhận tự động. Khi kitchen `ready` → thông báo shipper (shipper tự thấy trong Nearby Orders).

**Q: Settlement sai, Staff sửa được không?**
A: KHÔNG. WalletTransaction immutable. Báo Owner/SystemAdmin → tạo Reversal pattern (append-only).

**Q: Staff thấy data tenant khác không?**
A: KHÔNG. Staff tenant-scoped (cookie auth + role claims + HasQueryFilter IMustHaveTenant.TenantId).

---

> **Xem thêm:** [README index](./README.md) | [Shop Owner](./02-owner.md) | [Kitchen](./06-kitchen.md) | [Shipper](./04-shipper.md)
