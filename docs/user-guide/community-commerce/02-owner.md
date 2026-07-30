# HƯỚNG DẪN SHOP OWNER — COMMUNITY COMMERCE

> **Đối tượng:** Chủ cửa hàng / HKD (Hộ kinh doanh cá thể) — tenant trên hệ thống Vạn An.
> **Đăng nhập:** Tài khoản Owner tại `https://khachvip.online` (ShopERP Blazor Server, cookie auth, role = Owner).
> **Phạm vi:** Tenant của mình (per-tenant) — KHÔNG thấy data tenant khác.

---

## MỤC LỤC

1. [Tổng quan vai trò](#1-tổng-quan-vai-trò)
2. [Commerce Mode Override (tenant của mình)](#2-commerce-mode-override-tenant-của-mình)
3. [Quản lý Products + Featured Products](#3-quản-lý-products--featured-products)
4. [Quản lý Orders (Marketplace vs Reseller)](#4-quản-lý-orders-marketplace-vs-reseller)
5. [Settlement (thanh toán giữa các bên)](#5-settlement-thanh-toán-giữa-các-bên)
6. [Kitchen Display + POS coordination](#6-kitchen-display--pos-coordination)
7. [Tenant Settings (vị trí, theme, delivery)](#7-tenant-settings-vị-trí-theme-delivery)
8. [Onboarding SMS OTP (khi toggle ON)](#8-onboarding-sms-otp-khi-toggle-on)
9. [FAQ](#9-faq)

---

## 1. TỔNG QUAN VAI TRÒ

Owner là chủ cửa hàng F&B trên nền tảng Vạn An. Owner quản lý:
- **Products** (menu, giá, tồn kho) — per-tenant SQLite
- **Orders** (nhận, confirm, ready, completed) — Gateway PG (source of truth)
- **Kitchen** (giao việc cho bếp qua Kitchen Display)
- **Settlement** (thanh toán với shipper/Vạn An)
- **Tenant Settings** (vị trí shop, theme, delivery config)

**Kiến trúc data flow (Option C):**
- Orders + Accounting + Tenants trên **Gateway PG** (source of truth)
- Products trên **per-tenant SQLite** (ShopERP)
- Orders async-delivered qua NATS (routed by ShopInstanceId) → ShopERP SQLite (replica cho kitchen/POS display)

**Hai mode thương mại:**
| Mode | Vai trò Owner |
|---|---|
| **Marketplace** (default) | Owner tự định giá, bán trực tiếp, shipper thu hộ COD → shop nhận trực tiếp |
| **Reseller** | Vạn An mua hàng từ Owner (cost price) → bán lại cho customer. Owner chỉ cần giao hàng. Settlement qua Vạn An wallet. |

---

## 2. COMMERCE MODE OVERRIDE (TENANT CỦA MÌNH)

> **Sprint 7:** Owner KHÔNG tự toggle mode — chỉ SystemAdmin set override per-tenant. Owner thấy mode resolved của tenant mình.

### 2.1. Xem mode hiện tại

Owner không có trang `/admin/commerce-mode` (chỉ SystemAdmin). Owner thấy mode qua:
- **KhachLink UI badge** (nếu Owner cũng là customer): "Mô hình Reseller — Vạn An mua bán" hoặc "Mô hình Marketplace — Tenant bán trực tiếp".
- **Order detail**: mỗi order có `CommerceMode` snapshot.

### 2.2. Yêu cầu SystemAdmin override

Nếu Owner muốn giữ Marketplace trong khi global = Reseller (vd F&B chain tự định giá):
- Liên hệ SystemAdmin → SystemAdmin set `TenantSettings.CommerceModeOverride = Marketplace` cho tenant.
- API: `POST /api/admin/commerce-mode/tenant/{tenantId}` (SystemAdmin only).

### 2.3. Tác động của mode lên Owner

| Khía cạnh | Marketplace | Reseller |
|---|---|---|
| **Định giá** | Owner tự set price | Owner set cost price, Vạn An set sell price |
| **COD flow** | Shipper thu hộ → shop nhận trực tiếp | Customer trả Vạn An → Vạn An phân phối |
| **Advance payment** | Shipper ứng tiền cho shop | Vạn An ứng tiền cho shop (mua trước) |
| **Settlement** | Shipper ↔ Shop trực tiếp | Tất cả qua Vạn An wallet |
| **Commission salesman** | % orderTotal | % margin (SellPrice - CostPrice) |

---

## 3. QUẢN LÝ PRODUCTS + FEATURED PRODUCTS

### 3.1. Products (per-tenant SQLite)

Owner quản lý products tại ShopERP Admin → `/admin/products` (hoặc trang quản lý menu tương ứng).

**Fields:**
- Name, description, price (VND), category, image
- VatRate (snapshot cho e-invoice)
- IsActive (active/inactive)
- Stock (tồn kho — optional cho F&B)

### 3.2. Featured Products (Gateway PG — cho Salesman Nearby Products)

> **Quan trọng:** Salesman thấy products qua `GET /api/community/nearby-products` — query **FeaturedProducts trên Gateway PG** (PoC approach), KHÔNG query per-tenant SQLite.

**Owner cần set Featured Products** để salesman thấy product của mình:
- Truy cập `/admin/featured-products` (Owner thấy tenant của mình, SystemAdmin thấy tất cả).
- Chọn product → set display name, display price, sort order.
- Featured Product có tenant coordinates (từ TenantSettings) → salesman filter theo bán kính.

**Lưu ý:** Product chưa có `ProductReferralConfig` (do SystemAdmin set) → salesman thấy "Chưa thiết lập" (commission/bonus = 0). Owner nên đề nghị SystemAdmin set config cho product hot.

---

## 4. QUẢN LÝ ORDERS (MARKETPLACE VS RESELLER)

### 4.1. Order lifecycle (Owner perspective)

```
Customer đặt hàng (KhachLink)
   → Order created (Gateway PG, status=confirmed)
   → NATS routed → ShopERP SQLite (replica cho kitchen/POS)
   → Owner/Staff thấy order trong Order Management
   → Owner confirm (nếu cần) → status=confirmed
   → Kitchen nhận (KitchenHub push) → status=preparing
   → Kitchen xong → status=ready
   → Shipper accept (UC-04) → status=delivering
   → Shipper delivered → status=completed
```

### 4.2. Marketplace mode — Owner flow

1. Customer đặt → Owner thấy order mới.
2. Owner/Staff confirm → kitchen nhận.
3. Kitchen ready → shipper nhận đơn (Nearby Orders).
4. Shipper giao + thu COD → Owner nhận tiền trực tiếp từ shipper.
5. Settlement: Shipper ↔ Shop trực tiếp (Owner xác nhận nhận tiền).

### 4.3. Reseller mode — Owner flow

1. Customer đặt → Vạn An nhận order (Vạn An là buyer từ Owner).
2. Order có `CostPrice` (giá Vạn An mua) + `SellPrice` (giá Vạn An bán).
3. Owner/Staff confirm → kitchen nhận.
4. Kitchen ready → shipper nhận đơn.
5. Shipper giao + thu COD → Vạn An nhận tiền.
6. Settlement: Vạn An wallet → Owner wallet (qua `WalletTransaction.Settlement`).
7. Vạn An ứng tiền trước (AdvancePayment) nếu cần — Owner không cần shipper ứng.

### 4.4. Order fields Owner thấy

| Field | Marketplace | Reseller |
|---|---|---|
| TotalAmount | Giá customer trả | = SellPrice (giá Vạn An bán) |
| CostPrice | null | Giá Vạn An mua từ Owner |
| SellPrice | null | Giá Vạn An bán cho customer |
| PlatformMargin | null | SellPrice - CostPrice |
| DeliveryFee | null | Phí giao hàng Vạn An trả shipper |
| ShippingFee | Phí ship tenant set | (vẫn có — tenant-set delivery cost) |
| SalesmanId | (nếu có referral) | (nếu có referral) |

---

## 5. SETTLEMENT (THANH TOÁN GIỮA CÁC BÊN)

### 5.1. Marketplace — Shipper ↔ Shop trực tiếp

1. Shipper thu COD từ customer.
2. Shipper confirm COD → `WalletTransaction(CODCollection)` +amount cho shipper.
3. Settlement record tạo: shipper chuyển tiền cho shop.
4. Owner xác nhận nhận tiền → `WalletTransaction(Settlement)` -amount shipper, +amount shop.
5. Nếu shipper ứng tiền (AdvancePayment): `WalletTransaction(AdvancePayment)` -amount shipper, +amount shop (trước khi giao).

### 5.2. Reseller — Tất cả qua Vạn An wallet

1. Shipper thu COD → confirm → Vạn An nhận (6 transactions, xem README Section 6.2).
2. Settlement: Vạn An wallet → Owner wallet (`WalletTransaction.Settlement`).
3. AdvancePayment: Vạn An ứng trước cho Owner (`WalletTransaction.AdvancePayment` — platform wallet -amount, Owner +amount).
4. Owner rút tiền từ wallet → `WalletTransaction(Withdrawal)`.

### 5.3. Xem settlement history

> **Post-PoC:** Trang `/admin/settlements` chưa triển khai. Owner xem settlement tạm thời qua Gateway Admin API hoặc yêu cầu SystemAdmin query `WalletTransaction` type=Settlement/AdvancePayment. Planned cho Sprint 8+.

---

## 6. KITCHEN DISPLAY + POS COORDINATION

Owner điều phối giữa Kitchen (bếp) và Staff (quầy/POS):

- **Kitchen Display:** Bếp thấy order mới (KitchenHub push real-time) → chuẩn bị → bấm "Ready".
- **POS:** Staff nhận tiền tại quầy (dine-in/takeaway) → confirm order.
- **Owner** oversee cả 2 + xử lý exception (đơn hủy, đổi món, hoàn tiền).

> **Chi tiết Kitchen:** xem [06-kitchen.md](./06-kitchen.md).
> **Chi tiết Staff/POS:** xem [05-staff.md](./05-staff.md).

---

## 7. TENANT SETTINGS (VỊ TRÍ, THEME, DELIVERY)

> **Post-PoC:** Trang `/admin/tenant-settings` chưa triển khai. Owner cập nhật TenantSettings (Latitude/Longitude, Address, ShippingFee, Theme) tạm thời qua SystemAdmin (admin API) hoặc yêu cầu SystemAdmin set trực tiếp. Planned cho Sprint 8+.

### 7.1. Vị trí shop (QUAN TRỌNG cho Shipper Nearby Orders)

- **Latitude / Longitude:** Tọa độ shop — shipper thấy shop location khi accept đơn (UC-04).
- **Address:** Địa chỉ shop (text).
- Nếu chưa set lat/lng → shipper không thấy shop trên map → khó giao hàng.

### 7.2. Delivery config

- **ShippingFee:** Phí ship tenant set (Marketplace) — customer thấy khi checkout.
- **Delivery radius:** Bán kính giao hàng (km) — optional.

### 7.3. Theme + Display

- Theme, display mode, preferences — xem CRM-Loyalty Guide.

### 7.4. CommerceModeOverride (Sprint 7)

- Owner KHÔNG tự set — chỉ SystemAdmin.
- Owner thấy giá trị resolved (Inherit → global, hoặc override).

---

## 8. ONBOARDING SMS OTP (KHI TOGGLE ON)

> **Chỉ khi `CollaboratorSmsVerificationEnabled = ON`.** Owner là "Collaborator" khi onboarding tenant mới.

### 8.1. Flow

1. Owner onboarding tenant mới → nhập SĐT.
2. `POST /api/collaborator-verification/init` → server gửi SMS OTP (trừ phí deposit wallet).
3. Owner nhập OTP → `POST /api/collaborator-verification/verify`.
4. Verify thành công → `IsPhoneVerified = true`.
5. Tenant activated.

### 8.2. Retry limit

- Max 3 OTP gửi / 24h (anti-spam).
- Deposit hết → nạp thêm trước khi verify.

---

## 9. FAQ

**Q: Tôi có thể tự đổi mode Marketplace ↔ Reseller không?**
A: KHÔNG. Chỉ SystemAdmin set override per-tenant. Liên hệ SystemAdmin nếu muốn đổi.

**Q: Reseller mode, tôi có cần ứng tiền cho shipper không?**
A: KHÔNG. Vạn An ứng tiền cho bạn (AdvancePayment). Bạn chỉ cần giao hàng cho shipper.

**Q: Salesman giới thiệu product của tôi, tôi có bị trừ tiền không?**
A: KHÔNG. Commission salesman do Vạn An trả (từ margin Reseller, hoặc từ commission fund Marketplace). Owner nhận đầy đủ CostPrice (Reseller) hoặc TotalAmount - ShippingFee (Marketplace).

**Q: Product của tôi không hiện trong Salesman Nearby Products?**
A: Bạn cần set product làm **Featured Product** (`/admin/featured-products`) + đề nghị SystemAdmin set `ProductReferralConfig` (commission + bonus).

**Q: Shipper không thấy shop location trên map?**
A: Bạn chưa set Latitude/Longitude trong TenantSettings. Liên hệ SystemAdmin set tọa độ shop (trang `/admin/tenant-settings` chưa triển khai — Post-PoC).

**Q: Đơn COD tôi nhận tiền khi nào?**
A: Marketplace: shipper confirm COD → settlement → bạn xác nhận nhận tiền. Reseller: Vạn An nhận COD → settlement qua Vạn An wallet → bạn rút từ wallet.

---

> **Xem thêm:** [README index](./README.md) | [System Admin](./01-systemadmin.md) | [Staff](./05-staff.md) | [Kitchen](./06-kitchen.md)
