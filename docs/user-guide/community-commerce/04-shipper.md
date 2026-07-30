# HƯỚNG DẪN SHIPPER — CỘNG TÁC VIÊN GIAO HÀNG

> **Đối tượng:** Customer được System Admin kích hoạt role Shipper — nhận đơn giao hàng, thu COD, kiếm tiền từ phí giao hàng.
> **Đăng nhập:** KhachLink PWA (`diemthuong.khachvip.online`) — Google login + device fingerprint. Token lưu localStorage.
> **Nền tảng:** Blazor WebAssembly PWA — GPS chỉ hoạt động khi tab active. **Giữ app mở** khi đang giao.

---

## MỤC LỤC

1. [Tổng quan vai trò + điều kiện](#1-tổng-quan-vai-trò--điều-kiện)
2. [Đăng nhập + kích hoạt role](#2-đăng-nhập--kích-hoạt-role)
3. [Đơn hàng gần (Nearby Orders) — UC-03](#3-đơn-hàng-gần-nearby-orders--uc-03)
4. [Nhận đơn (Accept Order) — UC-04](#4-nhận-đơn-accept-order--uc-04)
5. [Chi tiết đơn hàng (OrderDetail)](#5-chi-tiết-đơn-hàng-orderdetail)
6. [Cập nhật trạng thái giao hàng — UC-05](#6-cập-nhật-trạng-thái-giao-hàng--uc-05)
7. [Cập nhật vị trí real-time — UC-06](#7-cập-nhật-vị-trí-real-time--uc-06)
8. [Chat với Customer — UC-07](#8-chat-với-customer--uc-07)
9. [Ứng tiền + thu COD — UC-11](#9-ứng-tiền--thu-cod--uc-11)
10. [Ví (Wallet) + rút tiền](#10-ví-wallet--rút-tiền)
11. [Marketplace vs Reseller — khác biệt cho Shipper](#11-marketplace-vs-reseller--khác-biệt-cho-shipper)
12. [FAQ](#12-faq)

---

## 1. TỔNG QUAN VAI TRÒ + ĐIỀU KIỆN

Shipper là cộng tác viên giao hàng — nhận đơn DELIVERY trong bán kính, giao hàng, thu COD (nếu có), cập nhật vị trí real-time cho customer theo dõi.

### 1.1. Điều kiện trở thành Shipper

- `IdentityLevel ≥ DeviceVerified` (device fingerprint pass) **HOẶC** `IdentityLevel ≥ Verified` (SMS OTP) + `LoyaltyPoints ≥ 1000`
- **Kích hoạt bởi System Admin**
- Khi toggle `CollaboratorSmsVerificationEnabled = ON`: bắt buộc SMS OTP + `IsPhoneVerified = true` + deposit wallet ≥ phí OTP

### 1.2. Một user nhiều role

Bạn có thể đồng thời là **Customer + Salesman + Shipper**. NavMenu KhachLink hiện tab theo role bạn có.

### 1.3. PWA GPS limitation

- GPS chỉ hoạt động khi **tab active** (mở app, không tắt).
- Polling adaptive: `OutForDelivery` 10s, `PickedUp` 30s, `Delivered/Failed` stop.
- **Thông báo "Giữ app mở để cập nhật vị trí"** — customer không thấy shipper di chuyển nếu shipper tắt tab.
- Post-PoC: đánh giá native app cho background GPS.

---

## 2. ĐĂNG NHẬP + KÍCH HOẠT ROLE

### 2.1. Đăng nhập (UC-01)

1. Mở KhachLink → bấm **Đăng nhập**.
2. Chọn **Google** → social auth redirect.
3. First login / new device: **Device Fingerprint Consent Dialog** → bấm **Đồng ý** → POST `/api/community/device/register`.
4. IdentityLevel = `Social` (1).
5. (OPTIONAL) Verify SĐT qua SMS OTP → upgrade `Verified` — bắt buộc khi toggle ON.
6. Token lưu localStorage.

> **Max 3 active devices per Customer.** Device thứ 4 → admin approval.

### 2.2. Kích hoạt role Shipper

1. Đạt điều kiện (IdentityLevel + LoyaltyPoints ≥ 1000).
2. System Admin kích hoạt → nhận push notification.
3. Login lại → NavMenu hiện thêm tab **"Đơn hàng gần"** + **"Đang giao"** + **"Ví"**.

### 2.3. SMS OTP verification (chỉ khi toggle ON)

Tương tự Salesman — xem [Salesman Section 2.3](./03-salesman.md#23-sms-otp-verification-chỉ-khi-toggle-on).

---

## 3. ĐƠN HÀNG GẦN (NEARBY ORDERS) — UC-03

### 3.1. Truy cập

Login KhachLink → tab **"Đơn hàng gần"** (chỉ hiện khi có role Shipper).

### 3.2. Flow

1. Mở page → browser lấy GPS vị trí hiện tại (1 lần, consent prompt).
2. Gọi API: `GET /api/community/nearby-orders?lat={lat}&lng={lng}&radiusKm=5`.
3. Hiển thị list đơn hàng `OrderType=DELIVERY` + status `confirmed` hoặc `ready` trong bán kính 5km.
4. Mỗi đơn hiện:
   - Shop name
   - Delivery address
   - Total amount (VND format: `{amount:N0}đ`)
   - Status (confirmed/ready)
   - Khoảng cách (Haversine formula)
5. Sort theo khoảng cách tăng dần.
6. **Không hiện đơn đã assign cho shipper khác.**

### 3.3. GPS consent

- Browser hỏi "Cho phép KhachLink truy cập vị trí?" → bấm **Cho phép**.
- Nếu deny → không thấy đơn hàng gần. Cần vào browser settings → cho phép lại.

---

## 4. NHẬN ĐƠN (ACCEPT ORDER) — UC-04

### 4.1. Flow

1. Shipper bấm **"Nhận đơn"** trên đơn trong list.
2. `POST /api/community/orders/{orderId}/accept`.
3. Tạo `DeliveryTask` record: shipperId, orderId, status=`Assigned`.
4. Order status → `delivering` (nếu đang `ready`) hoặc giữ `confirmed` (nếu chưa ready).
5. Shipper thấy shop location + customer location (chuyển sang OrderDetail page).

### 4.2. Concurrency

- Chỉ **1 shipper** accept được (optimistic locking hoặc DB unique constraint).
- Nếu đơn đã được accept → trả **409 Conflict** → shipper thấy "Đơn đã được nhận".

### 4.3. Sau accept

- Đơn biến mất khỏi Nearby Orders list (đã assign).
- Shipper chuyển sang tab **"Đang giao"** để xem đơn đang xử lý.

---

## 5. CHI TIẾT ĐƠN HÀNG (ORDERDETAIL)

### 5.1. Route

`/community/orders/{orderId}` — mở sau khi accept, hoặc từ tab "Đang giao".

### 5.2. UI Elements

- **Header:** "Chi tiết đơn hàng #{orderId short}"
- **Shop info card:** shop name, shop address, shop lat/lng (từ TenantSettings)
- **Customer info card:** delivery address, delivery lat/lng (từ Order.DeliveryLat/Lng)
- **Map:** LeafletMap component với:
  - Shop marker (red)
  - Customer marker (green)
  - Route line (straight — PoC, không OSRM)
- **Order items list:** product name, qty, price
- **Total amount** (VND format)
- **Status badge:** current Order.Status (color-coded)
- **Back button** → NearbyOrders page

### 5.3. States

- **Loading:** spinner
- **Error (order not found / not authorized):** "Không tìm thấy đơn hàng hoặc bạn không có quyền xem"
- **Empty (no delivery coords):** "Khách chưa cung cấp vị trí giao hàng — gọi khách để xác nhận"

> **Lưu ý:** Map dùng **Leaflet.js** (open-source, free, không phải Google Maps iframe). Marker cho shipper + shop + customer.

---

## 6. CẬP NHẬT TRẠNG THÁI GIAO HÀNG — UC-05

### 6.1. State machine

```
Assigned → PickedUp → OutForDelivery → Delivered
                                    ↘ Failed
```

### 6.2. Flow

1. Shipper đến shop → bấm **"Đã nhận hàng"** → `POST /api/community/orders/{orderId}/pickup` → `DeliveryTask.PickedUpAt` set.
2. Shipper giao hàng → bấm **"Đang giao"** → `POST /api/community/orders/{orderId}/delivering` → `DeliveryTask.OutForDelivery` set. **GPS polling bắt đầu 10s interval.**
3. Shipper đến nơi → bấm **"Đã giao"** → `POST /api/community/orders/{orderId}/delivered` → `DeliveryTask.DeliveredAt` set. Order status → `completed`. **GPS polling stop.**
4. (Tùy chọn) Shipper bấm **"Giao thất bại"** → `POST /api/community/orders/{orderId}/failed` → `DeliveryTask.Failed` + reason text required. **GPS polling stop.**

### 6.3. Mỗi transition

- Ghi timestamp (PickedUpAt, OutForDelivery, DeliveredAt, FailedAt).
- Order status sync: Delivered → Order.Completed.
- **Customer nhận SignalR notification** mỗi transition (real-time).
- Failed transition yêu cầu **reason text** (vd "Khách không nghe máy", "Sai địa chỉ").

### 6.4. DeliveryTracking buttons (UI spec)

| Button | API | Status transition |
|---|---|---|
| Đã nhận hàng | `/pickup` | Assigned → PickedUp |
| Đang giao | `/delivering` | PickedUp → OutForDelivery |
| Đã giao | `/delivered` | OutForDelivery → Delivered |
| Giao thất bại | `/failed` | (any) → Failed + reason |

---

## 7. CẬP NHẬT VỊ TRÍ REAL-TIME — UC-06

### 7.1. Flow

1. Khi DeliveryTask status = `OutForDelivery` → page tự poll GPS mỗi **10s** (khi tab active).
2. `POST /api/community/location/update { lat, lng }`.
3. Server push qua **SignalR LocationHub** → customer subscribe `order_{orderId}`.
4. Customer thấy marker shipper di chuyển trên map (Leaflet).

### 7.2. Adaptive polling

| Status | Interval | Lý do |
|---|---|---|
| OutForDelivery | 10s | Cần tracking chính xác |
| PickedUp | 30s | Chưa giao, ít cần update |
| Delivered/Failed/Cancelled | Stop | Không cần tracking |

### 7.3. PWA limitation

- GPS chỉ hoạt động khi **tab active**.
- **Thông báo "Giữ app mở để cập nhật vị trí"** — hiển thị trên UI.
- Nếu shipper tắt tab / switch app → GPS stop → customer không thấy update.
- Location lưu vào `DeliveryTracking` table (append-only).

### 7.4. Auth SignalR

- LocationHub auth qua `X-Customer-Token` query string (SignalR support).
- nginx route cho `/locationHub` negotiate.

---

## 8. CHAT VỚI CUSTOMER — UC-07

### 8.1. Mở chat

1. Shipper mở chat panel trong OrderDetail (hoặc tab chat riêng).
2. Chat chỉ mở khi `DeliveryTask` tồn tại (shipper đã accept đơn).

### 8.2. Flow

1. Nhập message → `POST /api/community/chat/messages`.
2. **SignalR ChatHub** push đến customer real-time.
3. Message lưu DB (`Conversation` + `Message` entity) với timestamp, senderId, receiverId.
4. Chat history load khi mở panel.

### 8.3. Lưu ý

- **Human-to-human chat** — KHÔNG có AI chatbot trong PoC.
- Chat biến mất khi DeliveryTask = Delivered/Failed (hoặc giữ history nhưng không push mới).
- Auth ChatHub qua `X-Customer-Token` query string.

---

## 9. ỨNG TIỀN + THU COD — UC-11

> **Chỉ khi Order.PaymentMethod = COD.** Reseller mode: Vạn An ứng tiền, shipper KHÔNG ứng.

### 9.1. Marketplace mode — Shipper ứng + thu COD

1. Shipper thấy **"Cần ứng tiền"** trên đơn (nếu shop yêu cầu advance payment).
2. Shipper xác nhận đã ứng tiền cho shop → `POST /api/community/wallet/confirm-advance` → `WalletTransaction(AdvancePayment)` -amount shipper, +amount shop.
3. Shipper thu tiền của customer khi giao.
4. Shipper xác nhận đã thu COD → `POST /api/community/wallet/confirm-cod` → `WalletTransaction(CODCollection)` +amount shipper.
5. Settlement record tạo: shipper chuyển tiền cho shop → `WalletTransaction(Settlement)` -amount shipper, +amount shop.

**Net flow shipper:** +COD (thu từ customer) -Settlement (trả shop) -AdvancePayment (ứng shop) = phí giao hàng (lợi nhuận shipper).

### 9.2. Reseller mode — Vạn An ứng, shipper chỉ thu COD

1. **KHÔNG có "Ứng tiền cho shop" button** (Vạn An ứng, không phải shipper).
2. Shipper thu tiền customer khi giao.
3. Shipper xác nhận đã thu COD → `POST /api/community/wallet/confirm-cod` → Vạn An tạo **6 transactions** (xem README Section 6.2):
   - CODCollection (+COD cho shipper)
   - Settlement (-COD, chuyển shop)
   - DeliveryFee (+delivery fee cho shipper)
   - Commission (+commission cho salesman, nếu có)
   - PlatformFee (+platform fee cho Vạn An)
   - CommunityFund (+community fund)
4. Shipper nhận **DeliveryFee** (phí giao hàng Vạn An trả) — tách khỏi COD.

**Net flow shipper (Reseller):** +DeliveryFee (phí giao hàng). COD thu hộ Vạn An, không phải lợi nhuận shipper.

### 9.3. UI difference (DeliveryTracking.razor)

```
@if (commerceMode == "Marketplace")
{
    <VanAnButton OnClick="ConfirmAdvance">Ứng tiền cho shop</VanAnButton>
}
// Reseller mode: ẩn advance button (Vạn An ứng, không phải shipper)
```

---

## 10. VÍ (WALLET) + RÚT TIỀN

### 10.1. Xem ví

Login KhachLink → tab **"Ví"** — hiển thị:
- Balance hiện tại (VND)
- Mode badge (Marketplace/Reseller)
- List `WalletTransaction` (immutable ledger — append-only)

### 10.2. Transaction types shipper thấy

| Type | Mode | Ý nghĩa |
|---|---|---|
| CODCollection | Cả 2 | +COD (thu từ customer) |
| AdvancePayment | Marketplace | -amount (ứng shop) |
| Settlement | Cả 2 | -amount (trả shop) hoặc +amount (nhận từ Vạn An Reseller) |
| DeliveryFee | Reseller | +delivery fee (phí giao hàng Vạn An trả) |
| Withdrawal | Cả 2 | -amount (rút tiền) |
| Reversal | Cả 2 | Hoàn giao dịch sai |

### 10.3. Rút tiền

Tương tự Salesman — KYC bank account + min 500.000đ + balance đủ. Xem [Salesman Section 7.2](./03-salesman.md#72-rút-tiền-withdrawal).

### 10.4. Reversal pattern

Nếu confirm COD sai → KHÔNG update. Tạo `WalletTransaction(Reversal)` amount=-original, `RelatedTransactionId=original.Id`.

---

## 11. MARKETPLACE VS RESELLER — KHÁC BIỆT CHO SHIPPER

| Khía cạnh | Marketplace | Reseller |
|---|---|---|
| **Ứng tiền cho shop** | Shipper ứng (AdvancePayment) | Vạn An ứng — shipper KHÔNG ứng |
| **COD flow** | Shipper thu hộ → shop nhận trực tiếp | Shipper thu hộ → Vạn An nhận → phân phối |
| **Lợi nhuận shipper** | Phí giao hàng (từ Settlement spread) | DeliveryFee (Vạn An trả riêng) |
| **Settlement** | Shipper ↔ Shop trực tiếp | Tất cả qua Vạn An wallet |
| **Advance button UI** | Có | Ẩn |
| **Wallet badge** | "Marketplace — Tenant bán trực tiếp" | "Reseller — Vạn An mua bán" |

---

## 12. FAQ

**Q: GPS không cập nhật khi tôi tắt app?**
A: Đúng. PWA chỉ track GPS khi tab active. **Giữ app mở** khi đang giao. Thông báo hiển thị trên UI.

**Q: Đơn đã được shipper khác nhận, tôi có nhận được không?**
A: KHÔNG. Chỉ 1 shipper accept được. Nếu đã accept → 409 Conflict. Đơn biến mất khỏi Nearby Orders.

**Q: Reseller mode, tôi có cần ứng tiền cho shop không?**
A: KHÔNG. Vạn An ứng tiền cho shop. Bạn chỉ thu COD + nhận DeliveryFee.

**Q: Giao thất bại thì sao?**
A: Bấm "Giao thất bại" + nhập reason. DeliveryTask=Failed. COD không thu. Nếu đã ứng tiền (Marketplace) → liên hệ admin để Reversal.

**Q: Customer không cung cấp vị trí giao hàng?**
A: OrderDetail hiện "Khách chưa cung cấp vị trí giao hàng — gọi khách để xác nhận". Dùng chat (UC-07) hoặc SĐT để liên hệ.

**Q: Chat có AI chatbot không?**
A: KHÔNG. PoC chỉ human-to-human chat. AI chatbot là sprint riêng sau PoC.

**Q: Tôi có thể vừa là Shipper vừa là Salesman không?**
A: CÓ. Một user nhiều role. System Admin kích hoạt từng role.

**Q: Polling GPS 10s tốn battery/data không?**
A: Adaptive — OutForDelivery 10s, PickedUp 30s. 10 shippers PoC = 60 req/phút. Giữ tab active khi đang giao, tắt khi Delivered.

---

> **Xem thêm:** [README index](./README.md) | [Customer](./07-customer.md) | [Salesman](./03-salesman.md) | [System Admin](./01-systemadmin.md)
