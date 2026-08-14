# TASK CARD — Sprint 3: Guard UI — ShopERP (Issue #126)

> **Status:** 📋 PENDING
> **Priority:** P3 — After Sprint 2 approval
> **Branch:** `feature/guard-qr-verify`
> **Mode:** IMPLEMENT (UI Phase)
> **Domain modification:** NO

## Objective
Rewrite `Scan.cshtml` (100% hardcode) → Blazor component `Scan.razor` + code-behind với:
- Camera QR scan (jsQR)
- Photo capture (plate + customer)
- Issue flow → display QR + short code
- Verify flow → display plate + 2 photos + Match/Mismatch buttons
- Today's sessions list (real data)
- Stats (real data)
- UI Platform components (Gate 5 compliance)

## Prerequisites
- [ ] Sprint 2 complete (API ready)
- [ ] Sprint 0 QR scan library confirmed (jsQR or @zxing/browser)

## Task 1: Delete old hardcode page
- Delete `5_WebApps/ShopERP/Pages/Guard/Scan.cshtml` (100% hardcode, replaced)
- Remove Tailwind CDN dependency (violate UI Platform)

## Task 2: Create Blazor component

### File: `5_WebApps/ShopERP/Components/Pages/Guard/Scan.razor`
- Route: `@page "/guard/scan"`
- `@attribute [Authorize(Roles="Guard")]`
- UI Platform components: `VanAnCard`, `VanAnButton`, `VanAnModal`, `VanAnTable`, `VanAnInput`
- Layout: 3 tabs (Issue | Verify | Today)

### Tab 1: Issue (Cấp QR)
```
[Step 1: Chụp ảnh]
  - "Chụp ảnh biển số" button → mở camera → capture → preview
  - "Chụp ảnh khách" button → mở camera → capture → preview
  - Input: biển số (VanAnInput)
  - Input: SĐT khách (optional)
  - "Tạo QR" button (disabled until both photos + plate)

[Step 2: Display QR]
  - QR image (from API, base64 PNG)
  - 6-digit short code (large text)
  - "In vé" button (Sprint 5)
  - "Quét lại" button (reset)
  - Auto-show fullscreen QR modal (cho khách quét)
```

### Tab 2: Verify (Xác minh lúc lấy xe)
```
[Step 1: Scan]
  - Camera viewfinder → jsQR scan loop
  - "Nhập mã thủ công" fallback (paste QR payload hoặc short code)

[Step 2: Result]
  - If match: hiển thị biển số + 2 ảnh (biển số, chân dung) + issuedAt
  - 2 buttons: "✅ Match — Check-out" | "❌ Mismatch — Flag"
  - If mismatch/voided: error message + "Quét lại"
```

### Tab 3: Today (Hoạt động hôm nay)
```
- Stats cards: Check-in / Check-out / Đang trong bãi (real data từ API)
- Table: sessions list (plate, status, issuedAt, checkedOutAt) — VanAnTable
- Filter by status (dropdown)
- Pagination
- Click row → detail modal (photos, scan logs)
```

### File: `5_WebApps/ShopERP/Components/Pages/Guard/Scan.razor.cs`
- Inject `IGuardService` (via Gateway HTTP client — check existing pattern)
- Or inject `HttpClient` + call Gateway API directly (match existing ShopERP→Gateway pattern)
- State: current tab, issue step, verify step, today sessions, pagination
- Methods: `PresignUpload`, `Issue`, `Verify`, `Checkout`, `Flag`, `LoadTodaySessions`

## Task 3: JS interop for camera + QR scan

### File: `5_WebApps/ShopERP/wwwroot/js/guard-scanner.js`
- `startCamera(videoElementId)` → open getUserMedia
- `capturePhoto(videoElementId, canvasElementId)` → return base64 JPEG
- `startQrScan(videoElementId, callback)` → jsQR loop, call callback on detect
- `stopCamera()` → cleanup

### File: `5_WebApps/ShopERP/wwwroot/lib/jsQR/jsQR.js` (vendored or npm copy)
- Pure JS QR decoder
- MIT license

## Task 4: NavMenu update
- Existing NavMenu already has `/guard/scan` link (Guard role) — verify still works
- No change needed (confirmed in Sprint 0)

## Validation
- [ ] `dotnet build VanAn.sln` 0 errors
- [ ] `guard-check.ps1` ALL PASSED
- [ ] UI Platform compliance check (no Tailwind CDN, no raw HTML/CSS)
- [ ] Manual test: Issue flow (capture photos → create QR → display)
- [ ] Manual test: Verify flow (scan QR → show photos → checkout)
- [ ] Manual test: Today tab (real stats + list, 0 hardcode)
- [ ] Mobile responsive (guard uses phone/tablet)

## Files Modified (expected)
1. `5_WebApps/ShopERP/Pages/Guard/Scan.cshtml` — DELETE
2. `5_WebApps/ShopERP/Components/Pages/Guard/Scan.razor` — NEW
3. `5_WebApps/ShopERP/Components/Pages/Guard/Scan.razor.cs` — NEW
4. `5_WebApps/ShopERP/wwwroot/js/guard-scanner.js` — NEW
5. `5_WebApps/ShopERP/wwwroot/lib/jsQR/jsQR.js` — NEW (vendored)

## Rollback
- Feature flag OFF → redirect `/guard/scan` to old page (keep old page as backup until RV)
- Or: `git revert` UI commit

## Approval Gate
- [ ] Build pass + UI Platform compliance
- [ ] User approval before Sprint 4
