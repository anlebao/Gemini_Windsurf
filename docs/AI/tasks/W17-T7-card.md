# W17-T7 — Verify + E2E Retention Flow

**Wave:** 17 — KhachLink Retention & Loyalty
**Branch:** `feature/wave17-khachlink-retention`
**Priority:** 🔴 CRITICAL — gate bắt buộc trước khi merge Wave 17
**Conflict risk:** NONE — chỉ chạy scripts, không sửa code
**Depends on:** W17-T1 → W17-T6 tất cả complete
**Estimated effort:** 0.5 session

---

## Mục tiêu

Xác nhận toàn bộ retention loop hoạt động end-to-end và không có regression từ Wave 15/16.

---

## Verification Scripts

### Script 1 — Build + Architecture

```powershell
# 1a. Build toàn bộ solution
dotnet build C:\VibeCoding\Gemini_Windsurf\VanAn.sln --no-restore
# Expected: Build succeeded. 0 Error(s)

# 1b. Architecture tests
dotnet test C:\VibeCoding\Gemini_Windsurf\6_Tests\VanAn.Architecture.Tests --no-build --verbosity minimal
# Expected: 7/7 passed

# 1c. Unit tests
dotnet test C:\VibeCoding\Gemini_Windsurf\6_Tests\VanAn.Unit.Tests --no-build --verbosity minimal
# Expected: no regressions từ Wave 17 changes
```

### Script 2 — Dead code đã xóa (Wave 15 regression check)

```powershell
# Không còn files đã xóa ở Wave 15
$deletedFiles = @(
    "5_WebApps\KhachLink\Pages\Index.razor",
    "5_WebApps\KhachLink\Pages\IndexModern.cshtml",
    "5_WebApps\KhachLink\Pages\_Host.cshtml",
    "5_WebApps\KhachLink\Pages\Index.cshtml",
    "5_WebApps\KhachLink\Pages\Index.cshtml.cs",
    "5_WebApps\KhachLink\Components\Pages\Home.razor",
    "5_WebApps\KhachLink\Pages\Dashboard.cshtml"
)
$base = "C:\VibeCoding\Gemini_Windsurf"
$deletedFiles | ForEach-Object {
    $path = Join-Path $base $_
    if (Test-Path $path) { Write-Host "FAIL: $_ còn tồn tại" -ForegroundColor Red }
    else { Write-Host "OK: $_ đã xóa" -ForegroundColor Green }
}
```

### Script 3 — Anti-pattern checks (Wave 16 + 17)

```powershell
$root = "C:\VibeCoding\Gemini_Windsurf\5_WebApps\KhachLink"

# 3a. Không còn Guid.NewGuid() trong product loading
$match = Select-String -Path "$root\Pages\Home.razor" -Pattern "Guid\.NewGuid"
if ($match) { Write-Host "FAIL: Home.razor vẫn còn Guid.NewGuid()" -ForegroundColor Red }
else { Write-Host "OK: Home.razor - no Guid.NewGuid()" -ForegroundColor Green }

# 3b. Không còn async void Dispose
$match = Select-String -Path "$root\Components\PWA\PWAInstallPrompt.razor" -Pattern "async void Dispose"
if ($match) { Write-Host "FAIL: PWAInstallPrompt vẫn còn async void Dispose" -ForegroundColor Red }
else { Write-Host "OK: PWAInstallPrompt - IAsyncDisposable correct" -ForegroundColor Green }

# 3c. Không còn Google Maps dummy key
$match = Select-String -Path "$root\Components\GoogleMaps.razor" -Pattern "DummyKey"
if ($match) { Write-Host "FAIL: GoogleMaps.razor vẫn còn DummyKey" -ForegroundColor Red }
else { Write-Host "OK: GoogleMaps.razor - no DummyKey" -ForegroundColor Green }

# 3d. Không còn "demo-shop" hardcode
$match = Get-ChildItem -Path $root -Recurse -Include "*.razor","*.cs" |
    Select-String -Pattern '"demo-shop"'
if ($match) { Write-Host "FAIL: còn 'demo-shop' hardcode: $($match.Path)" -ForegroundColor Red }
else { Write-Host "OK: no 'demo-shop' hardcode" -ForegroundColor Green }

# 3e. Không còn @inject HttpClient Http (direct inject)
$match = Get-ChildItem -Path $root -Recurse -Include "*.razor" |
    Select-String -Pattern "@inject HttpClient Http"
if ($match) { Write-Host "FAIL: direct HttpClient inject: $($match.Path)" -ForegroundColor Red }
else { Write-Host "OK: no direct HttpClient inject" -ForegroundColor Green }

# 3f. Không còn ISocialCampaignService inject trong KhachLink
$match = Get-ChildItem -Path $root -Recurse -Include "*.cs","*.razor","*.cshtml" |
    Select-String -Pattern "ISocialCampaignService|IOrderWorkflowService" |
    Where-Object { $_.Path -notmatch "Program\.cs" }
if ($match) { Write-Host "FAIL: CoreHub service direct inject: $($match.Path)" -ForegroundColor Red }
else { Write-Host "OK: no CoreHub service direct inject in pages" -ForegroundColor Green }
```

### Script 4 — Gateway endpoints smoke test

```powershell
# Requires: KhachLink (5002), Gateway (5001), ShopERP (5003) đang chạy
$gateway = "http://localhost:5001"
$tenantId = "00000000-0000-0000-0000-000000000001" # default test tenant

# 4a. Products endpoint (Wave 16)
try {
    $r = Invoke-WebRequest "$gateway/api/products?tenantId=$tenantId" -UseBasicParsing
    if ($r.StatusCode -eq 200) { Write-Host "OK: GET /api/products -> 200" -ForegroundColor Green }
} catch { Write-Host "FAIL: GET /api/products -> $($_.Exception.Message)" -ForegroundColor Red }

# 4b. OTP send endpoint (Wave 17 T1)
try {
    $body = @{ phoneNumber="0901234567"; tenantId=$tenantId; deviceId="test-device" } | ConvertTo-Json
    $r = Invoke-WebRequest "$gateway/api/customers/otp/send" -Method POST `
         -Body $body -ContentType "application/json" -UseBasicParsing
    if ($r.StatusCode -eq 200) { Write-Host "OK: POST /api/customers/otp/send -> 200" -ForegroundColor Green }
    # Dev mode: check X-Dev-OTP header
    $devOtp = $r.Headers["X-Dev-OTP"]
    if ($devOtp) { Write-Host "  Dev OTP: $devOtp" -ForegroundColor Cyan }
} catch { Write-Host "FAIL: POST /api/customers/otp/send -> $($_.Exception.Message)" -ForegroundColor Red }

# 4c. Shops endpoint (Wave 17 T5)
try {
    $r = Invoke-WebRequest "$gateway/api/shops?tenantId=$tenantId" -UseBasicParsing
    if ($r.StatusCode -eq 200) { Write-Host "OK: GET /api/shops -> 200" -ForegroundColor Green }
} catch { Write-Host "FAIL: GET /api/shops -> $($_.Exception.Message)" -ForegroundColor Red }

# 4d. Push subscribe endpoint (Wave 17 T4)
try {
    $body = @{ subscriptionJson="{}"; tenantId=$tenantId } | ConvertTo-Json
    $r = Invoke-WebRequest "$gateway/api/notifications/push/subscribe" -Method POST `
         -Body $body -ContentType "application/json" `
         -Headers @{"X-Customer-Token"="invalid-token"} -UseBasicParsing
    # Expected: 401 Unauthorized (valid behavior — no valid token)
    if ($r.StatusCode -in 200,401) { Write-Host "OK: POST /api/notifications/push/subscribe -> $($r.StatusCode)" -ForegroundColor Green }
} catch {
    if ($_.Exception.Response.StatusCode -eq 401) { Write-Host "OK: POST /api/notifications/push/subscribe -> 401 (expected)" -ForegroundColor Green }
    else { Write-Host "FAIL: $($_.Exception.Message)" -ForegroundColor Red }
}
```

### Script 5 — Route contract check

```powershell
$root = "C:\VibeCoding\Gemini_Windsurf\5_WebApps\KhachLink"

# Kiểm tra tất cả @page directives — không còn conflict
$routes = Get-ChildItem -Path $root -Recurse -Include "*.razor" |
    ForEach-Object {
        $file = $_.FullName.Replace($root + "\", "")
        Select-String -Path $_.FullName -Pattern "^@page " |
            ForEach-Object { [PSCustomObject]@{ File=$file; Route=$_.Line.Trim() } }
    }

# Tìm duplicates
$dupes = $routes | Group-Object Route | Where-Object { $_.Count -gt 1 }
if ($dupes) {
    Write-Host "FAIL: Route conflicts found:" -ForegroundColor Red
    $dupes | ForEach-Object { Write-Host "  $($_.Name): $($_.Group.File -join ', ')" -ForegroundColor Red }
} else { Write-Host "OK: No route conflicts" -ForegroundColor Green }

# Verify Wave 17 routes tồn tại
$requiredRoutes = @(
    '@page "/login"',
    '@page "/my-loyalty"',
    '@page "/my-orders"',
    '@page "/stores"',
    '@page "/dashboard"'
)
$allRoutes = $routes.Route
$requiredRoutes | ForEach-Object {
    if ($allRoutes -contains $_) { Write-Host "OK: $_ exists" -ForegroundColor Green }
    else { Write-Host "FAIL: $_ missing" -ForegroundColor Red }
}
```

### Script 6 — NavMenu sanity check

```powershell
$navFile = "C:\VibeCoding\Gemini_Windsurf\5_WebApps\KhachLink\Components\Layout\NavMenu.razor"

# Không còn Counter và Weather links
@("counter", "weather", "VanAnDashboard") | ForEach-Object {
    $match = Select-String -Path $navFile -Pattern "href=`"$_`""
    if ($match) { Write-Host "FAIL: NavMenu vẫn còn link '$_'" -ForegroundColor Red }
    else { Write-Host "OK: NavMenu - no '$_' link" -ForegroundColor Green }
}

# Có đủ 6 retention routes
@("/cart", "/my-orders", "/my-loyalty", "/stores", "/login", "/profile") | ForEach-Object {
    $match = Select-String -Path $navFile -Pattern $_
    if ($match) { Write-Host "OK: NavMenu has '$_'" -ForegroundColor Green }
    else { Write-Host "WARN: NavMenu missing '$_'" -ForegroundColor Yellow }
}
```

---

## Happy Path Manual Checklist

```
[ ] 1. Mở KhachLink tại http://localhost:5002/
        → Home.razor load được danh sách sản phẩm từ API (không phải hardcode)

[ ] 2. Click "Đặt ngay" cho 1 sản phẩm
        → Sản phẩm vào giỏ hàng với ProductId thật

[ ] 3. Vào /cart → thấy sản phẩm đúng
        → Proceed to /checkout

[ ] 4. Checkout → POST /api/orders → nhận orderId thật
        → IdentityUpgradeModal hiện (lần đầu)

[ ] 5. Trong modal: nhập SĐT → nhận OTP
        (dev mode: xem X-Dev-OTP trong browser devtools Network tab)
        → Nhập OTP → customer_token lưu vào localStorage

[ ] 6. Vào /my-loyalty
        → Thấy tier (Bronze), số điểm (100 welcome bonus), lịch sử

[ ] 7. Vào /my-orders
        → Thấy đơn hàng vừa đặt

[ ] 8. Vào /order-tracking/{id}
        → Thấy trạng thái đơn thật từ Gateway

[ ] 9. Vào /stores
        → Thấy danh sách cửa hàng (Google Maps load nếu có API key)

[ ] 10. Refresh trang
        → PWA banner không hiện lại (đã dismiss persist) hoặc hiện lại nếu chưa dismiss
        → Navbar hiển thị "Tài khoản" (logged in state)
```

---

## Entry criteria
- [ ] W17-T1 → W17-T6 tất cả complete
- [ ] `dotnet build VanAn.sln` → 0 errors (chạy trước khi bắt đầu verify)
- [ ] 3 services đang chạy: KhachLink (5002), Gateway (5001), ShopERP (5003)

## Success criteria — TẤT CẢ phải PASS trước khi merge
- [ ] Script 1: Build 0 errors, Architecture tests 7/7 PASS
- [ ] Script 2: 7 deleted files không còn tồn tại
- [ ] Script 3: 6 anti-pattern checks đều PASS
- [ ] Script 4: 4 endpoint smoke tests PASS (hoặc expected status)
- [ ] Script 5: 0 route conflicts, 5 Wave-17 routes tồn tại
- [ ] Script 6: NavMenu sanity PASS
- [ ] Manual checklist: 10/10 steps
