# TASK CARD — Phase 0: Bug 3 Runtime Debug (Tenant HKD "Mở sổ" không hoạt động)

> **Status:** ✅ COMPLETE — root cause identified (H3 variant: sync-over-async deadlock), fix applied (Option A: Task.Run wrapper), committed, pushed, VPS verification pending
> **Prerequisite:** Local dev environment running (ShopERP 5003 + Gateway 5001 + PostgreSQL + NATS)
> **Branch:** `feature/tenant-fix-phase0-bug3-debug`
> **Estimated sessions:** 1 (debug only) + 1 (fix after root cause identified)
> **Mode:** ANALYZE → FIX_ONLY (after root cause)

## Objective
Xác định root cause của Bug 3: tenant HKD bấm "📖 Mở sổ" tại `/accounting/hkd-books` nhưng không hoạt động (không navigate, không load trang detail, hoặc trang trắng).

Code path tĩnh đã verify CORRECT — cần runtime evidence để xác định 1 trong 4 hypotheses.

## Prerequisites
- [ ] Local dev environment running:
  - PostgreSQL (Docker, port 5432)
  - NATS (Docker, port 4222)
  - Gateway (5001)
  - ShopERP (5003)
- [ ] Tenant HKD test data tồn tại (BusinessType=HouseholdBusiness, HKDGroup=Group1/2/3)
- [ ] User có quyền OwnerOnly cho tenant HKD đó
- [ ] Browser Chrome/Edge với DevTools

## Hypotheses (4 — phải xác định 1)

### H1: Blazor interactivity Category C — button click không fire
**Symptom:** Bấm button không có phản ứng, không có SignalR message gửi đi
**Diagnostic:**
- DevTools → Network → WS tab: kiểm tra WebSocket connection có alive không
- Bấm button → quan sát có message gửi đi trên WS không
- DevTools → Console: có error "WebAssembly/Server disconnected" không?

**If confirmed → Root cause:** SignalR connection drop hoặc VanAButton render issue
**Fix direction:** Deep clean bin/obj + restart, hoặc fix VanAButton OnClick binding

### H2: Navigation fail — URL không đổi
**Symptom:** Click có fire (WS message gửi) nhưng URL không đổi
**Diagnostic:**
- DevTools → Console: có error "NavigationManager" không?
- Kiểm tra `OpenBook` method có throw không (try/catch swallow)?

**If confirmed → Root cause:** NavigationManager exception hoặc route conflict
**Fix direction:** Add try/catch + logging trong OpenBook, kiểm tra route table

### H3: `GenerateBookAsync` runtime exception
**Symptom:** URL đổi thành `/accounting/hkd-books/{code}` nhưng trang hiện errorMessage hoặc loading forever
**Diagnostic:**
- Trang load → quan sát nội dung: có `errorMessage` text không? (xem `HKDBookDetail.razor:35-38`)
- DevTools → Network: có HTTP 500 không?
- ShopERP logs: có exception từ `HKDBookGenerationService.GenerateBookAsync` không?

**If confirmed → Root cause:** Service throw (tenant not found, HKDGroup null, template code invalid, journal entries query fail)
**Fix direction:** Fix service exception (likely Pattern #1 hoặc #5 — `EF.Property<Guid>` hoặc `e.Period.Year`)

### H4: Layout/Authorize block — trang trắng
**Symptom:** URL đổi nhưng trang trắng (không render gì)
**Diagnostic:**
- DevTools → Console: có authorize error không?
- Kiểm tra `AccountingLayout.razor:58-67` — `_shouldRedirect` có trigger không (SystemAdmin without tenant)?
- Kiểm tra `OwnerOnly` policy — user có tenant_id claim không?

**If confirmed → Root cause:** SystemAdmin impersonation chưa set tenant_id, hoặc policy block
**Fix direction:** Fix impersonation flow hoặc policy check

## Debug Procedure

### Step 1: Setup
```powershell
# Start local dev
cd C:\VibeCoding\Gemini_Windsurf
.\scripts\run-dev.ps1  # or individual: dotnet run --project 5_WebApps/ShopERP
```

### Step 2: Login as tenant HKD owner
- Mở `http://localhost:5003` (hoặc qua Gateway 5001)
- Login với tenant HKD owner account
- Verify: `/accounting` page load thành công (menu có "Sổ HKD")

### Step 3: Navigate to HKD Books list
- Vào `/accounting/hkd-books`
- Verify: trang load, hiện danh sách templates (nếu không có template → Bug khác: tenant HKDGroup null)

### Step 4: Open DevTools + click "Mở sổ"
- F12 → Console + Network tabs
- Click "📖 Mở sổ" trên 1 template bất kỳ
- **CAPTURE:**
  - Screenshot Console (tất cả errors/warnings)
  - Screenshot Network (WS messages + HTTP requests)
  - URL bar sau khi click
  - Nội dung trang sau khi click (blank / error / loading / content)

### Step 5: Report findings
Gửi captures cho Devin → xác định hypothesis → lập fix plan

## Files to Investigate (read-only, NO changes trong Phase 0)
| File | Line | Purpose |
|------|------|---------|
| `5_WebApps/ShopERP/Components/Pages/Accounting/HKDBooks.razor` | 74-76, 122-125 | Button + OpenBook method |
| `5_WebApps/ShopERP/Components/Pages/Accounting/HKDBookDetail.razor` | 1-4, 125-166 | Route + OnInitializedAsync + GenerateBook |
| `5_WebApps/ShopERP/Components/Pages/Accounting/AccountingLayout.razor` | 51-67 | _shouldRedirect logic |
| `3_CoreHub/Services/Template/HKDBookGenerationService.cs` | 30-69 | GenerateBookAsync (check exception) |
| `5_WebApps/ShopERP/Program.cs` | (grep "OwnerOnly") | Authorize policy config |

## Verification
- [x] User chạy debug procedure Steps 1-4
- [x] Captures gửi cho Devin (Console + Network + URL + page content)
- [x] Devin xác định 1 trong 4 hypotheses (H1-H4) → **H3 CONFIRMED** (variant: sync-over-async deadlock, not throw)
- [x] Lập fix plan riêng (inline in this task card — Option A applied)

## Root Cause (CONFIRMED 2026-08-03)
**Hypothesis H3 confirmed (variant: HANG, not throw).** `ScopedDataProvider.cs:86,120` called `GetPreAggregatedDataAsync(context).GetAwaiter().GetResult()` — sync-over-async blocked the Blazor Server single-threaded sync context. The async chain (`GetPreAggregatedDataAsync` → `GetAccountAggregatesAsync` → `GetAccountSumAsync` → `ToListAsync()`) awaits without `ConfigureAwait(false)`, so its continuation could not resume → infinite hang.

**Server log evidence:**
- 17:50:59 — GET `/accounting/hkd-books/S1a_HKD`, "Generating HKD book...", "Starting smart pre-aggregation...", "Extracted 2 unique account patterns", AccountingEntries SQL executed (7ms)
- 17:51:27 — Blazor circuit died (61s timeout)
- 17:51:31 — New circuit reconnected
- No "HKD book generated successfully" log, no exception, no ERR — request hung silently

## Fix Applied (Option A — quick, minimal)
Wrapped both sync-over-async calls in `Task.Run(() => GetPreAggregatedDataAsync(context)).GetAwaiter().GetResult()`:
- `ScopedDataProvider.cs:86` (GetAccountSum)
- `ScopedDataProvider.cs:126` (GetAccountSum with IndustrySector)

`Task.Run` offloads the async chain to the thread pool (no sync context), letting the `ToListAsync()` continuation complete and `.GetResult()` unblock.

**Tech debt:** TD-ASYNCDP-001 logged — proper fix (Option B) is to make `IFormulaEngine.Evaluate` + `IDataProvider.GetAccountSum` async-native (`EvaluateAsync`/`GetAccountSumAsync`), eliminating sync-over-async entirely. Large interface change, deferred.

## Rollback
- Phase 0 là debug only — không có code change → không cần rollback
- Nếu có diagnostic logging tạm: revert sau khi root cause xác định

## Output
- 1 trong 4 hypotheses confirmed (H1/H2/H3/H4)
- Root cause specific (file:line + exception type + message)
- Fix plan (file changes + test)
