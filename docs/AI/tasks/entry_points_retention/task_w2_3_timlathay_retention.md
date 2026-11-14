# Task W2-3 — TIMLATHAY.COM RETENTION (FR-U1, FR-U4)

> **Status:** PENDING · **Wave:** W2 · **Gate 4:** CÓ (Directory SSR layout)
> **Phụ thuộc:** không (endpoint featured/cooperatives có thể dùng dữ liệu có sẵn — fallback graceful nếu W2-1 chưa xong).

## Mục tiêu giá trị

Trang `timlathay.com` không còn "rỗng dưới search box" — khách mới có thứ để click ngay; traffic không bị đẩy sang domain khác.

## Scope

1. **FR-U1 — Below-fold content** (`Directory/Components/Pages/Home.razor`, initial state `!_hasSearched` hiện render trống):
   - Category chips (8–10): "Quán ăn", "Cà phê", "Sửa xe", "Spa", "Tạp hoá"… → `SearchWithKeyword` (đã có sẵn method).
   - "Tìm gần đây": localStorage lưu last 5 keywords → chips.
   - "Cửa hàng nổi bật": dùng data sẵn có (featured tenants / catalog recommended endpoint — verify endpoint phù hợp khi implement; fallback: top tenants từ `/api/tenants/search` rỗng keyword hoặc endpoint mới tối thiểu).
2. **FR-U4 — Giữ traffic on-domain:**
   - `GoToStoreFinder` hiện `forceLoad → https://khachvip.online/stores` → đổi sang route nội bộ `StoreFinder.razor` trong Directory (đã tồn tại page này — verify nó render đủ tính năng; nếu thiếu thì port tối thiểu phần map/list).
   - `NavigateToStore`: giữ redirect sang tenant domain (đúng thiết kế) — nhưng thêm intermediate "Xem trong danh bạ" nếu tenant không có domain riêng? (đánh giá khi implement — ưu tiên đơn giản: giữ nguyên hành vi, chỉ sửa store finder).
3. Loading state cho below-fold sections (skeleton nhẹ).

## Files

- `5_WebApps/Directory/Components/Pages/Home.razor`
- `5_WebApps/Directory/Components/Pages/StoreFinder.razor` (nếu cần nâng cấp nhận query param)
- `5_WebApps/Directory/` services (localStorage recent keywords — JS interop nhỏ)
- `6_Testing/e2e-tests/` — spec directory home

## Tests

- E2E: initial load shows chips + featured section (không trống); click chip → search chạy; "Dùng vị trí" không rời domain.
- guard-check + build + Directory tests.

## RV checklist

- [ ] `timlathay.com` cold load → có nội dung dưới search box (chips + featured).
- [ ] Click chip → kết quả search.
- [ ] "Dùng vị trí của tôi" → vẫn trong timlathay.com.
- [ ] 375px: chips wrap gọn, cards không tràn.
