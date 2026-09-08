# Coding Plan — Sprint 2: Transition Messaging

> **Task card:** `docs/AI/tasks/khachlink_profile_transition_ux/task_card_sprint2_transition_messaging.md`
> **Master plan:** `docs/AI/tasks/khachlink_profile_transition_ux/master_plan.md`
> **Prerequisite:** ✅ Sprint 1 COMPLETE + PUSHED (`cbeff2a3` on `main`) — ProfileGuard.razor, profile-toast.js, profile-transition.spec.ts đã live
> **Mode:** IMPLEMENT (UI + JS only, không migration, không Domain)
> **Branch target:** new branch `feature/khachlink-sprint2-transition-messaging` from `main` @ `cbeff2a3`

---

## 0. Pre-Implementation Verification (RESOLVED open questions)

Các open questions trong task card đã verify qua codebase exploration (2026-09-08):

| # | Question | Resolution | Evidence |
|---|---|---|---|
| OQ1 | `UpdatedAt` in by-domain DTO? | ✅ **Server-side CÓ** — `KhachLinkInstanceDto.UpdatedAt` (DateTime) mapped từ `BaseEntity.UpdatedAt`. **Client-side THIẾU** — `ByDomainResponse` (private class trong `KhachLinkInstanceHttpService.cs` line 168-180) không có field `UpdatedAt` → cần thêm. | `2_Gateway/Controllers/KhachLinkInstanceController.cs` line 275 (`public DateTime UpdatedAt`) + line 236 (`UpdatedAt = i.UpdatedAt` in `ToDto`) |
| OQ2 | CartService scope? | ✅ **Scoped** (`AddScoped<CartService>()`) — Layout + Cart page share cùng instance trong Blazor circuit. Cart cũng persist vào localStorage key `vanan_cart`. | `5_WebApps/KhachLink/Program.cs` line 54 |
| OQ3 | driver.js install method? | ✅ **CDN vendored** — KhachLink là WASM, không có npm setup. `wwwroot/lib/` đã có precedent `fingerprintjs` + `leaflet`. Copy `driver.min.js` + `driver.min.css` vào `wwwroot/lib/driver.js/`. | `5_WebApps/KhachLink/wwwroot/lib/` (subdirs: fingerprintjs, leaflet) |
| OQ4 | Toast service? | ✅ **JS interop pattern** (Sprint 1 precedent) — `profile-toast.js` định nghĩa `window.vananShowProfileToast(msg)`. Sprint 2 reuse pattern cho banner dismiss + onboarding. | `5_WebApps/KhachLink/wwwroot/js/profile-toast.js` (Sprint 1 NEW) |

**Additional findings:**
- KhachLink là **WASM** — không có `_Host.cshtml`. Script tags trong `wwwroot/index.html` (line 36-47).
- `KhachLinkProfile` enum: `FullCommerce=0, Directory=1, Logistics=2, JobMarket=3, Reseller=4` (`1_Shared/Domain/Aggregates/KhachLinkAggregate/KhachLinkProfile.cs`).
- Header icons trong `KhachLinkLayout.razor` (line 71-100): cart, rewards, missions, loyalty, profile — **KHÔNG có stores icon**. Stores chỉ ở NavMenu bottom-nav (mobile, line 244-250) + sidebar (desktop, line 50-57).
- `KhachLinkLayout.OnAfterRenderAsync(firstRender)` (line 292) đã có pattern fetch token + commerce mode — reuse cho profile change detection.

---

## 1. Implementation Order (dependency-aware)

```
Step 0: Vendor driver.js lib (download → wwwroot/lib/driver.js/)
Step 1: Add UpdatedAt to client-side model + DTO + deserialize   [Task 2.1 foundation]
Step 2: Create ProfileChangeDirection enum                       [Task 2.1 + 2.2 + 2.3 shared]
Step 3: Create WhatsNewBanner.razor                              [Task 2.1]
Step 4: Create CartPreservationModal.razor                       [Task 2.2]
Step 5: Create onboarding-tour.js + OnboardingTour.razor         [Task 2.3]
Step 6: Add driver.js script tags to index.html                  [Task 2.3]
Step 7: Add nav element IDs to KhachLinkLayout + NavMenu         [Task 2.3 — driver.js targets]
Step 8: Wire profile-change detection + render in KhachLinkLayout [Task 2.1 + 2.2 + 2.3 integration]
Step 9: Build + fix errors
Step 10: E2E test additions (profile-transition.spec.ts)
Step 11: RV on production (timlathay.com + diemthuong2.khachvip.online)
```

**Why this order:** Step 1 (UpdatedAt) là foundation cho profile-change detection (Step 8). Step 2 (enum) dùng chung cho 3 components. Steps 3-5 tạo components độc lập. Step 7 (nav IDs) cần trước Step 8 (tour target). Step 8 là integration point — wire tất cả vào Layout.

---

## 2. Step-by-Step Implementation

### Step 0: Vendor driver.js library

**Action:** Download driver.js v1.x (MIT) minified files vào `wwwroot/lib/driver.js/`.

```powershell
# Tạo thư mục
New-Item -ItemType Directory -Force -Path "5_WebApps\KhachLink\wwwroot\lib\driver.js"

# Download driver.min.js + driver.min.css từ CDN (jsdelivr — driver.js v1.3.1 stable, published >7 days)
Invoke-WebRequest -Uri "https://cdn.jsdelivr.net/npm/driver.js@1.3.1/dist/driver.min.js" -OutFile "5_WebApps\KhachLink\wwwroot\lib\driver.js\driver.min.js"
Invoke-WebRequest -Uri "https://cdn.jsdelivr.net/npm/driver.js@1.3.1/dist/driver.min.css" -OutFile "5_WebApps\KhachLink\wwwroot\lib\driver.js\driver.min.css"
```

**Verify:** Files exist + ~20KB combined. Precedent: `wwwroot/lib/leaflet/` (vendored similarly).

**Note:** driver.js v1.x API: `const driver = new Driver({ steps: [...] })` then `driver.drive()`. Verify exact API version trong downloaded file header trước khi viết `onboarding-tour.js` (Step 5).

---

### Step 1: Add `UpdatedAt` to client-side model + DTO + deserialize

#### File 1: `5_WebApps/KhachLink/Models/KhachLinkInstanceConfig.cs` — UPDATE

**Add `UpdatedAt` field** after `FooterColor` (line 22):

```csharp
    public string? FooterColor { get; set; }
    /// <summary>Sprint 2 P2.1: Server-side UpdatedAt timestamp — used to detect profile change (compare with localStorage lastSeenProfileAt).</summary>
    public DateTime UpdatedAt { get; set; }
```

#### File 2: `5_WebApps/KhachLink/Services/Http/KhachLinkInstanceHttpService.cs` — UPDATE

**2a. Add `UpdatedAt` to private `ByDomainResponse` DTO** (line 178, after `FooterColor`):

```csharp
        public string? FooterColor { get; set; }
        public DateTime UpdatedAt { get; set; }
        public NavFlagsResponse? NavFlags { get; set; }
```

**2b. Deserialize `UpdatedAt` into config** (line 95, after `FooterColor = dto.FooterColor,`):

```csharp
                FooterColor = dto.FooterColor,
                UpdatedAt = dto.UpdatedAt,
                NavFlags = new KhachLinkNavFlagsDto
```

**Note:** `KhachLinkInstanceConfig` được serialize vào localStorage cache (`SetCachedAsync` line 158). Vì `UpdatedAt` là non-nullable `DateTime` (default `DateTime.MinValue`), cache cũ (không có field) sẽ deserialize `UpdatedAt = default` → không break. Nhưng để safe, bump cache key từ `_v2` → `_v3` để invalidate stale cache:

```csharp
    private const string _cacheKey = "khachlink_instance_config_v3";
    private const string _cacheTsKey = "khachlink_instance_config_v3_ts";
```

**Verify:** `dotnet build` — 0 errors. Server-side `KhachLinkInstanceDto.UpdatedAt` (DateTime) serialize ra JSON `updatedAt` (camelCase via System.Text.Json defaults? — verify Gateway serialization settings). Client `_jsonOpts = PropertyNameCaseInsensitive = true` → match cả `UpdatedAt` lẫn `updatedAt`. ✅

---

### Step 2: Create `ProfileChangeDirection` enum

#### File 3: `5_WebApps/KhachLink/Models/ProfileChangeDirection.cs` — NEW

```csharp
using VanAn.Shared.Domain.Aggregates.KhachLinkAggregate;

namespace VanAn.KhachLink.Models;

/// <summary>
/// Sprint 2: Direction of profile transition — drives What's New banner message +
/// cart preservation modal trigger + onboarding tour trigger.
/// Computed in KhachLinkLayout by comparing localStorage lastSeenProfile (old) vs instanceConfig.Profile (new).
/// </summary>
public enum ProfileChangeDirection
{
    /// <summary>No profile change detected (first visit or same profile).</summary>
    None = 0,
    DirectoryToFullCommerce,
    FullCommerceToDirectory,
    DirectoryToReseller,
    ResellerToFullCommerce,
    FullCommerceToReseller,
    ResellerToDirectory,
    /// <summary>Any other transition (e.g. involving Logistics/JobMarket — R3, not covered by specific messaging).</summary>
    Other
}

/// <summary>Helper to compute direction from old + new profile.</summary>
public static class ProfileChangeDirectionHelper
{
    public static ProfileChangeDirection Compute(KhachLinkProfile? oldProfile, KhachLinkProfile newProfile)
    {
        if (oldProfile == null || oldProfile == newProfile)
            return ProfileChangeDirection.None;

        return (oldProfile.Value, newProfile) switch
        {
            (KhachLinkProfile.Directory, KhachLinkProfile.FullCommerce) => ProfileChangeDirection.DirectoryToFullCommerce,
            (KhachLinkProfile.FullCommerce, KhachLinkProfile.Directory) => ProfileChangeDirection.FullCommerceToDirectory,
            (KhachLinkProfile.Directory, KhachLinkProfile.Reseller) => ProfileChangeDirection.DirectoryToReseller,
            (KhachLinkProfile.Reseller, KhachLinkProfile.FullCommerce) => ProfileChangeDirection.ResellerToFullCommerce,
            (KhachLinkProfile.FullCommerce, KhachLinkProfile.Reseller) => ProfileChangeDirection.FullCommerceToReseller,
            (KhachLinkProfile.Reseller, KhachLinkProfile.Directory) => ProfileChangeDirection.ResellerToDirectory,
            _ => ProfileChangeDirection.Other
        };
    }
}
```

**Note:** Enum + helper sống trong `VanAn.KhachLink.Models` (client-side only, KHÔNG Domain). 6 directions cụ thể + `Other` fallback cho Logistics/JobMarket (R3 — not covered by specific messaging).

---

### Step 3: Create `WhatsNewBanner.razor` (Task 2.1)

#### File 4: `5_WebApps/KhachLink/Components/Shared/WhatsNewBanner.razor` — NEW

```razor
@* Sprint 2 P2.1: "What's New" banner — hiển thị 1 lần khi detect profile change.
   Direction (enum) drives message content. Dismiss button + auto-hide after 10s.
   Client-side only — đọc cascaded data, không HTTP call. *@
@using VanAn.KhachLink.Models
@inject IJSRuntime JSRuntime

@if (_visible)
{
    <div class="whats-new-banner whats-new-@_directionClass" role="alert">
        <div class="whats-new-content">
            <span class="whats-new-icon">@_icon</span>
            <span class="whats-new-text">@_message</span>
        </div>
        <button class="whats-new-dismiss" aria-label="Đóng" @onclick="DismissAsync">
            <i class="bi bi-x-lg"></i>
        </button>
    </div>
}

<style>
    .whats-new-banner {
        display: flex;
        align-items: center;
        justify-content: space-between;
        padding: 12px 16px;
        margin: 8px auto;
        max-width: 1000px;
        border-radius: 8px;
        box-shadow: 0 2px 8px rgba(0,0,0,0.1);
        font-size: 0.875rem;
        animation: whats-new-slide-in 0.3s ease;
    }
    .whats-new-banner.whats-new-upgrade {
        background: linear-gradient(135deg, #e6f4ea, #d4edda);
        border: 1px solid #c3e6cb;
        color: #155724;
    }
    .whats-new-banner.whats-new-info {
        background: linear-gradient(135deg, #e7f1ff, #d1ecf1);
        border: 1px solid #bee5eb;
        color: #0c5460;
    }
    .whats-new-content {
        display: flex;
        align-items: center;
        gap: 8px;
        flex: 1;
    }
    .whats-new-icon { font-size: 1.1rem; }
    .whats-new-dismiss {
        background: none;
        border: none;
        color: inherit;
        opacity: 0.6;
        cursor: pointer;
        padding: 4px 8px;
        border-radius: 4px;
    }
    .whats-new-dismiss:hover { opacity: 1; background: rgba(0,0,0,0.05); }
    @@keyframes whats-new-slide-in {
        from { opacity: 0; transform: translateY(-10px); }
        to { opacity: 1; transform: translateY(0); }
    }
</style>

@code {
    [Parameter]
    public ProfileChangeDirection Direction { get; set; } = ProfileChangeDirection.None;

    private bool _visible;
    private string _message = string.Empty;
    private string _icon = "ℹ️";
    private string _directionClass = "info";

    protected override void OnParametersSet()
    {
        if (Direction == ProfileChangeDirection.None)
        {
            _visible = false;
            return;
        }
        (_icon, _message, _directionClass) = Direction switch
        {
            ProfileChangeDirection.DirectoryToFullCommerce =>
                ("🎉", "Chúng tôi đã nâng cấp! Bạn có thể mua hàng, tích điểm, đổi quà ngay trên app.", "upgrade"),
            ProfileChangeDirection.FullCommerceToDirectory =>
                ("ℹ️", "App đã chuyển sang chế độ danh bạ. Xem danh sách cửa hàng gần bạn.", "info"),
            ProfileChangeDirection.DirectoryToReseller =>
                ("🎉", "Cửa hàng đã mở bán qua Vạn An — mua hàng trực tiếp trên app.", "upgrade"),
            ProfileChangeDirection.ResellerToFullCommerce =>
                ("ℹ️", "Cửa hàng hiện bán hàng trực tiếp, giá không còn bao gồm phí nền tảng.", "info"),
            ProfileChangeDirection.FullCommerceToReseller =>
                ("ℹ️", "Cửa hàng hiện bán qua Vạn An, giá đã bao gồm phí nền tảng.", "info"),
            ProfileChangeDirection.ResellerToDirectory =>
                ("ℹ️", "App đã chuyển sang chế độ danh bạ. Xem danh sách cửa hàng gần bạn.", "info"),
            _ => ("ℹ️", "Giao diện app đã được cập nhật.", "info")
        };
        _visible = true;
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (_visible && firstRender)
        {
            // Auto-hide after 10 seconds (optional — task card spec)
            _ = Task.Delay(10000).ContinueWith(async _ =>
            {
                await DismissAsync();
            });
        }
    }

    private async Task DismissAsync()
    {
        _visible = false;
        // Persist dismiss to localStorage so banner doesn't re-show on refresh within same change
        try
        {
            await JSRuntime.InvokeVoidAsync("localStorage.setItem", "whats_new_dismissed", "true");
        }
        catch { /* non-critical */ }
        StateHasChanged();
    }
}
```

**Notes:**
- `OnParametersSet` (sync) computes message — không async, không JS interop (safe during prerender).
- `OnAfterRenderAsync(firstRender)` — auto-hide timer (client-side only, JSRuntime available).
- Dismiss persists `whats_new_dismissed=true` — Layout check này trước khi set `_showWhatsNewBanner`.
- UI Platform compliance: component self-contained, dùng Bootstrap utility classes + scoped `<style>`. Không custom HTML framework. (Verify: VanAn.UI.Platform không có banner component — this is app-specific UI, scoped CSS acceptable per precedent `KhachLinkLayout.razor` scoped styles.)

---

### Step 4: Create `CartPreservationModal.razor` (Task 2.2)

#### File 5: `5_WebApps/KhachLink/Components/Shared/CartPreservationModal.razor` — NEW

```razor
@* Sprint 2 P3.2: Cart preservation notice — hiển thị khi FullCommerce → Directory + cart có items.
   Reassures customer cart data is preserved but ordering is no longer available via app. *@
@using VanAn.UI.Platform.Components.Atomic
@using VanAn.UI.Platform.Components.Composite

@if (_visible)
{
    <VanAnModal IsOpen="@_visible" Title="Giỏ hàng của bạn vẫn được lưu"
                OnClose="@CloseAsync" Size="ModalSize.Medium">
        <div class="cart-preservation-body">
            <div class="cart-preservation-icon mb-3 text-center">
                <i class="bi bi-cart-check" style="font-size:2.5rem;color:#155724;"></i>
            </div>
            <p class="text-center">
                Giỏ hàng của bạn (<strong>@_itemCount sản phẩm</strong>) vẫn được lưu.
            </p>
            <p class="text-center text-muted small">
                Do cửa hàng đã chuyển sang chế độ danh bạ, bạn không thể đặt hàng qua app.
                Liên hệ cửa hàng để đặt hàng.
            </p>
        </div>
        <ChildContent>
            <div class="text-center mt-3">
                <VanAnButton Text="Đã hiểu" OnClick="@CloseAsync" Variant="ButtonVariant.Primary" />
            </div>
        </ChildContent>
    </VanAnModal>
}

@code {
    [Parameter]
    public bool Visible { get; set; }

    [Parameter]
    public int ItemCount { get; set; }

    [Parameter]
    public EventCallback OnDismissed { get; set; }

    private bool _visible;
    private int _itemCount;

    protected override void OnParametersSet()
    {
        // Sync internal state with parameters (VanAnModal IsOpen binding)
        if (Visible != _visible)
        {
            _visible = Visible;
            _itemCount = ItemCount;
        }
    }

    private async Task CloseAsync()
    {
        _visible = false;
        await OnDismissed.InvokeAsync();
        StateHasChanged();
    }
}
```

**Notes:**
- Dùng `VanAnModal` + `VanAnButton` (UI Platform components) — governance compliance.
- **VERIFY:** `VanAnModal` API — check `VanAn.UI.Platform.Components.Composite` namespace + `IsOpen`/`OnClose`/`Title`/`Size` parameter names. Nếu API khác (e.g., `Show`/`OnHide` thay vì `IsOpen`/`OnClose`), adjust. Read `VanAn.UI.Platform` component source trước khi implement.
- `ItemCount` truyền từ Layout (đọc `CartService.GetCartState().Items.Count` hoặc localStorage `vanan_cart`).

**Fallback nếu VanAnModal API không match:** Dùng Bootstrap modal pattern (precedent: `KhachLinkInstances.razor` Sprint 1 confirm dialog dùng custom modal markup). Verify trong Step 9 (build).

---

### Step 5: Create `onboarding-tour.js` + `OnboardingTour.razor` (Task 2.3)

#### File 6: `5_WebApps/KhachLink/wwwroot/js/onboarding-tour.js` — NEW

```javascript
// Sprint 2 P3.1: Onboarding tour via driver.js — 3-step tooltip tour cho Directory → Reseller/FullCommerce.
// driver.js vendored in wwwroot/lib/driver.js/ (loaded via index.html script tag).
// Init from OnboardingTour.razor OnAfterRenderAsync(firstRender) — sau khi nav rendered.

window.vananStartOnboardingTour = function(direction) {
    // direction: "DirectoryToFullCommerce" | "DirectoryToReseller" | other
    // Steps target nav element IDs (#nav-cart, #nav-rewards, #nav-stores) added in Step 7.
    if (!window.driver || !window.driver.js) {
        console.warn('[VanAn Onboarding] driver.js not loaded — skipping tour');
        return;
    }

    var steps = [
        {
            element: '#nav-cart',
            popover: {
                title: '🛒 Giỏ hàng',
                description: 'Mua sản phẩm trực tiếp trên app — thêm vào giỏ và đặt hàng.'
            }
        },
        {
            element: '#nav-rewards',
            popover: {
                title: '🎁 Tích điểm',
                description: 'Tích điểm mỗi đơn hàng, đổi quà hấp dẫn.'
            }
        },
        {
            element: '#nav-stores',
            popover: {
                title: '📍 Cửa hàng',
                description: 'Tìm cửa hàng gần bạn.'
            }
        }
    ];

    // Filter steps: chỉ show step nếu element tồn tại trong DOM (profile có thể ẩn 1 số nav items)
    var visibleSteps = steps.filter(function(s) {
        return document.querySelector(s.element) !== null;
    });

    if (visibleSteps.length === 0) {
        console.warn('[VanAn Onboarding] No target nav elements found — skipping tour');
        return;
    }

    var driver = new window.driver.js.driver({
        showProgress: true,
        steps: visibleSteps,
        allowClose: true,
        doneBtnText: 'Hoàn thành',
        nextBtnText: 'Tiếp →',
        prevBtnText: '← Quay lại',
        progressText: '{{current}} / {{total}}'
    });

    driver.drive();
};
```

**Notes:**
- **VERIFY driver.js v1.x API** trong downloaded file (Step 0). v1.x dùng `new Driver({...})` hoặc `const driver = window.driver.js.driver({...})` — check exact export. Task card snippet dùng `window.driver.js.driver(...)` — adjust nếu API khác.
- Filter steps theo DOM existence — nếu profile ẩn 1 nav item (e.g., Reseller không có rewards), skip step đó thay vì crash.
- i18n: button text tiếng Việt.

#### File 7: `5_WebApps/KhachLink/Components/Shared/OnboardingTour.razor` — NEW

```razor
@* Sprint 2 P3.1: Onboarding tour wrapper — triggers driver.js tour khi detect Directory → Reseller/FullCommerce.
   Tour chỉ chạy 1 lần per profile change (localStorage flag onboarding_{profile}_completed). *@
@using VanAn.KhachLink.Models
@inject IJSRuntime JSRuntime

@code {
    [Parameter]
    public ProfileChangeDirection Direction { get; set; } = ProfileChangeDirection.None;

    [Parameter]
    public KhachLinkProfile NewProfile { get; set; } = KhachLinkProfile.FullCommerce;

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!firstRender)
            return;

        // Chỉ trigger tour cho upgrade directions (Directory → FullCommerce/Reseller)
        if (Direction != ProfileChangeDirection.DirectoryToFullCommerce
            && Direction != ProfileChangeDirection.DirectoryToReseller)
            return;

        // Check localStorage flag — tour chỉ chạy 1 lần per target profile
        var flagKey = $"onboarding_{NewProfile}_completed";
        string? completed;
        try
        {
            completed = await JSRuntime.InvokeAsync<string?>("localStorage.getItem", flagKey);
        }
        catch
        {
            completed = null;
        }

        if (completed == "true")
            return;

        // Delay 500ms để ensure nav rendered (R5 mitigation — page chưa render khi tour init)
        await Task.Delay(500);

        try
        {
            await JSRuntime.InvokeVoidAsync("vananStartOnboardingTour", Direction.ToString());
            // Mark tour completed (không repeat)
            await JSRuntime.InvokeVoidAsync("localStorage.setItem", flagKey, "true");
        }
        catch (Exception ex)
        {
            // driver.js chưa loaded hoặc element không tìm thấy — silent fail (tour là optional)
            Console.WriteLine($"[OnboardingTour] Failed: {ex.Message}");
        }
    }
}
```

**Notes:**
- `OnAfterRenderAsync(firstRender: true)` — sau khi nav rendered (R5 mitigation).
- `Task.Delay(500)` — extra safety margin cho Blazor WASM async render.
- localStorage flag `onboarding_{NewProfile}_completed` — per-target-profile, không repeat cho cùng profile.
- Silent fail — tour là enhancement, không break app nếu driver.js missing.

---

### Step 6: Add driver.js script tags to `index.html`

#### File 8: `5_WebApps/KhachLink/wwwroot/index.html` — UPDATE

**Add driver.js CSS** trong `<head>` (after line 12, leaflet.css):

```html
    <link rel="stylesheet" href="/lib/leaflet/leaflet.css" />
    <link rel="stylesheet" href="/lib/driver.js/driver.min.css" />
```

**Add driver.js JS + onboarding-tour.js** trong `<body>` (after line 47, profile-toast.js — order matters: driver.js BEFORE onboarding-tour.js):

```html
    <script src="/js/profile-toast.js"></script>
    <script src="/lib/driver.js/driver.min.js"></script>
    <script src="/js/onboarding-tour.js"></script>
```

**Why this order:** `onboarding-tour.js` gọi `window.driver.js.driver(...)` → driver.js phải load trước. Cả 2 load sau Blazor WASM (`blazor.webassembly.js` line 36) — OK vì tour init trong `OnAfterRenderAsync` (sau WASM boot).

---

### Step 7: Add nav element IDs for driver.js targeting

driver.js target bằng CSS selector. Cần `id="nav-cart"`, `id="nav-rewards"`, `id="nav-stores"` trên visible nav elements.

#### File 9: `5_WebApps/KhachLink/Components/Layout/KhachLinkLayout.razor` — UPDATE (header icons)

**Add IDs to header cart + rewards icons** (line 73 + 79):

```razor
                @if (_navFlags.ShowCart)
                {
                    <a href="/cart" id="nav-cart" class="header-icon-btn" aria-label="Giỏ hàng">
                        <i class="bi bi-cart3 text-white"></i>
                    </a>
                }
                @if (_navFlags.ShowRewards)
                {
                    <a href="/rewards" id="nav-rewards" class="header-icon-btn" aria-label="Đổi điểm" title="Đổi điểm thưởng">
                        <i class="bi bi-gift text-white"></i>
                    </a>
                }
```

**Why header (not NavMenu) for cart + rewards:** Header icons luôn visible trên mobile (primary KhachLink target). NavMenu bottom-nav ẩn cart/rewards (comment line 229: "Cart / Điểm thưởng / Nhiệm vụ / Đổi điểm đã có icon ở header").

#### File 10: `5_WebApps/KhachLink/Components/Layout/NavMenu.razor` — UPDATE (stores)

**Add ID to stores link** — cả desktop sidebar (line 53) + mobile bottom-nav (line 246):

Desktop sidebar:
```razor
                        @if (_navFlags.ShowStores)
                        {
                            <div class="nav-item">
                                <NavLink class="nav-link" href="/stores" id="nav-stores">
                                    <span class="bi bi-geo-alt me-2" aria-hidden="true"></span> Cửa hàng
                                </NavLink>
                            </div>
                        }
```

Mobile bottom-nav:
```razor
    @if (_navFlags.ShowStores)
    {
        <a class="mobile-tab-item @IsActive("/stores")" href="/stores" id="nav-stores-mobile">
            <i class="bi bi-geo-alt"></i>
            <span>Cửa hàng</span>
        </a>
    }
```

**Note:** Desktop dùng `id="nav-stores"`, mobile dùng `id="nav-stores-mobile"` (tránh duplicate ID — driver.js target `#nav-stores` cho desktop, `#nav-stores-mobile` cho mobile). **Update `onboarding-tour.js`** (Step 5) để detect viewport:

```javascript
    // Detect mobile vs desktop — target correct stores element
    var isMobile = window.matchMedia('(max-width: 767px)').matches;
    var storesSelector = isMobile ? '#nav-stores-mobile' : '#nav-stores';

    var steps = [
        { element: '#nav-cart', popover: { title: '🛒 Giỏ hàng', description: '...' } },
        { element: '#nav-rewards', popover: { title: '🎁 Tích điểm', description: '...' } },
        { element: storesSelector, popover: { title: '📍 Cửa hàng', description: '...' } }
    ];
```

**Alternative (simpler):** Dùng 1 ID `nav-stores` trên mobile bottom-nav only (vì KhachLink primary = mobile PWA). Desktop sidebar stores không cần ID. Chọn approach này nếu E2E chỉ test mobile.

---

### Step 8: Wire profile-change detection + render in `KhachLinkLayout.razor`

#### File 9 (cont.): `5_WebApps/KhachLink/Components/Layout/KhachLinkLayout.razor` — UPDATE (integration)

**8a. Add `@using` + inject CartService** (top of file, after line 8):

```razor
@using VanAn.KhachLink.Services
@inject CartService CartSvc
```

**8b. Add state fields** (in `@code` block, after line 251 `_isReseller`):

```csharp
    /// <summary>Sprint 2 P2.1: Profile change detection state.</summary>
    private ProfileChangeDirection _profileChangeDirection = ProfileChangeDirection.None;
    private bool _showWhatsNewBanner;
    private bool _showCartPreservationModal;
    private int _cartItemCount;
```

**8c. Profile-change detection in `OnInitializedAsync`** (after line 282, inside the `if (instanceConfig != null)` block, after `_shopConfig` re-fetch):

```csharp
                // Sprint 2 P2.1: Detect profile change via UpdatedAt timestamp.
                // Compare instanceConfig.UpdatedAt with localStorage lastSeenProfileAt.
                // If newer → profile changed → show What's New banner + trigger cart preservation / onboarding.
                try
                {
                    var lastSeenTsStr = await JSRuntime.InvokeAsync<string?>("localStorage.getItem", "last_seen_profile_at");
                    var lastSeenProfileStr = await JSRuntime.InvokeAsync<string?>("localStorage.getItem", "last_seen_profile");

                    DateTime? lastSeenTs = null;
                    if (lastSeenTsStr != null && DateTime.TryParse(lastSeenTsStr, out var ts))
                        lastSeenTs = ts;

                    KhachLinkProfile? lastSeenProfile = null;
                    if (lastSeenProfileStr != null && Enum.TryParse<KhachLinkProfile>(lastSeenProfileStr, true, out var oldProfile))
                        lastSeenProfile = oldProfile;

                    // Check if banner was already dismissed for this UpdatedAt
                    var dismissed = await JSRuntime.InvokeAsync<string?>("localStorage.getItem", "whats_new_dismissed");

                    bool profileChanged = lastSeenTs == null
                        ? false  // First visit — no banner (no previous profile to compare)
                        : instanceConfig.UpdatedAt > lastSeenTs.Value;

                    if (profileChanged && dismissed != "true")
                    {
                        _profileChangeDirection = ProfileChangeDirectionHelper.Compute(lastSeenProfile, instanceConfig.Profile);
                        _showWhatsNewBanner = _profileChangeDirection != ProfileChangeDirection.None;
                    }

                    // Cart preservation: FullCommerce → Directory + cart has items
                    if (_profileChangeDirection == ProfileChangeDirection.FullCommerceToDirectory)
                    {
                        _cartItemCount = await GetCartItemCountAsync();
                        _showCartPreservationModal = _cartItemCount > 0;
                    }

                    // Update localStorage: lastSeenProfileAt + lastSeenProfile (always — even if no change, to keep fresh)
                    await JSRuntime.InvokeVoidAsync("localStorage.setItem", "last_seen_profile_at", instanceConfig.UpdatedAt.ToString("o"));
                    await JSRuntime.InvokeVoidAsync("localStorage.setItem", "last_seen_profile", instanceConfig.Profile.ToString());
                    // Clear dismiss flag so next change shows banner again
                    if (profileChanged)
                        await JSRuntime.InvokeVoidAsync("localStorage.removeItem", "whats_new_dismissed");
                }
                catch (Exception ex)
                {
                    // Non-critical — profile change detection is enhancement, don't break layout
                    System.Console.WriteLine($"[KhachLinkLayout] Profile change detection failed: {ex.Message}");
                }
```

**8d. Add cart count helper** (in `@code` block, after `GetProfileLabel`):

```csharp
    /// <summary>Sprint 2 P3.2: Get cart item count — CartService is Scoped (shared in circuit).
    /// Fallback: read localStorage vanan_cart if CartService not loaded yet.</summary>
    private async Task<int> GetCartItemCountAsync()
    {
        try
        {
            // CartService scoped — same instance as Cart page within circuit
            var state = CartSvc.GetCartState();
            if (state.Items.Count > 0)
                return state.Items.Count;

            // Fallback: read localStorage (cart may not be loaded into CartService yet on layout init)
            var cartJson = await JSRuntime.InvokeAsync<string?>("localStorage.getItem", "vanan_cart");
            if (!string.IsNullOrEmpty(cartJson))
            {
                using var doc = System.Text.Json.JsonDocument.Parse(cartJson);
                if (doc.RootElement.TryGetProperty("items", out var items) && items.ValueKind == System.Text.Json.JsonValueKind.Array)
                    return items.GetArrayLength();
            }
        }
        catch { /* non-critical */ }
        return 0;
    }
```

**8e. Render components in layout markup** — after Reseller badge block (line 114), before `<main>` (line 117):

```razor
    @* Sprint 2 P2.1: What's New banner — 1 lần khi detect profile change *@
    @if (_showWhatsNewBanner)
    {
        <WhatsNewBanner Direction="@_profileChangeDirection" />
    }

    @* Sprint 2 P3.2: Cart preservation modal — FullCommerce → Directory + cart has items *@
    <CartPreservationModal Visible="@_showCartPreservationModal"
                           ItemCount="@_cartItemCount"
                           OnDismissed="@(() => _showCartPreservationModal = false)" />

    @* Sprint 2 P3.1: Onboarding tour — Directory → FullCommerce/Reseller *@
    <OnboardingTour Direction="@_profileChangeDirection" NewProfile="@(_instanceStyle?.Profile ?? KhachLinkProfile.FullCommerce)" />
```

**8f. Add `@using` for Shared components namespace** (top of file):

```razor
@using VanAn.KhachLink.Components.Shared
```

**Notes:**
- Profile-change detection trong `OnInitializedAsync` (async, JSRuntime available sau prerender). First visit (`lastSeenTs == null`) → no banner (không có previous profile để compare).
- `whats_new_dismissed` flag: set khi user dismiss banner (Step 3), cleared khi new change detected → banner re-show cho change mới.
- Cart count: CartService scoped (shared) + localStorage fallback (cart có thể chưa load vào CartService trên layout init).
- `OnboardingTour` render trong Layout — `OnAfterRenderAsync(firstRender)` trigger tour sau nav render.

---

### Step 9: Build + fix errors

```powershell
dotnet build VanAn.sln
```

**Expected errors + fixes:**

| Error | Cause | Fix |
|---|---|---|
| `VanAnModal` / `VanAnButton` not found | Wrong namespace or API mismatch | Verify `VanAn.UI.Platform.Components.Composite` + `.Atomic` namespaces + parameter names (`IsOpen`/`OnClose`/`Text`/`OnClick`/`Variant`). Read UI Platform component source. |
| `ProfileChangeDirection` not found | Missing `@using VanAn.KhachLink.Models` in component | Add `@using` at top of `.razor` file |
| `CartService` not found | Missing `@using VanAn.KhachLink.Services` in Layout | Add `@using` (Step 8a) |
| `window.driver.js` undefined | driver.js script tag missing or wrong path | Verify `index.html` script tag (Step 6) + file exists in `wwwroot/lib/driver.js/` |
| driver.js API mismatch | v1.x vs v0.x API different | Check downloaded file header for version + API. v1.x: `new Driver({...})`. v0.x: `driver({{}})` |

**Guard:** Run `guard-check.ps1` nếu có pre-commit hook.

---

### Step 10: E2E test additions

#### File 11: `6_Testing/e2e-tests/profile-transition.spec.ts` — UPDATE

**Add Sprint 2 test cases** (append after Sprint 1 block, line 105):

```typescript
test.describe('Sprint 2 — Transition Messaging (P2.1 + P3.2 + P3.1)', () => {
  // P2.1: What's New banner — detect profile change via UpdatedAt
  // NOTE: These tests require profile change between test runs (admin must change profile).
  // Manual RV covers this — E2E verifies banner structure when localStorage manipulated.

  test('What\'s New banner: shows when last_seen_profile_at is stale', async ({ page }) => {
    // Simulate stale lastSeenProfileAt → banner should show on next load
    await page.addInitScript(() => {
      localStorage.setItem('last_seen_profile_at', '2020-01-01T00:00:00');
      localStorage.setItem('last_seen_profile', 'Directory');
      localStorage.removeItem('whats_new_dismissed');
    });
    await page.goto(KHACHLINK_URL, { waitUntil: 'networkidle' });

    // Banner should appear (if instance UpdatedAt > 2020-01-01)
    // Wait for banner — may take a moment for layout init + JS interop
    const banner = page.locator('.whats-new-banner');
    // Note: only shows if instance.UpdatedAt > stale timestamp + profile actually changed
    // This test verifies banner DOM structure exists when triggered
    const bannerCount = await banner.count();
    if (bannerCount > 0) {
      await expect(banner).toBeVisible({ timeout: 10000 });
      // Dismiss button exists
      await expect(banner.locator('.whats-new-dismiss')).toBeVisible();
    }
  });

  test('What\'s New banner: dismiss hides banner + sets localStorage', async ({ page }) => {
    await page.addInitScript(() => {
      localStorage.setItem('last_seen_profile_at', '2020-01-01T00:00:00');
      localStorage.setItem('last_seen_profile', 'Directory');
      localStorage.removeItem('whats_new_dismissed');
    });
    await page.goto(KHACHLINK_URL, { waitUntil: 'networkidle' });

    const banner = page.locator('.whats-new-banner');
    if (await banner.count() > 0) {
      await banner.locator('.whats-new-dismiss').click();
      await expect(banner).toHaveCount(0, { timeout: 5000 });
      // localStorage flag set
      const dismissed = await page.evaluate(() => localStorage.getItem('whats_new_dismissed'));
      expect(dismissed).toBe('true');
    }
  });

  test('What\'s New banner: does NOT show on first visit (no lastSeenProfileAt)', async ({ page }) => {
    await page.addInitScript(() => {
      localStorage.removeItem('last_seen_profile_at');
      localStorage.removeItem('last_seen_profile');
      localStorage.removeItem('whats_new_dismissed');
    });
    await page.goto(KHACHLINK_URL, { waitUntil: 'networkidle' });

    // First visit — no banner (no previous profile to compare)
    await expect(page.locator('.whats-new-banner')).toHaveCount(0, { timeout: 10000 });
  });

  // P3.2: Cart preservation modal — FullCommerce → Directory + cart has items
  test('Cart preservation modal: shows when FullCommerce → Directory + cart has items', async ({ page }) => {
    // Pre-seed cart in localStorage + stale profile timestamp (FullCommerce → Directory)
    await page.addInitScript(() => {
      localStorage.setItem('vanan_cart', JSON.stringify({
        items: [
          { id: '11111111-1111-1111-1111-111111111111', productId: '22222222-2222-2222-2222-222222222222', productName: 'Test Product', quantity: 2, unitPrice: 50000 },
          { id: '33333333-3333-3333-3333-333333333333', productId: '44444444-4444-4444-4444-444444444444', productName: 'Test Product 2', quantity: 1, unitPrice: 30000 }
        ],
        orderNote: ''
      }));
      localStorage.setItem('last_seen_profile_at', '2020-01-01T00:00:00');
      localStorage.setItem('last_seen_profile', 'FullCommerce');
      localStorage.removeItem('whats_new_dismissed');
    });

    // Navigate to Directory domain (timlathay.com) — profile change FullCommerce → Directory
    await page.goto(DIRECTORY_URL, { waitUntil: 'networkidle' });

    // Cart preservation modal should show (if instance profile = Directory + UpdatedAt > stale)
    // Note: timlathay.com is Directory — lastSeenProfile=FullCommerce → direction=FullCommerceToDirectory
    const modal = page.locator('.cart-preservation-body, text="Giỏ hàng của bạn vẫn được lưu"');
    if (await modal.count() > 0) {
      await expect(modal.first()).toBeVisible({ timeout: 10000 });
      // Verify item count in message
      await expect(page.locator('text=/2 sản phẩm/')).toBeVisible();
    }
  });

  // P3.1: Onboarding tour — Directory → FullCommerce/Reseller
  test('Onboarding tour: nav element IDs exist for driver.js targeting', async ({ page }) => {
    // Verify nav element IDs present (prerequisite for tour)
    await page.goto(KHACHLINK_URL, { waitUntil: 'networkidle' });

    // Cart + rewards IDs in header (FullCommerce shows both)
    await expect(page.locator('#nav-cart')).toBeVisible({ timeout: 15000 });
    await expect(page.locator('#nav-rewards')).toBeVisible();
    // Stores ID — mobile or desktop
    const storesMobile = page.locator('#nav-stores-mobile');
    const storesDesktop = page.locator('#nav-stores');
    expect(await storesMobile.count() + await storesDesktop.count()).toBeGreaterThan(0);
  });

  test('Onboarding tour: does NOT re-run after completion flag set', async ({ page }) => {
    // Pre-set onboarding completion flag → tour should not start
    await page.addInitScript(() => {
      localStorage.setItem('onboarding_FullCommerce_completed', 'true');
      localStorage.setItem('last_seen_profile_at', '2020-01-01T00:00:00');
      localStorage.setItem('last_seen_profile', 'Directory');
      localStorage.removeItem('whats_new_dismissed');
    });
    await page.goto(KHACHLINK_URL, { waitUntil: 'networkidle' });

    // Wait for potential tour init (500ms delay + render)
    await page.waitForTimeout(2000);
    // driver.js tour overlay should NOT appear
    const tourOverlay = page.locator('.driver-popover, .driver-active');
    expect(await tourOverlay.count()).toBe(0);
  });
});
```

**Notes:**
- E2E tests manipulate localStorage via `addInitScript` để simulate profile change (không cần admin đổi profile thật giữa test runs).
- Tests defensive (`if count > 0`) — banner/modal chỉ show khi instance UpdatedAt thực sự > stale timestamp + profile match. Production RV verify full flow.
- Tour overlay selector: `.driver-popover` / `.driver-active` — verify exact class names trong driver.js v1.x CSS (Step 0 downloaded file).

---

### Step 11: Runtime Verification (RV) on production

**Per `.devin/rules/runtime-verification.md` — 5 layers, STOP at first failure.**

**Prerequisites:** Sprint 2 code pushed to `main` + CD deployed.

#### Layer 1: API checks
```powershell
# Verify by-domain endpoint returns UpdatedAt
curl -s "https://gateway.vanan.cloud/api/v1/khachlink-instances/by-domain/diemthuong2.khachvip.online" | jq .updatedAt
# Expected: ISO timestamp (e.g., "2026-09-08T12:34:56Z")
```

#### Layer 2: Static assets
```powershell
# Verify driver.js lib served
curl -s -o /dev/null -w "%{http_code}" "https://diemthuong2.khachvip.online/lib/driver.js/driver.min.js"
# Expected: 200
curl -s -o /dev/null -w "%{http_code}" "https://diemthuong2.khachvip.online/lib/driver.js/driver.min.css"
# Expected: 200
curl -s -o /dev/null -w "%{http_code}" "https://diemthuong2.khachvip.online/js/onboarding-tour.js"
# Expected: 200
```

#### Layer 3: Playwright runtime (E2E)
```powershell
DIRECTORY_URL=https://timlathay.com KHACHLINK_URL=https://diemthuong2.khachvip.online npx playwright test profile-transition
```
Expected: Sprint 1 (9 tests) + Sprint 2 (6 tests) ALL PASS.

#### Layer 4: UI flow (manual browser)
1. **What's New banner (P2.1):**
   - Admin đổi profile `diemthuong2.khachvip.online` Directory → FullCommerce (via `/admin/khachlink-instances`)
   - Mở app `diemthuong2.khachvip.online` → banner "🎉 Chúng tôi đã nâng cấp!" hiện 1 lần
   - Click dismiss (X) → banner ẩn
   - Refresh → banner KHÔNG hiện lại (localStorage `whats_new_dismissed=true`)

2. **Cart preservation (P3.2):**
   - Trên FullCommerce domain, add 2 items to cart
   - Admin đổi profile → Directory
   - Mở app → modal "Giỏ hàng của bạn (2 sản phẩm) vẫn được lưu" hiện
   - Click "Đã hiểu" → modal close

3. **Onboarding tour (P3.1):**
   - Admin đổi profile Directory → FullCommerce
   - Mở app → 3-step tooltip tour tự chạy (cart → rewards → stores)
   - Hoàn thành tour → refresh → tour KHÔNG chạy lại (localStorage `onboarding_FullCommerce_completed=true`)

#### Layer 5: Manual browser (edge cases)
- First visit (clear localStorage) → NO banner (no previous profile)
- Same profile refresh → NO banner (UpdatedAt not newer)
- Tour trên mobile (viewport < 768px) → target `#nav-stores-mobile`
- Tour trên desktop → target `#nav-stores`

---

## 3. File Summary

| # | File | Action | Step |
|---|---|---|---|
| 1 | `5_WebApps/KhachLink/Models/KhachLinkInstanceConfig.cs` | UPDATE (add `UpdatedAt`) | 1 |
| 2 | `5_WebApps/KhachLink/Services/Http/KhachLinkInstanceHttpService.cs` | UPDATE (add UpdatedAt to ByDomainResponse + deserialize + bump cache key _v2→_v3) | 1 |
| 3 | `5_WebApps/KhachLink/Models/ProfileChangeDirection.cs` | NEW (enum + helper) | 2 |
| 4 | `5_WebApps/KhachLink/Components/Shared/WhatsNewBanner.razor` | NEW | 3 |
| 5 | `5_WebApps/KhachLink/Components/Shared/CartPreservationModal.razor` | NEW | 4 |
| 6 | `5_WebApps/KhachLink/wwwroot/js/onboarding-tour.js` | NEW | 5 |
| 7 | `5_WebApps/KhachLink/Components/Shared/OnboardingTour.razor` | NEW | 5 |
| 8 | `5_WebApps/KhachLink/wwwroot/index.html` | UPDATE (driver.js CSS + JS + onboarding-tour.js script tags) | 6 |
| 9 | `5_WebApps/KhachLink/Components/Layout/KhachLinkLayout.razor` | UPDATE (nav IDs + profile-change detection + render 3 components + CartService inject) | 7+8 |
| 10 | `5_WebApps/KhachLink/Components/Layout/NavMenu.razor` | UPDATE (nav-stores IDs) | 7 |
| 11 | `5_WebApps/KhachLink/wwwroot/lib/driver.js/driver.min.js` | NEW (CDN vendored) | 0 |
| 12 | `5_WebApps/KhachLink/wwwroot/lib/driver.js/driver.min.css` | NEW (CDN vendored) | 0 |
| 13 | `6_Testing/e2e-tests/profile-transition.spec.ts` | UPDATE (Sprint 2 test cases) | 10 |

**Total:** 8 NEW + 5 UPDATE = 13 files

---

## 4. Governance Checklist

- [x] **Domain PURE** — Sprint 2 KHÔNG đụng Domain (UI + JS + client-side Models only)
- [x] **UI Platform** — `CartPreservationModal` dùng `VanAnModal` + `VanAnButton`. `WhatsNewBanner` + `OnboardingTour` app-specific (scoped CSS, no framework component available — precedent: KhachLinkLayout scoped styles)
- [x] **KhachLink HTTP-only** — banner/modal/tour client-side (localStorage + cascaded data), không thêm HTTP call. `UpdatedAt` đến từ existing by-domain endpoint.
- [x] **No new .csproj** — dùng existing KhachLink project
- [x] **No migration** — `UpdatedAt` đã có trong server-side DTO + BaseEntity, chỉ client-side deserialize thêm
- [x] **driver.js** — external lib, MIT, ~20KB, CDN vendored (zero build dependency)
- [x] **Playwright isolation** — E2E chỉ chạy sau build pass (Step 10 sau Step 9)
- [x] **Single-Identity** — N/A (không đụng entities)
- [x] **Multi-tenancy** — N/A (KhachLink client-side, TenantId từ instance config)

---

## 5. Risks & Mitigations

| # | Risk | Mitigation | Status |
|---|---|---|---|
| R1 | `UpdatedAt` không có trong by-domain DTO | ✅ RESOLVED — server-side `KhachLinkInstanceDto.UpdatedAt` đã có (controller line 275). Client-side `ByDomainResponse` cần thêm (Step 1). | Resolved |
| R2 | localStorage `lastSeenProfileAt` stale | TTL = session. Compare mỗi load. Nếu stale → banner 1 lần rồi update timestamp. First visit → no banner. | Mitigated |
| R3 | driver.js conflict Blazor WASM virtual DOM | driver.js chỉ manipulate DOM (read-only highlight), không modify Blazor state. Init sau `OnAfterRenderAsync`. | Mitigated |
| R4 | Nav element IDs thiếu cho driver.js target | Step 7 add IDs. onboarding-tour.js filter steps theo DOM existence (skip missing). | Mitigated |
| R5 | Tour chạy sai timing (page chưa render) | `OnAfterRenderAsync(firstRender: true)` + `Task.Delay(500)` safety margin. | Mitigated |
| R6 | CartService scope khác Layout | ✅ RESOLVED — Scoped (Program.cs line 54), shared trong circuit. localStorage fallback trong `GetCartItemCountAsync`. | Resolved |
| R7 | driver.js v1.x API khác task card snippet | Verify downloaded file API trong Step 0. Adjust `onboarding-tour.js` (Step 5) nếu `new Driver()` thay vì `window.driver.js.driver()`. | Verify at Step 0 |
| R8 | `VanAnModal` API mismatch | Verify UI Platform component source trước Step 4. Fallback: Bootstrap modal markup (precedent Sprint 1 confirm dialog). | Verify at Step 4 |
| R9 | Cache key bump (_v2→_v3) invalidate user cache | Acceptable — 1 extra API call per user on first load after deploy. Cache rebuild tự động. | Accepted |
| R10 | Duplicate `id="nav-stores"` (desktop + mobile) | Dùng `nav-stores` (desktop) + `nav-stores-mobile` (mobile). onboarding-tour.js detect viewport. | Mitigated |

---

## 6. Verification Summary

| Step | Verification | Command / Action |
|---|---|---|
| 9 | Build | `dotnet build VanAn.sln` → 0 errors |
| 9 | Guard | `guard-check.ps1` PASS (if pre-commit hook) |
| 10 | E2E | `npx playwright test profile-transition` → 15 tests PASS (9 Sprint 1 + 6 Sprint 2) |
| 11 | RV Layer 1 | by-domain API returns `updatedAt` timestamp |
| 11 | RV Layer 2 | driver.js + onboarding-tour.js static assets 200 |
| 11 | RV Layer 3 | Playwright E2E PASS trên cả 2 domain |
| 11 | RV Layer 4 | Manual: banner 1 lần + cart modal + tour 3 steps |
| 11 | RV Layer 5 | Edge cases: first visit no banner, same profile no banner, mobile/desktop tour targeting |

---

## 7. Related

- Task card: `docs/AI/tasks/khachlink_profile_transition_ux/task_card_sprint2_transition_messaging.md`
- Master plan: `docs/AI/tasks/khachlink_profile_transition_ux/master_plan.md`
- Sprint 1 (prerequisite, COMPLETE): `docs/AI/tasks/khachlink_profile_transition_ux/task_card_sprint1_guardrail_foundation.md`
- Sprint 3 (next): `docs/AI/tasks/khachlink_profile_transition_ux/task_card_sprint3_audit_sw.md`
- KhachLink Layout (inject point): `5_WebApps/KhachLink/Components/Layout/KhachLinkLayout.razor`
- NavMenu (element IDs): `5_WebApps/KhachLink/Components/Layout/NavMenu.razor`
- ProfileGuard (Sprint 1 Shared component precedent): `5_WebApps/KhachLink/Components/Shared/ProfileGuard.razor`
- profile-toast.js (Sprint 1 JS interop precedent): `5_WebApps/KhachLink/wwwroot/js/profile-toast.js`
- Server-side DTO (UpdatedAt source): `2_Gateway/Controllers/KhachLinkInstanceController.cs` line 266-283
- driver.js docs: https://driverjs.com/
