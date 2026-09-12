# HƯỚNG DẪN SYSTEM ADMIN — COMMUNITY COMMERCE

> **Đối tượng:** Nhân viên kỹ thuật / vận hành Vạn An, có quyền quản trị toàn hệ thống (cross-tenant).
> **Đăng nhập:** Tài khoản SystemAdmin tại `https://app2.khachvip.online` (ShopERP Blazor Server, cookie auth, role = SystemAdmin).
> **Phạm vi:** Tất cả tenant + community entities (Gateway PG) + global settings.

> **Nguyên tắc:** Mọi tác vụ đều thực hiện qua UI (menu Hệ thống / Cộng tác viên / Thương mại / Infrastructure). KHÔNG cần gọi API trực tiếp (curl/Postman) trừ khi debug.

---

## MỤC LỤC

1. [Danh sách trang quản trị](#1-danh-sách-trang-quản-trị)
2. [Kích hoạt vai trò cộng tác viên (Shipper/Salesman)](#2-kích-hoạt-vai-trò-cộng-tác-viên-shippersalesman)
3. [Thiết lập điều kiện kích hoạt role (Salesman/Shipper)](#3-thiết-lập-điều-kiện-kích-hoạt-role-salesmanshipper)
4. [Thiết lập Product Referral Config (commission + app-install bonus)](#4-thiết-lập-product-referral-config-commission--app-install-bonus)
5. [Commerce Mode Toggle (Marketplace ↔ Reseller) — Sprint 7](#5-commerce-mode-toggle-marketplace--reseller--sprint-7)
6. [Quản lý Community Fund (quỹ phát triển cộng đồng)](#6-quản-lý-community-fund-quỹ-phát-triển-cộng-đồng)
7. [Quản lý Product Cost Price (Reseller mode)](#7-quản-lý-product-cost-price-reseller-mode)
8. [Fraud Review — Xem xét gian lận](#8-fraud-review--xem-xét-gian-lận)
9. [Collaborator SMS OTP Toggle](#9-collaborator-sms-otp-toggle)
10. [Quản lý Device Registration](#10-quản-lý-device-registration)
11. [Hàng đợi Tenant Registrations](#11-hàng-đợi-tenant-registrations)
12. [FAQ](#12-faq)

---

## 1. DANH SÁCH TRANG QUẢN TRỊ

SystemAdmin đăng nhập → sidebar bên trái hiển thị 5 nhóm menu:

### Nhóm "Hệ thống"
| Trang | URL | Chức năng |
|---|---|---|
| Tenants | `/admin/tenants` | CRUD tenant + Pending/Duplicates tabs (crawl-to-onboard) |
| ShopERP Instances | `/admin/shop-instances` | Quản lý ShopERP instances (multi-VPS routing) |
| KhachLink Instances | `/admin/khachlink-instances` | Quản lý KhachLink instances |
| Tenant Domains | `/admin/domains` | Domain registrar + DNS A-record |
| Người dùng | `/admin/users` | CRUD platform users |
| Nhóm quyền | `/admin/permission-groups` | CRUD permission groups |
| Audit Trail | `/admin/audit-trail` | Audit log |
| **Tenant Registrations** | `/admin/tenant-registrations` | Hàng đợi merchant đăng ký → Contact/Onboard/Reject |
| Background Services | `/admin/background-services` | Quản lý background jobs |

### Nhóm "Cộng tác viên"
| Trang | URL | Chức năng |
|---|---|---|
| Quản lý CTV | `/admin/community/admin-panel` | Kích hoạt role cộng tác viên + toggle "Hiển thị tất cả" + bypass eligibility |
| Fraud Review | `/admin/community/fraud-flags` | Review queue gian lận (Pending → Confirm/Dismiss/Review) |
| Fraud Stats | `/admin/community/fraud-stats` | Thống kê fraud (rate, flags, confirmed, banned) |
| **Device Registrations** | `/admin/device-registrations` | Xem/deactivate/verify device + update risk score + fingerprint lookup |
| **Cấu hình tính năng** | `/settings/shop-features` | Thiết lập điều kiện Salesman/Shipper + Reseller filter + loyalty/notification toggles |
| Xác minh SMS | `/admin/collaborator-verification` | Toggle SMS OTP + phí + min deposit |
| Quỹ Cộng Đồng | `/admin/community-fund` | Xem balance + spend + history quỹ cộng đồng |
| Lịch sử Settlement | `/admin/settlements` | Settlement history (Reseller mode) |

### Nhóm "Thương mại"
| Trang | URL | Chức năng |
|---|---|---|
| Commerce Mode | `/admin/commerce-mode` | Toggle global mode + tenant overrides + **Confirm External Payment** (Reseller non-COD) |
| Referral Configs | `/admin/product-referral-configs` | CRUD commission rate + app-install bonus per product |
| Giá Vốn SP | `/admin/product-cost-prices` | CRUD cost price per product (Reseller mode) |
| VALCN v2.0 Features | `/admin/valcn-features` | Feature flags toggle |
| Sản phẩm nổi bật | `/admin/featured-products` | Featured products management |
| Reseller Kế toán | `/admin/reseller-accounting-reconciliation` | Reseller accounting reconciliation |

### Nhóm "Infrastructure"
| Trang | URL | Chức năng |
|---|---|---|
| Network Dashboard | `/admin/network-dashboard` | Network monitoring |
| Lưu trữ ảnh R2 | `/admin/r2-storage` | R2 storage config |
| Cài đặt OCR | `/admin/ocr-settings` | OCR settings |
| Cấu hình KhachLink | `/admin/khachlink-home-settings` | KhachLink home page config |

> **Lưu ý:** Các trang CRM & Loyalty (customers, campaigns, missions, redemption, loyalty-config) đã có trong CRM-Loyalty Guide — không lặp lại ở đây.

---

## 2. KÍCH HOẠT VAI TRÒ CỘNG TÁC VIÊN (SHIPPER/SALESMAN)

### 2.1. Điều kiện đủ điều kiện (eligible)

Customer đủ điều kiện khi:
- **Toggle OFF (mặc định):** `IdentityLevel ≥ DeviceVerified` (4) **HOẶC** `IdentityLevel ≥ Verified` (2) + `LoyaltyPoints ≥ 1000`
- **Toggle ON:** `IdentityLevel ≥ Verified` (2) **BẮT BUỘC** + `LoyaltyPoints ≥ 1000` + `CommunityRole.IsPhoneVerified = true` + deposit wallet ≥ phí SMS OTP

> **Điều kiện per-tenant:** SystemAdmin cấu hình per-tenant tại `/settings/shop-features` (xem Section 3).

### 2.2. Quy trình kích hoạt qua UI

**Bước 1:** Đăng nhập SystemAdmin → menu **Cộng tác viên → Quản lý CTV** (`/admin/community/admin-panel`).

**Bước 2:** Mặc định chỉ hiện customer đủ điều kiện. Bật toggle **"Hiển thị tất cả khách hàng"** để xem cả customer chưa đủ điều kiện (đăng nhập Google mới).

**Bước 3:** Mỗi customer có 2 nút:
- **+ Shipper** — kích hoạt role Shipper
- **+ Salesman** — kích hoạt role Salesman

**Bước 4:** Nếu customer đủ điều kiện → role kích hoạt ngay. Nếu chưa đủ điều kiện (toggle "Hiển thị tất cả" đang ON) → hiện **confirm dialog** "Xác nhận nâng cấp vượt điều kiện" → bấm **Xác nhận nâng cấp** để activate với `bypassEligibility=true`.

**Bước 5:** Customer thấy role mới trong Profile page sau khi login (tab mới xuất hiện trong NavMenu: "Đơn hàng gần" cho Shipper, "Sản phẩm gần" cho Salesman).

### 2.3. Khi toggle ON — flow verification (UC-02b)

Nếu customer chưa verify SĐT (`IsPhoneVerified = false`):
1. Admin redirect customer sang verification flow.
2. Customer mở verification page → nhập/confirm SĐT.
3. `POST /api/collaborator-verification/init` → server gửi SMS OTP (trừ phí deposit wallet).
4. Customer nhập OTP → `POST /api/collaborator-verification/verify`.
5. Verify thành công → `IsPhoneVerified = true` + `PhoneVerifiedAt = now`.
6. Admin activate role (Bước 3 ở trên).

**Lưu ý:**
- Retry limit: max 3 OTP gửi / 24h (anti-spam).
- Deposit hết → không gửi OTP → thông báo "Nạp thêm deposit".
- Phí SMS OTP trừ deposit wallet mỗi lần (`SmsOtpFeePerVerification`).

### 2.4. Hủy role (deactivate)

Trên cùng trang `/admin/community/admin-panel`, nút **− Shipper** / **− Salesman** để hủy role. Customer mất tab tương ứng trong NavMenu.

### 2.5. Admin Panel + Owner Panel + Override (PR #172)

Có 2 path kích hoạt role + tính năng bypass eligibility:

| Path | URL | Role | Phạm vi |
|---|---|---|---|
| Admin Panel | `/admin/community/admin-panel` | SystemAdmin | Cross-tenant (tất cả tenant) |
| Owner Panel | `/community/owner-panel` | Owner | Tenant-scoped (chỉ tenant của mình) |

**Tính năng (cả 2 panel, PR #172):**
- Toggle **"Hiển thị tất cả khách hàng"** — xem cả customer chưa đủ điều kiện (IdentityLevel/LoyaltyPoints thấp).
- **Bypass eligibility** — kích hoạt bỏ qua check điều kiện (dùng cho customer đặc biệt, admin override).
- **Confirm dialog** trước khi activate (tránh click nhầm).

---

## 3. THIẾT LẬP ĐIỀU KIỆN KÍCH HOẠT ROLE (SALESMAN/SHIPPER)

> **Per-tenant configuration (R2.1).** Mỗi tenant có thể thiết lập điều kiện riêng. Default: 1000 points + IdentityLevel ≥ Verified.

### 3.1. Truy cập

Đăng nhập SystemAdmin → menu **Cộng tác viên → Cấu hình tính năng** (`/settings/shop-features`).

> **Lưu ý SystemAdmin:** SystemAdmin không thuộc tenant cụ thể. Khi vào trang, hiện **dropdown chọn tenant** + toggle **"Chỉ hiện tenant Reseller"** để lọc. Chọn tenant → bấm **Chọn & Cấu hình** → impersonate tenant đó → trang load settings per-tenant.

### 3.2. Card "Cộng tác viên — Điều kiện kích hoạt (R2.1)"

| Field | Mô tả | Default |
|---|---|---|
| Điểm tối thiểu — Salesman | `Community_SalesmanMinPoints` | 1000 |
| Điểm tối thiểu — Shipper | `Community_ShipperMinPoints` | 1000 (có thể khác Salesman) |
| Mức xác thực tối thiểu | `Community_RequiredIdentityLevel` | Verified (2) — áp dụng cho cả Salesman + Shipper |

**Mức xác thực:**
- Guest (0) — Không yêu cầu
- Social (1) — Đăng nhập MXH
- Verified (2) — Xác thực SMS OTP (mặc định)
- Full (3) — Xác thực đầy đủ

### 3.3. Lưu

Bấm **Lưu** → settings lưu per-tenant. Thay đổi có hiệu lực ngay (không cần restart).

---

## 4. THIẾT LẬP PRODUCT REFERRAL CONFIG (COMMISSION + APP-INSTALL BONUS)

> **Quan trọng:** Commission rate + app-install bonus do **SystemAdmin thiết lập per-product** (KHÔNG hardcode). Salesman chỉ thấy config đã set — không tự thay đổi.

### 4.1. Truy cập

Đăng nhập SystemAdmin → menu **Thương mại → Referral Configs** (`/admin/product-referral-configs`).

### 4.2. Tạo config cho product

**Bước 1:** Bấm "Tạo mới" → chọn product (từ FeaturedProducts PG hoặc nhập ProductId).

**Bước 2:** Nhập các field:
| Field | Mô tả | Giá trị |
|---|---|---|
| `CommissionRate` | % hoa hồng chốt đơn | 2-5% (Marketplace: % orderTotal; Reseller: % margin) |
| `AppInstallBonus` | Thưởng cố định khi customer cài app | VND (vd 50000) |
| `ProductShortCode` | Mã ngắn gọn cho QR | 6-8 chars (vd `TR-001`) |
| `CommissionBase` (Sprint 7) | Cơ sở tính commission | `OnOrderTotal` (Marketplace) / `OnMargin` (Reseller) |
| `IsActive` | Trạng thái | true/false |

**Bước 3:** Bấm **Lưu**.

### 4.3. Update / Deactivate

- **Update:** Sửa config → bấm Lưu.
- **Deactivate (soft delete):** Set `IsActive = false` — KHÔNG xóa data.

### 4.4. Salesman thấy gì?

Salesman mở "Sản phẩm gần" → mỗi product hiện: name, price, shop name, distance, **commissionRate**, **appInstallBonus** (từ config). Product chưa có config → hiển thị "Chưa thiết lập" (salesman vẫn chọn nhưng commission/bonus = 0).

---

## 5. COMMERCE MODE TOGGLE (MARKETPLACE ↔ RESELLER) — SPRINT 7

> **Nguyên tắc:** "Mua giúp — Bán dùm". Vạn An mua hàng từ tenant rồi bán lại cho customer. Toggle toàn cục + override cấp tenant. Additive — không phá Sprint 0-6.

### 5.1. Truy cập

Đăng nhập SystemAdmin → menu **Thương mại → Commerce Mode** (`/admin/commerce-mode`).

### 5.2. Global Settings Card

| Field | Mô tả | Default |
|---|---|---|
| Mode toggle | `Marketplace` (radio) / `Reseller` (radio) | Marketplace |
| Platform Fee Rate | % slider 10-50% | 30% |
| Community Fund Rate | % slider 1-10% | 5% |
| Default Delivery Fee | VND number input | 15000 |

**Bấm Lưu** → áp dụng cho đơn hàng mới.

> **CẢNH BÁO hiển thị trên UI:** "Thay đổi áp dụng cho đơn hàng mới. Đơn hàng cũ không bị ảnh hưởng."

### 5.3. Tenant Overrides Table

| Column | Mô tả |
|---|---|
| Tenant | Tên tenant |
| Current Mode (resolved) | Mode thực tế (sau khi resolve override + global) |
| Override | Dropdown: `Inherit` / `Marketplace` / `Reseller` |
| Actions | **Đặt Override** button per row |

**Quy tắc ưu tiên:**
1. Override ≠ `Inherit` → dùng override
2. Override == `Inherit` → dùng global setting
3. Mỗi Order snapshot mode tại creation — toggle affect future orders only

### 5.4. Xác nhận thanh toán ngoài hệ thống (Reseller non-COD)

> **Sprint 7 Q5.** Khi khách hàng thanh toán Reseller order bằng VietQR/thẻ (không phải COD), SystemAdmin xác nhận tại đây.

Trên cùng trang `/admin/commerce-mode`, cuộn xuống card **"Xác nhận thanh toán ngoài hệ thống (Reseller non-COD)"**:

| Field | Mô tả |
|---|---|
| Order ID | GUID của order cần xác nhận |
| Số tiền (VND) | Số tiền khách đã thanh toán |
| Payment Ref | VietQR txn ID / card ref |

**Bấm Xác nhận** → hệ thống tạo 5-split: ExternalPayment + Settlement + DeliveryFee + Commission + PlatformFee + CommunityFund.

> **Lưu ý:** Trước đây SystemAdmin phải gọi API `POST /api/admin/commerce-mode/confirm-external-payment` trực tiếp. Giờ có UI form trên trang Commerce Mode.

### 5.5. Khi nào bật Reseller?

| Giai đoạn | Mode | Lý do |
|---|---|---|
| PoC (50 users) | Marketplace (default) | Friction thấp, tenant tự định giá |
| Scale (500+ users) | Reseller (toggle ON) | Vạn An kiểm soát margin, thu phí nền tảng |
| Tenant lớn (F&B chain) | Override: Marketplace | Họ tự định giá |
| Tenant nhỏ (cửa hàng cá thể) | Override: Reseller (hoặc Inherit) | Vạn An lo toàn bộ |

### 5.6. Rollout strategy (an toàn)

| Phase | Action | Risk |
|---|---|---|
| 1. Deploy Sprint 7 code | Toggle default OFF (Marketplace) | Zero — existing behavior unchanged |
| 2. Test Reseller trên 1 tenant | Override 1 tenant → Reseller | Isolated — chỉ tenant đó's new orders |
| 3. Toggle global → Reseller | Tất cả tenant (trừ override) switch | Medium — monitor financial flows |
| 4. Full Reseller | Tất cả tenant Reseller | High — require full RV |

---

## 6. QUẢN LÝ COMMUNITY FUND (QUỸ PHÁT TRIỂN CỘNG ĐỒNG)

> **Chỉ có trong Reseller mode.** % margin (default 5%) vào quỹ cộng đồng mỗi đơn COD.

### 6.1. Truy cập

Đăng nhập SystemAdmin → menu **Cộng tác viên → Quỹ Cộng Đồng** (`/admin/community-fund`).

### 6.2. Xem balance + history

- **Balance card:** Số dư hiện tại quỹ cộng đồng (VND) + tổng đã thu + tổng đã chi.
- **+ Chi quỹ button:** Rút tiền tái đầu tư cộng đồng.
- **History table:** List `CommunityFundSpendRecord` — amount, reason, date, approved by.

### 6.3. Rút tiền (spend)

**Bước 1:** Bấm **+ Chi quỹ** → nhập amount + reason (vd "Tài trợ sự kiện cộng đồng Q7").

**Bước 2:** Confirm → hệ thống tạo `WalletTransaction` type=`CommunityFundSpend` (11) — trừ balance quỹ.

> **Lưu ý:** Quỹ cộng đồng là wallet đặc biệt (SystemWalletIds.CommunityFundWallet) — KHÔNG tạo Customer entity cho nó.

---

## 7. QUẢN LÝ PRODUCT COST PRICE (RESELLER MODE)

> **Chỉ có trong Reseller mode.** Cost price = giá Vạn An mua từ tenant. Vạn An định giá bán = CostPrice + margin.

### 7.1. Truy cập

Đăng nhập SystemAdmin → menu **Thương mại → Giá Vốn SP** (`/admin/product-cost-prices`).

### 7.2. CRUD

- **Tạo:** Chọn product + nhập cost price (VND) + effective date.
- **Update:** Sửa cost price (snapshot per-order tại creation, không affect đơn cũ).
- **List:** Bảng product + cost price hiện tại + lịch sử thay đổi.

### 7.3. Mối quan hệ với SellPrice

- `SellPrice` = giá Vạn An bán cho customer (do Vạn An định, dựa trên cost price + margin).
- `PlatformMargin` = `SellPrice - CostPrice` (computed, snapshot per-order).
- Commission Reseller = `% PlatformMargin` (không phải % orderTotal như Marketplace).

---

## 8. FRAUD REVIEW — XEM XÉT GIAN LẬN

> **5-layer anti-fraud tự động flag + admin manual review.** Target fraud rate <0.5%.

### 8.1. Truy cập

Đăng nhập SystemAdmin → menu **Cộng tác viên → Fraud Review** (`/admin/community/fraud-flags`).

### 8.2. Review queue

- List `FraudFlag(Status=Pending)` sort by `RiskScore` desc.
- Mỗi flag hiện: customer, entity type (Customer/Order/SalesReferral/AppInstallAttribution/DeviceRegistration), risk score, risk factors (JSON), description, created date.

### 8.3. Click flag → xem detail

- Customer info + order history.
- Related entities: DeviceRegistration (fingerprint, IP, UA, platform), SalesReferral, AppInstallAttribution.
- Risk factors breakdown (vd "sameFingerprint:+50,sameIP:+30").

### 8.4. Admin actions (3 lựa chọn)

| Action | Hệ quả |
|---|---|
| **Confirm** | Penalty: `SalesReferral.CommissionStatus=Rejected` / `AppInstallAttribution.AttributionStatus=Rejected`. Customer banned nếu 3 strikes (3 confirmed flags → permanent ban). |
| **Dismiss** | False positive → whitelist entity (`IsVerified=true`, `RiskScore` giảm). |
| **MarkReviewed** | Neutral, info only — không penalty không whitelist. |

### 8.5. Fraud Stats

Menu **Cộng tác viên → Fraud Stats** (`/admin/community/fraud-stats`) — thống kê:
- Total flags / Pending / Confirmed / Dismissed
- Fraud rate (confirmed / total transactions)
- Banned accounts count
- Top risk factors

### 8.6. Auto behavior (không cần admin)

| RiskScore | Hệ thống tự động |
|---|---|
| <60 | Auto-approve sau 24h cooling period |
| 60-79 | Hold 48h + tạo FraudFlag(Pending) cho admin review |
| ≥80 | Auto-reject + FraudFlag(Pending) |

**Background services:** `CoolingPeriodJob` (hourly, auto-approve RiskScore<60 sau 24h) + `HeldTimeoutJob` (hourly, auto-reject Held sau 48h).

---

## 9. COLLABORATOR SMS OTP TOGGLE

> **Toggle ON/OFF** quyết định Salesman/Shipper/Owner có bắt buộc SMS OTP hay không. Customer KHÔNG bị ảnh hưởng.

### 9.1. Truy cập

Đăng nhập SystemAdmin → menu **Cộng tác viên → Xác minh SMS** (`/admin/collaborator-verification`).

### 9.2. Cài đặt

| Field | Mô tả | Default |
|---|---|---|
| Trạng thái | Bật/Tắt SMS OTP verification | Tắt (OFF) |
| Phí SMS mỗi lần xác minh (VND) | Trừ từ deposit wallet mỗi OTP | 200 |
| Số dư tối thiểu (VND) | Cộng tác viên phải nạp tối thiểu số này | 10000 |

Bấm **Lưu cài đặt** → thay đổi có hiệu lực ngay (SystemSetting, không cần restart).

### 9.3. Khi nào bật?

| Giai đoạn | Toggle | Lý do |
|---|---|---|
| Early stage (PoC, <500 users) | **OFF** (default) | Friction thấp, tối đa user + giao dịch |
| Scale (500+ users) | **ON** | Salesman/Shipper/Owner bắt buộc SMS OTP, phí trừ deposit wallet |

### 9.4. Domain changes khi toggle ON

- `WalletTransactionType.Deposit = 12` + `SmsOtpFee = 13` (nạp deposit + trừ phí OTP)
- `CommunityRole.IsPhoneVerified` + `PhoneVerifiedAt`
- `SystemSetting.CollaboratorSmsVerificationEnabled` (toggle)

> **Cập nhật (2026-09-09):** Renumbering dự đoán trong phiên bản trước KHÔNG xảy ra. Enum thực tế (Domain.cs): `PlatformFee=7, CommunityFund=8, DeliveryFee=9, ExternalPayment=10, CommunityFundSpend=11, Deposit=12, SmsOtpFee=13`. Xem README Section 6.1 cho bảng đầy đủ.

---

## 10. QUẢN LÝ DEVICE REGISTRATION

### 10.1. Truy cập

Đăng nhập SystemAdmin → menu **Cộng tác viên → Device Registrations** (`/admin/device-registrations`).

### 10.2. Max 3 devices per Customer

- Device 4+ → tạo với `IsActive=false` + `FraudFlag(Status=Pending)`.
- Admin review → approve (set `IsActive=true`) hoặc reject.

### 10.3. Trang quản lý

Trang `/admin/device-registrations` hiển thị bảng device registrations cross-tenant với:

**Bộ lọc:**
- Lọc theo CustomerId
- Lọc theo Fingerprint Hash
- Lọc theo trạng thái (Active/Inactive)

**Mỗi device hiện:**
- Khách hàng (tên + ID)
- Platform (iOS/Android/Web)
- IP Address
- Fingerprint Hash (rút gọn)
- Risk Score (badge màu: low/medium/high)
- Trạng thái Active/Inactive
- Trạng thái Verified/Unverified
- Last Seen timestamp

**Hành động (per device):**
| Nút | Hành động | Hệ quả |
|---|---|---|
| **Chi tiết** | Mở modal xem đầy đủ thông tin device | Device token, fingerprint, UA, IP, first/last seen, risk score |
| **Deactivate** | Tắt device | `IsActive=false` — customer logout device đó |
| **Verify** | Xác minh device | `IsVerified=true` — whitelist, RiskScore giảm |
| **Risk** | Mở modal cập nhật risk score | Nhập 0-100 → lưu |
| **Fingerprint** | Tra cứu fingerprint | Mở modal list tất cả device dùng cùng fingerprint (self-deal detection) |

### 10.4. Query anti-fraud

- **Fingerprint lookup** — bấm nút **Fingerprint** trên 1 device → modal hiện tất cả device khác dùng cùng fingerprint (self-deal detection).
- **Filter by CustomerId** — xem tất cả device của 1 customer.
- **Filter by Active status** — xem chỉ device active hoặc inactive.

---

## 11. HÀNG ĐỢI TENANT REGISTRATIONS

> **GTM Drill Machine W2.** Merchant submit registration qua Directory/demo form → SystemAdmin review → Contact/Onboard/Reject.

### 11.1. Truy cập

Đăng nhập SystemAdmin → menu **Hệ thống → Tenant Registrations** (`/admin/tenant-registrations`).

### 11.2. Hàng đợi

Trang hiển thị bảng registrations với:
- Shop Name + Industry
- Contact Name + Phone + Email
- Source (demo/audit/direct)
- Turnstile Verified badge
- Status (Submitted/Contacted/Onboarded/Rejected)
- Submitted date
- Rejection reason (nếu bị reject)
- Onboarded Tenant ID (nếu đã onboard)

### 11.3. Hành động

| Nút | Hành động | Khi nào |
|---|---|---|
| **Contact** | Đánh dấu đã liên hệ follow-up | Status = Submitted |
| **Onboard** | Mở modal nhập Onboarded Tenant ID → link registration với tenant đã tạo | Status = Submitted hoặc Contacted |
| **Reject** | Mở modal nhập lý do từ chối | Status = Submitted hoặc Contacted |

### 11.4. Quy trình onboard

1. Merchant submit registration (qua Directory form `/claim` hoặc demo form).
2. SystemAdmin vào `/admin/tenant-registrations` → bấm **Contact** để đánh dấu đã liên hệ.
3. SystemAdmin tạo tenant mới tại `/admin/tenants` (tab Pending → Verify).
4. Copy Tenant ID mới → quay lại `/admin/tenant-registrations` → bấm **Onboard** → paste Tenant ID → xác nhận.
5. Registration status → Onboarded + ghi `OnboardedTenantId`.

---

## 12. FAQ

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

**Q: Tôi cần gọi API trực tiếp (curl/Postman) không?**
A: KHÔNG. Tất cả tác vụ SystemAdmin đều có UI page. Chỉ gọi API khi debug hoặc tự động hóa.

**Q: Làm sao biết tenant nào đang ở chế độ Reseller?**
A: Vào `/settings/shop-features` → bật toggle "Chỉ hiện tenant Reseller" → dropdown chỉ hiện tenant Reseller. Hoặc xem cột "Resolved Mode" trên trang `/admin/commerce-mode`.

---

> **Xem thêm:** [README index](./README.md) | [Shop Owner](./02-owner.md) | [Salesman](./03-salesman.md) | [Shipper](./04-shipper.md)
