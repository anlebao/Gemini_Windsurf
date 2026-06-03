# Fix SignalR Circuit Disconnect - Detailed Coding Plan

## Problem Summary

**Issue:** Playwright test fails because Blazor Server SignalR circuit disconnects before button click event is processed.

**Root Cause:**
1. Program.cs only registers InteractiveServer, missing InteractiveWebAssembly support
2. No SignalR timeout configuration (uses defaults)
3. Circuit disconnects after ~18 seconds, preventing @onclick handlers from firing

**Solution:** Combine Option 4 (infrastructure fix) + Option 3 (InteractiveAuto rendermode)

---

## Implementation Plan

### Phase 1: Infrastructure Fixes (Option 4)

#### Step 1.1: Add InteractiveWebAssembly Support

**File:** `5_WebApps/ShopERP/Program.cs`
**Line:** 45
**Change:**

```csharp
// BEFORE:
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// AFTER:
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents()
    .AddInteractiveWebAssemblyComponents();
```

**Rationale:** Required to enable InteractiveAuto rendermode fallback to WebAssembly.

---

#### Step 1.2: Register WebAssembly RenderMode

**File:** `5_WebApps/ShopERP/Program.cs`
**Line:** 213
**Change:**

```csharp
// BEFORE:
app.MapRazorComponents<VanAn.ShopERP.Components.App>()
    .AddInteractiveServerRenderMode();

// AFTER:
app.MapRazorComponents<VanAn.ShopERP.Components.App>()
    .AddInteractiveServerRenderMode()
    .AddInteractiveWebAssemblyRenderMode();
```

**Rationale:** Maps WebAssembly rendermode to the application routing.

---

#### Step 1.3: Add SignalR Timeout Configuration

**File:** `5_WebApps/ShopERP/Program.cs`
**Location:** After line 45 (after AddRazorComponents)
**Add:**

```csharp
// Add SignalR timeout configuration to prevent circuit disconnect
builder.Services.AddServerSideBlazor()
    .AddHubOptions(options =>
    {
        options.KeepAliveInterval = TimeSpan.FromSeconds(30);
        options.ClientTimeoutInterval = TimeSpan.FromSeconds(60);
        options.HandshakeTimeout = TimeSpan.FromSeconds(15);
    });
```

**Rationale:** Increases timeout values to prevent premature circuit disconnect during test execution.

---

### Phase 2: Apply InteractiveAuto to ExpenseEntry (Option 3)

#### Step 2.1: Change RenderMode

**File:** `5_WebApps/ShopERP/Components/Pages/Accounting/ExpenseEntry.razor`
**Line:** 2
**Change:**

```razor
// BEFORE:
@rendermode InteractiveServer

// AFTER:
@rendermode InteractiveAuto
```

**Rationale:** InteractiveAuto automatically falls back to WebAssembly if Server fails, providing resilience.

---

### Phase 3: Verification

#### Step 3.1: Rebuild and Restart Server

```powershell
# Stop existing server
netstat -ano | findstr :5003
taskkill /F /PID <PID>

# Rebuild and start
cd 5_WebApps\ShopERP
dotnet run
```

#### Step 3.2: Run Playwright Test

```powershell
cd 6_Testing
npx playwright test e2e-tests/expense-entry-flow.spec.ts -g "should create expense entry with vendor info" --project=e2e-tests
```

#### Step 3.3: Verify Server Logs

Look for:
- `DynamicForm HandleSubmit ENTERED` - confirms event handler fires
- No circuit disconnect errors
- Successful expense entry creation

---

### Phase 4: Incremental Rollout (If Successful)

#### Step 4.1: Identify Other InteractiveServer Pages

Search for all files with `@rendermode InteractiveServer`:

```powershell
cd 5_WebApps\ShopERP
Get-ChildItem -Recurse -Filter "*.razor" | Select-String "InteractiveServer"
```

#### Step 4.2: Apply InteractiveAuto Incrementally

For each page found:
1. Change `@rendermode InteractiveServer` → `@rendermode InteractiveAuto`
2. Test the specific page
3. Verify no regression
4. Move to next page

**Priority Order:**
1. Accounting pages (RevenueEntry, BalanceDashboard)
2. Order management pages
3. Dashboard pages
4. Other feature pages

---

## Reverse Impact Analysis

### Low Risk Changes
- **SignalR timeout config:** Only affects timeout values, no logic change
- **AddInteractiveWebAssemblyComponents:** Only adds capability, doesn't break existing Server mode

### Medium Risk Changes
- **ExpenseEntry rendermode change:** 
  - Initial load may be slower (WASM bundle download)
  - Need to verify all functionality still works
  - Test both Server and WASM fallback paths

### High Risk Changes (Phase 4)
- **Rolling out InteractiveAuto to all pages:**
  - Requires full E2E test suite
  - May uncover WASM-specific bugs
  - Bundle size impact on performance

---

## Rollback Plan

If issues occur:

### Immediate Rollback (Phase 1-2)
1. Revert Program.cs changes (lines 45, 213, and SignalR config)
2. Revert ExpenseEntry.razor line 2 to `@rendermode InteractiveServer`
3. Restart server

### Partial Rollback (Phase 4)
1. Revert only the pages causing issues
2. Keep InteractiveAuto on successfully migrated pages
3. Document which pages work and which don't

---

## Success Criteria

1. ✅ Playwright test `should create expense entry with vendor info` passes
2. ✅ Server logs show `DynamicForm HandleSubmit ENTERED`
3. ✅ No circuit disconnect errors in logs
4. ✅ Expense entry successfully created in database
5. ✅ VanAAlert success message appears in UI

---

## Estimated Time

- Phase 1 (Infrastructure): 15 minutes
- Phase 2 (ExpenseEntry): 5 minutes
- Phase 3 (Verification): 10 minutes
- Phase 4 (Rollout): 30-60 minutes (if successful)

**Total: 30-90 minutes**

---

## Dependencies

- .NET 8.0 SDK (already installed)
- Blazor WebAssembly support (Microsoft.AspNetCore.Components.WebAssembly)
- No external dependencies required

---

## Notes

- InteractiveAuto requires .NET 8.0+
- WASM bundle size: ~2-5MB (acceptable for this use case)
- SignalR timeout values are conservative - can be adjusted if needed
- Consider monitoring circuit disconnect rates in production after deployment
