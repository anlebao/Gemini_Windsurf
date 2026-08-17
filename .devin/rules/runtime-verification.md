# RUNTIME VERIFICATION (RV) TEST METHODOLOGY

**Priority: HIGHEST — run RV after every deploy to verify fixes work in production, not just in code.**

## WHY RV MATTERS
Code can build + pass CI but still fail at runtime due to:
- JavaScript hoisting bugs (only visible when browser executes JS)
- JSON deserialize type mismatches (API returns string, DTO expects int)
- CORS blocking cross-origin fetches
- WASM not deployed (Docker image stale, cache not busted)
- Service Worker serving old cached assets
- localStorage caching stale config
- Blazor WASM rendering client-side (prerender HTML is just shell)

## RV PROTOCOL (5 LAYERS — STOP AT FIRST FAILURE)

### Layer 1: API-Level Checks (curl/Invoke-WebRequest — 10 seconds)
Fastest verification. No browser needed. Check before anything else.

```powershell
# Check API response + CORS header
$r = Invoke-WebRequest -Uri "https://api2.khachvip.online/api/v1/khachlink-instances/by-domain/timlathay.com" -Headers @{"Origin"="https://timlathay.com"} -UseBasicParsing
$r.Content  # Check JSON fields (isActive, profile, navFlags)
$r.Headers["Access-Control-Allow-Origin"]  # Must match origin

# Check Guard API endpoints (expect 401 without auth = endpoint exists)
Invoke-WebRequest -Uri "https://api2.khachvip.online/api/guard/verify" -Method POST -Body '{"qrPayload":"test"}' -ContentType "application/json"
```

**STOP if:** API returns wrong data, CORS header missing, or endpoint 404.

### Layer 2: Static Asset Verification (curl — 10 seconds)
Verify new code is actually deployed on VPS (Docker image rebuilt).

```powershell
# Check WASM DLL has new code strings (UTF-16 encoding for .wasm files)
$r = Invoke-WebRequest -Uri "https://timlathay.com/_framework/VanAn.KhachLink.wasm" -UseBasicParsing
$text16 = [System.Text.Encoding]::Unicode.GetString($r.Content)
$text16 -match "khachlink-disabled-page"  # Check for new code markers

# Check JS files served
Invoke-WebRequest -Uri "https://app2.khachvip.online/js/lib/qrcode.js" -UseBasicParsing

# Check Service Worker version
$r = Invoke-WebRequest -Uri "https://timlathay.com/service-worker.js" -UseBasicParsing
$r.Content -match "v18-instance-disable"

# Check appsettings.json
Invoke-WebRequest -Uri "https://timlathay.com/appsettings.json" -UseBasicParsing
```

**STOP if:** New code strings not found in WASM/JS → Docker image not rebuilt, CD pipeline failed.

### Layer 3: Playwright Headless Runtime Test (30-60 seconds)
**CRITICAL: This is where real bugs are caught.** API + static checks pass but runtime fails.

```javascript
const { chromium } = require('playwright');
const browser = await chromium.launch({ headless: true });
const context = await browser.newContext({ bypassCSP: true, ignoreHTTPSErrors: true });
const page = await context.newPage();

// MUST: Navigate to app origin FIRST (for CORS — about:blank origin is blocked)
await page.goto('https://app2.khachvip.online', { waitUntil: 'networkidle' });

// Login via UI form (NOT API — ShopERP uses cookie auth, not Bearer for SSR pages)
await page.goto(`${APP}/Login`, { waitUntil: 'networkidle' });
await page.locator('#username').fill('baove@vanan.vn');
await page.locator('#password').fill('2026@vanan');
await page.locator('button[type="submit"]').click();
await page.waitForNavigation({ waitUntil: 'networkidle' });

// Navigate to target page
await page.goto(`${APP}/guard/scan`, { waitUntil: 'networkidle' });
await page.waitForTimeout(5000); // Wait for Blazor WASM boot

// Check DOM elements
const hasDisabledPage = await page.locator('.khachlink-disabled-page').count() > 0;

// Check JS globals (catches hoisting bugs, load failures)
const qrLoaded = await page.evaluate(() => typeof window.vananQR !== 'undefined');

// Directly test JS functions (catches runtime errors that UI flow may not trigger)
const qrResult = await page.evaluate(async () => {
    return await window.vananGuardCamera.generateQrImage('test', 300);
});

// Screenshot for evidence
await page.screenshot({ path: 'rv-test.png', fullPage: true });
```

**Key patterns:**
- `bypassCSP: true` — needed to evaluate JS freely
- `ignoreHTTPSErrors: true` — for self-signed certs in staging
- Navigate to app origin BEFORE any fetch() — avoids CORS `null` origin
- Login via UI form, not API — ShopERP uses cookie auth for SSR pages
- Wait 5+ seconds after navigation — Blazor WASM needs time to boot
- Check `window.*` globals — catches JS load failures
- Call JS functions directly — catches runtime errors (hoisting, undefined refs)
- Collect console logs + pageerror events — catches silent failures

**STOP if:** DOM elements missing, JS globals undefined, JS functions throw errors, console has errors.

### Layer 4: Full UI Flow Test (Playwright — 60-120 seconds)
Only if Layers 1-3 pass. Simulate actual user interaction.

```
Login → Navigate to page → Fill form → Click button → Verify result (DOM/screenshot)
```

**For Guard QR (#130):**
- Login as `baove@vanan.vn` / `2026@vanan`
- Navigate to `/guard/scan`
- Fill plate number
- Click "Cấp QR" button
- Verify QR image renders (canvas/img element with content)
- Verify short code displayed

**For KhachLink disable (#134):**
- Navigate to `https://timlathay.com`
- Verify "Instance tạm ngưng" page shows
- Verify NO cart/profile icons (FullCommerce fallback)
- Check localStorage for `khachlink_instance_config_v2`

### Layer 5: Browser Manual Verification (user — 2-5 minutes)
Final check by user in real browser. Only if Layers 1-4 pass.

```
1. Hard refresh (Ctrl+Shift+R) or DevTools → Application → Service Workers → Unregister
2. Open target URL
3. Verify visual result matches expectation
4. Check DevTools Console for errors
5. Check DevTools Application → localStorage / Service Workers
```

## COMMON RV FAILURES & ROOT CAUSES

| Symptom | Root Cause | Fix |
|---------|-----------|-----|
| API returns correct data but UI shows wrong | WASM not deployed (old Docker image) | Check WASM bytes for new code strings (Layer 2) |
| JS function throws "Cannot read properties of undefined" | Variable hoisting bug (var used before assignment) | Move var definition before first use |
| CORS error in Playwright | Origin is `null` (about:blank) or cross-origin fetch with credentials | Navigate to app origin first; use same-origin fetch |
| Blazor WASM page blank | WASM still booting (needs 5+ seconds) | Add `waitForTimeout(5000)` after navigation |
| Login fails with 401 | Wrong endpoint or password | ShopERP: `/api/platform/login`, password `2026@vanan` |
| Disabled instance still shows full UI | JSON deserialize fails silently → null → fallback | Check DTO types match API response (string vs int) |
| Old layout still renders | Wrong layout checked (nested vs outer) | Check `Routes.razor` for `DefaultLayout` — that's the outer layout |
| Service Worker serves old cache | SW version not bumped or old SW registered | Bump SW cache version; unregister old SW in DevTools |

## RV SCRIPT TEMPLATE
Save as `rv-<issue>.js` in repo root (gitignore or delete after RV complete):

```javascript
const { chromium } = require('playwright');
(async () => {
    const browser = await chromium.launch({ headless: true });
    const context = await browser.newContext({ bypassCSP: true, ignoreHTTPSErrors: true });
    const page = await context.newPage();
    const logs = [];
    page.on('console', msg => logs.push(`[${msg.type()}] ${msg.text()}`));
    page.on('pageerror', err => logs.push(`[pageerror] ${err.message}`));

    // 1. Navigate to app origin (CORS)
    // 2. Login via UI form
    // 3. Navigate to target page
    // 4. Wait for Blazor WASM boot (5s)
    // 5. Check DOM elements
    // 6. Check JS globals
    // 7. Call JS functions directly
    // 8. Screenshot
    // 9. Print console errors
    // 10. Summary: PASS/FAIL

    await browser.close();
    process.exit(pass ? 0 : 1);
})();
```

## CREDENTIALS (PRODUCTION VPS)
- **ShopERP Login:** `baove@vanan.vn` (Guard) / `admin@vanan.vn` (Owner) / password: `2026@vanan`
- **Login endpoint:** `POST /api/platform/login` (ShopERP, not Gateway)
- **Gateway API:** `https://api2.khachvip.online`
- **ShopERP app:** `https://app2.khachvip.online`
- **KhachLink (timlathay):** `https://timlathay.com`
- **Tenant ID:** `00000000-0000-0000-0000-000000000001`

## RULES
1. **ALWAYS run Layer 1-3 before reporting "fixed"** — API checks + static asset + Playwright runtime
2. **NEVER trust build/CI pass as proof of runtime correctness** — CI tests unit logic, not deployed behavior
3. **NEVER skip Playwright (Layer 3)** — this is where JS hoisting, CORS, deserialize bugs are caught
4. **ALWAYS check console logs + pageerror events** — silent failures are common in WASM
5. **ALWAYS screenshot** — visual evidence for user review
6. **ALWAYS verify WASM bytes contain new code** — Docker image may be stale
7. **Delete RV scripts + screenshots before commit** — they are temporary, not part of codebase
