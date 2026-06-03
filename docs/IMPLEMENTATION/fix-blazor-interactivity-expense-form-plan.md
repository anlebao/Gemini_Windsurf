# Fix Blazor Interactivity Expense Form Plan

## Status

**Draft for review only. Do not implement until approved.**

## Objective

Fix the Expense Entry form issue where `DynamicForm.HandleSubmit` is not triggered and the success alert is not rendered after Playwright clicks `Lưu Chi Phí`.

This plan intentionally keeps the application on `InteractiveServer` and avoids introducing `InteractiveAuto` or Blazor WebAssembly until the current app architecture supports it safely.

## Current Evidence

Observed behavior:

- Expense page renders successfully.
- Button `Lưu Chi Phí` renders successfully.
- Playwright can find and click the button.
- Server logs show `DynamicForm OnInitialized`, `OnParametersSet`, and `OnAfterRender`.
- Server logs do not show `DynamicForm HandleSubmit ENTERED`.
- Previous `InteractiveAuto` attempt caused missing WASM asset errors such as `/_framework/dotnet.js 404`.

Current conclusion:

- The issue is not proven to be a simple button rendering issue.
- The issue is also not proven to be safely solved by `InteractiveAuto`.
- The next fix must focus on validating Blazor Server interactivity and form event delivery.

## Non-Goals

Do not do these in this fix:

- Do not migrate the app to Blazor WebAssembly.
- Do not use `InteractiveAuto`.
- Do not add a new WASM client project.
- Do not rewrite Playwright assertions to hide the failure.
- Do not broadly refactor UI Platform components.
- Do not roll out changes to unrelated pages.

## Phase 1: Roll Back Unsafe WASM Changes

### Files to inspect and restore

#### `5_WebApps/ShopERP/Program.cs`

Expected server-only configuration:

```csharp
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();
```

Endpoint mapping should remain server-only:

```csharp
app.MapRazorComponents<VanAn.ShopERP.Components.App>()
    .AddInteractiveServerRenderMode();
```

Remove if present:

```csharp
.AddInteractiveWebAssemblyComponents()
.AddInteractiveWebAssemblyRenderMode()
```

#### `5_WebApps/ShopERP/VanAn.ShopERP.csproj`

Remove if present:

```xml
<PackageReference Include="Microsoft.AspNetCore.Components.WebAssembly.Server" />
```

#### `Directory.Packages.props`

Remove if present:

```xml
<PackageVersion Include="Microsoft.AspNetCore.Components.WebAssembly.Server" Version="8.0.8" />
```

#### `5_WebApps/ShopERP/Components/Pages/Accounting/ExpenseEntry.razor`

Expected:

```razor
@rendermode InteractiveServer
```

## Phase 2: Restore Canonical Blazor Server Form Handling

### File

`UI.Platform/Components/Composite/DynamicForm.razor`

### Expected form tag

```razor
<form @onsubmit="HandleSubmit" @onsubmit:preventDefault class="@GetFormClasses()" @attributes="GetMergedAttributes()">
```

### Expected submit button

```razor
<button type="submit" class="btn btn-primary">
    @SubmitText
</button>
```

### Expected handler behavior

`HandleSubmit` must log at entry before any guard logic:

```csharp
Logger.LogInformation("DynamicForm HandleSubmit ENTERED - FieldDefinitions count: {Count}", FieldDefinitions?.Count ?? 0);
```

Do not use direct button `@onclick` as the primary solution unless diagnostics prove form submit cannot be used in this component.

## Phase 3: Add Minimal Server-Side Interactivity Diagnostics

Add temporary diagnostics only if needed and remove or downgrade them after validation.

Target signals:

- Page is interactive before Playwright clicks.
- No browser console error blocks Blazor.
- SignalR circuit remains connected during interaction.
- `HandleSubmit` is entered or not entered with clear timing.

Recommended diagnostics:

- Log `DynamicForm.OnAfterRender` first render.
- Log `ExpenseEntry.OnAfterRender` first render if available.
- Capture browser console errors from Playwright artifacts.
- Inspect server logs around `_blazor/negotiate` and `_blazor?id=...`.

Important note:

A WebSocket `101` request ending after the test fails does not by itself prove SignalR timeout is root cause. It may be caused by Playwright closing the page/context.

## Phase 4: Optional Low-Risk Blazor Server Hardening

Only add this if build-compatible and approved.

Preferred approach:

```csharp
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents(options =>
    {
        options.DisconnectedCircuitRetentionPeriod = TimeSpan.FromMinutes(3);
        options.JSInteropDefaultCallTimeout = TimeSpan.FromSeconds(60);
    });
```

Avoid duplicate or conflicting Blazor Server registrations unless verified.

## Phase 5: Verification Scope

Run only the classified failing spec once after implementation:

```powershell
cd 6_Testing
npx playwright test e2e-tests/expense-entry-flow.spec.ts -g "should create expense entry with vendor info" --project=e2e-tests
```

Verify:

- `DynamicForm HandleSubmit ENTERED` appears in server logs.
- `ExpenseEntry.HandleSubmit` runs.
- Success alert `Đã lưu chi phí thành công!` appears.
- Expense entry appears in history or database-backed state.

If the test still fails:

- Do not rerun repeatedly.
- Read screenshot, video, trace, console logs, and server logs.
- Classify whether the failure is interactivity, validation, service/database, or selector contract.

## Reverse Impact Analysis

### Low Risk

- Restoring `InteractiveServer`.
- Removing incomplete WASM support.
- Restoring canonical form submit.
- Adding temporary diagnostics.

### Medium Risk

- Changing Blazor Server circuit options.
- Removing `PreventDoubleClick` from submit flow if still present.

### High Risk

- Introducing `InteractiveAuto` without full WASM setup.
- Adding a WASM client project.
- Broadly changing UI Platform form behavior across many consumers.

## Rollback Plan

If the scoped fix causes regression:

1. Revert `DynamicForm.razor` to previous known-good rendering shape.
2. Revert `Program.cs` Blazor options.
3. Keep `ExpenseEntry.razor` on `InteractiveServer`.
4. Re-run only a smoke page load before deeper testing.

## Success Criteria

- Build succeeds.
- Server starts on `http://localhost:5003`.
- Single targeted Playwright spec passes.
- Server logs prove `DynamicForm.HandleSubmit` was entered.
- No WASM asset 404 errors are introduced.
- No unrelated pages are modified.
