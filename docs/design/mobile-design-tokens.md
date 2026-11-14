# Mobile Design Tokens & Conventions (W3-2 FR-L9)

> Phạm vi: quy ước layout mobile cho KhachLink PWA + Directory — **doc + tokens, không refactor lùi**.
> Nguồn: SRS Entry Points Retention FR-L9 + thực tế codebase (Bootstrap 5 + `VanAn.UI.Platform.Tokens`).

## 1. Breakpoints

|Breakpoint|Giá trị|Dùng cho|
|---|---|---|
|Ultra-narrow|`< 400px`|Header icon overflow (FR-L3): tối đa 3 icon — `⋮` menu + cart + profile; `.header-icon-secondary` ẩn bằng CSS `@media (max-width: 400px)` trong `KhachLinkLayout.razor`.|
|Small (sm)|`≥ 576px`|Bootstrap mặc định — `d-md-none`, bottom nav, `col-6` cards vẫn 2 cột.|
|Medium (md)|`≥ 768px`|Sidebar nav desktop (`d-none d-md-block`), grid `col-md-*`.|

Quy tắc: **mobile-first** — markup mặc định cho mobile, `md:`/`lg:` mở rộng. Không dùng breakpoint tự chế ngoài 400px trừ khi có lý do ghi comment (như FR-L3).

## 2. Grid convention

- **Card grids mobile:** `col-6` (2 cột) cho thẻ sản phẩm/cửa hàng trên mobile — tránh `col-12` làm phí diện tích; `col-md-6 col-lg-4/3` mở rộng desktop. Tiền lệ: W1-2 Home grid `col-6`, HoptacxaPortal member cards `col-6 col-md-4 col-lg-3`.
- **List/card trong `VanAnCard`:** `d-flex align-items-center gap-3`, media trái (`store-logo-sm` 48px hoặc icon `bi-shop` 2rem), nội dung `flex-grow-1`, actions xếp dọc `gap-2`.
- **Bottom nav mobile:** chiều cao động qua `--mobile-nav-total` (70px base, 128px khi có community row) — footer PHẢI chừa `height: var(--mobile-nav-total)`.

## 3. Spacing tokens (`VanAn.UI.Platform.Tokens.SpacingTokens`)

|Token|CSS|Dùng|
|---|---|---|
|XS|0.25rem|icon↔text gap nhỏ, badge inset|
|SM|0.5rem|`gap-2` tương đương — button groups, card padding nhỏ|
|MD|1rem|`--spacing-4` page padding (`khachlink-main`), section gap|
|LG|1.5rem|section spacing lớn|
|XL|3rem|hero/empty-state padding|

Quy tắc: ưu tiên class Bootstrap (`gap-2`, `p-3`, `mb-3`) trong markup; tokens C# cho component API (`VanAn*` `Padding`/`Margin` params). Không tự bịa giá trị rem lẻ — làm tròn về bậc token gần nhất.

## 4. Color tokens

- Instance-scoped CSS vars inject qua `HeadContent` trong layout: `--shop-primary`, `--shop-secondary`, `--shop-nav-color`, `--shop-header-color`, `--shop-footer-color` (nguồn: `InstanceStyle` override → `ShopConfig` fallback → default).
- Neutral scale: `--color-neutral-50` (#f9f9f9) page bg — xem `ColorTokens.cs`.
- Theme classes layout-level: `.theme-classic|modern|teen|lady|premium`.

## 5. Icon & asset conventions

- Icons: **Bootstrap Icons** (`bi-*`) — cấm emoji trong UI (FR-L5: 🎤/⏹️ → `bi-mic-fill`/`bi-stop-fill`).
- Ảnh placeholder: `/images/placeholder-product.svg` local — cấm `placehold.co` (offline/PWA, FR-L8).
- Logo: `_instanceStyle.IconUrl` → `LogoUrl` → `_shopConfig.LogoUrl` → `bi-shop` fallback; `IsDefaultPlaceholderLogo` guard chống 404.

## 6. Kiểm chứng

Mọi thay đổi layout mobile BẮT BUỘC E2E (`6_Testing/e2e-tests/`, viewport 375px) theo governance Gate 4. Tiền lệ: `mobile-layout.spec.ts`, `khachlink-retention.spec.ts`, `directory-retention.spec.ts`.
