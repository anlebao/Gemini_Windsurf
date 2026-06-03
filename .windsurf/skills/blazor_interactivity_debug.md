# Blazor Interactivity Debug Skill

> **Purpose:** Provides diagnostic and fix patterns for Blazor Server interactivity issues

## Diagnostic Commands

### Check Server Logs
```bash
Get-Content <app>/bin/Debug/netet8.0/Logs/<log-file>.txt -Tail 50
```

### Deep Clean Build
```bash
Stop-Process -Name "dotnet" -Force -ErrorAction SilentlyContinue
Remove-Item -Recurse -Force bin,obj -ErrorAction SilentlyContinue
dotnet build --no-incremental
```

### Kill and Restart Server
```bash
Stop-Process -Name "dotnet" -Force -ErrorAction SilentlyContinue
dotnet run --no-build
```

## Diagnostic Patterns

### Pattern 1: Lifecycle Logging
Add to component to debug lifecycle:
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

### Pattern 2: Event Handler Logging
Add to event handler:
```csharp
private async Task HandleSubmit()
{
    Logger.LogInformation("{Component} HandleSubmit ENTERED");
    // ... logic
}
```

### Pattern 3: Interactive State Tracking
Add to component:
```csharp
private bool IsInteractive { get; set; }

protected override void OnAfterRender(bool firstRender)
{
    if (firstRender)
    {
        IsInteractive = true;
        StateHasChanged();
    }
}
```

## Fix Patterns

### Pattern 1: Disable Prerender
```razor
<Component @rendermode="new InteractiveServerRenderMode(prerender: false)" />
```

### Pattern 2: Button Click Handler
```razor
<!-- Instead of form submit -->
<button type="button" @onclick="HandleButtonClick">Submit</button>

@code {
    private async Task HandleButtonClick()
    {
        await HandleSubmit();
    }
}
```

### Pattern 3: Deterministic Contract
```razor
<form data-blazor-interactive="@IsInteractive.ToString().ToLower()">
<button data-testid="submit-btn" disabled="@(!IsInteractive)">
```

### Pattern 4: Stable Test Selectors
```razor
<VanAAlert data-testid="success-alert" />
<button data-testid="submit-btn">Submit</button>
```

## Playwright Test Patterns

### Wait for Blazor Ready
```typescript
await expect(page.locator('form')).toHaveAttribute('data-blazor-interactive', 'true');
```

### Use Stable Selectors
```typescript
const submitBtn = page.getByTestId('submit-btn');
await expect(submitBtn).not.toHaveAttribute('disabled');
await submitBtn.click();

const successAlert = page.getByTestId('success-alert');
await expect(successAlert).toBeVisible();
```

## Log Analysis Checklist

- [ ] OnInitialized present?
- [ ] OnParametersSet present?
- [ ] OnAfterRender present? (CRITICAL)
- [ ] Event handler log present?
- [ ] Native GET request present?
- [ ] IsInteractive set to true?
- [ ] StateHasChanged called?

## Quick Reference

| Symptom | Root Cause | Fix |
|---------|-----------|-----|
| OnAfterRender missing | Prerendering blocking | Disable prerender |
| Native GET request | Button type="submit" | Use button click handler |
| Handler not called | Event binding delay | Add IsInteractive flag |
| Changes not reflected | DLL cache | Deep clean rebuild |
