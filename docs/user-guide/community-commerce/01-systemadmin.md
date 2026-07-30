# HƯỚNG DẪN SYSTEM ADMIN — COMMUNITY COMMERCE

> **Đối tượng:** Nhân viên kỹ thuật / vận hành Vạn An, có quyền quản trị toàn hệ thống (cross-tenant).
> **Đăng nhập:** Tài khoản SystemAdmin tại `https://khachvip.online` (ShopERP Blazor Server, cookie auth, role = SystemAdmin).
> **Phạm vi:** Tất cả tenant + community entities (Gateway PG) + global settings.

---

## MỤC LỤC

1. [Danh sách trang quản trị](#1-danh-sách-trang-quản-trị)
2. [Kích hoạt vai trò cộng tác viên (Shipper/Salesman)](#2-kích-hoạt-vai-trò-cộng-tác-viên-shippersalesman)
3. [Thiết lập Product Referral Config (commission + app-install bonus)](#3-thiết-lập-product-referral-config-commission--app-install-bonus)
4. [Commerce Mode Toggle (Marketplace ↔ Reseller) — Sprint 7](#4-commerce-mode-toggle-marketplace--reseller--sprint-7)
5. [Quản lý Community Fund (quỹ phát triển cộng đồng)](#5-quản-lý-community-fund-quỹ-phát-triển-cộng-đồng)
6. [Quản lý Product Cost Price (Reseller mode)](#6-quản-lý-product-cost-price-reseller-mode)
7. [Fraud Review — Xem xét gian lận](#7-fraud-review--xem-xét-gian-lận)
8. [Collaborator SMS OTP Toggle](#8-collaborator-sms-otp-toggle)
9. [Quản lý Device Registration](#9-quản-lý-device-registration)
10. [FAQ](#10-faq)

---

## 1. DANH SÁCH TRANG QUẢN TRỊ

| Trang | URL | Chức năng |
|---|---|---|
| Commerce Mode Settings | `/admin/commerce-mode` | Toggle global mode + tenant overrides + platform fee/fund/delivery rates |
| Community Fund | `/admin/community-fund` | Xem balance + spend + history quỹ cộng đồng |
| Product Cost Prices | `/admin/product-cost-prices` | CRUD cost price per product (Reseller mode) |
| Product Referral Configs | `/admin/product-referral-configs` | CRUD commission rate + app-install bonus per product |
| Fraud Flags | `/admin/fraud-flags` | Review queue gian lận (Pending → Confirm/Dismiss/Review) |
| Fraud Stats | `/admin/fraud-stats` | Thống kê fraud (rate, flags, confirmed, banned) |
| Community Eligible | Gateway API `GET /api/admin/community/eligible` | List customer đủ điều kiện kích hoạt role |
| Activate/Deactivate Role | Gateway API `POST /api/admin/community/{id}/activate-role` | Kích hoạt/hủy role Shipper/Salesman |
| Device Registrations | Gateway Admin API | Xem/deactivate/verify device fingerprint |

> **Lưu ý:** Các trang `/admin/*` khác (customers-global, tenants, missions, redemption, campaigns, users, audit-trail) đã có trong CRM-Loyalty Guide — không lặp lại ở đây.

---

## 2. KÍCH HOẠT VAI TRÒ CỘNG TÁC VIÊN (SHIPPER/SALESMAN)

### 2.1. Điều kiện đủ điều kiện (eligible)

Customer đủ điều kiện khi:
- **Toggle OFF (mặc định):** `IdentityLevel ≥ DeviceVerified` (4) **HOẶC** `IdentityLevel ≥ Verified` (2) + `LoyaltyPoints ≥ 1000`
- **Toggle ON:** `IdentityLevel ≥ Verified` (2) **BẮT BUỘC** + `LoyaltyPoints ≥ 1000` + `CommunityRole.IsPhoneVerified = true` + deposit wallet ≥ phí SMS OTP

### 2.2. Quy trình kích hoạt

**Bước 1:** Đăng nhập SystemAdmin → gọi Gateway Admin API `GET /api/admin/community/eligible`.
- API trả list customer đủ điều kiện (Verified + ≥1000 points, hoặc DeviceVerified + ≥1000 points nếu toggle OFF).

**Bước 2:** Chọn customer → `POST /api/admin/community/{customerId}/activate-role` với body:
```json
{ "role": "Shipper" }
```
hoặc
```json
{ "role": "Salesman" }
```

**Bước 3:** Hệ thống tạo bản ghi `CommunityRole` gắn với Customer + gửi push notification cho customer.

**Bước 4:** Customer thấy role mới trong Profile page sau khi login (tab mới xuất hiện trong NavMenu: "Đơn hàng gần" cho Shipper, "Sản phẩm gần" cho Salesman).

### 2.3. Khi toggle ON — flow verification (UC-02b)

Nếu customer chưa verify SĐT (`IsPhoneVerified = false`):
1. Admin redirect customer sang verification flow.
2. Customer mở verification page → nhập/confirm SĐT.
3. `POST /api/collaborator-verification/init` → server gửi SMS OTP (trừ phí deposit wallet).
4. Customer nhập OTP → `POST /api/collaborator-verification/verify`.
5. Verify thành công → `IsPhoneVerified = true` + `PhoneVerifiedAt = now`.
6. Admin activate role (Bước 2 ở trên).

**Lưu ý:**
- Retry limit: max 3 OTP gửi / 24h (anti-spam).
- Deposit hết → không gửi OTP → thông báo "Nạp thêm deposit".
- Phí SMS OTP trừ deposit wallet mỗi lần (`SmsOtpFeePerVerification`).

### 2.4. Hủy role (deactivate)

`POST /api/admin/community/{customerId}/deactivate-role` — hủy role cộng tác viên. Customer mất tab tương ứng trong NavMenu.

---

## 3. THIẾT LẬP PRODUCT REFERRAL CONFIG (COMMISSION + APP-INSTALL BONUS)

> **Quan trọng:** Commission rate + app-install bonus do **SystemAdmin thiết lập per-product** (KHÔNG hardcode). Salesman chỉ thấy config đã set — không tự thay đổi.

### 3.1. Truy cập

Đăng nhập SystemAdmin → `/admin/product-referral-configs`.

### 3.2. Tạo config cho product

**Bước 1:** Bấm "Tạo mới" → chọn product (từ FeaturedProducts PG hoặc nhập ProductId).

**Bước 2:** Nhập các field:
| Field | Mô tả | Giá trị |
|---|---|---|
| `CommissionRate` | % hoa hồng chốt đơn | 2-5% (Marketplace: % orderTotal; Reseller: % margin) |
| `AppInstallBonus` | Thưởng cố định khi customer cài app | VND (vd 50000) |
| `ProductShortCode` | Mã ngắn gọn cho QR | 6-8 chars (vd `TR-001`) |
| `CommissionBase` (Sprint 7) | Cơ sở tính commission | `OnOrderTotal` (Marketplace) / `OnMargin` (Reseller) |
| `IsActive` | Trạng thái | true/false |

**Bước 3:** Bấm **Lưu** → `POST /api/admin/products/{productId}/referral-config`.

### 3.3. Update / Deactivate

- **Update:** `PUT /api/admin/products/{productId}/referral-config`.
- **Deactivate (soft delete):** `DELETE /api/admin/products/{productId}/referral-config` — set `IsActive = false`, KHÔNG xóa data.

### 3.4. Salesman thấy gì?

Salesman mở "Sản phẩm gần" → mỗi product hiện: name, price, shop name, distance, **commissionRate**, **appInstallBonus** (từ config). Product chưa có config → hiển thị "Chưa thiết lập" (salesman vẫn chọn nhưng commission/bonus = 0).

---

## 4. COMMERCE MODE TOGGLE (MARKETPLACE ↔ RESELLER) — SPRINT 7

> **Nguyên tắc:** "Mua giúp — Bán dùm". Vạn An mua hàng từ tenant rồi bán lại cho customer. Toggle toàn cục + override cấp tenant. Additive — không phá Sprint 0-6.

### 4.1. Truy cập

Đăng nhập SystemAdmin → `/admin/commerce-mode` (ShopERP Admin, `@attribute [Authorize(Roles="SystemAdmin")]`).

### 4.2. Global Settings Card

| Field | Mô tả | Default |
|---|---|---|
| Mode toggle | `Marketplace` (radio) / `Reseller` (radio) | Marketplace |
| Platform Fee Rate | % slider 10-50% | 30% |
| Community Fund Rate | % slider 1-10% | 5% |
| Default Delivery Fee | VND number input | 15000 |

**Bấm Lưu** → `POST /api/admin/commerce-mode/global`.

> **CẢNH BÁO hiển thị trên UI:** "Thay đổi áp dụng cho đơn hàng mới. Đơn hàng cũ không bị ảnh hưởng."

### 4.3. Tenant Overrides Table

| Column | Mô tả |
|---|---|
| Tenant | Tên tenant |
| Current Mode (resolved) | Mode thực tế (sau khi resolve override + global) |
| Override | Dropdown: `Inherit` / `Marketplace` / `Reseller` |
| Actions | **Lưu** button per row → `POST /api/admin/commerce-mode/tenant/{tenantId}` |

**Quy tắc ưu tiên:**
1. Override ≠ `Inherit` → dùng override
2. Override == `Inherit` → dùng global setting
3. Mỗi Order snapshot mode tại creation — toggle affect future orders only

### 4.4. Khi nào bật Reseller?

| Giai đoạn | Mode | Lý do |
|---|---|---|
| PoC (50 users) | Marketplace (default) | Friction thấp, tenant tự định giá |
| Scale (500+ users) | Reseller (toggle ON) | Vạn An kiểm soát margin, thu phí nền tảng |
| Tenant lớn (F&B chain) | Override: Marketplace | Họ tự định giá |
| Tenant nhỏ (cửa hàng cá thể) | Override: Reseller (hoặc Inherit) | Vạn An lo toàn bộ |

### 4.5. Rollout strategy (an toàn)

| Phase | Action | Risk |
|---|---|---|
| 1. Deploy Sprint 7 code | Toggle default OFF (Marketplace) | Zero — existing behavior unchanged |
| 2. Test Reseller trên 1 tenant | Override 1 tenant → Reseller | Isolated — chỉ tenant đó's new orders |
| 3. Toggle global → Reseller | Tất cả tenant (trừ override) switch | Medium — monitor financial flows |
| 4. Full Reseller | Tất cả tenant Reseller | High — require full RV |

### 4.6. API endpoints

| Method | Endpoint | Mô tả |
|---|---|---|
| GET | `/api/admin/commerce-mode` | Lấy global mode + tenant overrides |
| POST | `/api/admin/commerce-mode/global` | Set global mode + rates |
| POST | `/api/admin/commerce-mode/tenant/{tenantId}` | Set tenant override |
| GET | `/api/community/commerce-mode` | Customer-facing: lấy mode resolved cho UI |

---

## 5. QUẢN LÝ COMMUNITY FUND (QUỸ PHÁT TRIỂN CỘNG ĐỒNG)

> **Chỉ có trong Reseller mode.** % margin (default 5%) vào quỹ cộng đồng mỗi đơn COD.

### 5.1. Truy cập

Đăng nhập SystemAdmin → `/admin/community-fund`.

### 5.2. Xem balance + history

- **Balance card:** Số dư hiện tại quỹ cộng đồng (VND).
- **Spend button:** Rút tiền tái đầu tư cộng đồng.
- **History table:** List `CommunityFundSpendRecord` — amount, reason, date, approved by.

### 5.3. Rút tiền (spend)

**Bước 1:** Bấm **Rút tiền** → nhập amount + reason (vd "Tài trợ sự kiện cộng đồng Q7").

**Bước 2:** Confirm → `POST /api/community/community-fund/spend` (controller `CommunityFundController`).

**Bước 3:** Hệ thống tạo `WalletTransaction` type=`CommunityFundSpend` (11) — trừ balance quỹ.

> **Lưu ý:** Quỹ cộng đồng là wallet đặc biệt (SystemWalletIds.CommunityFundWallet) — KHÔNG tạo Customer entity cho nó.

---

## 6. QUẢN LÝ PRODUCT COST PRICE (RESELLER MODE)

> **Chỉ có trong Reseller mode.** Cost price = giá Vạn An mua từ tenant. Vạn An định giá bán = CostPrice + margin.

### 6.1. Truy cập

Đăng nhập SystemAdmin → `/admin/product-cost-prices`.

### 6.2. CRUD

- **Tạo:** Chọn product + nhập cost price (VND) + effective date.
- **Update:** Sửa cost price (snapshot per-order tại creation, không affect đơn cũ).
- **List:** Bảng product + cost price hiện tại + lịch sử thay đổi.

**API:** `ProductCostPriceController` — CRUD qua Gateway Admin API (SystemAdmin JWT).

### 6.3. Mối quan hệ với SellPrice

- `SellPrice` = giá Vạn An bán cho customer (do Vạn An định, dựa trên cost price + margin).
- `PlatformMargin` = `SellPrice - CostPrice` (computed, snapshot per-order).
- Commission Reseller = `% PlatformMargin` (không phải % orderTotal như Marketplace).

---

## 7. FRAUD REVIEW — XEM XÉT GIAN LẬN

> **5-layer anti-fraud tự động flag + admin manual review.** Target fraud rate <0.5%.

### 7.1. Truy cập

Đăng nhập SystemAdmin → `/admin/fraud-flags`.

### 7.2. Review queue

- List `FraudFlag(Status=Pending)` sort by `RiskScore` desc.
- Mỗi flag hiện: customer, entity type (Customer/Order/SalesReferral/AppInstallAttribution/DeviceRegistration), risk score, risk factors (JSON), description, created date.

### 7.3. Click flag → xem detail

- Customer info + order history.
- Related entities: DeviceRegistration (fingerprint, IP, UA, platform), SalesReferral, AppInstallAttribution.
- Risk factors breakdown (vd "sameFingerprint:+50,sameIP:+30").

### 7.4. Admin actions (3 lựa chọn)

| Action | Hệ quả |
|---|---|
| **Confirm** | Penalty: `SalesReferral.CommissionStatus=Rejected` / `AppInstallAttribution.AttributionStatus=Rejected`. Customer banned nếu 3 strikes (3 confirmed flags → permanent ban). |
| **Dismiss** | False positive → whitelist entity (`IsVerified=true`, `RiskScore` giảm). |
| **MarkReviewed** | Neutral, info only — không penalty không whitelist. |

### 7.5. Fraud Stats

`/admin/fraud-stats` — thống kê:
- Total flags / Pending / Confirmed / Dismissed
- Fraud rate (confirmed / total transactions)
- Banned accounts count
- Top risk factors

### 7.6. Auto behavior (không cần admin)

| RiskScore | Hệ thống tự động |
|---|---|
| <60 | Auto-approve sau 24h cooling period |
| 60-79 | Hold 48h + tạo FraudFlag(Pending) cho admin review |
| ≥80 | Auto-reject + FraudFlag(Pending) |

**Background services:** `CoolingPeriodJob` (hourly, auto-approve RiskScore<60 sau 24h) + `HeldTimeoutJob` (hourly, auto-reject Held sau 48h).

---

## 8. COLLABORATOR SMS OTP TOGGLE

> **Toggle ON/OFF** quyết định Salesman/Shipper/Owner có bắt buộc SMS OTP hay không. Customer KHÔNG bị ảnh hưởng.

### 8.1. Truy cập

SystemSetting `CollaboratorSmsVerificationEnabled` — set qua Gateway Admin API hoặc SystemSetting config.

### 8.2. Khi nào bật?

| Giai đoạn | Toggle | Lý do |
|---|---|---|
| Early stage (PoC, <500 users) | **OFF** (default) | Friction thấp, tối đa user + giao dịch |
| Scale (500+ users) | **ON** | Salesman/Shipper/Owner bắt buộc SMS OTP, phí trừ deposit wallet |

### 8.3. Domain changes khi toggle ON

- `WalletTransactionType.Deposit=7` + `SmsOtpFee=8` (nạp deposit + trừ phí OTP)
- `CommunityRole.IsPhoneVerified` + `PhoneVerifiedAt`
- `SystemSetting.CollaboratorSmsVerificationEnabled` (toggle)

> **Coordination note:** Nếu CC-S6-T5 (SMS OTP toggle) deploy trước Sprint 7, enum values 7-8 = Deposit/SmsOtpFee, Sprint 7 renumber PlatformFee/CommunityFund sang 9-13.

---

## 9. QUẢN LÝ DEVICE REGISTRATION

### 9.1. Max 3 devices per Customer

- Device 4+ → tạo với `IsActive=false` + `FraudFlag(Status=Pending)`.
- Admin review → approve (set `IsActive=true`) hoặc reject.

### 9.2. Admin actions

| Action | API | Hệ quả |
|---|---|---|
| Deactivate device | `DeviceRegistration.Deactivate()` | `IsActive=false` — customer logout device đó |
| Verify device | `DeviceRegistration.Verify()` | `IsVerified=true` — whitelist, RiskScore giảm |
| Update risk score | `DeviceRegistration.UpdateRiskScore(score)` | Tăng/giảm device-level risk |

### 9.3. Query anti-fraud

- Query `FingerprintHash` → ai khác dùng fingerprint này? (self-deal detection)
- Query `(CustomerId, IsActive)` → active devices per customer
- Query `DeviceToken` → 1 token = 1 device

---

## 10. FAQ

**Q: Tôi có thể kích hoạt cả 2 role Shipper + Salesman cho 1 customer không?**
A: CÓ. Một user có thể đồng thời là Buyer + Salesman + Shipper. Mỗi role tạo 1 bản ghi `CommunityRole` riêng.

**Q: Đổi commission rate có ảnh hưởng đơn cũ không?**
A: KHÔNG. `SalesReferral` snapshot `CommissionRate` tại thời điểm chốt đơn (audit). Đổi config chỉ affect đơn mới.

**Q: Reseller mode có bắt buộc tất cả tenant không?**
A: KHÔNG. Toggle global + override per-tenant. Tenant lớn có thể override giữ Marketplace.

**Q: Community fund rút được bao nhiêu?**
A: Tùy balance hiện tại. Mỗi spend tạo `CommunityFundSpendRecord` + `WalletTransaction(CommunityFundSpend)` — audit trail đầy đủ.

**Q: Fraud flag confirmed 3 lần thì sao?**
A: Customer bị permanent ban. Tất cả commission/bonus pending → Rejected.

**Q: Tôi có thể revert một WalletTransaction không?**
A: KHÔNG update/delete. Dùng Reversal pattern — tạo `WalletTransaction(Type=Reversal, Amount=-original, RelatedTransactionId=original.Id)`. Giống AccountingEntry immutable pattern.

---

> **Xem thêm:** [README index](./README.md) | [Shop Owner](./02-owner.md) | [Salesman](./03-salesman.md) | [Shipper](./04-shipper.md)
