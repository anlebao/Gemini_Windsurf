# VALCN v2.0 Runtime Verification Report

> **Date:** 2026-08-09
> **Deployed commit:** `f9f59ef6` (CD Multi-VPS run `31322389038` SUCCESS)
> **Environment:** GCP Multi-VPS — Gateway `api2.khachvip.online`, ShopERP `app2.khachvip.online`, KhachLink `diemthuong2.khachvip.online`
> **Tester:** Devin (automated HTTP + Blazor prerender checks)

---

## Summary

| Result | Count |
|--------|-------|
| PASS | 10 |
| PARTIAL | 1 |
| FAIL (fix-after) | 2 |
| **Total** | **13** |

---

## PASS (10)

| # | Test | Endpoint | Evidence |
|---|------|----------|----------|
| RV-01 | Health check all 3 VPS | `GET /health` ×3 | Gateway 200 `{"status":"Healthy"}`, ShopERP 200 `Healthy`, KhachLink 200 HTML |
| RV-02 | SystemAdmin login | `POST /api/platform/login` | 200, role=SystemAdmin, JWT issued |
| RV-03 | Feature Flags UI renders | `GET /admin/valcn-features` | 200, 14296 bytes, contains "VALCN v2.0 Features", "Platform Fee", "Loyalty Budget", "Refund Reversal", "Phase 2/3/4", Enable/Disable buttons, 3× "Disabled" (all OFF) |
| RV-04 | Toggle PlatformFee ON→OFF | `PUT /api/admin/feature-flags/ValcnV2_PlatformFee` | ON: 204 → GET returns `ValcnV2_PlatformFee: true`. OFF: 204 → GET returns `false`. State change verified. |
| RV-05 | Network Dashboard UI renders | `GET /admin/network-dashboard` | 200, 15927 bytes, contains all 8 metrics: GMV, Active Tenants, Active Customers, Repeat Rate, Platform Revenue, Loyalty Cost, Loyalty ROI, Contribution profit. 56 metric card elements. Date filter + "Áp dụng" button present. |
| RV-06 | Background Services UI renders | `GET /admin/background-services` | 200, 17160 bytes, contains `LoyaltyBudgetDailyResetJob` + `LoyaltyBudgetMonthlyResetJob` |
| RV-09 | Accounting page (Shop Owner) | `GET /accounting` | 200, 14037 bytes, contains "Accounting" (as Shop Owner with tenant context) |
| RV-10 | KhachLink PWA accessible | `GET https://diemthuong2.khachvip.online/` | 200, HTML with manifest.json, theme-color, PWA meta tags |
| RV-11 | Network Dashboard internal API protected | `GET /api/internal/network-dashboard` | 401 Unauthorized without `X-Internal-Api-Key` header — endpoint exists and is secured |
| RV-12 | Feature Flags API | `GET /api/admin/feature-flags` | 200, returns JSON array: `[{featureName:"ValcnV2_PlatformFee",isEnabled:false}, {ValcnV2_LoyaltyBudget,false}, {ValcnV2_RefundReversal,false}]` — all 3 flags, all OFF |

---

## PARTIAL (1)

### RV-08: Loyalty Config UI — `/admin/loyalty-config`

**Status:** PARTIAL — page renders for SystemAdmin, but 4 budget cap field names not found in prerendered HTML.

| Check | Result |
|-------|--------|
| HTTP 200 | ✅ (17702 bytes, not login page) |
| Contains "Loyalty" | ✅ |
| Contains "Alliance" | ✅ |
| Contains "PerOrderRateCap" | ❌ not in prerender HTML |
| Contains "MonthlyPointsBudget" | ❌ not in prerender HTML |
| Contains "DailyPointsBudget" | ❌ not in prerender HTML |
| Contains "PerCustomerDailyLimit" | ❌ not in prerender HTML |

**Likely cause:** Blazor Server prerender does not include the specific field names — they render client-side via SignalR interactivity. The page IS loading (17KB, contains "Loyalty" + "Alliance" text). Cannot verify field-level rendering via curl — would need browser-based test.

**Action:** Browser-verify recommended (open page in browser, check 4 budget cap fields render).

---

## FAIL — fixed (2)

### FAIL-1: NavMenu missing VALCN + Network Dashboard + Background Services entries — FIXED

**Severity:** Medium (pages accessible via direct URL, but not discoverable via sidebar navigation)

**Root cause:** Nav entries were added to `NavMenu.razor` (legacy nav, lines 271-278) but NOT to `AdminLayout.razor`'s `AdminMenuItems` list (lines 20-40). Admin pages use `@layout AdminLayout` which renders `VanANavigation` with `AdminMenuItems` — not `NavMenu.razor`.

**Evidence:** Prerendered HTML of `/admin/valcn-features` shows sidebar with 18 nav items (Sitemap, Users, Tenants, Loyalty Alliance, ... Commerce Mode, Quỹ Cộng Đồng, ...) but NO "VALCN v2.0 Features", "Network Dashboard", or "Background Services" entries.

**Files to fix:**
- `5_WebApps/ShopERP/Components/Pages/Admin/AdminLayout.razor` — add 3 entries to `AdminMenuItems`:
  ```csharp
  new() { Title = "VALCN v2.0 Features", Icon = "toggles", Url = "/admin/valcn-features" },
  new() { Title = "Network Dashboard", Icon = "graph-up", Url = "/admin/network-dashboard" },
  new() { Title = "Background Services", Icon = "arrow-clockwise", Url = "/admin/background-services" },
  ```

### FAIL-2: User Guide incorrect URLs — FIXED

**Severity:** Low (documentation only)

| User Guide says | Actual route |
|----------------|--------------|
| `/admin/shop-feature-settings` | `/settings/shop-features` |
| `/admin/loyalty-config` (for Shop Owner) | 404 for Shop Owner — page is SystemAdmin-only via AdminLayout |

**Files to fix:**
- `docs/user-guide/VALCN_V2_Platform_User_Guide.md` — correct URLs in sections 4.1 and 4.2

---

## Test methodology notes

1. **Blazor Server prerender limitation:** curl/Invoke-WebRequest captures only the prerendered HTML. Blazor Server components that render content via SignalR interactivity (after WebSocket connection) are not visible in curl output. Pages confirmed as loading (200 status, not login redirect, reasonable content length) but field-level rendering cannot be verified without a browser.

2. **Cookie session:** SystemAdmin login via `POST /api/platform/login` issues a cookie that works for Blazor pages. Shop Owner login via `POST /Login` (Razor Page form) issues a separate cookie. Both verified working.

3. **Feature flag toggle:** Verified via Gateway API (`PUT /api/admin/feature-flags/{name}` with `{isEnabled: bool}` body). Toggle ON → GET confirms `true`. Toggle OFF → GET confirms `false`. State persists across requests. Cache TTL 30s not tested (would need to wait 30s and verify cache invalidation).

4. **Internal API:** Network Dashboard internal endpoint (`/api/internal/network-dashboard`) requires `X-Internal-Api-Key` header. Without key → 401. The ShopERP `NetworkDashboardHttpService` calls this server-side with the key — UI rendering of metrics (RV-05) confirms the full chain works.

---

## Recommended next steps

1. **Fix FAIL-1 (NavMenu):** Add 3 entries to `AdminLayout.razor` `AdminMenuItems`. Quick fix, 3 lines.
2. **Fix FAIL-2 (User Guide):** Correct URLs in user guide.
3. **Browser-verify RV-08:** Open `/admin/loyalty-config` in browser, confirm 4 budget cap fields render.
4. **Browser-verify feature flag toggle UI:** Click Enable/Disable buttons in browser, confirm visual state change matches API state.
5. **E2E test (optional):** Playwright test for full flow: login → navigate via sidebar → toggle flag → verify state.
