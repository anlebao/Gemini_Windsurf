# Task W2-2 — HOPTACXA PORTAL LANDING (FR-D4 + FR-U2 reframed)

> **Status:** PENDING · **Wave:** W2 · **Gate 4:** CÓ
> **Phụ thuộc:** W1-1 (instance/profile live), W2-1 (cooperative endpoints) — hero/shell có thể làm trước, member list đợi W2-1.

## Mục tiêu giá trị

Vào `hoptacxa.timlathay.com` thấy đúng "portal hợp tác xã": hero giới thiệu + các HTX + member shops + module cards — KHÔNG còn là trang FullCommerce generic.

## Scope

1. **Portal landing variant cho profile `Hoptacxa`** trong `Home.razor` (hoặc component riêng `HoptacxaPortal.razor` render khi `_instanceStyle.Profile == Hoptacxa` — tách component cho sạch, reuse `KhachLinkLayout`):
   - Hero: tên portal + tagline + CTA ("Khám phá các hợp tác xã", "Đăng ký HTX của bạn" → link onboarding nếu có).
   - Section "Các hợp tác xã" — card per cooperative (từ `/api/cooperatives`).
   - Section "Cửa hàng thành viên" — member shop cards (từ `/api/cooperatives/{id}/members` hoặc aggregate) → click vào member storefront (khachLinkDomain/`/store/{slug}`).
   - **Module cards chừa chỗ:** Cơm từ thiện (link → commienphi.timlathay.com), Cộng tác viên (→ community pages), Mua chung + Sàn việc = "Sắp ra mắt" disabled cards.
   - Cross-tenant search giữ lại nhưng hạ xuống dưới hero (portal vẫn cần search).
2. **Contextual feature entries:** member-shop cards chuẩn bị chỗ cho AI/voice entry (W3-1 lắp — chỉ cần truyền `tenantId` xuống card).
3. `NavFlags` đã hạn chế commerce ở W1-1 → landing không render cart chrome phụ thuộc.

## Files

- `5_WebApps/KhachLink/Pages/Home.razor` (branch theo profile) hoặc `Components/HoptacxaPortal.razor` mới
- `5_WebApps/KhachLink/Services/Http/CooperativeHttpService.cs` (client mới, pattern `CatalogHttpService`)
- `6_Testing/e2e-tests/` — spec portal landing

## Tests

- Component test/unit cho service; E2E mobile 375px (Gate 4) — hero + cards không bể, member list render.
- guard-check + build + Core.Tests.

## RV checklist

- [ ] `hoptacxa.timlathay.com` → hero "Hợp tác xã" + ít nhất 1 HTX + member shop cards.
- [ ] Module cards: Cơm từ thiện click → commienphi site; Mua chung/Sàn việc = "Sắp ra mắt".
- [ ] 375px: cards không tràn, hero chữ không vỡ.
- [ ] commienphi + diemthuong2 regression: Home không đổi (profile khác không vào branch này).
