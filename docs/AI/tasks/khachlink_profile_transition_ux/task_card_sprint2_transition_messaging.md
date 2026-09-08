# Task Card Sprint 2: Transition Messaging

> **Status:** ⏳ (after Sprint 1)
> **Sprint:** 2 / 3
> **Effort:** ~1 session
> **Master plan:** `docs/AI/tasks/khachlink_profile_transition_ux/master_plan.md`
> **Top-level card:** `docs/AI/tasks/khachlink_profile_transition_ux/task_card.md`
> **Prerequisite:** ✅ Sprint 1 COMPLETE + RV PASS

## Objective

Giúp khách hàng **hiểu sự thay đổi** khi profile đổi: banner "What's New" + cart preservation notice + onboarding tour (driver.js). 3 mảnh UI + JS, không migration, không Domain.

## Scope Checklist

### Task 2.1: "What's New" banner khi khách hàng mở app sau profile change
**File:** `5_WebApps/KhachLink/Components/Shared/WhatsNewBanner.razor` — NEW
**File:** `5_WebApps/KhachLink/Components/Layout/KhachLinkLayout.razor` — UPDATE (render banner)
**File:** `5_WebApps/KhachLink/Models/KhachLinkInstanceConfig.cs` — UPDATE (add `UpdatedAt` field)
**File:** `5_WebApps/KhachLink/Services/Http/KhachLinkInstanceHttpService.cs` — UPDATE (deserialize UpdatedAt)
- [ ] Thêm `UpdatedAt` (DateTime) vào `KhachLinkInstanceConfig`
- [ ] Trong `KhachLinkInstanceHttpService.GetByCurrentDomainAsync`, deserialize `UpdatedAt` từ by-domain response (verify DTO có field — `KhachLinkInstanceDto.UpdatedAt` từ `BaseEntity.UpdatedAt`)
- [ ] Trong `KhachLinkLayout.OnInitializedAsync`, sau khi fetch `instanceConfig`:
  - Đọc `lastSeenProfileAt` từ localStorage (JSRuntime)
  - So sánh `instanceConfig.UpdatedAt > lastSeenProfileAt`
  - Nếu true → set `_showWhatsNewBanner = true`, tính `_profileChangeDirection` (old profile từ localStorage `lastSeenProfile`, new profile từ `instanceConfig.Profile`)
  - Cập nhật `lastSeenProfileAt = instanceConfig.UpdatedAt` + `lastSeenProfile = instanceConfig.Profile` vào localStorage
- [ ] `WhatsNewBanner.razor` component:
  - Parameter: `Direction` (enum: `DirectoryToFullCommerce`, `FullCommerceToDirectory`, `DirectoryToReseller`, `ResellerToFullCommerce`, `Other`)
  - Render message theo direction:
    - DirectoryToFullCommerce: "🎉 Chúng tôi đã nâng cấp! Bạn có thể mua hàng, tích điểm, đổi quà ngay trên app."
    - FullCommerceToDirectory: "ℹ️ App đã chuyển sang chế độ danh bạ. Xem danh sách cửa hàng gần bạn."
    - DirectoryToReseller: "🎉 Cửa hàng đã mở bán qua Vạn An — mua hàng trực tiếp trên app."
    - ResellerToFullCommerce: "ℹ️ Cửa hàng hiện bán hàng trực tiếp, giá không còn bao gồm phí nền tảng."
    - Other: "ℹ️ Giao diện app đã được cập nhật."
  - Dismiss button (X) → set localStorage + hide
  - Auto-hide after 10 seconds (optional)
- [ ] Render `<WhatsNewBanner>` trong `KhachLinkLayout` (sau header, trước main — cùng vị trí Reseller badge)

### Task 2.2: Cart preservation notice (FullCommerce → Directory)
**File:** `5_WebApps/KhachLink/Components/Shared/CartPreservationModal.razor` — NEW
**File:** `5_WebApps/KhachLink/Components/Layout/KhachLinkLayout.razor` — UPDATE (render modal)
- [ ] Khi detect profile change `FullCommerceToDirectory` (Task 2.1):
  - Check `CartService.Items.Count > 0`
  - Nếu true → hiển thị `CartPreservationModal`
- [ ] Modal content:
  - Title: "Giỏ hàng của bạn vẫn được lưu"
  - Body: "Giỏ hàng của bạn (N sản phẩm) vẫn được lưu. Do cửa hàng đã chuyển sang chế độ danh bạ, bạn không thể đặt hàng qua app. Liên hệ cửa hàng để đặt hàng."
  - Button: "Đã hiểu" (close modal)
- [ ] **Lưu ý:** CartService là singleton/scoped? Verify scope — Layout và Cart page share cùng instance? Nếu không, check localStorage cart state.

### Task 2.3: Onboarding tour (driver.js) cho Directory → Reseller/FullCommerce
**File:** `5_WebApps/KhachLink/Components/Shared/OnboardingTour.razor` — NEW
**File:** `5_WebApps/KhachLink/wwwroot/js/onboarding-tour.js` — NEW
**File:** `5_WebApps/KhachLink/Pages/_Host.cshtml` hoặc `index.html` — UPDATE (driver.js script tag)
- [ ] **Install driver.js:** `npm install driver.js` (hoặc CDN vendored `wwwroot/lib/driver.js/`)
  - Verify KhachLink có npm setup — nếu không, CDN vendored (copy `driver.js` + `driver.css` vào `wwwroot/lib/`)
  - **Preferred:** CDN vendored (zero build dependency, precedent: `qrcode.js` vendored)
- [ ] Add script tag in `_Host.cshtml`/`index.html`: `<script src="lib/driver.js/driver.min.js"></script>` + `<link rel="stylesheet" href="lib/driver.js/driver.min.css">`
- [ ] `onboarding-tour.js`:
  ```js
  window.vananStartOnboardingTour = function() {
      const driver = window.driver.js.driver({
          showProgress: true,
          steps: [
              { element: '#nav-cart', popover: { title: '🛒 Giỏ hàng', description: 'Mua sản phẩm trực tiếp trên app' } },
              { element: '#nav-rewards', popover: { title: '🎁 Tích điểm', description: 'Tích điểm mỗi đơn hàng, đổi quà hấp dẫn' } },
              { element: '#nav-stores', popover: { title: '📍 Cửa hàng', description: 'Tìm cửa hàng gần bạn' } }
          ]
      });
      driver.drive();
  };
  ```
- [ ] `OnboardingTour.razor`:
  - Parameter: `Direction` (enum — chỉ trigger `DirectoryToFullCommerce` + `DirectoryToReseller`)
  - Trong `OnAfterRenderAsync(firstRender)`, nếu direction match → `JSRuntime.InvokeVoidAsync("vananStartOnboardingTour")`
  - Set localStorage `onboardingCompleted = true` sau khi tour chạy (không repeat)
- [ ] Render `<OnboardingTour>` trong `KhachLinkLayout`
- [ ] **Nav element IDs:** cần thêm `id="nav-cart"`, `id="nav-rewards"`, `id="nav-stores"` vào `NavMenu.razor` (hoặc header icons trong `KhachLinkLayout`) để driver.js target
- [ ] **Lưu ý:** tour chỉ chạy 1 lần per profile change (localStorage flag `onboarding_{profile}_completed`)

## Prerequisites

- ✅ Sprint 1 COMPLETE + RV PASS (P2.2 Profile indicator + P2.3 Route guard + P4.1 Reseller badge)
- ✅ `KhachLinkInstanceConfig.Profile` field (live — Sprint 1 verified)
- ✅ `KhachLinkLayout.OnInitializedAsync` fetch instance config (live)
- ✅ `CartService` (live — verify scope)
- ✅ JSRuntime interop (live — precedent PWA service)
- ✅ localStorage pattern (live — precedent `NavMenu.razor` line 445, `KhachLinkInstanceHttpService` cache)
- ⏳ driver.js lib (CDN vendored — copy vào `wwwroot/lib/driver.js/`)

## Verification

1. **Build:** `dotnet build VanAn.sln` → 0 errors
2. **What's New banner (P2.1):** đổi profile Directory → FullCommerce → mở app `diemthuong2.khachvip.online` → banner "🎉 Chúng tôi đã nâng cấp!" hiện 1 lần → dismiss → refresh → banner không hiện lại
3. **Cart preservation (P3.2):** trên FullCommerce, add 2 items to cart → admin đổi sang Directory → mở app → modal "Giỏ hàng của bạn (2 sản phẩm) vẫn được lưu" hiện
4. **Onboarding tour (P3.1):** đổi profile Directory → FullCommerce → mở app → 3-step tooltip tour tự chạy → hoàn thành → refresh → tour không chạy lại
5. **E2E:** `profile-transition.spec.ts` PASS trên cả 2 domain (thêm test cases cho Sprint 2)

## Governance checklist

- [ ] Domain PURE — Sprint 2 KHÔNG đụng Domain (UI + JS only)
- [ ] UI Platform — `WhatsNewBanner` + `CartPreservationModal` + `OnboardingTour` dùng VanAn.UI.Platform
- [ ] KhachLink HTTP-only — banner/modal client-side (đọc localStorage + cascaded data), không thêm HTTP call
- [ ] No new .csproj
- [ ] No migration
- [ ] driver.js — external lib, MIT, ~20KB, CDN vendored (zero build dependency)
- [ ] Playwright isolation — E2E chỉ chạy sau build pass

## Risks

| # | Risk | Mitigation |
|---|---|---|
| R1 | `UpdatedAt` không có trong by-domain DTO | Verify `KhachLinkInstanceDto` có `UpdatedAt` (BaseEntity field). Nếu thiếu → thêm vào DTO + `ToDto` mapping (additive). |
| R2 | localStorage `lastSeenProfileAt` stale | TTL = session (clear on logout). Compare mỗi load. Nếu stale → banner 1 lần rồi update. |
| R3 | driver.js conflict Blazor WASM virtual DOM | driver.js chỉ manipulate DOM (read-only highlight), không modify. Init sau `OnAfterRenderAsync`. |
| R4 | Nav element IDs thiếu cho driver.js target | Thêm `id="nav-cart"` etc. vào NavMenu.razor — additive, không break. |
| R5 | Tour chạy sai timing (page chưa render) | Init trong `OnAfterRenderAsync(firstRender: true)` — sau khi nav rendered. Delay 500ms nếu cần. |
| R6 | CartService scope khác Layout | Verify: nếu scoped per-page → check localStorage cart state thay vì CartService instance. |

## Files

| # | File | Action | Status |
|---|---|---|---|
| 1 | `5_WebApps/KhachLink/Components/Shared/WhatsNewBanner.razor` | NEW | ⏳ |
| 2 | `5_WebApps/KhachLink/Components/Shared/CartPreservationModal.razor` | NEW | ⏳ |
| 3 | `5_WebApps/KhachLink/Components/Shared/OnboardingTour.razor` | NEW | ⏳ |
| 4 | `5_WebApps/KhachLink/wwwroot/js/onboarding-tour.js` | NEW | ⏳ |
| 5 | `5_WebApps/KhachLink/wwwroot/lib/driver.js/driver.min.js` | NEW (CDN vendored) | ⏳ |
| 6 | `5_WebApps/KhachLink/wwwroot/lib/driver.js/driver.min.css` | NEW (CDN vendored) | ⏳ |
| 7 | `5_WebApps/KhachLink/Models/KhachLinkInstanceConfig.cs` | UPDATE (add UpdatedAt) | ⏳ |
| 8 | `5_WebApps/KhachLink/Services/Http/KhachLinkInstanceHttpService.cs` | UPDATE (deserialize UpdatedAt) | ⏳ |
| 9 | `5_WebApps/KhachLink/Components/Layout/KhachLinkLayout.razor` | UPDATE (render banner + modal + tour) | ⏳ |
| 10 | `5_WebApps/KhachLink/Components/Layout/NavMenu.razor` | UPDATE (add element IDs for tour) | ⏳ |
| 11 | `5_WebApps/KhachLink/Pages/_Host.cshtml` hoặc `index.html` | UPDATE (driver.js script tag) | ⏳ |
| 12 | `6_Testing/e2e-tests/profile-transition.spec.ts` | UPDATE (Sprint 2 test cases) | ⏳ |

## Open questions (resolve during Sprint 2)

1. **`UpdatedAt` in by-domain DTO:** Verify `KhachLinkInstanceDto` + `ByDomainResponse` có `UpdatedAt`. Nếu thiếu → thêm (additive). Verify lúc implement.
2. **CartService scope:** Scoped per-page hoặc singleton? Nếu scoped → check localStorage cart state. Verify lúc implement.
3. **driver.js install:** npm hoặc CDN vendored? Verify KhachLink có npm setup. Nếu không → CDN vendored (preferred — zero build dep).
4. **Toast service (carry from Sprint 1):** Nếu Sprint 1 dùng `VanAnAlert` session flag → Sprint 2 có thể refactor thành toast service proper (optional).

## Related

- Master plan: `docs/AI/tasks/khachlink_profile_transition_ux/master_plan.md`
- Top-level card: `docs/AI/tasks/khachlink_profile_transition_ux/task_card.md`
- Sprint 1 (prerequisite): `docs/AI/tasks/khachlink_profile_transition_ux/task_card_sprint1_guardrail_foundation.md`
- Sprint 3 (next): `docs/AI/tasks/khachlink_profile_transition_ux/task_card_sprint3_audit_sw.md`
- KhachLink Layout (inject point): `5_WebApps/KhachLink/Components/Layout/KhachLinkLayout.razor`
- NavMenu (element IDs): `5_WebApps/KhachLink/Components/Layout/NavMenu.razor`
- driver.js docs: https://driverjs.com/
