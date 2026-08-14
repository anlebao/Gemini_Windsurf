# TASK CARD — Sprint 5: Printer Integration (Issue #126)

> **Status:** ✅ COMPLETE (2026-08-14)
> **Priority:** P5 — After Sprint 3 approval (Guard UI)
> **Branch:** `feature/guard-qr-r1-sprint5`
> **Mode:** IMPLEMENT (UI Phase)
> **Domain modification:** NO

## Objective
Guard app "In vé" button → `PrintTicket.razor` page (reuse `PrintBill.razor` pattern) → `window.print()` → browser print dialog → thermal printer.

## Prerequisites
- [x] Sprint 3 complete (Guard UI Issue tab has "In vé" button placeholder)
- [x] Reference: `Components/Pages/Orders/PrintBill.razor` (existing print pattern)

## Approach: REUSE PrintBill.razor pattern

**Existing pattern (PrintBill.razor):**
- Route: `@page "/orders/{OrderId:guid}/print"`
- `window.vananPrintBill()` → `window.print()` (defined in `App.razor:51`)
- `@@media print` CSS — hides everything except `.bill-page`
- Auto-trigger print on page load (`OnAfterRenderAsync`)
- `VanAButton` for manual print + back buttons
- Opens in new tab (`forceLoad: true` from order list)

**New: PrintTicket.razor (same pattern, guard ticket data)**

### File: `5_WebApps/ShopERP/Components/Pages/Guard/PrintTicket.razor`
- Route: `@page "/guard/print/{SessionId:guid}"`
- `@attribute [Authorize(Roles="Guard")]`
- `@rendermode InteractiveServer`
- Same structure as PrintBill.razor:
  - `.ticket-page` (instead of `.bill-page`)
  - `.ticket-receipt` (instead of `.bill-receipt`)
  - `@@media print` — hide everything except `.ticket-page`
  - Auto-trigger `window.print()` on load
  - `VanAButton` "In vé" + "Quay lại"

### Ticket content:
```
        TENANT NAME (bold, large — from session data)
        ━━━━━━━━━━━━━━━━━━
        Biển số: 30A-12345
        Giờ vào: 14:30
        Ngày: 14/08/2026
        Mã vé: ABC123 (6-digit short code)
        ━━━━━━━━━━━━━━━━━━
        [QR code image — rendered from QrPayload]
        ━━━━━━━━━━━━━━━━━━
        Vạn An - Guard Scanner
        In lúc: 14:30 14/08/2026
```

### QR code rendering on ticket:
- Use existing `qrcode.js` (vendored from KhachLink in Sprint 3)
- Render QR to canvas → convert to `<img>` for print (canvas không in tốt trên một số browser)
- QR size: 200x200px (fits 58mm thermal printer, 384 dots width)

### Code-behind: `PrintTicket.razor.cs`
- Inject `IGuardService` (or HttpClient to Gateway)
- `OnInitializedAsync` → fetch session by SessionId → get QrPayload + plate + time + tenant name
- `OnAfterRenderAsync` → auto-trigger `window.print()`

## Task 2: JS function (reuse or alias)

**Option A (simplest):** Reuse `window.vananPrintBill` — already defined in `App.razor:51`, just call it from PrintTicket.razor.

**Option B (cleaner):** Add alias in `App.razor`:
```javascript
window.vananPrintTicket = function () { window.print(); };
```

**Decision:** Option A — reuse `vananPrintBill`. Function name khác nhưng logic identical (`window.print()`).

## Task 3: UI integration (Guard Scan.razor)

### In Issue tab Step 2 (Display QR):
- "In vé" button → `NavigationManager.NavigateTo($"/guard/print/{sessionId}", forceLoad: true)`
- `forceLoad: true` → opens in new tab (same as PrintBill pattern)
- User prints → closes tab → returns to Guard Scanner

## Task 4: Print CSS optimization

Copy `@@media print` from PrintBill.razor, adapt for `.ticket-page`:
```css
@@media print {
    body * { visibility: hidden; }
    .ticket-page, .ticket-page * { visibility: visible; }
    .ticket-page {
        position: absolute;
        left: 0;
        top: 0;
        max-width: 100%;
        padding: 0.5rem;
    }
    .no-print { display: none !important; }
    .ticket-receipt { font-size: 0.8rem; }
}
```

## Validation
- [ ] `dotnet build VanAn.sln` 0 errors
- [ ] `guard-check.ps1` ALL PASSED
- [ ] Manual test: Issue QR → "In vé" → new tab opens → print dialog auto-trigger
- [ ] Manual test: Print to thermal printer (or Save as PDF) → verify content (tenant, plate, time, QR)
- [ ] Manual test: Scan printed QR with Guard app → verify works (Channel C flow)
- [ ] UI Platform components (VanAButton)

## Files Modified (expected)
1. `5_WebApps/ShopERP/Components/Pages/Guard/PrintTicket.razor` — NEW
2. `5_WebApps/ShopERP/Components/Pages/Guard/PrintTicket.razor.cs` — NEW
3. `5_WebApps/ShopERP/Components/Pages/Guard/Scan.razor` — add "In vé" button (if not done in Sprint 3)

## Rollback
- Hide "In vé" button (feature flag or simple UI toggle)
- `git revert` commit

## Approval Gate
- [ ] Build pass + manual print test pass
- [ ] User approval before Sprint 6
