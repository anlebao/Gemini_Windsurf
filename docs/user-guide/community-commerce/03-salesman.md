# HƯỚNG DẪN SALESMAN — CỘNG TÁC VIÊN BÁN HÀNG

> **Đối tượng:** Customer được System Admin kích hoạt role Salesman — kiếm tiền từ commission chốt đơn + app-install bonus.
> **Đăng nhập:** KhachLink PWA (`diemthuong.khachvip.online`) — Google login + device fingerprint. Token lưu localStorage.
> **Nền tảng:** Blazor WebAssembly PWA — chạy trên mobile browser. GPS chỉ hoạt động khi tab active.

---

## MỤC LỤC

1. [Tổng quan vai trò + điều kiện](#1-tổng-quan-vai-trò--điều-kiện)
2. [Đăng nhập + kích hoạt role](#2-đăng-nhập--kích-hoạt-role)
3. [Sản phẩm gần (Nearby Products) — UC-08](#3-sản-phẩm-gần-nearby-products--uc-08)
4. [Tạo mã QR giới thiệu (Composite Referral) — UC-09](#4-tạo-mã-qr-giới-thiệu-composite-referral--uc-09)
5. [Doanh số + hoa hồng (Sales Dashboard) — UC-10](#5-doanh-số--hoa-hồng-sales-dashboard--uc-10)
6. [App-install bonus (UC-12) — thưởng khi customer cài app](#6-app-install-bonus-uc-12--thưởng-khi-customer-cài-app)
7. [Ví (Wallet) + rút tiền](#7-ví-wallet--rút-tiền)
8. [Anti-fraud — RiskScore + hold](#8-anti-fraud--riskscore--hold)
9. [Marketplace vs Reseller — khác biệt cho Salesman](#9-marketplace-vs-reseller--khác-biệt-cho-salesman)
10. [FAQ](#10-faq)

---

## 1. TỔNG QUAN VAI TRÒ + ĐIỀU KIỆN

Salesman là cộng tác viên bán hàng — giới thiệu product của tenant cho customer, chốt đơn qua referral code, kiếm commission.

### 1.1. 2 nguồn thu (do SystemAdmin set per-product, KHÔNG hardcode)

| Nguồn | Khi nào | Tính theo |
|---|---|---|
| **Commission chốt đơn** | Customer đặt hàng qua referral code của salesman | Marketplace: % orderTotal (2-5%); Reseller: % margin |
| **App-install bonus** | Customer cài KhachLink PWA (có referralCode trong localStorage) | Fixed amount per product (vd 50.000đ) |

### 1.2. Điều kiện trở thành Salesman

- `IdentityLevel ≥ DeviceVerified` (device fingerprint pass) **HOẶC** `IdentityLevel ≥ Verified` (SMS OTP) + `LoyaltyPoints ≥ 1000`
- **Kích hoạt bởi System Admin** (Owner không tự kích hoạt)
- Khi toggle `CollaboratorSmsVerificationEnabled = ON`: bắt buộc SMS OTP + `IsPhoneVerified = true` + deposit wallet ≥ phí OTP

### 1.3. Một user nhiều role

Bạn có thể đồng thời là **Customer + Salesman + Shipper**. Mỗi role tạo 1 bản ghi `CommunityRole` riêng. NavMenu KhachLink hiện tab theo role bạn có.

---

## 2. ĐĂNG NHẬP + KÍCH HOẠT ROLE

### 2.1. Đăng nhập (UC-01)

1. Mở KhachLink → bấm **Đăng nhập**.
2. Chọn **Google** (PoC scope) → social auth redirect.
3. First login / new device: browser generate DeviceToken + compute Fingerprint → **Device Fingerprint Consent Dialog** → bấm **Đồng ý** → POST `/api/community/device/register`.
4. IdentityLevel = `Social` (1).
5. (OPTIONAL) Verify SĐT qua SMS OTP → upgrade lên `Verified` (2) — KHÔNG bắt buộc cho customer, chỉ bắt buộc khi toggle ON cho collaborator.
6. Token lưu localStorage, tự gửi trong header mọi API call.

> **Max 3 active devices per Customer.** Device thứ 4 → yêu cầu admin approval.

### 2.2. Kích hoạt role Salesman

1. Đạt điều kiện (IdentityLevel + LoyaltyPoints ≥ 1000).
2. System Admin kích hoạt → bạn nhận push notification.
3. Login lại → NavMenu KhachLink hiện thêm tab **"Sản phẩm gần"** + **"Mã QR"** + **"Doanh số"**.

### 2.3. SMS OTP verification (chỉ khi toggle ON)

Nếu toggle ON và bạn chưa verify SĐT:
1. Nhận notification "Admin mời kích hoạt vai trò Salesman".
2. Mở verification page → nhập/confirm SĐT.
3. Nhận SMS OTP (phí trừ deposit wallet).
4. Nhập OTP → `IsPhoneVerified = true`.
5. Admin activate role.

**Retry limit:** Max 3 OTP / 24h. Deposit hết → nạp thêm.

---

## 3. SẢN PHẨM GẦN (NEARBY PRODUCTS) — UC-08

### 3.1. Truy cập

Login KhachLink → tab **"Sản phẩm gần"** (chỉ hiện khi có role Salesman).

### 3.2. Flow

1. Mở page → browser lấy GPS vị trí hiện tại (1 lần, consent prompt).
2. Gọi API: `GET /api/community/nearby-products?lat={lat}&lng={lng}&radiusKm=10`.
3. Hiển thị list products từ các tenant trong bán kính 10km, kèm khoảng cách.
4. Mỗi product hiện:
   - Name, price, shop name, distance
   - **Commission rate** (từ `ProductReferralConfig`) — vd "Hoa hồng 3%"
   - **App-install bonus** (từ `ProductReferralConfig`) — vd "Thưởng cài app 50.000đ"
   - Product chưa có config → "Chưa thiết lập" (commission/bonus = 0)
5. Sort theo khoảng cách tăng dần.

### 3.3. Chọn product để giới thiệu

1. Salesman chọn 1 product → bấm **"Tạo mã QR giới thiệu"**.
2. Generate composite referral code `{salesmanCode}|{productShortCode}` (vd `ABC123|TR-001`).
3. Chuyển sang "Mã QR của tôi" page.

> **Lưu ý PoC:** API query **FeaturedProducts trên Gateway PG** (chỉ product được Owner set làm Featured). KHÔNG query tất cả products per-tenant SQLite. Product không Featured → salesman không thấy.

---

## 4. TẠO MÃ QR GIỚI THIỆU (COMPOSITE REFERRAL) — UC-09

### 4.1. Composite referral code

Format: `{salesmanCode}|{productShortCode}`
- `salesmanCode`: 6-8 chars, human-readable, unique per salesman (vd `ABC123`).
- `productShortCode`: từ `ProductReferralConfig.ProductShortCode` (vd `TR-001`).

**Ví dụ:** `ABC123|TR-001` → salesman ABC123 giới thiệu product TR-001.

### 4.2. Tạo QR

1. Sau khi chọn product (UC-08) → mở **"Mã QR của tôi"** page.
2. Hiển thị QR code chứa URL: `https://khachlink.app/r/{salesmanCode}|{productShortCode}` (vd `/r/ABC123|TR-001`).
3. QR generate client-side (qrcode.js library, vendored MIT — không CDN).
4. Salesman chia sẻ QR cho customer (Zalo, Facebook, in ấn, v.v.).

### 4.3. Customer scan QR

1. Customer quét QR → redirect đến KhachLink với composite referral code.
2. Composite code lưu trong **localStorage** (cả salesmanCode + productShortCode).
3. Khi customer đặt hàng → composite referral code gửi trong order creation (`referralCode` field).
4. Server resolve → set `Order.SalesmanId` + `Order.ReferralProductId`.

### 4.4. Khi Order.Completed → SalesReferral tạo

1. Order completed → server tạo `SalesReferral` với `RiskScore` computed (deterministic 0-100).
2. `CommissionStatus` phụ thuộc RiskScore:
   - RiskScore < 60 → `Pending` → auto-approve sau 24h (cooling period)
   - RiskScore 60-79 → `Pending` hold 48h + `FraudFlag(Pending)` cho admin review
   - RiskScore ≥ 80 → `Rejected` (auto-reject) + `FraudFlag(Pending)`

---

## 5. DOANH SỐ + HOA HỒNG (SALES DASHBOARD) — UC-10

### 5.1. Truy cập

Login KhachLink → tab **"Doanh số"** (chỉ hiện khi có role Salesman).

### 5.2. Hiển thị

`GET /api/community/salesman/{salesmanId}/commissions` trả:

- **List đơn đã chốt:** Order có SalesmanId = salesman's CustomerId — mỗi order hiện product, total, commission, status.
- **Tổng doanh số:** Tổng giá trị đơn đã chốt.
- **Commission chốt đơn (per-order):** Rate từ `ProductReferralConfig` của product trên order (2-5%). Status: Pending → Paid.
- **App-install bonus (per attributed install):** List `AppInstallAttribution` có SalesmanId — mỗi attribution hiện bonus amount + status.
- **Tổng commission + tổng app-install bonus** hiển thị tách biệt.

### 5.3. Commission status

| Status | Ý nghĩa |
|---|---|
| Pending | Chờ approve (cooling 24h nếu RiskScore<60, hold 48h nếu 60-79) |
| Paid | Đã thanh toán vào wallet |
| Rejected | Bị reject (RiskScore≥80 auto, hoặc admin confirm fraud) |
| Held | Hold 48h chờ admin review |

---

## 6. APP-INSTALL BONUS (UC-12) — THƯỞNG KHI CUSTOMER CÀI APP

### 6.1. Điều kiện

- Customer có composite referralCode trong localStorage (đã scan QR của salesman ở UC-09).
- Customer chưa cài PWA trước đó (1 customer chỉ attribute 1 lần).

### 6.2. Flow

1. Customer mở KhachLink lần đầu (có referralCode trong localStorage).
2. Customer trigger PWA install (browser `beforeinstallprompt` event → bấm "Cài app").
3. PWA install success (`appinstalled` event) → `POST /api/community/app-install/attributed` với body `{ referralCode: "ABC123|TR-001" }`.
4. Server resolve referralCode → salesmanId + productId.
5. Server check customer chưa có `AppInstallAttribution` trước đó (unique constraint trên CustomerId).
6. Tạo `AppInstallAttribution`: customerId, salesmanId, productId, bonusAmount (từ `ProductReferralConfig.AppInstallBonus`).
7. Tạo `WalletTransaction(Commission)` amount=bonusAmount cho salesman.
8. Salesman thấy bonus trong SalesDashboard (UC-10).

### 6.3. Risk scoring (v1.2)

- `AppInstallAttribution.RiskScore` computed tại thời điểm attribution.
- RiskScore ≥ 60 → `AttributionStatus=Held` hold 48h + FraudFlag.
- RiskScore ≥ 80 → `AttributionStatus=Rejected` auto + FraudFlag.
- RiskScore < 60 → `Pending` auto-approve sau 24h.

**Risk factors:** appInstallTime<30s (+40 — cài quá nhanh = bot), salesmanFingerprint==customerFingerprint (+50 — self-deal), same IP 24h (+30), customerAgeDays<7 (+30).

---

## 7. VÍ (WALLET) + RÚT TIỀN

### 7.1. Xem ví

Login KhachLink → tab **"Ví"** — hiển thị:
- Balance hiện tại (VND)
- Mode badge: "Mô hình Reseller — Vạn An mua bán" hoặc "Mô hình Marketplace — Tenant bán trực tiếp"
- List `WalletTransaction` (immutable ledger — append-only, không update/delete)

### 7.2. Rút tiền (Withdrawal)

**Điều kiện:**
- KYC bank account verified (admin-verified bank account required cho payout).
- Min payout 500.000đ.
- Balance ≥ amount rút.

**Flow:**
1. Bấm **Rút tiền** → nhập amount + bank account (đã KYC).
2. `POST /api/community/wallet/withdraw` → tạo `WalletTransaction(Withdrawal)` -amount.
3. Admin process payout (bank transfer).
4. Balance cập nhật.

### 7.3. Reversal pattern

Nếu transaction sai (vd confirm COD nhầm) → KHÔNG update/delete. Tạo `WalletTransaction(Reversal)` amount=-original, `RelatedTransactionId=original.Id`. Giống AccountingEntry immutable pattern.

---

## 8. ANTI-FRAUD — RISKSCORE + HOLD

### 8.1. RiskScore (0-100) computed tự động

| Score | Hệ quả |
|---|---|
| <60 | Auto-approve sau 24h cooling period |
| 60-79 | Hold 48h + FraudFlag(Pending) cho admin review |
| ≥80 | Auto-reject + FraudFlag(Pending) |

### 8.2. Risk factors (tránh để score cao)

| Factor | +Score | Cách tránh |
|---|---|---|
| salesmanFingerprint == customerFingerprint | +50 | KHÔNG tự scan QR của mình / tự đặt hàng qua referral của mình |
| same IP 24h (salesman + customer) | +30 | KHÔNG giới thiệu cho người dùng cùng wifi/IP trong 24h |
| customerAgeDays < 7 | +30 | Customer mới đăng ký < 7 ngày → score cao |
| deviceFirstSeen < 24h | +25 | Device mới → score cao |
| ordersFromDeviceToday > 3 | +20 | 1 device đặt >3 đơn/ngày → score cao |
| referralBonusAmount > 50K | +15 | Bonus cao → scrutiny cao |
| appInstallTime < 30s | +40 | Cài app <30s sau khi scan = bot behavior |
| blacklistedFingerprint | +60 | Fingerprint đã bị flag fraud trước đó |

### 8.3. 3-strike ban

3 FraudFlag confirmed → **permanent ban**. Tất cả commission/bonus pending → Rejected.

### 8.4. Payout policy

- Hold 48h if RiskScore ≥ 60.
- Auto-reject if ≥ 80.
- KYC bank account required.
- Min payout 500.000đ.

---

## 9. MARKETPLACE VS RESELLER — KHÁC BIỆT CHO SALESMAN

| Khía cạnh | Marketplace | Reseller |
|---|---|---|
| **Commission base** | % orderTotal (2-5%) | % margin (SellPrice - CostPrice) |
| **Commission amount** | Thường cao hơn (base lớn hơn) | Thường thấp hơn (base = margin, nhỏ hơn orderTotal) |
| **Ai trả commission** | Vạn An (từ commission fund) | Vạn An (từ margin) |
| **Nearby Products price** | Tenant price (existing) | SellPrice (Vạn An price) + "Giá đã bao gồm phí nền tảng" |
| **Wallet badge** | "Marketplace — Tenant bán trực tiếp" | "Reseller — Vạn An mua bán" |

> **Lưu ý:** Commission rate vẫn do SystemAdmin set per-product qua `ProductReferralConfig.CommissionBase` (OnOrderTotal vs OnMargin). Salesman thấy rate trong Nearby Products — không cần lo về base calculation.

---

## 10. FAQ

**Q: Tôi có thể tự tạo ProductReferralConfig (set commission rate) không?**
A: KHÔNG. Chỉ SystemAdmin set per-product. Bạn chỉ thấy config đã set. Product chưa có config → "Chưa thiết lập" (commission/bonus = 0).

**Q: Customer scan QR nhưng không đặt hàng, tôi có được commission không?**
A: KHÔNG. Commission chốt đơn chỉ khi Order.Completed. App-install bonus riêng — customer cài app là có bonus (không cần đặt hàng).

**Q: Tôi giới thiệu cho người nhà (cùng IP, cùng device) được không?**
A: KHÔNG NÊN. sameFingerprint (+50) + sameIP (+30) → RiskScore ≥ 80 → auto-reject + FraudFlag. Đây là self-deal detection.

**Q: Commission bị hold 48h thì sao?**
A: RiskScore 60-79 → hold 48h chờ admin review. Admin Confirm → reject. Admin Dismiss → approve. Nếu không review → auto-reject sau 48h (HeldTimeoutJob).

**Q: Tôi có thể vừa là Salesman vừa là Shipper không?**
A: CÓ. Một user có thể đồng thời là Buyer + Salesman + Shipper. System Admin kích hoạt từng role riêng.

**Q: Rút tiền cần gì?**
A: KYC bank account verified + min 500.000đ + balance đủ. Liên hệ admin để KYC bank account.

**Q: App-install bonus bị reject vì "appInstallTime<30s" — sao?**
A: Customer cài app <30s sau khi scan QR = bot behavior (+40). Khuyến khách xem app trước khi cài, không cài vội.

---

> **Xem thêm:** [README index](./README.md) | [Customer](./07-customer.md) | [Shipper](./04-shipper.md) | [System Admin](./01-systemadmin.md)
