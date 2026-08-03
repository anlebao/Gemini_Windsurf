# HƯỚNG DẪN SỬ DỤNG CRM & LOYALTY — VẠN AN ECOSYSTEM

> **Phiên bản:** MVP 1.2 — cập nhật 2026-08-03
> **Áp dụng:** Loyalty Phase C + CRM + Promo Push + Loyalty Alliance (Phase 1-7 complete) + Loyalty Consistency Fix (BUG #0-#9 resolved, Phase 0-3 deployed, branch `main`)
> **Phạm vi:** Hệ thống CRM khách hàng, chương trình tích điểm (Silo + Alliance cross-tenant), nhiệm vụ (missions), chiến dịch khuyến mãi (promo push), đổi thưởng (redemption).
> **Thay đổi v1.2:** Tất cả đường đọc/ghi điểm giờ route theo mode (Silo → SQLite, Alliance → PG qua HTTP proxy). Legacy `POST /api/loyalty/redeem` đã deprecate (410 Gone). NATS sync mở rộng sang history. Idempotency keys đảm bảo retry-safe khi Network/Gateway tạm thời không khả dụng.

---

## MỤC LỤC

1. [Tổng quan & vai trò](#1-tổng-quan--vai-trò)
2. [System Admin — Quản trị hệ thống](#2-system-admin--quản-trị-hệ-thống)
3. [Shop Owner — Chủ cửa hàng](#3-shop-owner--chủ-cửa-hàng)
4. [Customer — Khách hàng](#4-customer--khách-hàng)
5. [Bảng tra cứu nhanh](#5-bảng-tra-cứu-nhanh)
6. [Câu hỏi thường gặp (FAQ)](#6-câu-hỏi-thường-gặp-faq)

---

## 1. TỔNG QUAN & VAI TRÒ

Hệ thống CRM-Loyalty của Vạn An phục vụ 3 đối tượng chính với quyền truy cập khác nhau:

| Vai trò | Mô tả | Nền tảng truy cập | Phạm vi dữ liệu |
|---|---|---|---|
| **System Admin** | Quản trị viên toàn hệ thống (Vạn An) | ShopERP Admin (`/admin/*`) | **Tất cả tenant** (cross-tenant) |
| **Shop Owner** | Chủ cửa hàng / HKD | ShopERP Admin (`/admin/*`) | **Tenant của mình** (per-tenant) |
| **Customer** | Khách hàng cuối | KhachLink PWA (`diemthuong.khachvip.online`) | **Tài khoản cá nhân** (token-based) |

### Kiến trúc tổng quan (Option C — PG source of truth + routed async delivery, Option B — HTTP proxy cho Alliance writes/reads)

```
Customer (KhachLink WASM, 5002)
   │  HTTP + X-Customer-Token
   ▼
Gateway (5001)  ── PostgreSQL (source of truth) ──┐
   │   • Orders + Accounting + Tenants            │
   │   • Users + FeaturedProducts                 │
   │   • Loyalty Alliance: AllianceWallet,        │
   │     AllianceTransaction (+ IdempotencyKey),  │
   │     LoyaltyGlobalConfig, LoyaltyTenantConfig │
   │   • Internal API: /api/internal/loyalty/*    │
   │     [X-Internal-Api-Key auth]                │
   │                                              ▼
   └── NATS  vanan.cloud.loyalty.changed.{deviceId} ──► ShopERP (5003)
      ▲                                              │  SQLite (replica)
      │ HTTP proxy (cache 10s + idempotency)         │  LoyaltyRewards.PointBalance
      │   • AllianceWalletServiceHttpProxy           │  + LoyaltyRewards.History
      │   • LoyaltyModeResolverHttpProxy (cache 60s) ▲
      │                                              │
      └────── ShopERP (5003) ───────────────────────┘
                  Owner / SystemAdmin (Blazor Server, cookie auth)
```

- **KhachLink** (Blazor WebAssembly PWA): giao diện khách hàng — đăng nhập OTP/Google, xem điểm (Silo + Alliance), làm nhiệm vụ, đổi thưởng, xem lịch sử đơn.
- **ShopERP** (Blazor Server): giao diện quản trị — CRM, chiến dịch promo, quản lý missions, catalog đổi thưởng, users, **cấu hình Loyalty Alliance**. Trong Alliance mode, ShopERP **không kết nối trực tiếp PG** mà gọi qua Gateway internal API bằng HTTP proxy (multi-VPS ready).
- **Auth**: Khách hàng dùng `X-Customer-Token` (token-based, không cookie); Owner/SystemAdmin dùng Cookie auth + role claims. **Internal API** giữa ShopERP ↔ Gateway dùng `X-Internal-Api-Key` (shared secret trong config).
- **HTTP proxy (Option B — Alliance writes/reads):** Khi mode=Alliance, các thao tác ghi điểm (welcome bonus, mission, redeem, refund) và đọc ví từ ShopERP đi qua `AllianceWalletServiceHttpProxy` → Gateway `InternalLoyaltyController` (5 endpoints) → PG. Wallet reads cache 10s, mode resolution cache 60s, write ops invalidate cache cho device đó. **Idempotency key** (`welcome:{customerId}`, `mission:{completionId}`, `redeem:{voucherCode}`, `refund:{recordId}`) đảm bảo retry-safe — Gateway check `AllianceTransactions.IdempotencyKey` trước khi xử lý, trùng key → trả cached result, không double-count.
- **NATS sync (Alliance mode only):** Khi ví liên minh (PG) thay đổi, Gateway publish `vanan.cloud.loyalty.changed.{customerDeviceId}` với payload mở rộng (`{ customerDeviceId, pointBalance, type, points, reason, tenantId, updatedAt }`) → ShopERP `LoyaltySyncSubscriber` cập nhật `LoyaltyRewards.PointBalance` **và append vào `LoyaltyRewards.History`** (idempotent — skip duplicate cùng timestamp+points+reason). Legacy payload (chỉ balance) vẫn tương thích ngược. **PG là source of truth, SQLite là replica** — không ngược lại.
- **Graceful fallback:** Nếu Gateway tạm thời không khả dụng khi đọc balance, `LoyaltyReadRouter` trả SQLite balance (có thể stale) thay vì lỗi — UI vẫn hoạt động, khách không thấy error page.

### Hai chế độ Loyalty: Silo vs Alliance

Hệ thống loyalty có **2 chế độ hoạt động**, do SystemAdmin cấu hình ở cấp toàn cục + override per-tenant:

| Chế độ | Lưu điểm | Dùng điểm | Phạm vi |
|---|---|---|---|
| **Silo** (mặc định) | SQLite của từng tenant | Chỉ tại tenant đó | Đóng (mỗi tenant độc lập) |
| **Alliance** | PostgreSQL `AllianceWallet` (cross-tenant) | Tại mọi tenant thành viên liên minh | Mở (cross-tenant) |

**Quyết định nghiệp vụ (Spec v1.0):**
- **Q1 — Switch Alliance→Silo:** điểm chia theo nguồn (tenant xuất xứ), không gộp.
- **Q2 — Tenant opt-out:** tenant set `IsAllianceMember=false` → bắt buộc Silo dù global=Alliance.
- **Q3 — Tenant Admin:** chỉ thấy transaction xảy ra tại tenant mình (không thấy cross-tenant).
- **Q4 — Refund:** hoàn điểm về tenant nơi redeem xảy ra (không phải tenant tích điểm).
- **Q5 — MaxWalletPoints:** configurable — global default 100,000 + per-tenant override.

**Cách xác định mode hiệu quả:** `LoyaltyModeResolver.GetEffectiveModeAsync(tenantId)` — kiểm tra per-tenant override trước, nếu không có thì lấy global. Tenant opt-out (Q2) luôn trả về Silo.

---

## 2. SYSTEM ADMIN — QUẢN TRỊ HỆ THỐNG

> **Đối tượng:** Nhân viên kỹ thuật / vận hành Vạn An, có quyền quản trị toàn hệ thống.
> **Đăng nhập:** Tài khoản SystemAdmin tại `https://khachvip.online` (cookie auth, role = SystemAdmin).

### 2.1. Danh sách trang quản trị

| Trang | URL | Chức năng chính |
|---|---|---|
| Khách hàng (toàn hệ thống) | `/admin/customers-global` | Xem khách hàng **tất cả tenant**, lọc theo điểm / lần mua / sinh nhật / chi tiêu, cột Tenant, phân trang 20/trang |
| Quản lý Tenant | `/admin/tenants` | CRUD tenant (tên, loại hình, email liên hệ, trạng thái), gán ShopInstance, suspend/reactivate, **impersonate** (đăng nhập thay tenant) |
| Shop Instances | `/admin/shop-instances` | CRUD ShopERP instance (label, base URL, max tenants, health check URL), kiểm tra sức khỏe, activate/deactivate |
| Quản lý Nhiệm vụ | `/admin/missions` | CRUD mission (PWAInstall, OtpVerify, BirthdayEntry, FacebookShare, TikTokShare, Custom), daily cap, one-time flag, sort order, active/inactive |
| Catalog Đổi thưởng | `/admin/redemption-catalog` | CRUD sản phẩm đổi thưởng (tên, mô tả, ảnh, điểm yêu cầu, tồn kho, hạn sử dụng), active/inactive |
| Lịch sử Đổi thưởng | `/admin/redemption-history` | Fulfill voucher theo mã, xem lịch sử đổi thưởng, hủy redemption đang chờ (hoàn điểm) |
| Featured Products | `/admin/featured-products` | CRUD sản phẩm nổi bật (display name, tenant, display price, sort order) |
| Social Campaigns | `/admin/campaigns` | CRUD chiến dịch social (campaign name, tenant, UTM source, tracking code), xem click/conversion stats |
| Push Campaigns | `/admin/push-campaigns` | Quản lý chiến dịch push notification toàn hệ thống |
| **Loyalty Alliance Config** | `/admin/loyalty-config` | Cấu hình chế độ Loyalty (Silo/Alliance) toàn cục + override per-tenant, MaxWalletPoints, trigger mode-switch migration (consolidate/split) |
| Audit Trail | `/admin/audit-trail` | Xem log audit (date range, action type, entity type, user ID, search term), export logs, detail modal |
| Quản lý Users | `/admin/users` | CRUD user (display name, email, tenant, role), activate/deactivate, gán role — **chung với Owner** nhưng SA thấy tất cả tenant |

### 2.2. Hướng dẫn dùng — các tác vụ thường gặp

#### 2.2.1. Xem khách hàng toàn hệ thống (`/admin/customers-global`)

Trang này cho phép SystemAdmin xem khách hàng **xuyên tenant** — hữu ích để audit, hỗ trợ CSKH, phân tích toàn hệ thống.

**Bước 1:** Đăng nhập SystemAdmin → vào `/admin/customers-global`.
**Bước 2:** Dùng bộ lọc (tùy chọn):
- **Điểm thưởng từ / đến** — lọc theo khoảng điểm
- **Lần mua gần nhất (trong N ngày)** — lọc khách hàng hoạt động gần đây
- **Sinh nhật trong tháng** — lọc theo tháng sinh (1-12)
- **Doanh số từ / đến (VND)** — lọc theo tổng chi tiêu
**Bước 3:** Bấm **Lọc**. Bảng kết quả hiển thị: Họ tên, Điện thoại, Hạng, Điểm, Tổng chi, Lần mua cuối, Sinh nhật, Định danh, **Tenant**, Trạng thái.
**Bước 4:** Phân trang 20/trang — dùng nút ‹ Trước / Sau ›.

> **Lưu ý:** Trang này **không** có nút "Gửi khuyến mãi" (chỉ Owner mới gửi promo được). SA muốn gửi promo phải impersonate tenant trước.

#### 2.2.2. Quản lý Nhiệm vụ (Missions) (`/admin/missions`)

Nhiệm vụ là các hành động khách hàng thực hiện để nhận điểm thưởng.

**Các loại mission:**
| Loại | Mô tả | Trigger |
|---|---|---|
| `PWAInstall` | Cài PWA KhachLink | Khách bấm "Cài app" trên /missions |
| `OtpVerify` | Xác thực OTP | Khách xác thực số điện thoại |
| `BirthdayEntry` | Nhập ngày sinh | Khách nhập ngày sinh trên /profile |
| `FacebookShare` | Chia sẻ lên Facebook | Khách dán URL bài share Facebook |
| `TikTokShare` | Chia sẻ lên TikTok | Khách dán URL video TikTok |
| `Custom` | Tùy chỉnh | Mission tự define |

**Tạo mission mới:**
1. Bấm **+ Thêm nhiệm vụ**
2. Nhập: Tên, Mô tả, Loại (mission type), Điểm thưởng, Daily cap (giới hạn số lần/ngày), One-time (chỉ làm 1 lần), Sort order
3. Bấm **Lưu**. Mission ở trạng thái Active mặc định.

**Chỉnh sửa:** Bấm vào tên mission → sửa → Lưu.
**Tạm dừng:** Bật/tắt công tắc Active/Inactive.

#### 2.2.3. Quản lý Catalog Đổi thưởng (`/admin/redemption-catalog`)

**Tạo sản phẩm đổi thưởng:**
1. Bấm **+ Thêm sản phẩm**
2. Nhập: Tên sản phẩm, Mô tả, URL ảnh, Điểm yêu cầu, Số lượng tồn kho, Số ngày hết hạn (expiry days)
3. Bấm **Lưu**.

**Khi khách đổi thưởng:** SystemAdmin (hoặc Owner) vào `/admin/redemption-history` → nhập mã voucher → bấm **Fulfill** để xác nhận đã giao thưởng.

#### 2.2.4. Quản lý Tenant & Impersonate (`/admin/tenants`)

**Impersonate** = đăng nhập dưới danh tính Owner của tenant đó — hữu ích để hỗ trợ, debug, cấu hình thay khách.

1. Vào `/admin/tenants` → tìm tenant cần impersonate
2. Bấm **Impersonate**
3. Hệ thống chuyển bạn sang giao diện Owner của tenant đó
4. Khi xong, bấm **Exit Impersonation** (thoát) để quay lại SystemAdmin

> **Cảnh báo:** Impersonate ghi log audit. Chỉ dùng khi cần thiết — mọi thao tác sẽ được ghi nhận dưới tên SystemAdmin.

#### 2.2.5. Xem Audit Trail (`/admin/audit-trail`)

Trang xem log mọi thao tác quan trọng trong hệ thống.

**Bộ lọc:**
- **Date range** — từ ngày / đến ngày
- **Action type** — Create / Update / Delete / Login / etc.
- **Entity type** — Customer / Order / PromoCampaign / etc.
- **User ID** — lọc theo người thực hiện
- **Search term** — tìm kiếm tự do

Bấm **Export** để tải log ra file (CSV/JSON).

#### 2.2.6. Cấu hình Loyalty Alliance (`/admin/loyalty-config`)

Trang này cho phép SystemAdmin **bật/tắt chế độ liên minh** (Alliance) và quản lý tham gia của từng tenant.

**Phần 1 — Cấu hình toàn cục (Global Config):**
| Trường | Ý nghĩa | Mặc định |
|---|---|---|
| Mode | `Silo` hoặc `Alliance` | Silo |
| MaxPointsPerOrder | Giới hạn điểm cộng tối đa / đơn | — |
| MaxWalletPoints | Giới hạn điểm tối đa trong ví | 100,000 |

**Phần 2 — Cấu hình per-tenant (Tenant Override):**
| Trường | Ý nghĩa |
|---|---|
| Mode | `null` = kế thừa global, hoặc ép `Silo`/`Alliance` cho tenant này |
| IsAllianceMember | `true` = tenant tham gia liên minh; `false` = opt-out (Q2 — bắt buộc Silo dù global=Alliance) |
| MaxWalletPoints | `null` = kế thừa global, hoặc override cho tenant này |

**Tạo/chỉnh sửa per-tenant config:**
1. Tìm tenant trong danh sách → bấm **Chỉnh sửa**.
2. Set `IsAllianceMember=true` để tenant tham gia liên minh.
3. (Tùy chọn) Override Mode / MaxWalletPoints — để `null` nếu muốn kế thừa global.
4. Bấm **Lưu**.

**Trigger mode-switch migration:**
- **Silo → Alliance (Consolidate):** Khi chuyển global mode từ Silo sang Alliance, bấm **Migrate**. Hệ thống gộp điểm SQLite của từng tenant thành ví AllianceWallet (PG) cho mỗi khách + ghi transaction `ADJUST`. Idempotent — chạy lại không double-count.
- **Alliance → Silo (Split-by-source):** Khi chuyển từ Alliance về Silo, bấm **Migrate**. Hệ thống chia điểm theo nguồn (tenant xuất xứ — Q1), phân bổ lại về SQLite từng tenant, **đóng băng** (freeze) ví AllianceWallet. Khách có net EARN ≤ 0 tại 1 tenant → không nhận phân bổ cho tenant đó.

> **Lưu ý quan trọng:**
> - Migration là **one-way operation** — chạy consolidate rồi split sẽ không trả về trạng thái ban đầu (split dựa trên transaction log, không phải snapshot).
> - Trước khi migrate, **backup PG** + thông báo cho Owner các tenant affected.
> - Migration ghi log vào Audit Trail (`/admin/audit-trail`) với `LastChangedBy` = SystemAdmin ID.

**Idempotency & retry safety (Alliance mode):**
Mỗi thao tác ghi điểm trong Alliance mode gắn idempotency key duy nhất — Gateway kiểm tra `AllianceTransactions.IdempotencyKey` trước khi xử lý. Nếu key đã tồn tại → trả cached balance, không cộng/trừ lại. Điều này đảm bảo an toàn khi NATS/HTTP retry:

| Thao tác | Idempotency key | Khi nào xảy ra retry |
|---|---|---|
| Welcome bonus (OTP verify) | `welcome:{customerId}` | Khách verify OTP nhiều lần trong cùng phiên |
| Mission completion | `mission:{completionId}` | Khách bấm "Hoàn thành" 2 lần, hoặc NATS redeliver |
| Annual mission | `mission_annual:{completionId}` | Mission lặp hàng năm |
| Redeem voucher | `redeem:{voucherCode}` | Khách bấm "Đổi ngay" 2 lần |
| Cancel redemption (refund) | `refund:{recordId}` | Owner/SA hủy redemption 2 lần |
| Order earn | `earn:{orderId}` | Order sync retry qua NATS |

> **Mẹo vận hành:** Nếu cần trace 1 giao dịch cụ thể trên PG, query `SELECT * FROM "AllianceTransactions" WHERE "IdempotencyKey" = 'welcome:{customerId}';` — mỗi thao tác nghiệp vụ có đúng 1 row (trừ khi retry, vẫn chỉ 1 row).

**API tương ứng (SystemAdmin policy):**
| Method | Endpoint | Mô tả |
|---|---|---|
| GET | `/api/platform/loyalty/config` | Lấy global config (hoặc defaults) |
| PUT | `/api/platform/loyalty/config` | Cập nhật global config |
| GET | `/api/platform/loyalty/tenant/{tenantId}/config` | Lấy per-tenant override (hoặc inherit) |
| PUT | `/api/platform/loyalty/tenant/{tenantId}/config` | Cập nhật per-tenant override |
| POST | `/api/platform/loyalty/migrate` | Trigger mode-switch migration (consolidate/split) |

---

## 3. SHOP OWNER — CHỦ CỬA HÀNG

> **Đối tượng:** Chủ cửa hàng / HKD — quản lý CRM, gửi khuyến mãi, quản lý nhân viên.
> **Đăng nhập:** Tài khoản Owner tại `https://khachvip.online` (cookie auth, role = Owner).
> **Phạm vi:** Chỉ thấy dữ liệu **tenant của mình**.

### 3.1. Danh sách trang

| Trang | URL | Chức năng chính |
|---|---|---|
| **CRM Khách hàng** | `/admin/customers` | Danh sách khách + bộ lọc + gửi promo (per-row / bulk / theo lọc) + cột Push + phân trang |
| **Chiến dịch Promo** | `/admin/promo-campaigns` | Danh sách chiến dịch + progress bar + "Chi tiết" expand + Hủy chiến dịch |
| Quản lý Users | `/admin/users` | CRUD nhân viên (Staff, Masterchef, Guard, StoreKeeper), gán role, activate/deactivate |
| Permission Groups | `/admin/permission-groups` | Quản lý nhóm quyền (nâng cao) |
| Quản lý Nhiệm vụ | `/admin/missions` | CRUD mission (chung với SA, nhưng scope tenant) |
| Catalog Đổi thưởng | `/admin/redemption-catalog` | CRUD catalog (chung với SA, scope tenant) |
| Lịch sử Đổi thưởng | `/admin/redemption-history` | Fulfill voucher, hủy redemption (scope tenant) |

### 3.2. Hướng dẫn dùng — CRM Khách hàng (`/admin/customers`)

Đây là trang **quan trọng nhất** của Owner — quản lý khách hàng thân thiết và gửi khuyến mãi.

> **Lưu ý mode-aware (v1.2 — BUG #8 fix):** Cột **Điểm** trong bảng khách hàng hiển thị **PG wallet balance** khi tenant đang ở Alliance mode + khách có DeviceId, fallback về SQLite balance khi Silo mode hoặc Gateway tạm không khả dụng. Owner không cần thao tác gì — hệ thống tự route qua `LoyaltyReadRouter`.

#### 3.2.1. Xem & lọc khách hàng

**Bộ lọc** (trong card "Bộ lọc khách hàng"):
| Trường | Ý nghĩa | Ví dụ |
|---|---|---|
| Điểm thưởng từ / đến | Lọc theo khoảng điểm tích lũy | 100 → 1000 |
| Lần mua gần nhất (trong N ngày) | Khách có đơn hàng trong N ngày qua | 30 |
| Sinh nhật trong tháng | Khách sinh tháng nào (1-12, 0 = tất cả) | 7 (tháng 7) |
| Doanh số từ / đến (VND) | Lọc theo tổng chi tiêu | 500000 → 5000000 |

Bấm **Lọc** để áp dụng, **Xóa lọc** để reset.

**Bảng kết quả** hiển thị các cột:
- Checkbox chọn (cho bulk action)
- Họ tên, Điện thoại, Hạng (tier), Điểm, Tổng chi
- Lần mua cuối (dd/MM/yyyy), Sinh nhật (dd/MM)
- Định danh (IdentityLevel: Social / Verified)
- **Push** — ✓ (đã đăng ký push) / ✗ (chưa)
- **Thao tác** — nút "Gửi" (gửi promo cho 1 khách)

#### 3.2.2. Gửi khuyến mãi cho 1 khách (per-row)

**Bước 1:** Tìm khách hàng cần gửi → bấm nút **"Gửi"** ở cột Thao tác.
**Bước 2:** Modal "Gửi thông báo khuyến mãi" hiện ra, hiển thị "Sẽ gửi cho **1 khách hàng**".
**Bước 3:** Nhập:
- **Tiêu đề** (tối đa 100 ký tự) — vd: "Khuyến mãi cuối tuần"
- **Nội dung thông báo** (tối đa 500 ký tự) — vd: "Giảm 20% cho tất cả món nước từ 15h-17h hôm nay!"
- **Link đích** (tùy chọn) — vd: `/rewards` hoặc URL đầy đủ
**Bước 4:** Bấm **Tạo chiến dịch**. Hệ thống tạo chiến dịch + gửi push notification đến khách.
**Bước 5:** Thông báo Success: "Đã tạo chiến dịch '...' với 1 người nhận. Đang xử lý — xem tiến độ tại /admin/promo-campaigns."

#### 3.2.3. Gửi khuyến mãi cho nhiều khách (bulk select)

**Bước 1:** Tick checkbox ở cột đầu tiên cho từng khách cần gửi.
- Tick **tất cả trên trang**: checkbox ở header bảng
- Selection **lưu xuyên suốt phân trang** — tick khách trang 1, sang trang 2 tick thêm, tổng chọn được giữ nguyên
- Bấm **"Gửi cho N đã chọn"** (nút hiển thị số lượng đã chọn)
**Bước 2:** Modal hiện ra với "Sẽ gửi cho **N khách hàng đã chọn** (danh sách tĩnh)".
**Bước 3:** Nhập tiêu đề + nội dung + link → bấm **Tạo chiến dịch**.

> **Lưu ý:** "Danh sách tĩnh" = danh sách ID khách được snapshot tại thời điểm tạo. Nếu khách bị xóa/inactive sau đó, hệ thống tự skip (không lỗi).

#### 3.2.4. Gửi khuyến mãi theo bộ lọc (segment)

**Bước 1:** Áp dụng bộ lọc (vd: sinh nhật tháng 7 + điểm > 100).
**Bước 2:** Bấm **"Gửi theo lọc (N)"** — N = số khách thỏa bộ lọc.
**Bước 3:** Modal hiện ra với "Sẽ gửi cho **N khách hàng** (theo bộ lọc hiện tại)".
**Bước 4:** Nhập tiêu đề + nội dung + link → bấm **Tạo chiến dịch**.

> **Khác biệt:** Segment = động (dựa trên criteria tại thời điểm gửi), Bulk = tĩnh (danh sách ID cố định).

#### 3.2.5. Xuất CSV danh sách khách

**Bước 1:** Áp dụng bộ lọc nếu cần.
**Bước 2:** Gọi `POST /api/customers/export` với body cùng cấu trúc `/segment` (SegmentRequest).
**Bước 3:** Hệ thống trả về file `customers.csv` với các cột:
`Name, Phone, Tier, Points, TotalSpent, LastOrder, Birthday, IdentityLevel, HasPush`

> **Mẹo:** Dùng curl/Postman để gọi API này. Hiện chưa có nút UI trực tiếp — endpoint API sẵn sàng để tích hợp.

### 3.3. Hướng dẫn dùng — Chiến dịch Promo (`/admin/promo-campaigns`)

#### 3.3.1. Xem danh sách chiến dịch

Bảng hiển thị: Tiêu đề, Nội dung, Người nhận, Đã gửi, Thất bại, Trạng thái (Pending / Processing / Completed / Cancelled), Ngày tạo.

#### 3.3.2. Theo dõi tiến độ (Progress bar)

- Chiến dịch ở trạng thái **Processing** sẽ hiển thị **progress bar** (sọc animated)
- Width = `SentCount / TotalRecipients * 100%`
- Trang **tự động refresh mỗi 5 giây** khi có chiến dịch Processing/Pending
- Khi tất cả chiến dịch Completed/Cancelled → auto-refresh dừng

#### 3.3.3. Xem chi tiết người nhận

**Bước 1:** Bấm **"Chi tiết"** trên dòng chiến dịch.
**Bước 2:** Bảng người nhận hiện ra bên dưới — hiển thị: Tên khách, Trạng thái gửi (Sent / Failed / Pending), Thời gian gửi, ErrorMessage (nếu fail).
**Bước 3:** Phân trang 20/người nhận — bấm **"Tải thêm"** để xem tiếp.

#### 3.3.4. Hủy chiến dịch

- Bấm **"Hủy"** trên chiến dịch ở trạng thái Pending hoặc Processing
- Hệ thống dừng gửi các notification chưa gửi. Những cái đã gửi không thu hồi được.

### 3.4. Quản lý nhân viên (`/admin/users`)

Owner tạo tài khoản nhân viên với các role:
| Role | Quyền |
|---|---|
| Staff | Xem đơn hàng, xử lý đơn |
| Masterchef | Xem + xử lý đơn + cập nhật trạng thái bếp |
| Guard | Xem đơn (read-only) |
| StoreKeeper | Quản lý kho |

**Tạo user:** Bấm **+ Thêm user** → nhập display name, email, role → Lưu.

---

## 4. CUSTOMER — KHÁCH HÀNG

> **Đối tượng:** Khách hàng cuối — tích điểm, làm nhiệm vụ, đổi thưởng.
> **Truy cập:** PWA KhachLink tại `https://diemthuong.khachvip.online`
> **Auth:** Đăng nhập bằng **Google OAuth** (miễn phí) hoặc **SĐT + OTP**.

### 4.1. Danh sách trang

| Trang | URL | Chức năng |
|---|---|---|
| Đăng nhập | `/login` | Google OAuth hoặc SĐT + OTP |
| Hồ sơ | `/profile` | Xem điểm / hạng / định danh, bật push, nhập sinh nhật, nâng cấp định danh |
| Thẻ tích điểm | `/my-loyalty` | Xem thẻ hạng, progress bar, đổi điểm, lịch sử giao dịch (Silo / replica) |
| **Ví điểm liên minh** | `/alliance-wallet` | Xem ví cross-tenant — tổng điểm, breakdown theo tenant, lịch sử giao dịch EARN/REDEEM/ADJUST (Alliance mode only) |
| Nhiệm vụ | `/missions` | Danh sách nhiệm vụ + làm nhiệm vụ + lịch sử hoàn thành |
| Đổi thưởng | `/rewards` | Catalog sản phẩm đổi + đổi voucher |
| Lịch sử đơn | `/my-orders` | Danh sách đơn hàng + tracking |
| Tìm cửa hàng | `/stores` | Tìm cửa hàng theo tên / vị trí |
| Cửa hàng | `/store/{slug}` | Trang cửa hàng cụ thể |

### 4.2. Hướng dẫn dùng

#### 4.2.1. Đăng nhập

**Cách 1 — Google OAuth (khuyến nghị):**
1. Vào `/login` → bấm **"Đăng nhập với Google"**
2. Chọn tài khoản Google → hoàn tất

**Cách 2 — SĐT + OTP:**
1. Vào `/login` → nhập số điện thoại → bấm **"Gửi OTP"**
2. Nhập mã OTP nhận được qua SMS → bấm **"Xác nhận"**

> Sau đăng nhập, hệ thống cấp `X-Customer-Token` — token này được dùng cho mọi API call tiếp theo (tự động, khách không cần thao tác).

> **Welcome bonus mode-aware (v1.2 — BUG #6 fix):** Khi khách xác thực OTP lần đầu, hệ thống tự động cộng welcome bonus. Trong **Alliance mode** (tenant là thành viên liên minh), bonus được cộng vào **PG AllianceWallet** qua HTTP proxy với idempotency key `welcome:{customerId}` — retry-safe. Trong **Silo mode**, bonus vào SQLite của tenant đó như cũ. Khách không cần biết cơ chế bên dưới — chỉ thấy điểm tăng ở `/my-loyalty` (hoặc `/alliance-wallet` nếu Alliance).

#### 4.2.2. Hồ sơ (`/profile`)

Trang hồ sơ hiển thị:
- **Họ tên** + **Hạng** (Bronze / Silver / Gold / Platinum)
- **Điểm tích lũy** hiện tại
- **Identity Level** — Social (mặc định) / Verified (đã xác thực OTP nâng cao)
- **Toggle Push Notification** — bật/tắt nhận thông báo push
- **Nhập ngày sinh** — nhập để nhận thưởng sinh nhật (mission BirthdayEntry)
- **Nâng cấp định danh** — bấm để xác thực OTP nâng cấp từ Social → Verified (cần Verified để đổi thưởng)

#### 4.2.3. Thẻ tích điểm (`/my-loyalty`)

- **Thẻ hạng** với gradient màu theo hạng (Bronze → Platinum)
- **Điểm hiện tại** + **Progress bar** đến hạng tiếp theo
- **Bảng quyền lợi hạng** — so sánh Bronze / Silver / Gold / Platinum
- **Đổi điểm** — nhập số điểm muốn đổi → bấm "Đổi"
- **Lịch sử giao dịch** — danh sách các giao dịch tích/đổi điểm gần đây

> **Lưu ý:** Để đổi điểm, khách phải ở IdentityLevel ≥ Verified. Nếu chưa, hệ thống hiển thị modal hướng dẫn nâng cấp.

> **Mode-aware balance (v1.2 — BUG #4 fix):** Endpoint `GET /api/loyalty/my` giờ route qua `LoyaltyReadRouter` — trong Alliance mode + khách có DeviceId, trả về **PG wallet balance** (qua HTTP proxy, cache 10s). Trong Silo mode hoặc khi Gateway tạm không khả dụng, trả SQLite balance. Lịch sử giao dịch hiển thị trên trang này là **local history** (SQLite) — để xem lịch sử cross-tenant đầy đủ, dùng `/alliance-wallet`.

#### 4.2.4. Ví điểm liên minh (`/alliance-wallet`)

Trang này chỉ hoạt động khi hệ thống đang ở **Alliance mode** và tenant của khách là **thành viên liên minh** (`IsAllianceMember=true`). Nếu không, trang hiển thị prompt "Chưa tham gia liên minh".

**4 trạng thái hiển thị:**
1. **Loading** — đang tải dữ liệu ví từ Gateway PG.
2. **Chưa đăng nhập** — yêu cầu đăng nhập trước.
3. **Chưa tham gia liên minh** — tenant chưa opt-in hoặc global mode=Silo. Hiển thị thông báo, không có dữ liệu.
4. **Ví đã tải** — hiển thị đầy đủ thông tin ví.

**Nội dung ví (state 4):**
- **Thẻ tổng điểm** (gradient) — `TotalPointBalance` từ PG `AllianceWallet`.
- **Breakdown theo tenant** — điểm tích lũy tại từng tenant (tên tenant được resolve qua API). Ví dụ:
  - Cửa hàng A: 1,200 điểm
  - Cửa hàng B: 850 điểm
  - **Tổng: 2,050 điểm**
- **Lịch sử giao dịch gần đây** (20 giao dịch mới nhất) — mỗi giao dịch có:
  - **Loại:** EARN (tích điểm) / REDEEM (đổi thưởng) / ADJUST (migration adjust)
  - **Icon + voucher code** (nếu là REDEEM)
  - **Số điểm** (+ / -)
  - **Thời gian**
- **Nút "Đổi điểm ngay"** → chuyển sang `/rewards` (RedemptionService tự route sang PG wallet khi Alliance mode).

**Cách truy cập:**
- Desktop: sidebar nav → **"Ví liên minh"**
- Mobile: bottom-nav → **"Liên minh"**
- Hoặc từ trang `/my-loyalty` → bấm link card **"Xem ví điểm liên minh (cross-tenant)"**

> **Khác biệt với `/my-loyalty`:**
> - `/my-loyalty` hiển thị điểm của **tenant hiện tại** (Silo balance, hoặc replica sync từ PG qua NATS khi Alliance).
> - `/alliance-wallet` hiển thị ví **cross-tenant** (PG source of truth) — tổng điểm + breakdown theo tenant.
> - Khi Alliance mode, **`/alliance-wallet` là nguồn chính xác** để xem tổng điểm khả dụng.

**API tương ứng (Customer, X-Customer-Token header):**
| Method | Endpoint | Mô tả |
|---|---|---|
| GET | `/api/loyalty/wallet` | Lấy ví liên minh (Gateway PG) — trả `{ totalPointBalance, breakdown, recentTransactions }` |
| GET | `/api/loyalty/my-identity` | Resolve token → deviceId (ShopERP forward, internal) |

> **Lưu ý:** Endpoint `/api/loyalty/wallet` trả 404 nếu khách chưa có device identity (chưa từng đăng nhập từ device nào). Khách cần đăng nhập qua PWA ít nhất 1 lần để tạo device identity.

#### 4.2.5. Nhiệm vụ (`/missions`)

Trang nhiệm vụ hiển thị 2 phần:

> **Mission points mode-aware (v1.2 — BUG #1 fix):** Khi khách hoàn thành mission, điểm thưởng được route theo mode — Alliance mode + tenant là thành viên liên minh → cộng vào **PG AllianceWallet** qua HTTP proxy với idempotency key `mission:{completionId}` (mission thường) hoặc `mission_annual:{completionId}` (mission lặp hàng năm). Silo mode → cộng vào SQLite tenant đó. Retry-safe — khách bấm "Hoàn thành" 2 lần không bị double-count.

**Phần 1 — Nhiệm vụ đang hoạt động:**
- Mỗi nhiệm vụ có: icon, tên, mô tả, điểm thưởng, badge (One-time / Daily cap), số lần đã hoàn thành
- **Nút hành động** tùy loại mission:
  - `PWAInstall` → nút **"Cài app"**
  - `OtpVerify` → nút **"Xác thực"** (chuyển sang /profile)
  - `BirthdayEntry` → nút **"Nhập sinh nhật"** (chuyển sang /profile)
  - `FacebookShare` / `TikTokShare` → nút **"Chia sẻ"** → modal dán URL bài share
- Khi hoàn thành, điểm tự động cộng vào tài khoản

**Phần 2 — Lịch sử hoàn thành:**
- Danh sách nhiệm vụ đã hoàn thành (phân trang 20/trang)
- Bấm **"Xem thêm"** để tải trang tiếp theo

#### 4.2.6. Đổi thưởng (`/rewards`)

**Bước 1:** Vào `/rewards` — hiển thị catalog sản phẩm đổi thưởng (ảnh, tên, mô tả, điểm yêu cầu, tồn kho).
**Bước 2:** Tìm sản phẩm muốn đổi → bấm **"Đổi ngay"**.
- Nút bị **disabled** nếu: không đủ điểm, hết hàng, hoặc chưa Verified
**Bước 3:** Hệ thống trừ điểm + tạo voucher → modal hiển thị:
- **Mã voucher** (code)
- **QR code** (quét tại quầy để nhận thưởng)
- **Ngày hết hạn**
**Bước 4:** Mang voucher đến cửa hàng — nhân viên quét QR hoặc nhập mã để fulfill.

> **Redeem mode-aware (v1.2 — BUG #2 fix):** Khi khách bấm "Đổi ngay", `RedemptionService.RedeemAsync` route theo mode — Alliance mode + tenant là thành viên liên minh → trừ điểm từ **PG AllianceWallet** (qua HTTP proxy, idempotency key `redeem:{voucherCode}`), voucher vẫn tạo trong SQLite của tenant redeem (để fulfill tại quầy). Silo mode → trừ SQLite như cũ.
>
> **Hủy redemption (refund) mode-aware:** Khi Owner/SA hủy redemption đang chờ ở `/admin/redemption-history`, refund cũng route theo mode — Alliance → hoàn điểm vào PG wallet tại **tenant nơi redeem xảy ra** (Q4), idempotency key `refund:{recordId}`. Silo → hoàn vào SQLite.
>
> **Legacy redeem endpoint DEPRECATED (v1.2 — BUG #3 / D3):** Endpoint cũ `POST /api/loyalty/redeem` (đổi điểm thẳng, không qua catalog) giờ trả **410 Gone**. Dùng `POST /api/redemption/redeem` (catalog-based) thay thế — endpoint mới có mode routing + idempotency. Khách hàng PWA không dùng endpoint legacy này (UI đã chuyển sang `/rewards`), note dành cho integration partner nếu có.

#### 4.2.7. Lịch sử đơn hàng (`/my-orders`)

- Tabs lọc theo trạng thái: Tất cả / Pending / Processing / Completed / Cancelled
- Mỗi đơn hiển thị: Mã đơn (8 ký tự đầu), ngày, số món, trạng thái, tổng tiền, VAT
- Bấm **"Theo dõi"** để xem chi tiết trạng thái đơn tại `/order-tracking/{orderId}`

#### 4.2.8. Tìm cửa hàng (`/stores`)

- **Tìm kiếm** theo tên / sản phẩm / dịch vụ
- **"Dùng vị trí của tôi"** — dùng GPS để tìm cửa hàng gần nhất
- **Bán kính** — 2km / 5km / 10km / 50km / 100km
- Mỗi cửa hàng hiển thị: tên, địa chỉ, SĐT, khoảng cách, link Google Maps, nút "Xem trang cửa hàng"

---

## 5. BẢNG TRA CỨU NHANH

### 5.1. Phân quyền theo vai trò

| Tính năng | System Admin | Owner | Customer |
|---|:---:|:---:|:---:|
| Xem khách hàng (tenant mình) | ✓ (impersonate) | ✓ | — |
| Xem khách hàng (tất cả tenant) | ✓ | — | — |
| Gửi promo per-row | — (impersonate) | ✓ | — |
| Gửi promo bulk | — (impersonate) | ✓ | — |
| Gửi promo theo segment | — (impersonate) | ✓ | — |
| Xuất CSV khách hàng | — (impersonate) | ✓ | — |
| CRUD Mission | ✓ | ✓ | — |
| CRUD Redemption Catalog | ✓ | ✓ | — |
| Fulfill voucher | ✓ | ✓ | — |
| CRUD Tenant | ✓ | — | — |
| CRUD ShopInstance | ✓ | — | — |
| Impersonate tenant | ✓ | — | — |
| Xem Audit Trail | ✓ | — | — |
| CRUD User (tenant) | ✓ (all) | ✓ (own) | — |
| Xem điểm / hạng / profile | — | — | ✓ |
| Làm nhiệm vụ | — | — | ✓ |
| Đổi thưởng | — | — | ✓ |
| Xem lịch sử đơn | — | — | ✓ |
| Nhận push notification | — | — | ✓ |

### 5.2. URL truy cập

| URL | Đối tượng | Mô tả |
|---|---|---|
| `https://khachvip.online` | Owner / SA | ShopERP Admin (Blazor Server) |
| `https://khachvip.online/admin/customers` | Owner | CRM khách hàng |
| `https://khachvip.online/admin/customers-global` | SA | CRM cross-tenant |
| `https://khachvip.online/admin/promo-campaigns` | Owner | Chiến dịch promo |
| `https://khachvip.online/admin/missions` | SA / Owner | Quản lý nhiệm vụ |
| `https://khachvip.online/admin/redemption-catalog` | SA / Owner | Catalog đổi thưởng |
| `https://khachvip.online/admin/redemption-history` | SA / Owner | Lịch sử đổi thưởng |
| `https://khachvip.online/admin/tenants` | SA | Quản lý tenant |
| `https://khachvip.online/admin/audit-trail` | SA | Audit log |
| `https://diemthuong.khachvip.online` | Customer | KhachLink PWA |
| `https://diemthuong.khachvip.online/login` | Customer | Đăng nhập |
| `https://diemthuong.khachvip.online/profile` | Customer | Hồ sơ |
| `https://diemthuong.khachvip.online/my-loyalty` | Customer | Thẻ tích điểm |
| `https://diemthuong.khachvip.online/missions` | Customer | Nhiệm vụ |
| `https://diemthuong.khachvip.online/rewards` | Customer | Đổi thưởng |
| `https://diemthuong.khachvip.online/my-orders` | Customer | Lịch sử đơn |

### 5.3. API endpoints theo vai trò

#### System Admin + Owner (Cookie auth, `OwnerOnly` / `SystemAdmin` policy)
| Method | Endpoint | Mô tả |
|---|---|---|
| GET | `/api/customers` | List khách (tenant-scoped, OwnerOnly) |
| GET | `/api/customers/global` | List khách cross-tenant (SystemAdmin) |
| POST | `/api/customers/segment` | Preview segment (dry-run) |
| POST | `/api/customers/export` | Export CSV |
| GET | `/api/promo-campaigns` | List chiến dịch |
| POST | `/api/promo-campaigns` | Tạo chiến dịch |
| GET | `/api/promo-campaigns/{id}/recipients` | List người nhận |
| POST | `/api/promo-campaigns/{id}/cancel` | Hủy chiến dịch |
| GET/POST/PUT/DELETE | `/api/missions` | CRUD mission |
| GET/POST/PUT/DELETE | `/api/redemption/catalog` | CRUD catalog |
| POST | `/api/redemption/fulfill` | Fulfill voucher |
| POST | `/api/redemption/cancel/{id}` | Hủy redemption |

#### Customer (X-Customer-Token header)
| Method | Endpoint | Mô tả |
|---|---|---|
| POST | `/api/customer-identity/otp/send` | Gửi OTP |
| POST | `/api/customer-identity/otp/verify` | Xác thực OTP |
| GET | `/api/customer-identity/me` | Thông tin khách (mode-aware — PG balance khi Alliance, v1.2 BUG #7 fix) |
| GET | `/api/loyalty/my` | Thông tin tích điểm (mode-aware — PG balance khi Alliance) |
| POST | `/api/loyalty/redeem` | ⚠️ **DEPRECATED — 410 Gone** (v1.2). Dùng `POST /api/redemption/redeem` (catalog-based, mode-aware) |
| GET | `/api/missions/active` | Nhiệm vụ đang hoạt động |
| GET | `/api/missions/my/progress` | Tiến độ nhiệm vụ |
| GET | `/api/missions/my/completions?page=1&pageSize=20` | Lịch sử hoàn thành (phân trang) |
| GET | `/api/redemption/catalog/active` | Catalog đổi thưởng |
| POST | `/api/redemption/redeem` | Đổi voucher |
| POST | `/api/customer-profile/birthday` | Lưu ngày sinh |
| POST | `/api/notifications/push/subscribe` | Đăng ký push |
| DELETE | `/api/notifications/push/subscribe` | Hủy push |
| GET | `/api/customerorders` | Lịch sử đơn hàng |

---

## 6. CÂU HỎI THƯỜNG GẶP (FAQ)

### Cho Owner

**Q: Khách báo không nhận được push notification?**
A: Vào `/admin/customers` → kiểm tra cột **Push**. Nếu ✗, khách chưa đăng ký push. Hướng dẫn khách vào `/profile` → bật toggle Push Notification. Nếu ✓ nhưng vẫn không nhận, kiểm tra trình duyệt khách có cho phép notification không.

**Q: Gửi promo cho 3 khách nhưng chỉ 2 người nhận?**
A: Có thể 1 khách đã bị xóa (IsDeleted) hoặc inactive sau thời điểm bạn mở modal. Hệ thống tự skip khách không hợp lệ — xem chi tiết tại `/admin/promo-campaigns` → bấm "Chi tiết" → cột ErrorMessage.

**Q: Progress bar không hiển thị?**
A: Progress bar chỉ hiện với chiến dịch **Processing**. Nếu chiến dịch đã Completed, bar sẽ biến mất. Trang auto-refresh 5s khi có Processing — nếu không refresh, kiểm tra trình duyệt có block JavaScript không.

**Q: Bulk select bị reset khi đổi filter?**
A: Đúng — khi đổi filter, selection được prune (loại bỏ khách không còn trong kết quả). Đây là behavior cố định để tránh gửi promo cho khách không thỏa điều kiện mới.

### Cho Customer

**Q: Tôi không đổi được thưởng?**
A: Có 3 nguyên nhân:
1. **Chưa Verified** — vào `/profile` → bấm "Nâng cấp định danh" → xác thực OTP
2. **Không đủ điểm** — xem điểm yêu cầu trên từng sản phẩm tại `/rewards`
3. **Hết hàng** — nút "Đổi ngay" bị disabled, liên hệ cửa hàng

**Q: Làm nhiệm vụ FacebookShare nhưng không được cộng điểm?**
A: URL bài share phải hợp lệ — phải chứa `/posts/` hoặc `permalink?story_id=`. URL homepage hoặc profile cá nhân sẽ bị từ chối. Xem chi tiết tại `/missions` → bấm "Chia sẻ" → dán URL đúng định dạng.

**Q: Điểm của tôi không tăng sau khi đặt hàng?**
A: Điểm được cộng tự động khi đơn hàng chuyển sang **Completed**. Nếu đơn đã Completed nhưng điểm chưa tăng, liên hệ cửa hàng — có thể khách chưa được liên kết với đơn (guest checkout không có CustomerId).

> **Alliance mode (v1.2):** Điểm EARN từ đơn hàng được cộng vào PG AllianceWallet với idempotency key `earn:{orderId}` — retry-safe qua NATS. Nếu Gateway tạm thời không khả dụng, order sync sẽ retry; điểm cộng đúng 1 lần khi Gateway phục hồi.

**Q: Tôi đăng nhập trên điện thoại, có nhận push không?**
A: Có — sau khi cài PWA (bấm "Cài app" trên /missions) và bật push notification ở `/profile`. Push hoạt động trên cả desktop và mobile PWA.

**Q: Tôi thấy 2 số dư khác nhau ở `/my-loyalty` và `/alliance-wallet`?**
A: Đây là behavior bình thường trong Alliance mode:
- `/my-loyalty` hiển thị balance của **tenant hiện tại** (local history, có thể stale vài giây do NATS sync).
- `/alliance-wallet` hiển thị **tổng ví cross-tenant** từ PG (source of truth, cache 10s).
- Nếu số chênh lệch nhiều hoặc không đổi sau 1 phút, có thể NATS sync đang lag — liên hệ cửa hàng để kiểm tra.

**Q: Tôi bấm "Đổi ngay" 2 lần nhưng chỉ bị trừ điểm 1 lần?**
A: Đúng — đây là tính năng idempotency (v1.2). Hệ thống gắn idempotency key `redeem:{voucherCode}` cho mỗi lần đổi. Nếu bấm 2 lần tạo cùng 1 voucher, Gateway chỉ xử lý 1 lần, lần 2 trả cached result. An toàn cho khách — không lo double-charge khi mạng lag.

### Cho System Admin

**Q: Impersonate tenant có an toàn không?**
A: Mọi thao tác impersonate đều được ghi vào Audit Trail (`/admin/audit-trail`) với User ID của SystemAdmin. Chỉ dùng khi cần hỗ trợ khách — không lạm dụng.

**Q: Làm sao xem khách hàng của 1 tenant cụ thể?**
A: 2 cách:
1. `/admin/customers-global` → lọc theo Tenant (cột Tenant)
2. Impersonate tenant đó → vào `/admin/customers` (sẽ thấy chỉ khách của tenant đó)

**Q: Cột Điểm trong `/admin/customers` hiển thị số khác với `/alliance-wallet` của khách?**
A: Trong Alliance mode, cột Điểm route qua `LoyaltyReadRouter` — lấy PG wallet balance (cache 10s). Có thể stale tối đa 10s so với PG thực tế. `/alliance-wallet` cũng cache 10s nên 2 số này nên khớp sau <10s. Nếu lệch liên tục, kiểm tra Gateway internal API (`X-Internal-Api-Key` config) + NATS sync subscriber có chạy không.

**Q: Gateway tạm thời down, khách có bị lỗi không?**
A: Không — `LoyaltyReadRouter` có **graceful fallback**: nếu Gateway không khả dụng khi đọc balance, trả SQLite balance (có thể stale) thay vì error page. Ghi điểm (welcome/mission/redeem) trong Alliance mode sẽ fail và hiển thị lỗi cho khách — đây là behavior cố ý để tránh mất điểm. Khi Gateway phục hồi, khách có thể thực hiện lại thao tác (idempotency key đảm bảo không double-count).

**Q: Làm sao trace 1 giao dịch trên PostgreSQL?**
A: Mỗi giao dịch có idempotency key duy nhất. Query trực tiếp:
```sql
SELECT "TransactionId", "Type", "Points", "Reason", "TransactionTenantId", "TransactionAt"
FROM "AllianceTransactions"
WHERE "IdempotencyKey" = 'welcome:{customerId}';
```
Tham khảo bảng idempotency key ở mục 2.2.6. Mỗi thao tác nghiệp vụ có đúng 1 row (trừ khi retry, vẫn chỉ 1 row).

**Q: NATS sync có đảm bảo history không bị duplicate?**
A: Có — `LoyaltySyncSubscriber` check `(timestamp, points, reason)` trước khi append vào `LoyaltyRewards.History`. Nếu NATS redeliver cùng message, history entry đã tồn tại → skip. Balance update thì idempotent theo bản chất (last-write-wins).

---

> **Tài liệu này áp dụng cho phiên bản MVP 1.2 (Loyalty Alliance Phase 1-7 + Loyalty Consistency Fix BUG #0-#9 resolved, 2026-08-03).**
> **Thay đổi v1.2:** Mode-aware balance reads (BUG #4/#7/#8) + mode-aware point writes (BUG #1/#2/#6) + legacy redeem 410 Gone (BUG #3/D3) + NATS history sync (BUG #9) + HTTP proxy infrastructure Option B (BUG #0). Phase 4 (VPS Runtime Verification 14-step) đang chờ thực hiện.
> **Cập nhật tiếp theo:** sau khi Phase 4 VPS RV pass, hoặc khi có Sprint 1 (Nearby Orders) / tính năng mới.
