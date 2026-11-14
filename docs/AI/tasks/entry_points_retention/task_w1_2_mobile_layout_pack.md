# Task W1-2 — MOBILE LAYOUT PACK (FR-L1/L2/L4/L6/L10)

> **Status:** PENDING · **Wave:** W1 — `docs/AI/plans/entry-points-retention-master-plan.md`
> **SRS refs:** FR-L1, FR-L2, FR-L4, FR-L6, FR-L10 · **Gate 4:** CÓ (layout change → E2E bắt buộc)
> **Phụ thuộc:** không — chạy song song W1-1

## Mục tiêu giá trị

Trên điện thoại 360–430px: bottom nav không tràn/label không cắt xấu, nội dung không bị nav che, product grid 2 cột gọn, splash WASM có skeleton thay spinner trơ.

## Scope

1. **FR-L1 — Bottom nav ≤5 tab** (`NavMenu.razor` mobile section): FullCommerce/Reseller hiện render tới 8 tab → giới hạn 5 tab chính, phần còn lại gom vào tab "Thêm" (bottom sheet hoặc menu). Giữ route nguyên — chỉ nhóm nav. Quy ước ưu tiên: Home, Orders(đã login)/Stores, Scan, QR gửi xe, Tài khoản + "Thêm". Label ≤8 ký tự.
2. **FR-L2 — Padding động theo số nav rows:** thay spacer hardcode `height:70px` (`KhachLinkLayout.razor:196`) bằng padding = (số nav rows × 56px + safe-area). Community row hiện → padding ≈112px+. Implement qua CSS class hoặc biến khi `_navFlags.ShowCommunity && hasRole`.
3. **FR-L4 — Product grid 2 cột mobile** (`Home.razor` featured/history/recently-viewed): `col-md-3` → `col-6 col-md-3`; card compact: ảnh ~140px (thay 200/180px), tên 2-line clamp (`-webkit-line-clamp:2`). Desktop giữ nguyên.
4. **FR-L6 — Skeleton splash** (`wwwroot/index.html`): thay full-screen spinner "Tìm Là Thấy đang tải..." bằng skeleton mirror Home (search bar pill + 4 card placeholder, shimmer). Thuần HTML/CSS trong `#app` — brand text trung lập hoặc đọc từ instance config nếu có sẵn trước boot (optional).
5. **FR-L10 — Breakpoints:** verify không overflow-x, không che nav tại 320/360/390/430px portrait — 3 domain (diemthuong2, commienphi, hoptacxa khi live).

## Files

- `5_WebApps/KhachLink/Components/Layout/NavMenu.razor` (nav cap + "Thêm")
- `5_WebApps/KhachLink/Components/Layout/KhachLinkLayout.razor` (padding động)
- `5_WebApps/KhachLink/Pages/Home.razor` (grid 2 cột + compact card CSS)
- `5_WebApps/KhachLink/wwwroot/index.html` (skeleton splash)
- `6_Testing/e2e-tests/` — spec mới `mobile-layout.spec.ts` (375px: nav ≤5 items, no horizontal scroll, product grid 2-col, footer không bị che)

## Tests

- E2E mobile spec (Gate 4). Node/unit nếu có nav logic.
- guard-check + `dotnet build VanAn.sln`.

## RV checklist (sau deploy)

- [ ] 375px viewport: bottom nav ≤5 tab, label không ellipsis xấu.
- [ ] Có role community → 2 nav rows: footer/nội dung không bị che (scroll hết được).
- [ ] Home featured products: 2 cột trên mobile, 4 cột desktop không đổi.
- [ ] Splash: skeleton hiện trong quá trình WASM load (throttle 3G verify).
- [ ] Regression desktop 1280px: nav sidebar + grid không đổi.
