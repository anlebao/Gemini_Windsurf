# Task W2-4 — KHACHLINK HOME RETENTION (FR-U3, FR-U5)

> **Status:** PENDING · **Wave:** W2 · **Gate 4:** CÓ
> **Phụ thuộc:** W1-2 (đừng đụng cùng vùng Home.razor đồng thời — làm sau hoặc rebase cẩn thận).

## Mục tiêu giá trị

New user vào `diemthuong2.khachvip.online` hiểu được đây là app gì + có lý do ở lại; mọi empty state đều có CTA phục hồi; retention hooks (nhiệm vụ/điểm/ưu đãi) surface được ngay cả khi chưa login.

## Scope

1. **FR-U3 — New-user hero strip** (`Home.razor`, khi `_lastTenantId` rỗng = new user):
   - Strip ngắn dưới search card: 1 câu value prop + nút "Cài app" (reuse `PWAService.CanInstallAsync` — ẩn nếu không install được) + "Cách dùng" 3 bước mini (Quét QR / Đặt món / Tích điểm).
   - Empty states: "Chưa có sản phẩm nổi bật" → + CTA "Khám phá cửa hàng" (→ `/stores`) + "Quét QR ngay" (→ `/scan`). Campaign/store section empty → CTA tương tự.
2. **FR-U5 — Retention hooks surface:**
   - Teaser card "Điểm thưởng của bạn": logged-in → số điểm + CTA "Đổi điểm"; logged-out → "Đăng nhập để tích điểm" CTA (→ `/login`).
   - "Nhiệm vụ hôm nay" teaser (2–3 missions nổi bật nếu API public/anonymous-safe; nếu cần auth → chỉ hiện logged-in).
   - Campaign section: hiện gate `_lastTenantId` — mở thêm nhánh cross-tenant: nếu không có lastTenant → lấy campaigns nổi bật global (cần endpoint; fallback ẩn như hiện nay nếu API chưa có).
3. Lịch sử/Đã xem: CTA "Mua lại" hiện → `/scan` → đổi deep-link tới store/product tương ứng (`/store/{slug}` hoặc product route — verify route tồn tại; nếu chưa có product detail route, giữ `/scan` và ghi nợ vào W3).

## Files

- `5_WebApps/KhachLink/Pages/Home.razor`
- `5_WebApps/KhachLink/Components/` (teaser cards — UI Platform components)
- `6_Testing/e2e-tests/` — spec new-user home

## Tests

- E2E 375px: new-user hero hiện khi không có `_lastTenantId` (localStorage sạch); empty-state CTA hoạt động.
- guard-check + build + Core.Tests.

## RV checklist

- [ ] Incognito mở diemthuong2 → hero strip + "Cài app" (nếu prompt khả dụng) + teasers.
- [ ] Empty state featured → có CTA, không text chết.
- [ ] Logged-in → teaser điểm đúng số.
- [ ] Regression: user có `_lastTenantId` → campaign/store section vẫn như cũ.
