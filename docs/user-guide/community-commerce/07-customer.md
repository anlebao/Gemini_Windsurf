# HƯỚNG DẪN CUSTOMER — KHÁCH HÀNG MUA HÀNG

> **Đối tượng:** Khách hàng cuối — mua hàng F&B qua KhachLink PWA, theo dõi giao hàng, tích điểm, đổi thưởng.
> **Đăng nhập:** KhachLink PWA (`diemthuong.khachvip.online`) — Google login + device fingerprint. KHÔNG bắt buộc SMS OTP.
> **Nền tảng:** Blazor WebAssembly PWA — cài đặt trên mobile home screen, hoạt động offline-capable.

---

## MỤC LỤC

1. [Tổng quan vai trò](#1-tổng-quan-vai-trò)
2. [Đăng nhập (UC-01) — Social + Device Fingerprint](#2-đăng-nhập-uc-01--social--device-fingerprint)
3. [Duyệt sản phẩm + đặt hàng](#3-duyệt-sản-phẩm--đặt-hàng)
4. [Checkout + referral code (Salesman)](#4-checkout--referral-code-salesman)
5. [Theo dõi giao hàng real-time (UC-06)](#5-theo-dõi-giao-hàng-real-time-uc-06)
6. [Chat với Shipper (UC-07)](#6-chat-với-shipper-uc-07)
7. [Ví (Wallet) + lịch sử giao dịch](#7-ví-wallet--lịch-sử-giao-dịch)
8. [Tích điểm + đổi thưởng (Loyalty)](#8-tích-điểm--đổi-thưởng-loyalty)
9. [Trở thành Salesman/Shipper](#9-trở-thành-salesmanshipper)
10. [Marketplace vs Reseller — khác biệt cho Customer](#10-marketplace-vs-reseller--khác-biệt-cho-customer)
11. [Privacy — Device Fingerprint Consent](#11-privacy--device-fingerprint-consent)
12. [FAQ](#12-faq)

---

## 1. TỔNG QUAN VAI TRÒ

Customer là khách hàng cuối — mua hàng F&B qua KhachLink PWA. Một customer có thể đồng thời là **Buyer + Salesman + Shipper** (nếu được System Admin kích hoạt).

**KhachLink PWA:**
- Blazor WebAssembly — chạy trên mobile browser.
- PWA installable — cài trên home screen như app native.
- Offline-capable (cache assets + queue writes).
- GPS chỉ hoạt động khi tab active.

**Auth:** `X-Customer-Token` (custom token, KHÔNG cookie) — lưu localStorage, tự gửi trong header mọi API call.

---

## 2. ĐĂNG NHẬP (UC-01) — SOCIAL + DEVICE FINGERPRINT

### 2.1. Flow đăng nhập

1. Mở KhachLink → bấm **Đăng nhập**.
2. Chọn phương thức (PoC scope):
   - **(A) Google login** → social auth redirect → tiếp bước 3
   - **(B) Facebook login** → social auth redirect → tiếp bước 3 (UI button Sprint 1)
   - **(C) Tiếp tục as Guest** → nhập tên + SĐT (không token) → checkout as guest (không tích điểm)
3. First login / new device: browser generate DeviceToken + compute Fingerprint → **Device Fingerprint Consent Dialog** → bấm **Đồng ý** → POST `/api/community/device/register`.
4. IdentityLevel = `Social` (1) nếu social, `Guest` (0) nếu guest.
5. (OPTIONAL) Verify SĐT qua SMS OTP → upgrade `Verified` (2) — **KHÔNG bắt buộc** cho customer.
6. Token lưu localStorage, tự gửi trong header mọi API call.

### 2.2. Device Fingerprint Consent Dialog (GDPR/PDPA)

- Hiển thị TRƯỚC khi collect fingerprint.
- Giải thích: "Chúng tôi thu thập device fingerprint để bảo vệ tài khoản của bạn khỏi gian lận."
- User có thể **decline** — lúc đó RiskScore sẽ cao hơn (không fingerprint), nhưng vẫn dùng được.
- Fingerprint signals: canvas, WebGL, audio, fonts, timezone, screen, plugins, v.v. (15+ signals) → SHA256 hash.

### 2.3. Max 3 active devices

- Max 3 active `DeviceRegistration` per Customer.
- Device thứ 4 → yêu cầu **admin approval** (tạo với IsActive=false + FraudFlag).
- Customer có thể logout device (deactivate) trong Profile page.

### 2.4. Guest mode

- Nhập tên + SĐT (không token).
- Checkout as guest — **KHÔNG tích điểm** (no LoyaltyPoints).
- Verify SĐT chỉ khi collaborator activation (UC-02 v1.5).

### 2.5. IdentityLevel

| Level | Ý nghĩa |
|---|---|
| Guest (0) | Khách vãng lai (không token) |
| Social (1) | Đăng nhập Google/Facebook |
| Verified (2) | SMS OTP verified (optional) |
| Full (3) | Full KYC |
| DeviceVerified (4) | Device fingerprint + behavioral pass (KHÔNG cần SMS) |

---

## 3. DUYỆT SẢN PHẨM + ĐẶT HÀNG

### 3.1. Duyệt sản phẩm

1. Mở KhachLink → Home page hiển thị Featured Products (GET `/api/catalog/recommended` — pure PG query, no ShopERP forward).
2. Hoặc duyệt product catalog (forwards via YARP to ShopERP for ShopConfig derivation).
3. Mỗi product hiện: name, price, shop name, image, description.

### 3.2. Reseller mode — price display

```
@if (commerceMode == "Reseller")
{
    // Show SellPrice (Vạn An price) + "Giá đã bao gồm phí nền tảng"
}
else
{
    // Show tenant price (Marketplace — existing)
}
```

### 3.3. Thêm vào giỏ + đặt hàng

1. Chọn product → thêm vào cart.
2. Chọn quantity, modifiers (vd size, topping), notes.
3. Chọn OrderType: **DELIVERY** (giao tận nơi) hoặc **TAKEAWAY** (đến lấy).
4. Nhập delivery address + GPS location (cho DELIVERY).
5. Chọn PaymentMethod: **COD** (trả tiền khi nhận) hoặc **Online** (VietQR/card — Reseller mode).
6. Review order → **Đặt hàng**.

### 3.4. Order creation

- Order created trên **Gateway PG** (Vạn An là order creator, Option C).
- Order snapshot `CommerceMode` tại creation time.
- Order async-delivered qua NATS (routed by ShopInstanceId) → ShopERP SQLite (replica cho kitchen/POS).
- Staff/Kitchen nhận order (KitchenHub push).

---

## 4. CHECKOUT + REFERRAL CODE (SALESMAN)

### 4.1. Referral code trong localStorage

Nếu bạn đã **scan QR của salesman** (UC-09):
- Composite referral code `{salesmanCode}|{productShortCode}` lưu trong localStorage.
- Khi đặt hàng → referral code tự gửi trong order creation (`referralCode` field).
- Server resolve → set `Order.SalesmanId` + `Order.ReferralProductId`.
- Salesman nhận commission khi Order.Completed.

### 4.2. Scan QR salesman

1. Salesman chia sẻ QR (Zalo, Facebook, in ấn).
2. Bạn quét QR → redirect đến KhachLink với composite referral code.
3. Composite code lưu localStorage (cả salesmanCode + productShortCode).
4. Lần đặt hàng tiếp theo → referral code tự áp dụng.

### 4.3. App-install bonus (UC-12)

Nếu bạn có referralCode trong localStorage + chưa cài PWA:
1. Mở KhachLink lần đầu (có referralCode).
2. Browser hiện "Cài app" (beforeinstallprompt).
3. Bấm **Cài app** → PWA install success (appinstalled event).
4. Hệ thống tự `POST /api/community/app-install/attributed` với referralCode.
5. Salesman nhận app-install bonus (từ ProductReferralConfig.AppInstallBonus).

> **Lưu ý:** 1 customer chỉ attribute 1 lần (unique constraint). Cài lại app không tính bonus lần 2.

---

## 5. THEO DÕI GIAO HÀNG REAL-TIME (UC-06)

### 5.1. Khi nào tracking?

Sau khi shipper accept đơn (Order status=`delivering`) → customer có thể tracking.

### 5.2. Tracking UI

1. Mở Order Detail → tab **Tracking**.
2. **Leaflet map** hiển thị:
   - Shop marker (red) — vị trí shop
   - Customer marker (green) — vị trí giao hàng
   - Shipper marker (blue) — di chuyển real-time
   - Route line (straight — PoC)
3. Shipper cập nhật vị trí mỗi **10s** (khi OutForDelivery) → SignalR LocationHub push → marker di chuyển.

### 5.3. Status notifications

Customer nhận **SignalR notification** mỗi transition:
- Shipper accept → "Shipper [tên] đã nhận đơn"
- Shipper pickup → "Shipper đã lấy hàng, đang đến"
- Shipper delivering → "Shipper đang giao hàng"
- Shipper delivered → "Đã giao thành công"
- Shipper failed → "Giao thất bại: [reason]"

### 5.4. PWA limitation

- Shipper GPS chỉ hoạt động khi **tab active** (shipper giữ app mở).
- Nếu shipper tắt app → marker không update → customer thấy "Shipper đang di chuyển" tĩnh.
- **Thông báo hiển thị:** "Vị trí cập nhật khi shipper mở app."

---

## 6. CHAT VỚI SHIPPER (UC-07)

### 6.1. Mở chat

1. Mở Order Detail → tab **Chat** (hoặc chat panel).
2. Chat chỉ mở khi `DeliveryTask` tồn tại (shipper đã accept đơn).

### 6.2. Flow

1. Nhập message → `POST /api/community/chat/messages`.
2. **SignalR ChatHub** push đến shipper real-time.
3. Message lưu DB (Conversation + Message) với timestamp, senderId, receiverId.
4. Chat history load khi mở panel.

### 6.3. Lưu ý

- **Human-to-human chat** — KHÔNG có AI chatbot trong PoC.
- Chat biến mất/không push khi DeliveryTask = Delivered/Failed.
- Auth ChatHub qua `X-Customer-Token` query string.

---

## 7. VÍ (WALLET) + LỊCH SỬ GIAO DỊCH

### 7.1. Xem ví

Login KhachLink → tab **"Ví"** (nếu có role Salesman/Shipper) hoặc Profile → Wallet.

- Balance hiện tại (VND)
- Mode badge: "Mô hình Reseller — Vạn An mua bán" hoặc "Mô hình Marketplace — Tenant bán trực tiếp"
- List `WalletTransaction` (immutable ledger — append-only)

### 7.2. Customer transactions

Customer thường KHÔNG có wallet balance (chỉ Salesman/Shipper có). Customer thấy:
- **Order history:** list đơn đã đặt + status
- **Payment history:** COD/Online payments

### 7.3. Reseller mode — ExternalPayment

Nếu Reseller mode + PaymentMethod = Online (VietQR/card):
- Customer trả Vạn An qua VietQR/card.
- `WalletTransaction(ExternalPayment)` — Vạn An nhận tiền trực tiếp.
- Sau đó Vạn An phân phối (6 transactions COD flow).

---

## 8. TÍCH ĐIỂM + ĐỔI THƯỞNG (LOYALTY)

> **Chi tiết đầy đủ:** xem `docs/user-guide/CRM_Loyalty_Guide.md` (Section 4 — Customer).

### 8.1. Tích điểm

- Mỗi đơn completed → LoyaltyPoints (formula-based, configurable).
- Guest checkout KHÔNG tích điểm (no token, no account).
- Điểm hiển thị trong Profile page.

### 8.2. Đổi thưởng

- Mở Catalog Đổi thưởng → chọn quà → bấm **Đổi**.
- Điểm trừ → redemption record (Pending) → admin fulfill voucher.
- Xem lịch sử đổi thưởng trong Profile.

### 8.3. Nhiệm vụ (Missions)

- PWAInstall, OtpVerify, BirthdayEntry, FacebookShare, TikTokShare, Custom missions.
- Hoàn nhiệm vụ → nhận điểm bonus.

### 8.4. SMS OTP — KHÔNG bắt buộc cho customer

- Customer KHÔNG cần SMS OTP để tích điểm / đổi điểm (luôn, bất kể toggle).
- SMS OTP chỉ cho collaborator (Salesman/Shipper/Owner) khi toggle ON.

---

## 9. TRỞ THÀNH SALESMAN/SHIPPER

### 9.1. Điều kiện

- `IdentityLevel ≥ DeviceVerified` (device fingerprint pass) **HOẶC** `IdentityLevel ≥ Verified` (SMS OTP) + `LoyaltyPoints ≥ 1000`
- **Kích hoạt bởi System Admin** (customer không tự đăng ký).

### 9.2. Quy trình

1. Tích đủ 1000 điểm + device fingerprint pass.
2. System Admin liên hệ / tự thấy bạn trong eligible list.
3. Admin kích hoạt → bạn nhận push notification.
4. Login lại → NavMenu hiện thêm tab Salesman/Shipper.

### 9.3. SMS OTP (chỉ khi toggle ON)

Nếu toggle ON + bạn chưa verify SĐT:
1. Nhận notification "Admin mời kích hoạt vai trò".
2. Verify SĐT qua SMS OTP (phí trừ deposit wallet).
3. Admin activate role.

> **Chi tiết Salesman:** xem [03-salesman.md](./03-salesman.md).
> **Chi tiết Shipper:** xem [04-shipper.md](./04-shipper.md).

---

## 10. MARKETPLACE VS RESELLER — KHÁC BIỆT CHO CUSTOMER

| Khía cạnh | Marketplace | Reseller |
|---|---|---|
| **Giá hiển thị** | Tenant price | SellPrice (Vạn An price) + "Giá đã bao gồm phí nền tảng" |
| **Ai bán** | Tenant bán trực tiếp | Vạn An mua từ tenant → bán lại cho bạn |
| **COD** | Trả shipper khi nhận | Trả shipper khi nhận (Vạn An thu hộ) |
| **Online payment** | (tùy tenant) | VietQR/card → Vạn An nhận trực tiếp |
| **Wallet badge** | "Marketplace — Tenant bán trực tiếp" | "Reseller — Vạn An mua bán" |
| **Trải nghiệm đặt hàng** | Giống nhau | Giống nhau (chỉ price display khác) |

> **Lưu ý:** Customer KHÔNG cần quan tâm mode — trải nghiệm đặt hàng giống nhau. Mode chỉ affect financial flow backend.

---

## 11. PRIVACY — DEVICE FINGERPRINT CONSENT

### 11.1. Device Fingerprint Consent Dialog

- Hiển thị TRƯỚC khi collect fingerprint (GDPR/PDPA compliance).
- Giải thích mục đích: bảo vệ tài khoản khỏi gian lận.
- User có thể **decline** — vẫn dùng được nhưng RiskScore cao hơn.

### 11.2. Dữ liệu thu thập

| Signal | Mục đích |
|---|---|
| Canvas hash | Device fingerprint unique |
| WebGL | GPU info |
| Audio | Audio context fingerprint |
| Fonts | Installed fonts |
| Timezone | Timezone |
| Screen | Resolution |
| UserAgent | Browser/OS |
| IP address | Network location |
| Platform | navigator.platform |

- Tất cả self-hosted (KHÔNG gửi cho bên thứ 3).
- FingerprintJS MIT, vendored trong KhachLink wwwroot/lib/.

### 11.3. Max 3 devices

- Max 3 active devices per account.
- Device 4+ → admin approval.
- Quản lý devices trong Profile page (deactivate device cũ).

### 11.4. Anti-fraud — RiskScore

Hệ thống tự compute RiskScore (0-100) cho mỗi transaction. Customer KHÔNG thấy score, nhưng:
- Score cao → commission/bonus của salesman bị hold/reject (nếu bạn được salesman giới thiệu).
- KHÔNG affect customer experience — customer vẫn mua hàng bình thường.

---

## 12. FAQ

**Q: Tôi có cần verify SĐT (SMS OTP) không?**
A: KHÔNG bắt buộc. Device fingerprint là primary. SMS OTP chỉ nếu muốn upgrade IdentityLevel hoặc trở thành Salesman/Shipper (khi toggle ON).

**Q: Tôi có thể mua hàng không cần đăng nhập không?**
A: CÓ — Guest mode (nhập tên + SĐT, không token). Nhưng KHÔNG tích điểm + KHÔNG tracking + KHÔNG chat.

**Q: GPS của tôi có bị track không?**
A: CHỈ khi bạn đặt hàng DELIVERY (cần delivery location) + khi tracking shipper. KHÔNG background track. PWA chỉ GPS khi tab active.

**Q: Scan QR salesman có bắt buộc không?**
A: KHÔNG. Tùy chọn. Scan → salesman nhận commission. Không scan → không có salesman.

**Q: Cài app (PWA) có được thưởng không?**
A: CÓ nếu bạn scan QR salesman trước (referralCode trong localStorage). App-install bonus cho salesman — bạn không nhận trực tiếp, nhưng ủng hộ salesman giới thiệu cho bạn.

**Q: Reseller mode, tôi trả tiền cho ai?**
A: COD → trả shipper khi nhận (Vạn An thu hộ). Online → trả Vạn An qua VietQR/card. KHÔNG khác biệt trải nghiệm so Marketplace.

**Q: Đổi mode có ảnh hưởng đơn cũ của tôi không?**
A: KHÔNG. Mỗi order snapshot mode tại creation. Đơn cũ giữ mode cũ.

**Q: Tôi có thể vừa mua hàng vừa là Salesman/Shipper không?**
A: CÓ. Một user nhiều role. System Admin kích hoạt từng role.

**Q: Device fingerprint có vi phạm privacy không?**
A: Device fingerprint là hash (KHÔNG phải dữ liệu cá nhân rõ ràng). Self-hosted (KHÔNG gửi bên thứ 3). Consent dialog hiển thị trước khi collect (GDPR/PDPA). Bạn có thể decline.

**Q: Chat với shipper có AI chatbot không?**
A: KHÔNG. PoC chỉ human-to-human chat. AI chatbot là sprint riêng sau PoC.

---

> **Xem thêm:** [README index](./README.md) | [Salesman](./03-salesman.md) | [Shipper](./04-shipper.md) | [CRM-Loyalty Guide](../CRM_Loyalty_Guide.md)
