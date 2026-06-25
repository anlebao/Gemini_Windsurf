# W17-T9 — KhachLink End-User Layout (Đẹp + Tùy biến theo ShopConfig)

**Wave:** 17 — KhachLink Retention & Loyalty
**Branch:** `feature/wave17-khachlink-retention`
**Priority:** 🟡 HIGH — ảnh hưởng toàn bộ UX end-user, là "mặt tiền" của app
**Conflict risk:** MEDIUM — sửa `KhachLinkLayout.razor` ảnh hưởng tất cả pages
**Depends on:** W17-T1 (cần biết login state cho header), W17-T6 (NavMenu mới)
**Estimated effort:** 1 session

---

## Vấn đề hiện tại

`KhachLinkLayout.razor` hiện có:
- Background = `var(--color-neutral-50)` — xám nhạt mặc định
- Header = sticky nav với logo chữ "KhachLink" hardcode
- Không dùng `ShopConfig.PrimaryColor`, `SecondaryColor`, `LogoUrl`
- Không có hero/banner section
- `ThemeType` enum đã có `Classic, Modern, Teen, Lady, Premium` — **chưa được áp dụng vào CSS**
- `IThemeProvider.CurrentTheme` inject có sẵn — chưa làm gì ngoài gắn class name
- Responsive breakpoint duy nhất: `768px`, logic chỉ wrap flex direction

---

## Hiện trạng ShopConfig — dùng được ngay

```csharp
public record ShopConfig
{
    public string PrimaryColor   { get; init; } = "#8B4513";   // nâu cà phê
    public string SecondaryColor { get; init; } = "#D2691E";   // chocolate
    public Uri    LogoUrl        { get; init; } = ...;         // logo shop
    public ThemeType ActiveTheme { get; set; } = ThemeType.Classic;
    // Classic | Modern | Teen | Lady | Premium
}
```

**Không sửa ShopConfig** — dùng những fields đã có.

---

## Thiết kế target

### Cấu trúc layout mới

```
┌─────────────────────────────────────────┐
│  HERO HEADER                            │  ← dynamic bg color từ PrimaryColor
│  [Logo]  Tên shop                       │     hoặc hero image nếu có
│  [🛒 0]  [👤 Đăng nhập]                 │
├─────────────────────────────────────────┤
│                                         │
│  CONTENT (@ChildContent)                │
│                                         │
├─────────────────────────────────────────┤
│  BOTTOM NAV (mobile)                    │  ← từ W17-T6
│  🏠  🛒  📋  💎  📍  👤               │
├─────────────────────────────────────────┤
│  FOOTER                                 │
│  © Tên shop · SĐT · Social links       │
└─────────────────────────────────────────┘
```

### Theme Variations (5 themes từ ThemeType enum)

| Theme | Header BG | Font style | Accent | Vibe |
|-------|-----------|-----------|--------|------|
| `Classic` | `PrimaryColor` solid gradient-down | Serif hoặc rounded sans | Nâu ấm | Cà phê, trà truyền thống |
| `Modern` | White + bottom border màu `PrimaryColor` | Clean sans-serif | Tối giản | Minimalist, specialty coffee |
| `Teen` | Gradient pastel từ `PrimaryColor` → `SecondaryColor` | Rounded, bubbly | Bright, playful | Trà sữa, giới trẻ |
| `Lady` | Soft gradient + subtle floral pattern overlay | Elegant serif | Rose/cream | Milk tea, dessert |
| `Premium` | Dark near-black + `PrimaryColor` accent | Luxury serif | Gold/copper | Fine dining, premium brand |

---

## Files cần sửa/tạo

### SỬA: `5_WebApps/KhachLink/Components/Layout/KhachLinkLayout.razor`

**Mục tiêu:** Layout inject `ShopConfig` → render CSS variables theo `PrimaryColor`, `SecondaryColor`, `ActiveTheme`. Hero header hiện logo thật + tên shop thật.

```razor
@using VanAn.UI.Platform.Components.Atomic
@using VanAn.UI.Platform.Components.Composite
@using VanAn.UI.Platform.Tokens
@inject IThemeProvider ThemeProvider
@inject ITenantService TenantService
@inject IShopConfigService ShopConfigService
@inject IJSRuntime JSRuntime
@inherits VanAn.UI.Platform.Components.Base.BaseComponent

@* CSS variables injection — set tại root để toàn bộ app inherit *@
<HeadContent>
    <style>
        :root {
            --shop-primary:   @_primaryColor;
            --shop-secondary: @_secondaryColor;
            --shop-primary-light: @_primaryColorLight;
            --shop-primary-dark:  @_primaryColorDark;
        }
    </style>
</HeadContent>

<div class="khachlink-layout theme-@_themeName tenant-@_tenantId">

    <!-- HERO HEADER -->
    <header class="kl-header">
        <div class="kl-header-inner">
            <!-- Logo + Shop name -->
            <a href="/" class="kl-brand">
                @if (_logoUrl != null)
                {
                    <img src="@_logoUrl" alt="@_shopName" class="kl-logo" />
                }
                <span class="kl-shop-name">@_shopName</span>
            </a>

            <!-- Header actions -->
            <div class="kl-header-actions">
                <a href="/cart" class="kl-icon-btn" title="Giỏ hàng">
                    <i class="fas fa-shopping-cart"></i>
                    @if (_cartCount > 0)
                    {
                        <span class="kl-cart-badge">@_cartCount</span>
                    }
                </a>
                <a href="@(_isLoggedIn ? "/profile" : "/login")" class="kl-icon-btn" title="Tài khoản">
                    <i class="fas fa-user-circle"></i>
                </a>
            </div>
        </div>
    </header>

    <!-- MAIN CONTENT -->
    <main class="kl-main">
        @ChildContent
    </main>

    <!-- FOOTER -->
    <footer class="kl-footer">
        <div class="kl-footer-inner">
            <div class="kl-footer-brand">
                @if (_logoUrl != null)
                {
                    <img src="@_logoUrl" alt="@_shopName" class="kl-footer-logo" />
                }
                <span>@_shopName</span>
            </div>
            @if (!string.IsNullOrEmpty(_shopPhone))
            {
                <a href="tel:@_shopPhone" class="kl-footer-link">
                    <i class="fas fa-phone me-1"></i>@_shopPhone
                </a>
            }
            <div class="kl-footer-social">
                @if (!string.IsNullOrEmpty(_fbLink))
                {
                    <a href="@_fbLink" target="_blank" class="kl-social-btn">
                        <i class="fab fa-facebook"></i>
                    </a>
                }
                @if (!string.IsNullOrEmpty(_tiktokLink))
                {
                    <a href="@_tiktokLink" target="_blank" class="kl-social-btn">
                        <i class="fab fa-tiktok"></i>
                    </a>
                }
            </div>
            <small class="kl-footer-copy">© @DateTime.Now.Year @_shopName</small>
        </div>
    </footer>
</div>

<style>
/* =====================================================
   KhachLink Layout — Dynamic Theme System
   CSS variables: --shop-primary, --shop-secondary
   Theme classes: theme-classic | modern | teen | lady | premium
   ===================================================== */

.khachlink-layout {
    min-height: 100vh;
    display: flex;
    flex-direction: column;
    background: var(--color-neutral-50);
    font-family: var(--font-body, 'Nunito', 'Helvetica Neue', sans-serif);
}

/* ─── HEADER ─── */
.kl-header {
    position: sticky;
    top: 0;
    z-index: 100;
    backdrop-filter: blur(12px);
    -webkit-backdrop-filter: blur(12px);
}

.kl-header-inner {
    display: flex;
    align-items: center;
    justify-content: space-between;
    padding: 0.75rem 1.25rem;
    max-width: 680px;
    margin: 0 auto;
    width: 100%;
}

.kl-brand {
    display: flex;
    align-items: center;
    gap: 0.6rem;
    text-decoration: none;
}

.kl-logo {
    height: 36px;
    width: auto;
    border-radius: 8px;
    object-fit: contain;
}

.kl-shop-name {
    font-size: 1.1rem;
    font-weight: 700;
    letter-spacing: -0.02em;
}

.kl-header-actions {
    display: flex;
    gap: 0.5rem;
    align-items: center;
}

.kl-icon-btn {
    position: relative;
    display: flex;
    align-items: center;
    justify-content: center;
    width: 40px;
    height: 40px;
    border-radius: 50%;
    font-size: 1.1rem;
    text-decoration: none;
    transition: background 0.15s, transform 0.1s;
}

.kl-icon-btn:active { transform: scale(0.93); }

.kl-cart-badge {
    position: absolute;
    top: 2px; right: 2px;
    min-width: 16px;
    height: 16px;
    border-radius: 8px;
    font-size: 0.65rem;
    font-weight: 700;
    display: flex;
    align-items: center;
    justify-content: center;
    padding: 0 3px;
}

/* ─── MAIN ─── */
.kl-main {
    flex: 1;
    max-width: 680px;
    margin: 0 auto;
    width: 100%;
    padding: 1rem 1rem 5rem; /* bottom: space cho mobile nav */
}

/* ─── FOOTER ─── */
.kl-footer {
    background: rgba(0,0,0,0.04);
    border-top: 1px solid rgba(0,0,0,0.06);
    padding: 1.25rem 1.25rem 6rem; /* extra bottom padding cho mobile nav */
}

.kl-footer-inner {
    max-width: 680px;
    margin: 0 auto;
    display: flex;
    flex-direction: column;
    align-items: center;
    gap: 0.5rem;
    text-align: center;
}

.kl-footer-brand {
    display: flex;
    align-items: center;
    gap: 0.5rem;
    font-weight: 600;
    font-size: 0.9rem;
}

.kl-footer-logo { height: 24px; width: auto; border-radius: 4px; }

.kl-footer-link {
    font-size: 0.85rem;
    text-decoration: none;
    opacity: 0.7;
}

.kl-social-btn {
    font-size: 1.2rem;
    text-decoration: none;
    opacity: 0.7;
    transition: opacity 0.15s;
}
.kl-social-btn:hover { opacity: 1; }

.kl-footer-social { display: flex; gap: 0.75rem; }

.kl-footer-copy { font-size: 0.75rem; opacity: 0.5; }

/* =====================================================
   THEME: CLASSIC — Cà phê truyền thống, ấm áp
   ===================================================== */
.theme-classic .kl-header {
    background: linear-gradient(135deg,
        var(--shop-primary) 0%,
        var(--shop-primary-dark) 100%);
    box-shadow: 0 2px 12px rgba(0,0,0,0.15);
}
.theme-classic .kl-shop-name { color: #fff; }
.theme-classic .kl-icon-btn  { color: rgba(255,255,255,0.9); background: rgba(255,255,255,0.15); }
.theme-classic .kl-icon-btn:hover { background: rgba(255,255,255,0.25); }
.theme-classic .kl-cart-badge { background: #fff; color: var(--shop-primary); }
.theme-classic .khachlink-layout { background: #faf7f4; }

/* =====================================================
   THEME: MODERN — Minimalist, tối giản
   ===================================================== */
.theme-modern .kl-header {
    background: rgba(255,255,255,0.92);
    border-bottom: 2px solid var(--shop-primary);
}
.theme-modern .kl-shop-name { color: var(--shop-primary); }
.theme-modern .kl-icon-btn  { color: var(--shop-primary); background: transparent; }
.theme-modern .kl-icon-btn:hover { background: rgba(0,0,0,0.05); }
.theme-modern .kl-cart-badge { background: var(--shop-primary); color: #fff; }
.theme-modern .khachlink-layout { background: #f9f9f9; }

/* =====================================================
   THEME: TEEN — Trà sữa, gradient pastel
   ===================================================== */
.theme-teen .kl-header {
    background: linear-gradient(135deg,
        var(--shop-primary) 0%,
        var(--shop-secondary) 100%);
    box-shadow: 0 4px 20px rgba(0,0,0,0.1);
}
.theme-teen .kl-shop-name { color: #fff; font-family: 'Nunito', sans-serif; font-weight: 800; }
.theme-teen .kl-icon-btn  { color: #fff; background: rgba(255,255,255,0.2); border-radius: 12px; }
.theme-teen .kl-icon-btn:hover { background: rgba(255,255,255,0.35); }
.theme-teen .kl-cart-badge { background: #fff; color: var(--shop-primary); }
.theme-teen .khachlink-layout { background: linear-gradient(180deg, #fdf6ff 0%, #fff 120px); }

/* =====================================================
   THEME: LADY — Elegant, soft & feminine
   ===================================================== */
.theme-lady .kl-header {
    background: linear-gradient(135deg, #fff5f7 0%, #fce4ec 100%);
    border-bottom: 1px solid rgba(233,30,99,0.15);
}
.theme-lady .kl-shop-name { color: #ad1457; font-style: italic; }
.theme-lady .kl-icon-btn  { color: #ad1457; background: rgba(233,30,99,0.08); }
.theme-lady .kl-icon-btn:hover { background: rgba(233,30,99,0.15); }
.theme-lady .kl-cart-badge { background: #e91e63; color: #fff; }
.theme-lady .khachlink-layout { background: #fffafc; }

/* =====================================================
   THEME: PREMIUM — Dark luxury
   ===================================================== */
.theme-premium .kl-header {
    background: linear-gradient(135deg, #1a1a2e 0%, #16213e 100%);
    border-bottom: 1px solid var(--shop-primary);
}
.theme-premium .kl-shop-name {
    color: var(--shop-primary);
    font-family: 'Playfair Display', Georgia, serif;
    letter-spacing: 0.05em;
}
.theme-premium .kl-icon-btn  { color: var(--shop-primary); background: rgba(255,255,255,0.05); }
.theme-premium .kl-icon-btn:hover { background: rgba(255,255,255,0.1); }
.theme-premium .kl-cart-badge { background: var(--shop-primary); color: #1a1a2e; }
.theme-premium .khachlink-layout { background: #0f0f1a; color: #e8e8e8; }
.theme-premium .kl-footer { background: #0a0a14; border-top-color: rgba(255,255,255,0.05); }
.theme-premium .kl-footer-copy,
.theme-premium .kl-footer-link,
.theme-premium .kl-footer-brand { color: rgba(255,255,255,0.6); }
</style>

@code {
    [Parameter] public RenderFragment? ChildContent { get; set; }
    [Inject]    public NavigationManager Navigation  { get; set; } = default!;

    private string _primaryColor      = "#8B4513";
    private string _secondaryColor    = "#D2691E";
    private string _primaryColorLight = "#c0784a";
    private string _primaryColorDark  = "#5c2d0a";
    private string _themeName         = "classic";
    private string _shopName          = "Vạn An";
    private string? _logoUrl;
    private string? _shopPhone;
    private string? _fbLink;
    private string? _tiktokLink;
    private Guid   _tenantId;
    private bool   _isLoggedIn;
    private int    _cartCount;

    protected override async Task OnInitializedAsync()
    {
        await base.OnInitializedAsync();
        _tenantId = TenantService.GetCurrentTenantId();

        try
        {
            var config = await ShopConfigService.GetShopConfigAsync(_tenantId);
            if (config != null)
            {
                _primaryColor      = config.PrimaryColor;
                _secondaryColor    = config.SecondaryColor;
                _primaryColorLight = LightenColor(config.PrimaryColor, 0.2);
                _primaryColorDark  = DarkenColor(config.PrimaryColor, 0.2);
                _themeName         = config.ActiveTheme.ToString().ToLower();
                _shopName          = config.ShopName;
                _logoUrl           = config.LogoUrl?.ToString();
                _shopPhone         = config.Phone;
                _fbLink            = config.SocialLinksFb;
                _tiktokLink        = config.SocialLinksTiktok;
            }
        }
        catch { /* fallback to defaults */ }
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!firstRender) return;
        var token = await JSRuntime.InvokeAsync<string>("localStorage.getItem", "customer_token");
        _isLoggedIn = !string.IsNullOrEmpty(token);
        // Cart count từ localStorage (set bởi Cart page)
        var cartStr = await JSRuntime.InvokeAsync<string>("localStorage.getItem", "cart_count");
        _cartCount  = int.TryParse(cartStr, out var n) ? n : 0;
        StateHasChanged();
    }

    // Tính toán màu sắc sáng hơn/tối hơn từ hex
    private static string LightenColor(string hex, double amount)
    {
        try
        {
            var (r, g, b) = HexToRgb(hex);
            r = (int)Math.Min(255, r + 255 * amount);
            g = (int)Math.Min(255, g + 255 * amount);
            b = (int)Math.Min(255, b + 255 * amount);
            return $"#{r:X2}{g:X2}{b:X2}";
        }
        catch { return hex; }
    }

    private static string DarkenColor(string hex, double amount)
    {
        try
        {
            var (r, g, b) = HexToRgb(hex);
            r = (int)Math.Max(0, r - 255 * amount);
            g = (int)Math.Max(0, g - 255 * amount);
            b = (int)Math.Max(0, b - 255 * amount);
            return $"#{r:X2}{g:X2}{b:X2}";
        }
        catch { return hex; }
    }

    private static (int r, int g, int b) HexToRgb(string hex)
    {
        hex = hex.TrimStart('#');
        return (
            Convert.ToInt32(hex[..2], 16),
            Convert.ToInt32(hex.Substring(2, 2), 16),
            Convert.ToInt32(hex.Substring(4, 2), 16)
        );
    }
}
```

---

## CSS variables flow

```
ShopConfig.PrimaryColor = "#8B4513"
    ↓ (KhachLinkLayout.OnInitializedAsync)
:root { --shop-primary: #8B4513; --shop-primary-dark: #5c2d0a; ... }
    ↓ (CSS theme class .theme-classic)
.kl-header { background: linear-gradient(135deg, var(--shop-primary), var(--shop-primary-dark)) }
    ↓
Header màu nâu cà phê đặc trưng của shop
```

---

## Cách admin đổi theme / màu

Trong ShopERP → Settings → KhachLink Appearance:
- Đổi `PrimaryColor` (color picker) → header màu mới ngay lập tức
- Đổi `ActiveTheme` (Classic / Modern / Teen / Lady / Premium) → layout hoàn toàn thay đổi
- Upload logo → `LogoUrl` → hiện trên header + footer
- Điền SĐT, Facebook, TikTok → hiện trong footer

**Wave 17 không tạo Settings UI** — chỉ implement layout consume ShopConfig. Settings UI là Wave 18.

---

## Entry criteria
- [ ] W17-T1 complete (cần `customer_token` để hiện đúng header login state)
- [ ] W17-T6 complete (NavMenu mới đã có)
- [ ] `IShopConfigService.GetShopConfigAsync()` available trong KhachLink DI

## Success criteria
- [ ] `KhachLinkLayout.razor` render đúng logo, tên shop, màu từ `ShopConfig`
- [ ] 5 themes hoạt động: đổi `ActiveTheme` → CSS class thay đổi, toàn bộ màu sắc flip
- [ ] CSS variables `--shop-primary` / `--shop-secondary` được set tại `:root` — accessible toàn app
- [ ] Header sticky, scroll-safe, backdrop-blur hoạt động trên iOS Safari
- [ ] Cart badge hiện số đúng khi có sản phẩm trong giỏ
- [ ] "Tài khoản" / "Đăng nhập" switch đúng theo login state
- [ ] Mobile: max-width 680px centered, bottom padding cho mobile nav
- [ ] Footer hiển thị SĐT, Facebook, TikTok links nếu có trong ShopConfig
- [ ] Theme `Premium` chuyển sang dark background — text vẫn readable
- [ ] `dotnet build VanAn.sln` → 0 errors
- [ ] Không sử dụng custom HTML/CSS khi UI Platform component đã có (governance rule)

## Hard stops
- KHÔNG sửa `ShopConfig` record trong Domain — dùng fields hiện có
- KHÔNG tạo custom CSS framework riêng — extend VanAnLayout đã có
- `--shop-primary` là CSS variable thuần, KHÔNG inject vào JS
- Màu Lady theme (`#ad1457`, `#e91e63`) hardcode cho safety — không map từ `SecondaryColor` vì Lady theme có bộ màu riêng theo concept
