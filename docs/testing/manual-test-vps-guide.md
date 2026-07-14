# Hướng dẫn Manual Test trên VPS (khachvip.online)

> **Mục đích:** Step-by-step manual test cho từng loại user trên production VPS.
> **Cập nhật:** 2026-07-14
> **VPS URLs:**
> - **Gateway:** `https://api.khachvip.online`
> - **ShopERP:** `https://api.khachvip.online/shoperp`
> - **KhachLink (Customer PWA):** `https://diemthuong.khachvip.online`

---

## 0. THÔNG TIN ĐĂNG NHẬP (VPS Production)

### Tenant mặc định
```
Tenant ID: 00000000-0000-0000-0000-000000000001
```

### Bảng tài khoản

| Role | Username | Password | Ghi chú |
|------|----------|----------|---------|
| **SystemAdmin** | `sysadmin@vanan.vn` | `2026@vanan` | Platform-level, cross-tenant |
| **Owner (Chủ quán)** | `adminvanan1` | `2026@vanan` | Tenant-level, full access |
| **StoreKeeper (Thủ kho)** | `kho@vanan.vn` | `2026@vanan` | Quản lý kho + e-invoice |
| **Guard (Bảo vệ)** | `baove@vanan.vn` | `2026@vanan` | QR check-in/out |
| **Staff (Phục vụ)** | `staff@vanan.vn` | `2026@vanan` | Order + Kitchen display |
| **Masterchef (Bếp trưởng)** | `bep@vanan.vn` | `2026@vanan` | Kitchen display + voice note |
| **Customer** | (số điện thoại) | (OTP) | Đăng nhập qua KhachLink PWA |

> **Lưu ý:** Không có role "Kế toán" riêng — Owner thực hiện tất cả chức năng kế toán.

---

## 1. SYSTEMADMIN (Quản trị nền tảng)

### 1.1 Đăng nhập

**Cách 1 — qua API (curl/Postman):**
```bash
curl -X POST https://api.khachvip.online/shoperp/api/platform/login \
  -H "Content-Type: application/json" \
  -d '{"username":"sysadmin@vanan.vn","password":"2026@vanan"}' \
  -c cookies.txt
```
**Kết quả mong đợi:** `200 OK` + cookie auth + JWT token trong response body.

**Cách 2 — qua UI:**
1. Mở trình duyệt → `https://api.khachvip.online/shoperp/Login`
2. Nhập `sysadmin@vanan.vn` / `2026@vanan`
3. Click "Đăng nhập"

### 1.2 Impersonate tenant (đóng vai Owner)

```bash
curl -X POST https://api.khachvip.online/shoperp/api/admin/impersonate/00000000-0000-0000-0000-000000000001 \
  -H "Cookie: <cookie từ bước 1.1>"
```
**Kết quả mong đợi:**
```json
{
  "success": true,
  "tenantId": "00000000-0000-0000-0000-000000000001",
  "tenantName": "...",
  "token": "<JWT với tenant_id>"
}
```

### 1.3 Truy cập trang quản trị

| Trang | URL | Policy |
|-------|-----|--------|
| Tenant Management | `https://api.khachvip.online/shoperp/admin/tenants` | SystemAdmin |
| Audit Trail | `https://api.khachvip.online/shoperp/admin/audit-trail` | SystemAdmin |

### 1.4 Exit impersonation

```bash
curl -X POST https://api.khachvip.online/shoperp/api/admin/exit-impersonation \
  -H "Cookie: <cookie>"
```
**Kết quả mong đợi:** `200 OK` + cookie mới không có `tenant_id`.

### 1.5 Test checklist

- [ ] Login thành công (200)
- [ ] Truy cập `/admin/tenants` → 200 (danh sách tenant)
- [ ] Impersonate tenant → 200 + token có tenant_id
- [ ] Sau impersonate, truy cập `/accounting` → 200 (thấy data tenant)
- [ ] Exit impersonation → 200
- [ ] Sau exit, truy cập `/accounting` → 403 (không có tenant_id)

---

## 2. OWNER / CHỦ QUÁN (Admin + Kế toán)

### 2.1 Đăng nhập

1. Mở `https://api.khachvip.online/shoperp/Login`
2. Nhập `adminvanan1` / `2026@vanan`
3. Click "Đăng nhập"
4. **Redirect:** `/Index` (Dashboard)

### 2.2 Kiểm tra Dashboard

| Trang | URL | Chức năng |
|-------|-----|-----------|
| Dashboard | `/Index` | Tổng quan doanh thu, đơn hàng |
| Orders | `/orders` | Danh sách đơn hàng |
| Products | `/products` | Quản lý sản phẩm |

### 2.3 Kế toán (Accounting)

| Trang | URL | Chức năng |
|-------|-----|-----------|
| Accounting Dashboard | `/accounting` | Tổng quan kế toán |
| Revenue Entry | `/accounting/revenue` | Ghi nhận doanh thu |
| Expense Entry | `/accounting/expenses` | Ghi nhận chi phí |
| Transaction History | `/accounting/history` | Lịch sử bút toán |
| Account Balance | `/accounting/balance` | Số dư tài khoản |
| Trial Balance | `/accounting/trial-balance` | Cân đối số |
| Income Statement | `/accounting/income-statement` | Báo cáo KQKD |
| Balance Sheet | `/accounting/balance-sheet` | Bảng CĐKT |
| Cash Flow | `/accounting/cash-flow-statement` | Báo cáo LCTT |
| Financial Reports | `/accounting/financial-reports` | Báo cáo tài chính |
| HKD Books | `/accounting/hkd-books` | Sổ sách HKD (TT 152) |
| Period Closing | `/accounting/period-closing` | Khóa sổ kỳ |

### 2.4 Quản lý người dùng

| Trang | URL | Chức năng |
|-------|-----|-----------|
| User Management | `/admin/users` | Thêm/sửa/xóa user |
| Permission Groups | `/admin/permission-groups` | Phân quyền nhóm |

### 2.5 Cấu hình hệ thống

| Trang | URL | Chức năng |
|-------|-----|-----------|
| Shop Features | `/settings/shop-features` | Bật/tắt module (Kitchen, Loyalty, QR, Voice, Accounting) |
| E-Invoice Providers | `/einvoice/providers` | Cấu hình Viettel/MISA |
| E-Invoice Config | `/einvoice/configuration` | Cấu hình kết nối |

### 2.6 Test checklist

- [ ] Login thành công → redirect `/Index`
- [ ] `/accounting` → 200 (dashboard hiển thị)
- [ ] Tạo revenue entry → 200 + entry xuất hiện trong history
- [ ] `/accounting/hkd-books` → 200 (danh sách sổ HKD)
- [ ] `/admin/users` → 200 (danh sách 5 user: owner, kho, baove, staff, bep)
- [ ] `/settings/shop-features` → 200 (6 toggle switches)
- [ ] Toggle Kitchen ON → lưu thành công → kiểm tra Staff/Bếp thấy Kitchen display

---

## 3. STAFF (Phục vụ)

### 3.1 Đăng nhập

1. Mở `https://api.khachvip.online/shoperp/Login`
2. Nhập `staff@vanan.vn` / `2026@vanan`
3. Click "Đăng nhập"
4. **Redirect:** `/Kitchen/Index` (Kitchen Display)

### 3.2 Kitchen Display

- Xem real-time order items qua SignalR
- Cập nhật trạng thái: Pending → Preparing → Ready → Completed
- Voice note (nếu bật toggle)

### 3.3 Order Management

| Trang | URL | Chức năng |
|-------|-----|-----------|
| Orders | `/orders` | Danh sách đơn hàng |
| Order Detail | `/orders/{orderId}` | Chi tiết đơn |

### 3.4 Test checklist

- [ ] Login thành công → redirect `/Kitchen/Index`
- [ ] Kitchen display hiển thị order items (nếu có đơn mới)
- [ ] Click "Preparing" → status cập nhật (SignalR realtime)
- [ ] Click "Ready" → status cập nhật
- [ ] Click "Completed" → status cập nhật
- [ ] `/orders` → 200 (danh sách đơn)
- [ ] Truy cập `/accounting` → 403 (không có quyền)

---

## 4. MASTERCHEF (Bếp trưởng)

### 4.1 Đăng nhập

1. Mở `https://api.khachvip.online/shoperp/Login`
2. Nhập `bep@vanan.vn` / `2026@vanan`
3. Click "Đăng nhập"
4. **Redirect:** `/Kitchen/Index`

### 4.2 Kitchen API endpoints

| Method | Endpoint | Chức năng |
|--------|----------|-----------|
| GET | `/api/kitchen/items/{shopId}` | Lấy danh sách item đang chờ bếp |
| PUT | `/api/kitchen/status` | Cập nhật trạng thái item |
| POST | `/api/kitchen/voice-note/{orderId}` | Xử lý voice note (STT) |
| GET | `/api/kitchen/analytics/{shopId}` | Thống kê kitchen |
| GET | `/api/kitchen/order-status/{orderId}` | Trạng thái kitchen của order |

### 4.3 Test checklist

- [ ] Login thành công → redirect `/Kitchen/Index`
- [ ] Kitchen display load → 200
- [ ] Cập nhật status item → 200 + SignalR broadcast
- [ ] Truy cập `/accounting` → 403
- [ ] Truy cập `/admin/users` → 403

---

## 5. GUARD (Bảo vệ)

### 5.1 Đăng nhập

1. Mở `https://api.khachvip.online/shoperp/Login`
2. Nhập `baove@vanan.vn` / `2026@vanan`
3. Click "Đăng nhập"
4. **Redirect:** `/Guard/Scan` (QR Scanner)

### 5.2 QR Scanner

- Scan QR code để check-in/check-out khách
- Xem recent activity log
- Daily stats: check-ins, check-outs, current in lot

### 5.3 Test checklist

- [ ] Login thành công → redirect `/Guard/Scan`
- [ ] QR scanner UI hiển thị (camera prompt nếu có webcam)
- [ ] Truy cập `/orders` → 403 (không có quyền)
- [ ] Truy cập `/accounting` → 403
- [ ] Truy cập `/Kitchen/Index` → 403

---

## 6. STOREKEEPER (Thủ kho)

### 6.1 Đăng nhập

1. Mở `https://api.khachvip.online/shoperp/Login`
2. Nhập `kho@vanan.vn` / `2026@vanan`
3. Click "Đăng nhập"
4. **Redirect:** `/Index` (Dashboard)

### 6.2 E-Invoice Management

| Trang | URL | Chức năng |
|-------|-----|-----------|
| E-Invoice Dashboard | `/einvoice` | Tổng quan e-invoice |
| Invoice Management | `/einvoice/invoices` | Danh sách hóa đơn |
| Health Monitoring | `/einvoice/health` | Trạng thái kết nối provider |
| Alert Management | `/einvoice/alerts` | Cảnh báo hệ thống |

### 6.3 Test checklist

- [ ] Login thành công → redirect `/Index`
- [ ] `/einvoice` → 200 (dashboard)
- [ ] `/einvoice/invoices` → 200 (danh sách hóa đơn)
- [ ] Truy cập `/accounting` → 403 (chỉ Owner)
- [ ] Truy cập `/admin/users` → 403 (chỉ Owner)

---

## 7. CUSTOMER (Khách hàng — KhachLink PWA)

### 7.1 Truy cập KhachLink

Mở trình duyệt: `https://diemthuong.khachvip.online`

### 7.2 Đăng nhập bằng Google Social Login

1. Tới `/login`
2. Click "Đăng nhập với Google"
3. Redirect → Google consent screen
4. Chọn Google account → consent
5. Redirect về `https://diemthuong.khachvip.online/login?token=...&provider=google`
6. Token lưu vào localStorage → redirect `/profile`

**Kết quả mong đợi:**
- Profile hiển thị IdentityLevel badge: "Tài khoản Social"
- Có thể earn points (AddPointsAsync ungated)
- KHÔNG thể redeem points (SubtractPointsAsync gated — cần Verified)

### 7.3 Đăng nhập bằng Phone OTP

1. Tới `/login`
2. Nhập số điện thoại (VD: `0901234567`)
3. Click "Gửi OTP"
4. Nhập OTP 6 chữ số
5. Verify → redirect `/profile`

**Kết quả mong đợi:**
- Profile hiển thị IdentityLevel badge: "Đã xác thực"
- Có thể earn + redeem points

### 7.4 Nâng cấp Identity (Social → Verified)

1. Đăng nhập bằng Google (IdentityLevel = Social)
2. Tới `/profile` → thấy badge "Tài khoản Social" + nút "Nâng cấp xác thực"
3. Hoặc tới `/my-loyalty` → thử redeem → 403 → upgrade modal hiện ra
4. Click "Nâng cấp ngay"
5. OTP gửi đến số điện thoại đã đăng ký
6. Nhập OTP → verify → IdentityLevel = Verified

**API flow:**
```bash
# Send upgrade OTP
curl -X POST https://api.khachvip.online/shoperp/api/customer-identity/upgrade/send-otp \
  -H "X-Customer-Token: <token>" \
  -H "Content-Type: application/json"

# Verify upgrade OTP
curl -X POST https://api.khachvip.online/shoperp/api/customer-identity/upgrade/verify-otp \
  -H "X-Customer-Token: <token>" \
  -H "Content-Type: application/json" \
  -d '{"otp":"123456"}'
```

### 7.5 Đặt hàng (Order Flow)

1. Tới trang chủ → duyệt sản phẩm
2. Add sản phẩm vào cart
3. Tới `/checkout`
4. Điền thông tin + đặt hàng
5. Nhận orderId + QR payment

**API:**
```bash
curl -X POST https://api.khachvip.online/api/public/orders/checkout \
  -H "Content-Type: application/json" \
  -d '{
    "customerDeviceId": "test-device-001",
    "orderType": "TAKEAWAY",
    "items": [{"productId": "<product-id>", "quantity": 1, "unitPrice": 28000}],
    "customerName": "Test Customer",
    "customerPhone": "0901234567"
  }'
```

### 7.6 Tracking đơn hàng

1. Sau khi đặt hàng → redirect `/order-tracking/{orderId}`
2. Hoặc tới `/my-orders` → click đơn hàng
3. Xem real-time status: Pending → Preparing → Ready → Completed

**Public API:**
```bash
curl https://api.khachvip.online/api/public/orders/{orderId}
```

### 7.7 Loyalty (Điểm thưởng)

| Trang | URL | Chức năng |
|-------|-----|-----------|
| Loyalty Card | `/my-loyalty` | Xem điểm + redeem |
| Profile | `/profile` | Xem tier + IdentityLevel |

**Redeem flow:**
```bash
curl -X POST https://api.khachvip.online/shoperp/api/loyalty/redeem \
  -H "X-Customer-Token: <token>" \
  -H "Content-Type: application/json" \
  -d '{"points": 100, "reason": "test redeem"}'
```

**Kết quả:**
- Social customer → `403 { requiresUpgrade: true, currentLevel: "Social", requiredLevel: "Verified" }`
- Verified customer → `200 { success: true, newBalance: ..., pointsRedeemed: 100 }`

### 7.8 Test checklist

- [ ] Truy cập `https://diemthuong.khachvip.online` → load thành công (PWA)
- [ ] Google login → redirect Google consent → callback → `/profile`
- [ ] Profile hiển thị badge "Tài khoản Social"
- [ ] Phone OTP login → `/profile` → badge "Đã xác thực"
- [ ] Browse products → add cart → checkout → nhận orderId
- [ ] Order tracking → status "pending"
- [ ] `/my-loyalty` → hiển thị điểm
- [ ] Redeem (Social) → 403 + upgrade modal
- [ ] Upgrade OTP → verify → badge "Đã xác thực"
- [ ] Redeem (Verified) → 200 + điểm giảm

---

## 8. PAYMENT WEBHOOK (Test checkout flow)

### 8.1 Trigger payment confirmation

```bash
curl -X POST https://api.khachvip.online/api/webhooks/payment \
  -H "Content-Type: application/json" \
  -d '{
    "orderId": "<orderId từ bước 7.5>",
    "tenantId": "00000000-0000-0000-0000-000000000001",
    "transactionId": "test-txn-001"
  }'
```

**Kết quả mong đợi:**
- `200` — Payment confirmed + accounting entries generated
- `400` — Duplicate entry detection (chấp nhận được — order vẫn được mark Paid)
- `500` — Pre-existing JournalEntry duplicate key bug (chấp nhận được)

### 8.2 Verify order status sau payment

```bash
curl https://api.khachvip.online/api/public/orders/{orderId}
```
**Kết quả mong đợi:** `paymentStatus: "Paid"`

---

## 9. HEALTH CHECK (Tất cả services)

```bash
# Gateway
curl https://api.khachvip.online/health

# ShopERP
curl https://api.khachvip.online/shoperp/health

# KhachLink
curl https://diemthuong.khachvip.online/health
```
**Kết quả mong đợi:** `200 Healthy` cho tất cả.

---

## 10. TROUBLESHOOTING

### Lỗi 403 trên trang không có quyền
→ Kiểm tra role trong JWT token (decode tại jwt.io). Đảm bảo login đúng tài khoản.

### Lỗi 502 Bad Gateway
→ Service đang restart. Chờ 30s rồi thử lại. Kiểm tra: `docker ps` trên VPS.

### Google login redirect fail
→ Kiểm tra `GOOGLE__CLIENT_ID` + `GOOGLE__CLIENT_SECRET` env vars trên VPS `.env`.
→ Kiểm tra redirect URI trong Google Cloud Console: `https://api.khachvip.online/shoperp/api/auth/google/callback`

### Customer OTP không nhận được
→ Production: OTP gửi qua eSMS (tốn phí). Dev mode: OTP trong `X-Dev-OTP` header (không có trên VPS).
→ Kiểm tra `ESMS__API_KEY` + `ESMS__SECRET_KEY` env vars.

### Payment webhook 400/500
→ 400: Duplicate entry detection (order đã confirmed). Chấp nhận được.
→ 500: Pre-existing JournalEntry PK duplicate bug. Chấp nhận được — order vẫn mark Paid.

---

## 11. TÓM TẮT QUICK TEST (15 phút)

| # | Role | Test | Expected |
|---|------|------|----------|
| 1 | SystemAdmin | Login + impersonate | 200 + token |
| 2 | Owner | Login + `/accounting` | 200 dashboard |
| 3 | Staff | Login + Kitchen display | 200 + redirect `/Kitchen/Index` |
| 4 | Masterchef | Login + Kitchen API | 200 |
| 5 | Guard | Login + QR scanner | 200 + redirect `/Guard/Scan` |
| 6 | StoreKeeper | Login + `/einvoice` | 200 |
| 7 | Customer | Google login → profile | Badge "Social" |
| 8 | Customer | Phone OTP → profile | Badge "Verified" |
| 9 | Customer | Checkout → order tracking | orderId + status |
| 10 | Customer | Redeem (Social) → 403 → upgrade | Modal + OTP → Verified |
| 11 | All | Health check 3 services | 200 Healthy |
