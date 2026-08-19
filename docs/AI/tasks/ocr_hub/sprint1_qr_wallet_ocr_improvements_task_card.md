# SPRINT 1: QR Wallet Merge + OCR Plate Improvements — Task Card

> **Sprint:** 1 — QR Wallet Merge + OCR Plate Improvements
> **Status:** ✅ COMPLETE + MERGED + DEPLOYED + RV PASS (PR #149)
> **Branch:** `feature/ocr-hub-client` → merged to `main`
> **Merged via:** PR #149 (squash) — S1+S2 combined
> **#150 fix:** Commit `6c67f594` — JSON case-insensitive deserialization fix for QR wallet
> **Master plan:** `docs/AI/tasks/ocr_hub/master_plan.md`
> **Estimated files changed:** 5
> **Estimated effort:** ~50 minutes

## Objective

1. **QR Wallet Merge:** Gộp `/qr/claim` + `/qr/wallet` thành 1 trang 2 tab, bỏ login requirement — khách quét QR vé xe không cần đăng nhập, lưu vào wallet giống add-to-cart
2. **OCR Plate Improvements:** Tách 2 hàng biển số VN trước OCR, PSM 7 từng hàng, char whitelist chặt hơn — cải thiện accuracy + speed

## Tasks

| # | Task | Files | Status |
|---|---|---|---|
| 1 | `Wallet.razor` — rewrite thành 2 tab "Vé của tôi" + "Nhận QR mới", bỏ login gate, nhúng QRScanner + short code input | `5_WebApps/KhachLink/Components/Pages/Qr/Wallet.razor` | ⏳ |
| 2 | `Wallet.razor.cs` — rewrite: luôn load localStorage, `DoClaimAsync` xử lý login + chưa login, deep link `?data=` handler | `5_WebApps/KhachLink/Components/Pages/Qr/Wallet.razor.cs` | ⏳ |
| 3 | `NavMenu.razor` — đổi link `/qr/claim` → `/qr/wallet` (desktop + mobile) | `5_WebApps/KhachLink/Components/Layout/NavMenu.razor` | ⏳ |
| 4 | `Claim.razor` — redirect `/qr/claim` → `/qr/wallet` (backward compat) | `5_WebApps/KhachLink/Components/Pages/Qr/Claim.razor` | ⏳ |
| 5 | `guard-camera.js` — OCR plate improvements: tách 2 hàng ROI, PSM 7 từng hàng, whitelist chặt, regex validate, downscale 300px | `5_WebApps/ShopERP/wwwroot/js/guard-camera.js` | ⏳ |
| 6 | Build + guard-check | — | ⏳ |
| 7 | Commit + push + create PR | — | ⏳ |
| 8 | Merge + CD + RV | — | ⏳ |

## Task Details

### Task 1: Wallet.razor — 2 tab UI

**Current:** Single-purpose wallet list, login-gated
**New:** 2-tab page, no login gate

```
┌─────────────────────────────────────┐
│  Ví QR gửi xe                        │
│  ┌──────────┬──────────────┐        │
│  │ Vé của tôi │ Nhận QR mới │        │
│  └──────────┴──────────────┘        │
│                                      │
│  [Tab 1: Vé của tôi]                 │
│   - List wallet sessions             │
│   - Tap → fullscreen QR              │
│   - Empty state: "Chưa có vé"        │
│                                      │
│  [Tab 2: Nhận QR mới]                │
│   - QRScanner component               │
│   - Short code input (6 digits)      │
│   - "Nhận QR" button                 │
│                                      │
│  [Fullscreen QR modal — unchanged]   │
└─────────────────────────────────────┘
```

**Key changes:**
- Remove `@if (!_isLoggedIn) { login prompt }` block
- Add tab state `_activeTab = "wallet" | "claim"`
- Tab 1: existing wallet list code (move from current Wallet.razor)
- Tab 2: QRScanner + short code input (move from Claim.razor)
- Fullscreen QR modal: keep as-is

### Task 2: Wallet.razor.cs — logic

**Current:** Login-gated, server sync required
**New:** Always load localStorage, optional server sync + backup warning

**Key changes:**
- `OnAfterRenderAsync`: always load wallet from localStorage (remove login check)
- `DoClaimAsync(qrPayload, shortCode)`:
  - **Chưa login:** lưu `{sessionId, qrPayload, shortCode, tenantId, claimedAt}` vào localStorage via `vananQrWallet.addSession()` → show backup warning toast → switch tab "Vé của tôi"
  - **Đã login:** gọi API `POST /api/guard/claim` (existing flow) → save wallet → switch tab
- `OnQrDetected(string qrPayload)` → `DoClaimAsync(qrPayload, null)`
- `ClaimByCode()` → `DoClaimAsync(null, _shortCodeInput.Trim())`
- Deep link handler: `/qr/wallet?data={base64}` → auto-claim (move from Claim.razor.cs)
- `LoadWalletAsync`: always load from localStorage; sync server status only if logged in (existing logic at lines 81-84)

**Review fix — localStorage backup warning:**
```razor
@* Show warning toast after saving QR to wallet (anonymous) *@
@if (_showBackupWarning)
{
    <VanAnAlert Variant="AlertVariant.Warning"
                Message="⚠️ Đã lưu vé vào điện thoại. Vui lòng chụp màn hình này để phòng mất vé nếu xóa dữ liệu trình duyệt."
                class="mt-2" />
    <VanAnButton Variant="ButtonVariant.Outline" Text="📸 Chụp màn hình" OnClick="@ScreenshotPrompt" />
}
```

```csharp
private bool _showBackupWarning = false;

private async Task DoClaimAsync(string? qrPayload, string? shortCode)
{
    // ... save to localStorage ...
    if (string.IsNullOrEmpty(_customerToken))
    {
        _showBackupWarning = true; // Show backup warning for anonymous users
    }
    // ... switch tab ...
}

private async Task ScreenshotPrompt()
{
    // JS: trigger screenshot or open fullscreen QR for manual screenshot
    await JS.InvokeVoidAsync("vananQrWallet.promptScreenshot");
}
```

### Task 3: NavMenu.razor — 1 link

**Current:** 2 links (`/qr/claim` in NavMenu, `/qr/wallet` only via GoClaim button)
**New:** 1 link `/qr/wallet`

- Desktop (line 99): `href="/qr/claim"` → `href="/qr/wallet"`
- Mobile (line 222): `href="/qr/claim"` → `href="/qr/wallet"`
- Label: keep "QR gửi xe"

### Task 4: Claim.razor — redirect

**Current:** Full claim page with QRScanner + short code
**New:** Simple redirect to `/qr/wallet`

```razor
@page "/qr/claim"
@inject NavigationManager Nav
@code {
    protected override void OnInitialized()
    {
        // Preserve query string (deep link from QR ticket: /qr/claim?data=...)
        var uri = Nav.Uri.Replace("/qr/claim", "/qr/wallet");
        Nav.NavigateTo(uri, forceLoad: true);
    }
}
```

**Why `forceLoad: true`:** Blazor client-side navigation may not trigger `OnAfterRenderAsync` properly on redirect — force load ensures clean state.

### Task 5: guard-camera.js — OCR plate improvements

**Current:** OCR cả ROI 1 lần với PSM 7, whitelist `0123456789ABCDEFGHKLMNPRSTUVXYZĐ-`
**New:** Tách 2 hàng (with tilt check), PSM 7 từng hàng, whitelist chặt, regex validate, downscale 300px

**Changes in `_ocrRoi`:**
1. **Check tilt/aspect ratio** — nếu ROI lệch góc > 15° → skip 2-row crop, fallback full-ROI OCR
2. Crop hàng trên (nửa trên ROI) → preprocess → OCR PSM 7 → normalize `^\d{2}[A-ZĐ]{1,2}$`
3. Crop hàng dưới (nửa dưới ROI) → preprocess → OCR PSM 7 → normalize `^\d{3,5}(\.\d{2})?$`
4. Ghép: `hàngTrên + '-' + hàngDưới`
5. Nếu 1 hàng fail → fallback OCR cả ROI (PSM 7) như hiện tại

**Review fix — tilt detection:**
```javascript
_detectTilt(canvas) {
    // Simple horizontal projection profile:
    // 1. Convert to grayscale + threshold
    // 2. Project rows → find text row boundaries
    // 3. If row boundaries not horizontal (skewed) → tilt detected
    // Alternative: use Hough line transform (heavier but more accurate)
    // For simplicity: check if top 30% and bottom 30% have similar text density
    // If not → tilted → fallback full-ROI
    const ctx = canvas.getContext('2d');
    const imageData = ctx.getImageData(0, 0, canvas.width, canvas.height);
    // ... projection profile analysis ...
    return tiltDegrees; // 0 if straight, > 0 if tilted
}
```

**Changes in `_preprocessRoiForOcr`:**
- Downscale target: 400px → 300px

**Changes in `preloadOcrWorker`:**
- Whitelist: `0123456789ABCDEFGHKLMNPRSTXYZĐ-.` (bỏ Q, J, U, W, V, I — không có trong biển VN)

**New helper: `_ocrTwoRows(canvas)`:**
```javascript
async _ocrTwoRows(canvas) {
    // Review fix: check tilt before crop
    const tilt = this._detectTilt(canvas);
    if (tilt > 15) {
        console.log('[Scanner] Tilt detected (' + tilt + '°) — fallback to full-ROI OCR');
        return null; // fallback to full ROI OCR
    }
    
    const midY = Math.round(canvas.height * 0.5);
    const topCanvas = this._cropCanvas(canvas, 0, 0, canvas.width, midY);
    const bottomCanvas = this._cropCanvas(canvas, 0, midY, canvas.width, canvas.height - midY);
    
    const topResult = await this._ocrSingleRow(topCanvas, 'top');
    const bottomResult = await this._ocrSingleRow(bottomCanvas, 'bottom');
    
    if (topResult && bottomResult) {
        return { plate: topResult + '-' + bottomResult, confidence: ... };
    }
    return null; // fallback to full ROI OCR
}
```

**New helper: `_cropCanvas(src, sx, sy, sw, sh)`:**
- Create sub-canvas, drawImage with source rect

## Entry Criteria

- [ ] Master plan reviewed + approved by user
- [ ] Current `main` branch clean + build PASS
- [ ] PR #148 (issue #147 fix) merged + deployed

## Exit Criteria — ALL PASSED

- [ ] `dotnet build VanAn.sln` — 0 errors
- [ ] `guard-check.ps1` — ALL PASSED
- [ ] CI pre-push pipeline — ALL PASSED
- [ ] CD Multi-VPS — SUCCESS
- [ ] RV Sprint 1 (8 tests) — ALL PASS
- [ ] PR merged to `main`

## RV Plan (8 tests)

| # | Test | Expected | Layer |
|---|---|---|---|
| 1 | API health: Gateway/ShopERP/KhachLink | 200 | L1 |
| 2 | `/qr/wallet` page load (no login) | 2 tabs visible | L2 |
| 3 | Quét QR vé xe (no login) → save wallet | Success, tab switch | L3 |
| 4 | Tab "Vé của tôi" → tap vé → fullscreen QR | QR canvas renders | L3 |
| 5 | `/qr/claim` → redirect `/qr/wallet` | 302/301 | L2 |
| 6 | NavMenu "QR gửi xe" link → `/qr/wallet` | Navigate | L2 |
| 7 | OCR biển số 2 hàng → result format `##X-#####` | Accuracy improved | L3 |
| 8 | Short code input → save wallet | Success | L3 |

## Verification Gates

| Gate | Result |
|---|---|
| guard-check.ps1 | ⏳ |
| dotnet build | ⏳ |
| Core.Tests | ⏳ |
| Integration.Tests | ⏳ |
| Architecture.Tests | ⏳ |
| CI pre-push | ⏳ |
| CD Multi-VPS | ⏳ |
| RV on VPS | ⏳ |

## Notes

- Sprint 1 ships independently — no dependency on Sprint 2-4
- QR wallet merge không thay đổi backend — guard verify flow không cần session đã "claim"
- OCR improvements chỉ thay đổi `guard-camera.js` — không ảnh hưởng server
- `Claim.razor.cs` có thể delete sau khi redirect hoạt động (giữ file cho backward compat)
