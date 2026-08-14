# TASK CARD — Sprint 4: KhachLink Claim + QR Wallet (Issue #126)

> **Status:** ✅ COMPLETE (PR #128 merged `08f8ff60` 2026-08-15)
> **Priority:** P4 — After Sprint 2 approval (parallel with Sprint 3)
> **Branch:** `feature/guard-qr-r2-sprint4` → merged to `main`
> **Mode:** IMPLEMENT (UI Phase)
> **Domain modification:** NO

## Objective
KhachLink (Blazor WASM) 2 trang mới:
1. `/qr/claim` — khách nhận QR gửi xe (camera scan hoặc 6-digit code)
2. `/qr/wallet` — "Ví QR" — list claimed QR, tap to show fullscreen cho guard quét

## Prerequisites
- [ ] Sprint 2 complete (API `/api/guard/claim` ready)
- [ ] Sprint 0 QR scan library confirmed

## Task 1: /qr/claim page

### File: `5_WebApps/KhachLink/Components/Pages/Qr/Claim.razor`
- Route: `@page "/qr/claim"`
- 2 modes (toggle):
  - **Camera mode:** Camera viewfinder → jsQR scan QR
    - Quét QR trên màn hình Guard (Channel A — primary)
    - **Quét QR trên giấy vé paper ticket (Channel C → A migration — khách rảnh sau, muốn số hóa vé)**
    - Cùng 1 flow — jsQR không phân biệt nguồn QR, chỉ decode payload → POST /api/guard/claim
  - **Code mode:** Input 6-digit short code (fallback khi không camera)

### Flow:
```
[Camera mode — áp dụng cho cả Channel A và Channel C→A]
  1. Mở camera → jsQR loop
  2. Detect QR payload → POST /api/guard/claim {qrPayload, customerId}
  3. Success → toast "Đã nhận QR gửi xe" → navigate to /qr/wallet
     - Khách Channel A: QR từ màn hình Guard → claim ngay tại bãi
     - Khách Channel C→A: QR từ giấy vé → claim trễ tại nhà/bất cứ đâu
  4. Error handling:
     - "Vé đã được nhận" (already claimed by another customer) → toast error
     - "Vé đã sử dụng" (session CheckedOut — khách đã lấy xe) → toast info
     - "Vé đã hết hạn" (session Voided) → toast error
     - "Không tìm thấy vé" (unknown QR) → toast error → stay

[Code mode]
  1. Input 6 digits → POST /api/guard/claim {shortCode, customerId}
  2. Same success/error handling (áp dụng cho cả A, B, C→A)
```

### Channel C → A migration UX:
- **Không cần UI riêng** — khách Channel C chỉ cần mở /qr/claim → camera mode → quét QR trên giấy vé
- **Hint text trên Claim page:** "Bạn có vé giấy? Quét mã QR trên vé để lưu vào điện thoại — chống mất, ướt, rách"
- **Sau khi claim thành công (C→A):** toast "Đã lưu vé vào điện thoại. Vé giấy không cần nữa."
- **Paper ticket vẫn dùng được** cho guard quét trực tiếp (nếu khách chưa kịp C→A) — cùng QrToken
- **Edge case:** Khách claim C→A nhưng giấy rách trước lúc lấy xe → dùng QR trong Ví QR (KhachLink) → guard quét bình thường

### Auth:
- Customer JWT (existing KhachLink auth)
- If not logged in → redirect to login → return to /qr/claim

## Task 2: /qr/wallet page

### File: `5_WebApps/KhachLink/Components/Pages/Qr/Wallet.razor`
- Route: `@page "/qr/wallet"`
- List claimed QR sessions (active only — Status=Claimed, not CheckedOut)
- API: `GET /api/guard/my-sessions` (new endpoint — customer-scoped, or reuse claim result)

### Each QR card:
```
┌─────────────────────┐
│ 🚗 30A-12345        │
│ Vào: 14:30 14/08    │
│ [Hiển thị QR]       │ ← tap → fullscreen QR modal
└─────────────────────┘
```

### Fullscreen QR modal:
- Show QR large (fill screen)
- Brightness max (JS interop)
- "Đóng" button (top corner)
- Guard quét QR này → verify → checkout

## Task 3: API client

### File: `5_WebApps/KhachLink/Services/GuardQrApiClient.cs`
- `ClaimAsync(ClaimRequest)` → ClaimResult
- `GetMySessionsAsync()` → List<SessionSummary>
- HttpClient → Gateway (existing pattern, check `GatewayApiClientBase` or similar)

### New Gateway endpoint needed:
- `GET /api/guard/my-sessions` — customer-scoped (from JWT), return claimed + not checked out sessions
- Add to GuardController (Sprint 2 extension or quick add here)

## Task 4: JS interop (camera + QR scan)

### File: `5_WebApps/KhachLink/wwwroot/js/qr-claim.js`
- Same pattern as guard-scanner.js (startCamera, startQrScan, stopCamera)
- Reuse jsQR lib (vendored to KhachLink wwwroot/lib/jsQR/)

## Task 5: NavMenu / Bottom nav update
- Add "QR gửi xe" link to KhachLink nav (check existing nav structure)
- Icon: `bi-qr-code` or similar

## Task 6: PWA offline consideration
- QR wallet should work offline (claimed QR cached in IndexedDB)
- When guard scans → verify goes to Gateway (online required)
- If offline: show cached QR (customer can still show to guard, guard verifies online)
- Document: PWA cache strategy for QR wallet (add to service-worker.js cache list)

## Validation
- [ ] `dotnet build VanAn.sln` 0 errors
- [ ] `guard-check.ps1` ALL PASSED
- [ ] UI Platform compliance (KhachLink uses UI Platform components)
- [ ] Manual test: Claim via camera (scan QR from another screen)
- [ ] Manual test: Claim via 6-digit code
- [ ] Manual test: Wallet shows claimed QR
- [ ] Manual test: Fullscreen QR display
- [ ] Mobile responsive (KhachLink is mobile-first PWA)

## Files Modified (expected)
1. `5_WebApps/KhachLink/Components/Pages/Qr/Claim.razor` — NEW
2. `5_WebApps/KhachLink/Components/Pages/Qr/Claim.razor.cs` — NEW
3. `5_WebApps/KhachLink/Components/Pages/Qr/Wallet.razor` — NEW
4. `5_WebApps/KhachLink/Components/Pages/Qr/Wallet.razor.cs` — NEW
5. `5_WebApps/KhachLink/Services/GuardQrApiClient.cs` — NEW
6. `5_WebApps/KhachLink/wwwroot/js/qr-claim.js` — NEW
7. `5_WebApps/KhachLink/wwwroot/lib/jsQR/jsQR.js` — NEW (vendored)
8. `2_Gateway/Controllers/GuardController.cs` — add `GET /api/guard/my-sessions`
9. KhachLink nav menu — add QR link

## Rollback
- Remove 2 pages + nav link
- `git revert` commit

## Approval Gate
- [ ] Build pass + UI Platform compliance
- [ ] User approval before Sprint 5
