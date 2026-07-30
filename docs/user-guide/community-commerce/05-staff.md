# HƯỚNG DẪN STAFF — NHÂN VIÊN QUẦY/POS

> **Đối tượng:** Nhân viên quầy / POS của tenant (cửa hàng F&B) — role `Staff` trong UserRole enum.
> **Đăng nhập:** Tài khoản Staff tại `https://khachvip.online` (ShopERP Blazor Server, cookie auth, role = Staff).
> **Phạm vi:** Tenant của mình — xử lý order tại quầy, POS, hỗ trợ settlement.

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
- Owner (chủ cửa hàng)
- StoreKeeper (thủ kho)
- Guard (bảo vệ)
- **Staff** (nhân viên quầy/POS)
- Masterchef (bếp trưởng)

> **Lưu ý:** Shipper/Salesman KHÔNG nằm trong UserRole enum — họ là CommunityRole (cross-tenant, tách biệt). Staff là role tenant-scoped.

**Kiến trúc data flow:**
- Orders trên **Gateway PG** (source of truth) → NATS routed → ShopERP SQLite (replica cho POS/kitchen display).
- Staff thao tác qua ShopERP Admin (Blazor Server, cookie auth).

---

## 2. ORDER MANAGEMENT — NHẬN + CONFIRM ĐƠN

### 2.1. Truy cập

Đăng nhập Staff → `/admin/orders` (hoặc trang Order Management tương ứng).

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
4. Staff **confirm** order (nếu status=pending) → status=`confirmed` → kitchen nhận (KitchenHub push).
5. Kitchen chuẩn bị → status=`preparing` → xong → status=`ready`.
6. Shipper thấy đơn `ready` trong Nearby Orders → accept → status=`delivering`.
7. Shipper delivered → status=`completed`.

### 2.4. Tạo order POS (Dine-in/Takeaway)

1. Staff bấm **Tạo đơn** → chọn products từ menu.
2. Chọn OrderType (DINEIN/TAKEAWAY) + table (nếu dine-in).
3. Nhập customer info (tên, SĐT — optional cho dine-in).
4. Apply discount/promo (nếu có).
5. **Confirm** → Order created → kitchen nhận (KitchenHub push).
6. Customer thanh toán tại quầy (cash/card/QR) → Staff confirm payment.

---

## 3. POS — BÁN HÀNG TẠU QUẦY (DINE-IN/TAKEAWAY)

### 3.1. POS flow

1. Staff mở POS page → chọn products (tap vào menu items).
2. Cart hiển thị: items, qty, price, total.
3. Chọn OrderType: DINEIN (chọn table) / TAKEAWAY.
4. Apply promo code / discount (nếu có).
5. Customer thanh toán:
   - **Cash:** Staff nhập amount nhận → tính thối.
   - **Card:** Staff quẹt card / nhập amount.
   - **QR (VietQR):** Customer quét QR → Staff confirm nhận tiền.
6. **Confirm order** → Order created + payment confirmed → kitchen nhận.
7. In bill / KOT (Kitchen Order Ticket) — optional.

### 3.2. POS vs Delivery

| Khía cạnh | POS (Dine-in/Takeaway) | Delivery |
|---|---|---|
| Source | Staff tạo | Customer đặt qua KhachLink |
| Payment | Tại quầy (cash/card/QR) | COD (shipper thu) hoặc online |
| Shipper | Không cần | Cần shipper accept + giao |
| Kitchen | KitchenHub push | KitchenHub push |

---

## 4. ORDER STATUS TRANSITIONS (STAFF PERSPECTIVE)

```
pending → confirmed → preparing → ready → delivering → completed
                                                       ↘ cancelled
```

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

- Staff tạo/confirm order → **KitchenHub push** real-time → bếp thấy order mới.
- Kitchen `ready` → Staff thấy status update → thông báo shipper/customer.
- Nếu đổi món / hủy → Staff update order → KitchenHub push update.

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
