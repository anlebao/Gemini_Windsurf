# Task Card Sprint 1: Guardrail + Foundation

> **Status:** ⏳ NEXT (awaiting session start)
> **Sprint:** 1 / 3
> **Effort:** ~1 session
> **Master plan:** `docs/AI/tasks/khachlink_profile_transition_ux/master_plan.md`
> **Top-level card:** `docs/AI/tasks/khachlink_profile_transition_ux/task_card.md`
> **Prerequisite:** ✅ ANALYZE complete + user approved

## Objective

Chặn lỗi ở nguồn (confirm dialog cho SystemAdmin) + đặt foundation cho Sprint 2 (profile indicator + route guard + Reseller badge global). 4 mảnh UI-only, không migration, không Domain.

## Scope Checklist

### Task 1.1: Confirmation dialog + impact preview khi đổi profile
**File:** `5_WebApps/ShopERP/Components/Pages/Admin/KhachLinkInstances.razor` — UPDATE
- [ ] Thêm `_showConfirmModal` state (bool) + `_confirmDiff` (list of {Flag, OldValue, NewValue})
- [ ] Trong `HandleSubmit`, trước khi gọi `InstanceApi.UpdateAsync`:
  - So sánh `_editForm.Profile` vs `item.Profile` (cũ) — nếu khác → compute diff
  - So sánh `_editForm.NavFlags` vs `item.NavFlags` (cũ) — list flags thay đổi
  - Nếu có thay đổi → set `_showConfirmModal = true`, **không call API yet**
  - Nếu không thay đổi (chỉ đổi style/color) → call API directly (không annoy admin)
- [ ] Confirm modal thứ 2 (VanAModal):
  - Title: "Xác nhận đổi profile"
  - Body: "Bạn đang đổi profile từ **{OldProfile} → {NewProfile}**. Khách hàng trên domain `{domain}` sẽ thấy thay đổi sau tối đa 1 phút."
  - Diff list: "+ Giỏ hàng", "+ Đơn hàng", "- QR gửi xe"... (so sánh NavFlags)
  - Buttons: "Xác nhận" (call API) + "Huỷ" (close)
- [ ] Sau confirm → call `InstanceApi.UpdateAsync` → close cả 2 modal

### Task 1.2: Profile indicator persistent (footer label)
**File:** `5_WebApps/KhachLink/Components/Layout/KhachLinkLayout.razor` — UPDATE
- [ ] Thêm label nhỏ ở footer (sau shop name, line ~111):
  ```
  @if (_instanceStyle?.Profile != KhachLinkProfile.FullCommerce && _instanceStyle != null)
  {
      <div class="small text-muted mt-1">
          <i class="bi bi-info-circle"></i> Chế độ: @GetProfileLabel(_instanceStyle.Profile)
      </div>
  }
  ```
- [ ] `GetProfileLabel` helper:
  - Directory → "Danh bạ cửa hàng"
  - Reseller → "Đại lý Vạn An"
  - FullCommerce → "" (ẩn — default)
  - Logistics → "Logistics" (R3)
  - JobMarket → "Sàn việc" (R3)
- [ ] CSS: `.profile-indicator { font-size: 0.75rem; opacity: 0.7; }` (reuse existing footer style)
- [ ] **Lưu ý:** `_instanceStyle` hiện là `KhachLinkInstanceConfig?` — cần thêm `Profile` field (đã có trong `KhachLinkInstanceConfig.cs` line 12). Verify `KhachLinkInstanceHttpService` deserialize `Profile` từ by-domain response (line 87 — đã có).

### Task 1.3: Route guard theo profile
**File:** `5_WebApps/KhachLink/Components/Shared/ProfileGuard.razor` — NEW
- [ ] Component wrap page content:
  ```razor
  <CascadingParameter Name="NavFlags" /> KhachLinkNavFlagsDto _navFlags
  [Parameter] public string RequiredFlag { get; set; }  // "ShowCart", "ShowRewards"...
  [Parameter] public string FeatureName { get; set; }   // "Giỏ hàng", "Đổi điểm"...
  @code {
      protected override void OnInitialized()
      {
          var flagValue = (bool)(typeof(KhachLinkNavFlagsDto)
              .GetProperty(RequiredFlag)?.GetValue(_navFlags) ?? false);
          if (!flagValue)
          {
              // Toast + redirect
              Navigation.NavigateTo("/", forceLoad: false);
              // Toast: "Tính năng {FeatureName} hiện không khả dụng trong chế độ này"
          }
      }
  }
  ```
- [ ] **Toast:** reuse existing toast pattern (verify KhachLink có toast service — nếu không, dùng `VanAnAlert` tạm thời render 1 lần)
- [ ] Wrap các page commerce-only (thêm `<ProfileGuard RequiredFlag="ShowCart" FeatureName="Giỏ hàng">` wrap content):
  - `Pages/Cart.razor` — ShowCart
  - `Pages/MyOrders.razor` — ShowOrders
  - `Pages/Rewards.razor` — ShowRewards
  - `Pages/Missions.razor` — ShowMissions
  - `Pages/MyLoyalty.razor` — ShowLoyaltyHistory
  - `Pages/Scan.razor` — ShowScan
  - `Pages/QrWallet.razor` — ShowQrClaim
  - `Pages/Campaigns.razor` — ShowCampaigns

### Task 1.4: Reseller badge global (di chuyển từ Home vào Layout)
**File:** `5_WebApps/KhachLink/Components/Layout/KhachLinkLayout.razor` — UPDATE
**File:** `5_WebApps/KhachLink/Pages/Home.razor` — UPDATE (remove badge)
- [ ] Trong `KhachLinkLayout.razor`:
  - Thêm inject `CommunityHttpService` (đã có trong Home — di chuyển)
  - Trong `OnInitializedAsync`, fetch `_isReseller` qua `CommunityHttpService.GetCommerceModeAsync(token)` (cần token — verify auth flow)
  - Render badge dưới header (sau `</header>`, trước `<main>`):
    ```razor
    @if (_commerceModeLoaded && _isReseller)
    {
        <div class="container" style="max-width:1000px;">
            <div class="alert alert-info py-2 mb-0 d-flex align-items-center gap-2">
                <i class="bi bi-info-circle"></i>
                <small><strong>Mô hình Reseller</strong> — Vạn An mua bán, giá đã bao gồm phí nền tảng.</small>
            </div>
        </div>
    }
    ```
- [ ] Trong `Home.razor`: **xóa** badge block (lines 40-49) + xóa `_commerceModeLoaded`/`_isReseller` fields + xóa fetch logic (lines 668-676) — đã di chuyển vào Layout
- [ ] **Lưu ý auth:** `GetCommerceModeAsync` cần token. Layout hiện không fetch token — cần thêm `JSRuntime.InvokeAsync<string?>("localStorage.getItem", "customer_token")` (pattern từ `NavMenu.razor` line 445). Nếu không login → `_isReseller = false` (badge ẩn — safe default).

## Prerequisites

- ✅ `KhachLinkInstances.razor` admin page (live — inject point for P1.1)
- ✅ `KhachLinkLayout.razor` (live — inject point for P2.2 + P4.1)
- ✅ `KhachLinkNavFlagsDto` cascaded via `CascadingValue Name="NavFlags"` (live — P2.3 reads this)
- ✅ `CommunityHttpService.GetCommerceModeAsync` (live — P4.1 reuse)
- ✅ `KhachLinkInstanceConfig.Profile` field (live — P2.2 reads this)
- ✅ UI Platform components (VanAModal, VanAnAlert, VanAButton)
- ✅ Both RV domains live: `timlathay.com` + `diemthuong2.khachvip.online`

## Verification

1. **Build:** `dotnet build VanAn.sln` → 0 errors
2. **Admin UI (P1.1):** vào `/admin/khachlink-instances` → edit 1 instance → đổi Profile Directory → FullCommerce → click "Lưu" → confirm dialog hiện diff ("+ Giỏ hàng, + Đơn hàng, ...") → click "Xác nhận" → API call → success alert
3. **Admin UI (P1.1 no-change):** edit instance → không đổi Profile, chỉ đổi color → click "Lưu" → **không** confirm dialog → API call directly
4. **KhachLink `timlathay.com` (P2.2):** mở app → footer hiện "Chế độ: Danh bạ cửa hàng"
5. **KhachLink `diemthuong2.khachvip.online` (P2.2):** mở app → footer **không** hiện indicator (FullCommerce = default)
6. **Route guard (P2.3):** trên `timlathay.com` (Directory), navigate `/cart` → redirect `/` + toast "Tính năng Giỏ hàng hiện không khả dụng"
7. **Route guard (P2.3):** trên `diemthuong2.khachvip.online` (FullCommerce), navigate `/cart` → page render bình thường
8. **Reseller badge (P4.1):** trên domain Reseller mode → badge "Mô hình Reseller" hiện ở `/cart`, `/stores`, `/rewards` (mọi page, không chỉ Home)
9. **E2E:** `profile-transition.spec.ts` PASS trên cả 2 domain

## Governance checklist

- [ ] Domain PURE — Sprint 1 KHÔNG đụng Domain (UI-only)
- [ ] UI Platform — `ProfileGuard.razor` + confirm modal dùng VanAn.UI.Platform
- [ ] KhachLink HTTP-only — `ProfileGuard` client-side only (đọc cascaded NavFlags, không HTTP call)
- [ ] No new .csproj
- [ ] No migration — Sprint 1 không cần PG migration
- [ ] Playwright isolation — E2E chỉ chạy sau build pass

## Risks

| # | Risk | Mitigation |
|---|---|---|
| R1 | Confirm dialog annoy admin nếu đổi style-only | Chỉ hiện confirm khi Profile hoặc NavFlags thực sự đổi (diff check), không hiện khi chỉ đổi color/theme/logo |
| R2 | Route guard break PWA shortcut | Test: PWA shortcut `search.png` (Directory OK — ShowStores=true). `/cart` deep link (Directory → redirect Home + toast). E2E cover. |
| R3 | Reseller badge fetch trên every page load gây chậm | Cache `_isReseller` trong layout state (1 call/session). `CommunityHttpService` có cache nếu có. |
| R4 | `ProfileGuard` redirect loop | Guard check `RequiredFlag` — nếu false redirect `/`. Home page không có guard → không loop. |
| R5 | Toast service không có trong KhachLink | Verify: nếu không có, dùng `VanAnAlert` render 1 lần trong Layout (session flag) hoặc JS interop `alert()`. |

## Files

| # | File | Action | Status |
|---|---|---|---|
| 1 | `5_WebApps/ShopERP/Components/Pages/Admin/KhachLinkInstances.razor` | UPDATE (confirm dialog + diff) | ⏳ |
| 2 | `5_WebApps/KhachLink/Components/Shared/ProfileGuard.razor` | NEW | ⏳ |
| 3 | `5_WebApps/KhachLink/Components/Layout/KhachLinkLayout.razor` | UPDATE (indicator + badge) | ⏳ |
| 4 | `5_WebApps/KhachLink/Pages/Home.razor` | UPDATE (remove badge block) | ⏳ |
| 5 | `5_WebApps/KhachLink/Pages/Cart.razor` | UPDATE (wrap ProfileGuard) | ⏳ |
| 6 | `5_WebApps/KhachLink/Pages/MyOrders.razor` | UPDATE (wrap ProfileGuard) | ⏳ |
| 7 | `5_WebApps/KhachLink/Pages/Rewards.razor` | UPDATE (wrap ProfileGuard) | ⏳ |
| 8 | `5_WebApps/KhachLink/Pages/Missions.razor` | UPDATE (wrap ProfileGuard) | ⏳ |
| 9 | `5_WebApps/KhachLink/Pages/MyLoyalty.razor` | UPDATE (wrap ProfileGuard) | ⏳ |
| 10 | `5_WebApps/KhachLink/Pages/Scan.razor` | UPDATE (wrap ProfileGuard) | ⏳ |
| 11 | `5_WebApps/KhachLink/Pages/QrWallet.razor` | UPDATE (wrap ProfileGuard) | ⏳ |
| 12 | `5_WebApps/KhachLink/Pages/Campaigns.razor` | UPDATE (wrap ProfileGuard) | ⏳ |
| 13 | `6_Testing/e2e-tests/profile-transition.spec.ts` | NEW | ⏳ |

## Open questions (resolve during Sprint 1)

1. **Toast service:** KhachLink có toast service không? Nếu không → dùng `VanAnAlert` session flag hoặc JS `alert()`. Verify lúc implement.
2. **`GetCommerceModeAsync` auth:** Cần token. Layout fetch token via JSRuntime localStorage (pattern NavMenu line 445). Nếu không login → `_isReseller = false` (safe default). Verify lúc implement.
3. **`ProfileGuard` CascadingParameter:** Verify `NavFlags` cascaded từ `KhachLinkLayout` đến page content (line 42 `<CascadingValue Value="_navFlags" Name="NavFlags">`). ProfileGuard trong page content nhận được. Verify lúc implement.

## Related

- Master plan: `docs/AI/tasks/khachlink_profile_transition_ux/master_plan.md`
- Top-level card: `docs/AI/tasks/khachlink_profile_transition_ux/task_card.md`
- Sprint 2 (next): `docs/AI/tasks/khachlink_profile_transition_ux/task_card_sprint2_transition_messaging.md`
- Sprint 3 (later): `docs/AI/tasks/khachlink_profile_transition_ux/task_card_sprint3_audit_sw.md`
- Admin UI (inject point): `5_WebApps/ShopERP/Components/Pages/Admin/KhachLinkInstances.razor`
- KhachLink Layout (inject point): `5_WebApps/KhachLink/Components/Layout/KhachLinkLayout.razor`
- NavMenu (cascaded NavFlags pattern): `5_WebApps/KhachLink/Components/Layout/NavMenu.razor` line 427-428
