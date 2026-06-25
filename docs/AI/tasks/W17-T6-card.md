# W17-T6 — NavMenu Update (4 retention routes mới)

**Wave:** 17 — KhachLink Retention & Loyalty
**Branch:** `feature/wave17-khachlink-retention`
**Priority:** 🟢 MEDIUM — blocking UX nếu user không tìm được các tính năng mới
**Conflict risk:** VERY LOW — chỉ sửa NavMenu.razor
**Depends on:** W17-T1 complete (cần biết user có logged in không để hiển thị đúng)
**Estimated effort:** 0.25 session

---

## Hiện trạng NavMenu.razor

```razor
// HIỆN TẠI — chỉ có 4 items, 2 trong đó là scaffold (Counter, Weather)
<NavLink href="">Home</NavLink>
<NavLink href="counter">Counter</NavLink>      ← scaffold, xóa
<NavLink href="weather">Weather</NavLink>      ← scaffold, xóa
<NavLink href="VanAnDashboard">Dashboard</NavLink>  ← route cũ, Wave 16 đổi thành /dashboard
```

---

## Target NavMenu

```
┌─────────────────┐
│ 🍵 Vạn An       │
├─────────────────┤
│ 🏠 Trang chủ    │  /
│ 🛒 Giỏ hàng     │  /cart
│ 📋 Đơn hàng     │  /my-orders
│ 💎 Điểm thưởng  │  /my-loyalty
│ 📍 Cửa hàng     │  /stores
│ 👤 Tài khoản    │  /profile  (hoặc /login nếu chưa đăng nhập)
│ ─────────────── │
│ 📊 Dashboard    │  /dashboard  (staff only — ẩn cho anonymous)
└─────────────────┘
```

---

## Files cần sửa

### SỬA: `5_WebApps/KhachLink/Components/Layout/NavMenu.razor`

```razor
@using VanAn.UI.Platform.Components.Atomic
@inject IJSRuntime JSRuntime

<VanAnCard CssClass="nav-menu-card" Shadow="false">
    <div class="nav-header">
        <a class="nav-brand" href="/">🍵 Vạn An</a>
    </div>

    <nav class="nav-scrollable">
        <div class="nav-item">
            <NavLink class="nav-link" href="/" Match="NavLinkMatch.All">
                <i class="fas fa-home me-2"></i> Trang chủ
            </NavLink>
        </div>

        <div class="nav-item">
            <NavLink class="nav-link" href="/cart">
                <i class="fas fa-shopping-cart me-2"></i> Giỏ hàng
            </NavLink>
        </div>

        <div class="nav-item">
            <NavLink class="nav-link" href="/my-orders">
                <i class="fas fa-clipboard-list me-2"></i> Đơn hàng của tôi
            </NavLink>
        </div>

        <div class="nav-item">
            <NavLink class="nav-link" href="/my-loyalty">
                <i class="fas fa-gem me-2"></i> Điểm thưởng
            </NavLink>
        </div>

        <div class="nav-item">
            <NavLink class="nav-link" href="/stores">
                <i class="fas fa-map-marker-alt me-2"></i> Tìm cửa hàng
            </NavLink>
        </div>

        <div class="nav-item">
            <NavLink class="nav-link" href="@(_isLoggedIn ? "/profile" : "/login")">
                <i class="fas fa-user me-2"></i>
                @(_isLoggedIn ? "Tài khoản" : "Đăng nhập")
            </NavLink>
        </div>

        @if (_isStaff)
        {
            <hr class="nav-divider" />
            <div class="nav-item">
                <NavLink class="nav-link" href="/dashboard">
                    <i class="fas fa-chart-line me-2"></i> Dashboard
                </NavLink>
            </div>
        }
    </nav>
</VanAnCard>

<style>
    .nav-menu-card {
        background: #1a1a2e;
        color: white;
        height: 100%;
        border-radius: 0;
    }

    .nav-menu-card ::deep .card-body {
        padding: 0;
        display: flex;
        flex-direction: column;
    }

    .nav-header {
        padding: 1rem;
        background: rgba(255, 255, 255, 0.1);
    }

    .nav-brand {
        color: white;
        text-decoration: none;
        font-weight: bold;
        font-size: 1.2rem;
    }

    .nav-brand:hover { color: #ddd; }

    .nav-scrollable {
        flex: 1;
        overflow-y: auto;
        padding: 0.5rem 0;
    }

    .nav-item { padding: 0.25rem 0.5rem; }

    .nav-link {
        color: rgba(255, 255, 255, 0.8);
        text-decoration: none;
        display: flex;
        align-items: center;
        padding: 0.6rem 0.75rem;
        border-radius: 6px;
        transition: all 0.2s;
        font-size: 0.95rem;
    }

    .nav-link:hover {
        background: rgba(255, 255, 255, 0.12);
        color: white;
    }

    .nav-link.active {
        background: rgba(255, 255, 255, 0.2);
        color: white;
        font-weight: 600;
    }

    .nav-divider {
        border-color: rgba(255,255,255,0.15);
        margin: 0.5rem 1rem;
    }

    /* Mobile: bottom tab bar */
    @@media (max-width: 640px) {
        .nav-menu-card {
            position: fixed;
            bottom: 0;
            left: 0; right: 0;
            height: auto;
            z-index: 100;
        }
        .nav-header { display: none; }
        .nav-scrollable {
            display: flex;
            flex-direction: row;
            overflow-x: auto;
            padding: 0.25rem 0;
        }
        .nav-item { padding: 0.25rem; flex: 1; text-align: center; }
        .nav-link {
            flex-direction: column;
            font-size: 0.7rem;
            padding: 0.3rem 0.5rem;
            gap: 2px;
        }
        .nav-link i { font-size: 1.1rem; }
    }
</style>

@code {
    private bool _isLoggedIn = false;
    private bool _isStaff    = false;

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!firstRender) return;
        var token = await JSRuntime.InvokeAsync<string>("localStorage.getItem", "customer_token");
        _isLoggedIn = !string.IsNullOrEmpty(token);
        // Staff detection: check localStorage("staff_token") — set by ShopERP staff login
        var staffToken = await JSRuntime.InvokeAsync<string>("localStorage.getItem", "staff_token");
        _isStaff = !string.IsNullOrEmpty(staffToken);
        StateHasChanged();
    }
}
```

---

## Entry criteria
- [ ] W17-T1 complete — `customer_token` localStorage pattern đã xác lập
- [ ] `/dashboard` route tồn tại (Wave 16-T4 tạo `Pages/Dashboard.razor`)

## Success criteria
- [ ] NavMenu không còn Counter, Weather, VanAnDashboard links
- [ ] 6 nav items: Trang chủ, Giỏ hàng, Đơn hàng, Điểm thưởng, Cửa hàng, Tài khoản/Đăng nhập
- [ ] Dashboard link chỉ hiện khi `staff_token` tồn tại
- [ ] "Tài khoản" label khi logged in, "Đăng nhập" khi anonymous
- [ ] Mobile: bottom tab bar layout (max-width 640px)
- [ ] Active route highlight đúng
- [ ] `dotnet build VanAn.sln` → 0 errors
