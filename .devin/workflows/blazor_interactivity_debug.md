# Blazor Server Interactivity Debug Workflow

> **Purpose:** Debug and fix Blazor Server interactivity issues where event handlers (OnSubmit, OnClick) are not triggered.

## Prerequisites
- Blazor Server application with InteractiveServer render mode
- Playwright E2E test failing due to event handler not executing
- Server logging enabled (EnableFileLogging: true)

## Step 1 — Collect Diagnostic Information

### 1.1 Check Server Logs
```bash
Get-Content <app>/bin/Debug/net8.0/Logs/<log-file>.txt -Tail 50
```

Look for:
- `OnInitialized` logs - confirms component is rendering
- `OnParametersSet` logs - confirms parameters are set
- `OnAfterRender` logs - **CRITICAL** - if missing, prerendering is blocking
- Event handler logs (e.g., `HandleSubmit ENTERED`) - confirms handler execution
- Native GET requests (e.g., `GET /accounting/expenses?`) - indicates native form submit

### 1.2 Check Component Lifecycle
Add diagnostic logs to component:
```csharp
protected override void OnInitialized()
{
    Logger.LogInformation("{Component} OnInitialized");
}

protected override void OnParametersSet()
{
    Logger.LogInformation("{Component} OnParametersSet");
}

protected override void OnAfterRender(bool firstRender)
{
    Logger.LogInformation("{Component} OnAfterRender - firstRender: {FirstRender}", firstRender);
}
```

### 1.3 Check Event Handler
Add logging to event handler:
```csharp
private async Task HandleSubmit()
{
    Logger.LogInformation("{Component} HandleSubmit ENTERED");
    // ... handler logic
}
```

STOP — Analyze logs before proceeding.

## Step 2 — Classify Issue

Based on logs, classify into one of:

### Category A: OnAfterRender Not Called
**Symptoms:**
- `OnInitialized` and `OnParametersSet` present
- `OnAfterRender` ABSENT
- Component renders but interactive state never updates

**Root Cause:** Prerendering blocking - component rendered in static SSR mode

**Solution:** Force interactive rendering
```razor
<!-- Option 1: Add rendermode to component instance -->
<DynamicForm @rendermode="new InteractiveServerRenderMode(prerender: false)" />

<!-- Option 2: Ensure parent has @rendermode InteractiveServer -->
@page "/route"
@rendermode InteractiveServer
```

### Category B: Native Form Submit Occurs
**Symptoms:**
- `OnAfterRender` present
- Native GET request in logs (e.g., `GET /accounting/expenses?`)
- Event handler logs ABSENT
- Page reloads

**Root Cause:** Button type="submit" triggers native form before Blazor @onsubmit:preventDefault binds via SignalR

**Solution:** Use inline JavaScript hardening for synchronous client-side defense
```razor
<!-- Add inline JS to block native submit at browser level (0ms synchronous) -->
<form onsubmit="event.preventDefault(); return false;" @onsubmit="OnFormSubmit" @onsubmit:preventDefault>
    <!-- form content -->
    <button type="submit">Submit</button>
</form>

@code {
    private async Task OnFormSubmit()
    {
        // Blazor handler - SignalR will route here after inline JS blocks native submit
        await HandleSubmit();
    }
}
```

**Critical Note:** The inline `onsubmit="event.preventDefault(); return false;"` (lowercase) is executed synchronously by the browser before Blazor's `@onsubmit` (with @) can bind. This eliminates the timing window where native submit can occur during SignalR hydration.

### Category C: Event Handler Not Called
**Symptoms:**
- `OnAfterRender` present
- No native GET request
- Event handler logs ABSENT
- Button click occurs but nothing happens

**Root Cause:** Blazor event binding delay or button still disabled

**Solution:** Add interactive state tracking
```razor
<button type="button" disabled="@(!IsInteractive)" @onclick="HandleButtonClick">Submit</button>

@code {
    private bool IsInteractive { get; set; }

    protected override void OnAfterRender(bool firstRender)
    {
        if (firstRender)
        {
            IsInteractive = true;
            StateHasChanged();
        }
    }

    private async Task HandleButtonClick()
    {
        if (!IsInteractive) return;
        await HandleSubmit();
    }
}
```

### Category D: DLL Cache Issue
**Symptoms:**
- Logs show old code behavior
- Changes not reflected
- Inconsistent behavior

**Solution:** Deep clean and rebuild
```bash
Stop-Process -Name "dotnet" -Force
Remove-Item -Recurse -Force bin,obj
dotnet build --no-incremental
```

STOP — Apply solution based on classification.

## Step 3 — Implement Fix

### 3.1 Add Deterministic Contract
Add `data-blazor-interactive` attribute for Playwright:
```razor
<form data-blazor-interactive="@IsInteractive.ToString().ToLower()">
```

### 3.2 Add Stable Test Selectors
Use `data-testid` instead of CSS classes:
```razor
<VanAAlert data-testid="expense-success-alert" />
<button data-testid="expense-submit">Submit</button>
```

### 3.3 Update Playwright Test
```typescript
// 1. Wait for Blazor Form to mark itself as interactive
const form = page.locator('form');
await expect(form).toHaveAttribute('data-blazor-interactive', 'true', { timeout: 10000 });

// 2. HARDENING STEP: Ép thêm chốt chặn đảm bảo nút bấm đã đồng bộ trạng thái type thành submit dưới DOM
const submitBtn = page.getByTestId('expense-submit');
await expect(submitBtn).toHaveAttribute('type', 'submit');
await expect(submitBtn).not.toHaveAttribute('disabled');

// 3. Tiến hành click an toàn
await submitBtn.click();

const successAlert = page.getByTestId('expense-success-alert');
await expect(successAlert).toBeVisible();
```

### 3.4 Rebuild and Test
```bash
dotnet build --no-incremental
dotnet run
npx playwright test <spec>
```

STOP — Verify logs show event handler execution.

## Step 4 — Verify Fix

### 4.1 Check Logs
Confirm:
- Event handler log present (e.g., `HandleSubmit ENTERED`)
- No native GET requests
- Success alert visible in Playwright

### 4.2 Run Playwright Test
```bash
npx playwright test <spec>
```

Expected: Exit code 0, success alert visible.

## Step 5 — Clean Up

Remove diagnostic logs after fix verified:
```csharp
// Remove these after fix:
// Logger.LogInformation("{Component} OnInitialized");
// Logger.LogInformation("{Component} OnParametersSet");
// Logger.LogInformation("{Component} OnAfterRender...");
// Logger.LogInformation("{Component} HandleSubmit ENTERED");
```

## Fix Budget
- Max 3 iterations per issue
- If no progress after 3 iterations → escalate to architecture review

## Common Patterns

### Pattern 1: Prerendering Blocking
**Detection:** OnAfterRender missing
**Fix:** Disable prerender on component instance

### Pattern 2: Native Submit Race Condition
**Detection:** Native GET request in logs
**Fix:** Use button click handler instead of form submit

### Pattern 3: Event Binding Delay
**Detection:** Handler not called, no native submit
**Fix:** Add IsInteractive flag with StateHasChanged

### Pattern 4: DLL Cache
**Detection:** Changes not reflected
**Fix:** Deep clean bin/obj, kill dotnet processes

### Pattern 5: Hydration Time-Window Gap
**Detection:** Logs cho thấy IsInteractive đã set thành true, StateHasChanged đã gọi, nhưng khoảng 1s sau VẪN CÓ native GET request và HandleSubmit không chạy.

**Root Cause:** Thuộc tính DOM của Blazor render xong trước khi SignalR kịp hoàn tất đăng ký Event Listener chặn submit ngầm của tệp blazor.server.js. Khoảng thời gian này tạo ra "Timing Window" nơi trình duyệt có thể trigger native submit.

**Fix:** Ép inline `onsubmit="event.preventDefault(); return false;"` vĩnh viễn trên thẻ form gốc để triệt tiêu năng lực native submit của trình duyệt. Inline JS này được thực thi đồng bộ (synchronous 0ms) bởi trình duyệt trước khi Blazor's @onsubmit có thể bind qua SignalR.

```razor
<form onsubmit="event.preventDefault(); return false;" @onsubmit="OnFormSubmit" @onsubmit:preventDefault>
    <!-- form content -->
</form>
```

**Critical:** Luôn luôn dùng inline JS tĩnh onsubmit="..." (chữ thường) để trình duyệt chặn ngay lập tức hành vi nạp lại trang tại máy Client, bàn giao 100% quyền xử lý sự kiện cho mạch SignalR @onsubmit (có chữ @).
