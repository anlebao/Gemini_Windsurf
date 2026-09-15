# Task Card #3: GPS Mock for Playwright RV — Test-Only Fix

> **Status:** PLANNED (awaiting implementation approval)
> **Priority:** P3 — test tooling improvement, no production impact
> **Created:** 2026-09-15
> **Master plan:** `docs/AI/tasks/community_commerce_fixes/master_plan.md`
> **Prerequisite:** None (independent of Issues #1, #2)
> **Effort:** 0.5 ngày (~2h)

## Problem

Playwright headless browser không có GPS thật. Blazor WASM `vananPWA.getCurrentPosition()` JS interop return `null` → map không render trong RV test.

**Evidence (RV 2026-09-14):**
- Playwright `context.setGeolocation({lat, lng})` + `permissions: ['geolocation']` → Blazor WASM không trigger
- `DeliveryTracking.razor` (đã fix commit `88f3496f`) không block khi GPS fail — chỉ ẩn map
- `NearbyProducts.razor` + `NearbyOrders.razor` hiển thị "Không lấy được vị trí GPS" → không load data

**Impact:** Test-only. Production users có GPS thật (mobile browser). Không ảnh hưởng production.

## Solution

Inject mock `vananPWA.getCurrentPosition` JS function trước khi navigate:

```javascript
await page.addInitScript(() => {
    window.vananPWA = window.vananPWA || {};
    window.vananPWA.getCurrentPosition = () =>
        Promise.resolve({ Lat: 10.966, Lng: 106.594 });
});
```

Mock trả về tọa độ gần tenant shop (10.966, 106.594 — Vạn An Cafe HKD Group 1) để nearby-products và nearby-orders API trả data.

**Không đổi production code.** Chỉ sửa RV script.

## Scope Checklist

### Phase A — Update RV scripts (Day 1)
- [ ] A1: `.devin/rv-cc-full-otp.js` — Add `page.addInitScript()` mock GPS cho tất cả browser contexts
- [ ] A2: `.devin/rv-cc-otp-bypass.js` — Same mock
- [ ] A3: Verify nearby-products page loads products (không còn "Không lấy được vị trí GPS")
- [ ] A4: Verify nearby-orders page loads orders
- [ ] A5: Verify delivery-tracking page renders map (Leaflet)

### Phase B — Re-run full RV (Day 1)
- [ ] B1: Reset test data (delivery task + order status)
- [ ] B2: Run `.devin/rv-cc-full-otp.js` → all steps PASS (including map render)
- [ ] B3: Document any remaining failures

## Prerequisites

- Test customers + roles exist in production DB (from RV 2026-09-14)
- Tenant GPS coordinates: 10.966, 106.594 (Vạn An Cafe HKD Group 1)
- Playwright installed locally

## Verification

1. **Nearby products:** Salesman login → `/community/nearby-products` → products load (no GPS error)
2. **Nearby orders:** Shipper login → `/community/nearby-orders` → orders load (no GPS error)
3. **Delivery tracking:** Shipper → `/community/delivery-tracking/{orderId}` → map renders (Leaflet visible)
4. **Order tracking:** Customer → `/order-tracking/{orderId}` → map renders

## Files to modify

| File | Change | Lines |
|---|---|---|
| `.devin/rv-cc-full-otp.js` | Add `addInitScript` GPS mock to `createContext()` | ~5 lines |
| `.devin/rv-cc-otp-bypass.js` | Same | ~5 lines |

## Risks

| # | Risk | Mitigation |
|---|---|---|
| R1 | Mock GPS không match production behavior | Chỉ cho RV, production user có GPS thật |
| R2 | Mock break other JS functions | Only override `getCurrentPosition`, preserve other `vananPWA.*` methods |
| R3 | Tọa độ mock không near tenant | Use tenant GPS (10.966, 106.594) — verified in DB |

## Related

- Master plan: `docs/AI/tasks/community_commerce_fixes/master_plan.md`
- RV script: `.devin/rv-cc-full-otp.js`
- Source: `5_WebApps/KhachLink/Pages/NearbyProducts.razor` (GPS check)
- Source: `5_WebApps/KhachLink/Pages/NearbyOrders.razor` (GPS check)
- Source: `5_WebApps/KhachLink/Pages/DeliveryTracking.razor` (GPS optional after fix 88f3496f)
- Tenant GPS: PostgreSQL `Tenants.Settings_Latitude=10.966, Settings_Longitude=106.594`
