# MASTER PLAN — ICON APP (PWA) RIÊNG CHO TỪNG KHACHLINK INSTANCE

> **Date:** 2026-10-15 · **Status:** PLAN — chờ user duyệt (newfeaturebuild Step 4)
> **Objective:** KhachLink instance có icon/logo riêng phân biệt theo loại (Profile); SystemAdmin đổi được; PWA cài trên điện thoại khách hiển thị icon này.

## 1. HIỆN TRẠNG (đã verify)
- `manifest.json` của KhachLink là **file TĨNH** (`wwwroot/manifest.json` — icons `icons/icon-*.png` cố định, name hardcode "timlathay.com") — **mọi instance/domain dùng chung 1 icon**; nginx chỉ override content-type (`location = /manifest.json`).
- `KhachLinkInstance` (PG) đã có: `Profile` (FullCommerce=0 · Directory=1 · Logistics=2 · JobMarket=3 · Reseller=4 — "các loại") + style overrides `Theme/LogoUrl/NavColor/HeaderColor/FooterColor` (Issue #143 — logo header đã có) — **CHƯA có trường icon app (PWA)**.
- Admin `/admin/khachlink-instances` (SystemAdmin, `KhachLinkInstanceApiClient`) quản lý instance + style; có public `GET /api/v1/khachlink-instances/by-domain/{domain}` (KhachLink runtime).

## 2. GIẢI PHÁP

### P1 — Domain (Gate 5)
`KhachLinkInstance` += `byte[]? IconData` · `string? IconContentType` · `DateTime? IconUpdatedAt` + `UpdateIcon(byte[]? data, string? contentType)` (null = xóa override → về **icon mặc định theo Profile**; audit). Icon đại diện app — KHÔNG đụng `LogoUrl` (logo header giữ riêng).

### P2 — Gateway (PG source of truth)
- **Admin (SystemAdmin):** `PUT /api/v1/khachlink-instances/{id}/icon` { iconBase64, contentType } (limit 2MB, validate PNG) + `DELETE /api/v1/khachlink-instances/{id}/icon` (clear → default).
- **Public (no auth):**
  - `GET /api/public/khachlink-instances/icons/{instanceId}` → bytes icon (instance override hoặc **default theo Profile**) + `Cache-Control`.
  - `GET /api/public/khachlink-instances/manifest` → resolve **domain từ Host header** → instance → manifest JSON động: `name`/`short_name` = Label, `theme_color` = HeaderColor/Theme, `icons` = absolute Gateway URL trỏ icons endpoint (kèm default static khi không có instance/unknown domain — giữ PWA hoạt động).
- **Default icons theo loại:** 5 PNG (FullCommerce · Directory · Logistics · JobMarket · Reseller) đặt trong Gateway assets → serve qua icons endpoint khi instance chưa override.

### P3 — nginx (KhachLink VPS)
`location = /manifest.json` → **proxy_pass Gateway manifest endpoint** (giữ `Host` để resolve domain, no-cache). Directory domains cũng đi qua (manifest location độc lập với `$directory_target`).

### P4 — Admin UI (ShopERP `/admin/khachlink-instances`)
Card **"Icon ứng dụng (PWA)"** trong modal create/edit: preview icon hiện tại (instance hoặc default theo Profile) + nút upload PNG (≤2MB) → base64 → API + nút "Dùng mặc định theo loại" (clear). DTO `KhachLinkInstanceDto` += `IconUpdatedAt` (để UI biết có override không). `KhachLinkInstanceApiClient` += upload/clear icon.

### P5 — KhachLink (tùy chọn, ghi chú)
Dùng icon làm **favicon** (`index.html` là static — chỉ được cho 1 icon chung; giới hạn ghi nhận). iOS `apple-touch-icon` cần index.html động — ngoài scope (ghi chú: Android/Chrome PWA là mục tiêu chính).

### P6 — Tests
Domain `UpdateIcon` guard · service (set/clear) · controller (admin auth, manifest by domain — icon đúng instance, fallback default, unknown domain → static default) · bUnit admin page (upload/clear) · manifest JSON shape.

### P7 — Deploy + RV
Migration PG `AddKhachLinkInstanceIcon` · guard + build + Core.Tests + ShopERP.Tests → FAST PUSH → CD → RV:
- L1 markers + migration applied (cột IconData/IconContentType/IconUpdatedAt).
- L2 health + `/api/public/khachlink-instances/manifest` (curl theo Host → manifest đúng instance).
- L3/L4: upload icon qua admin → manifest trả icon mới · PWA install (browser) hiện icon instance (user manual — Android).

## 3. RÀNG BUỘC / RISK
- Domain PURE; `AccountingEntry` không đụng; enum `KhachLinkProfile` giữ nguyên.
- Icon ≤ 2MB/PNG (validate) — tránh DB phình; vài instance → chấp nhận PG bytes.
- Manifest động phụ thuộc Gateway: nếu Gateway down → /manifest.json lỗi (PWA install tạm gián đoạn) — chấp nhận (Gateway là dependency chính của app); fallback unknown-domain = manifest tĩnh hiện tại.
- CORS/CDN: icon URL tuyệt đối tới Gateway — trình duyệt tải icon cross-origin OK (manifest icon là ảnh tĩnh).

## 4. QUYẾT ĐỊNH USER (đã chốt 2026-10-15)
- Q1 Lưu trữ: **bytes trong PG** + Gateway serve (permanent, tự chứa).
- Q2 Icon mặc định: **tôi tự tạo 5 icon** (commerce/directory/logistics/jobmarket/reseller) trong Gateway assets.
- Q3 Phạm vi: **GỘP CẢ LOGO HEADER** — icon app cũng là nguồn logo header KhachLink (ưu tiên: instance icon → LogoUrl → ShopConfig.LogoUrl → profile default → generic).

## 5. CÂU HỎI ĐÃ CHỐT (2026-10-15)
- Q1 PG bytes · Q2 tự tạo 5 icon · Q3 gộp logo header.

