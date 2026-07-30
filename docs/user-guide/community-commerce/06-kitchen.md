# HƯỚNG DẪN BẾP / KITCHEN — KITCHEN DISPLAY

> **Đối tượng:** Bếp / Kitchen Staff — role `Masterchef` (bếp trưởng) hoặc nhân viên bếp trong tenant.
> **Đăng nhập:** Tài khoản Masterchef/Kitchen tại `https://khachvip.online` (ShopERP Blazor Server, cookie auth).
> **Phạm vi:** Tenant của mình — nhận order từ POS + Delivery, chuẩn bị, báo ready.

---

## MỤC LỤC

1. [Tổng quan vai trò](#1-tổng-quan-vai-trò)
2. [Kitchen Display System (KDS)](#2-kitchen-display-system-kds)
3. [Nhận order real-time (KitchenHub)](#3-nhận-order-real-time-kitchenhub)
4. [Order status transitions (Kitchen perspective)](#4-order-status-transitions-kitchen-perspective)
5. [Báo ready — thông báo shipper/staff](#5-báo-ready--thông-báo-shipperstaff)
6. [Coordination với Staff + Shipper](#6-coordination-với-staff--shipper)
7. [FAQ](#7-faq)

---

## 1. TỔNG QUAN VAI TRÒ

Bếp/Kitchen nhận order từ 2 nguồn:
- **POS (Dine-in/Takeaway):** Staff tạo order tại quầy → KitchenHub push.
- **Delivery (KhachLink):** Customer đặt online → Staff confirm → KitchenHub push.

Bếp chuẩn bị → báo `ready` → shipper (delivery) hoặc customer (dine-in/takeaway) nhận.

**UserRole enum:**
- **Masterchef** (bếp trưởng) — quyền cao nhất kitchen.
- Staff (nhân viên quầy) — có thể cũng thao tác kitchen nếu tenant nhỏ.

**Kiến trúc:**
- Orders trên **Gateway PG** (source of truth) → NATS routed → ShopERP SQLite (replica cho kitchen display).
- **KitchenHub** (SignalR) push real-time khi order mới / status change.

---

## 2. KITCHEN DISPLAY SYSTEM (KDS)

### 2.1. Truy cập

Đăng nhập Masterchef → `/admin/kitchen` (hoặc trang Kitchen Display tương ứng).

### 2.2. Layout

Kitchen Display thường hiển thị dạng **kanban board** hoặc **list**:
- **Columns/sections:** theo status (New/Preparing/Ready)
- **Cards:** mỗi order là 1 card — hiện order #, items, qty, notes, time elapsed, table (dine-in) / address (delivery)
- **Color coding:** 
  - New (vừa nhận) — xanh
  - Preparing (đang làm) — vàng
  - Ready (xong) — xanh lá
  - Overdue (quá lâu) — đỏ (alert)

### 2.3. Card info

Mỗi order card hiện:
- Order # (short ID)
- OrderType (DELIVERY/DINEIN/TAKEAWAY)
- Items list: product name, qty, modifiers (vd "Không hành", "Ít cay")
- Notes (special requests)
- Time elapsed (timer — alert nếu quá lâu)
- Table (dine-in) / Delivery address (delivery)
- Customer name (nếu có)

---

## 3. NHẬN ORDER REAL-TIME (KITCHENHUB)

### 3.1. KitchenHub (SignalR)

- Khi Staff tạo/confirm order → **KitchenHub push** real-time → bếp thấy card mới ngay lập tức.
- Không cần refresh page — SignalR WebSocket persistent.
- Auth: cookie auth (ShopERP Blazor Server) — không cần X-Customer-Token.

### 3.2. Order sources

| Source | Flow |
|---|---|
| POS (Dine-in) | Staff tạo + confirm → KitchenHub push |
| POS (Takeaway) | Staff tạo + confirm → KitchenHub push |
| Delivery (KhachLink) | Customer đặt → Staff confirm → KitchenHub push |

### 3.3. Update từ kitchen

Khi bếp chuyển status (New → Preparing → Ready) → KitchenHub push update → Staff/Shipper thấy real-time.

---

## 4. ORDER STATUS TRANSITIONS (KITCHEN PERSPECTIVE)

```
confirmed → preparing → ready
```

| Status | Ai chuyển | Kitchen action |
|---|---|---|
| confirmed | Staff | Kitchen nhận tự động (KitchenHub push) |
| preparing | Kitchen | Bếp bấm "Bắt đầu làm" → status=preparing |
| ready | Kitchen | Bếp bấm "Xong" → status=ready → thông báo shipper/staff |

### 4.1. Bếp "Bắt đầu làm"

1. Bếp thấy card mới (status=confirmed).
2. Bấm **"Bắt đầu làm"** → status=`preparing` → timer bắt đầu.
3. Card chuyển sang column "Preparing".

### 4.2. Bếp "Xong" (Ready)

1. Bếp xong món → bấm **"Xong"** → status=`ready`.
2. Card chuyển sang column "Ready".
3. **KitchenHub push** → Staff thấy ready → thông báo:
   - Delivery: shipper thấy đơn `ready` trong Nearby Orders → accept.
   - Dine-in: Staff mang ra bàn.
   - Takeaway: Staff gọi customer đến lấy.

---

## 5. BÁO READY — THÔNG BÁO SHIPPER/STAFF

### 5.1. Delivery — shipper tự thấy

Khi kitchen `ready`:
- Order status=`ready` → shipper thấy đơn trong Nearby Orders (status `ready`).
- Shipper tự accept (UC-04) → không cần kitchen/staff assign.
- Shipper đến shop lấy hàng → bấm "Đã nhận hàng" (UC-05).

### 5.2. Dine-in — Staff mang ra

Khi kitchen `ready`:
- Staff thấy status=`ready` → mang món ra bàn.
- Customer ăn → Staff confirm completed (hoặc tự động).

### 5.3. Takeaway — Customer đến lấy

Khi kitchen `ready`:
- Staff thấy status=`ready` → gọi customer (nếu customer đang chờ) hoặc customer đến lấy.
- Staff confirm completed.

---

## 6. COORDINATION VỚI STAFF + SHIPPER

### 6.1. Với Staff

- Kitchen nhận order từ Staff (POS) hoặc Customer (Delivery, Staff confirm).
- Kitchen `ready` → Staff thấy real-time → điều phối (mang ra bàn / thông báo shipper).
- Nếu đổi món / hủy → Staff update order → KitchenHub push update → bếp thấy thay đổi.

### 6.2. Với Shipper

- Kitchen KHÔNG trực tiếp liên hệ shipper.
- Shipper tự thấy đơn `ready` trong Nearby Orders → accept → đến shop.
- Shipper bấm "Đã nhận hàng" (pickup) → kitchen/staff thấy status=`delivering`.

### 6.3. Exception handling

| Tình huống | Xử lý |
|---|---|
| Hết nguyên liệu | Báo Staff → Staff liên hệ customer đổi món / hủy |
| Món làm sai | Báo Staff → Staff quyết định (làm lại / hoàn tiền) |
| Order quá lâu (overdue) | Timer alert đỏ → ưu tiên làm |
| Customer hủy đơn | Staff cancel → KitchenHub push → bếp bỏ card |

---

## 7. FAQ

**Q: Bếp có cần đăng nhập Google / device fingerprint không?**
A: KHÔNG. Bếp dùng ShopERP Blazor Server (cookie auth, role Masterchef). Device fingerprint chỉ cho KhachLink PWA (customer/salesman/shipper).

**Q: Bếp có thể chat với customer không?**
A: KHÔNG trực tiếp. Chat (UC-07) là Customer ↔ Shipper. Bếp báo Staff nếu cần liên hệ customer.

**Q: Kitchen Display có trên mobile không?**
A: ShopERP Blazor Server responsive — có thể mở trên tablet/mobile browser. Nhưng PoC tối ưu cho desktop/tablet trong bếp.

**Q: Đơn Delivery và Dine-in hiện chung không?**
A: Tùy config KDS — có thể filter theo OrderType, hoặc hiện chung với badge phân biệt (DELIVERY/DINEIN/TAKEAWAY).

**Q: Bếp có thấy giá tiền không?**
A: Tùy config — thường KDS chỉ hiện items + qty + notes, không hiện giá (bếp không cần). POS Staff thấy giá.

**Q: Mode Marketplace/Reseller ảnh hưởng kitchen không?**
A: KHÔNG. Kitchen chỉ quan tâm order + items + status. Mode affect financial flow (settlement/COD), không affect kitchen operations.

**Q: Bếp có thể tự tạo order không?**
A: KHÔNG (PoC). Staff/Owner tạo order. Bếp chỉ nhận + chuẩn bị + báo ready. Post-PoC có thể thêm kitchen-initiated order (vd món test).

---

> **Xem thêm:** [README index](./README.md) | [Shop Owner](./02-owner.md) | [Staff](./05-staff.md) | [Shipper](./04-shipper.md)
