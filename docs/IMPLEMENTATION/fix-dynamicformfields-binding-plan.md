# Fix Plan: DynamicFormFields Two-Way Binding (EventCallback)

> **Status:** DRAFT — awaiting review before implementation
> **Date:** 2026-05-31
> **Scope:** Fix data binding propagation in the Host Form architecture for Expense Entry

---

## 1. Background / Current State

The architectural refactor (Host Form pattern) is **functionally complete** and the original
"Assembly Boundary Event Dropping" problem is **solved**:

- `DynamicFormFields.razor` created as pure UI component (renders input fields only).
- `DynamicForm.razor` now delegates to `DynamicFormFields` (backward compatible).
- `ExpenseEntry.razor` hosts the `<form>` + submit button and owns `HandleSubmit`.
- Server logs confirm `ExpenseEntry HandleSubmit ENTERED` — the event handler fires correctly.

**Remaining defect:** Submitted form values arrive empty in C#, so validation fails with
`Amount validation failed` → `Số tiền phải lớn hơn 0`, and the success alert never shows.

---

## 2. Root Cause

`DynamicFormFields.razor` uses `@bind="field.Value"` on inputs. When the parent passes
`expenseFields` (a `List<FormField>`) down as a `[Parameter]`, the child's `@bind` writes are
**not reliably propagating** back to the parent's `FormField` instances in time for the submit
read. As a result `formData.Values["amount"]` is empty when `HandleSubmit` runs.

DOM snapshot confirms the UI holds the value (`spinbutton "Số Tiền (VNĐ)": "500000"`), but the
C# model is empty → mismatch between DOM state and component state.

---

## 3. Chosen Solution — EventCallback Two-Way Bind (Blazor canonical)

Replace implicit `@bind` with explicit `value="@field.Value"` + `@onchange` handlers that:
1. Mutate the shared `FormField` reference directly (`field.Value = newValue`).
2. Raise an `OnFieldChanged` `EventCallback<FormField>` so the host can react if needed.

This is the standard Blazor pattern, removes reliance on `@bind` code-gen across the component
boundary, and requires **no JavaScript interop**.

---

## 4. Files to Change

### 4.1 `UI.Platform/Components/Composite/DynamicFormFields.razor`

**Change every input from `@bind` to explicit value + onchange.** Example (Currency):

```razor
@* BEFORE *@
<input type="number" id="@field.Id" @bind="@field.Value" step="0.01" min="0" ... />

@* AFTER *@
<input type="number"
       id="@field.Id"
       value="@field.Value"
       @onchange="@(e => HandleFieldChange(field, e.Value?.ToString()))"
       step="0.01" min="0" ... />
```

Apply the same pattern to: **Text, Select, Date, Currency, TextArea, Number, Email, Password.**

**Checkbox** uses `checked` instead of `value`:

```razor
<input type="checkbox"
       id="@field.Id"
       checked="@(field.Value?.ToString() == "true")"
       @onchange="@(e => HandleFieldChange(field, e.Value?.ToString()))"
       disabled="@State.IsDisabled" />
```

**Add to `@code` block:**

```csharp
[Parameter]
public EventCallback<FormField> OnFieldChanged { get; set; }

private async Task HandleFieldChange(FormField field, string? newValue)
{
    field.Value = newValue ?? string.Empty;
    if (OnFieldChanged.HasDelegate)
    {
        await OnFieldChanged.InvokeAsync(field);
    }
}
```

### 4.2 `5_WebApps/ShopERP/Components/Pages/Accounting/ExpenseEntry.razor`

**Revert the temporary `eval`-based JS interop** (added while debugging) back to reading values
straight from the now-correctly-bound `expenseFields`:

```csharp
@* REVERT FROM (JS interop, remove) *@
var value = await JSRuntime.InvokeAsync<string>("eval", $"document.getElementById('{field.Id}').value");
formData.Values[field.Id] = value ?? string.Empty;

@* BACK TO (model read) *@
foreach (var field in expenseFields)
{
    formData.Values[field.Id] = field.Value?.ToString() ?? string.Empty;
}
```

**Remove now-unused dependencies:**
- `@using Microsoft.JSInterop`
- `@inject IJSRuntime JSRuntime`

> Note: `DynamicForm.razor` needs **no change** — its internal usage of `DynamicFormFields`
> continues to work; the new `OnFieldChanged` parameter is optional.

---

## 5. Verification Steps

1. **Build class library:** `dotnet build UI.Platform/VanAn.UI.Platform.csproj -c Debug --no-incremental`
2. **Build web app:** `dotnet build 5_WebApps/ShopERP/VanAn.ShopERP.csproj -c Debug --no-incremental`
3. **Restart server:** kill stray `dotnet`, then `dotnet run --no-build` in `5_WebApps/ShopERP`
4. **Run targeted Playwright test:**
   ```bash
   npx playwright test e2e-tests/expense-entry-flow.spec.ts -g "should create expense entry with vendor info" --project=e2e-tests
   ```
5. **Confirm in server logs:**
   - `ExpenseEntry HandleSubmit ENTERED`
   - `Validating amount: 500000` (non-empty)
   - `Amount validation passed`
   - `Setting success state: showSuccess=true`
6. **Confirm in Playwright:** `expense-success-alert` visible, exit code 0.

---

## 6. Risks & Mitigations

| Risk | Mitigation |
|------|-----------|
| `@onchange` fires on blur only; last field needs blur before click | Test already does `press('Tab')` after description; submit click also blurs |
| Select `value` + onchange differs from `@bind` selected behavior | Keep `selected="@(option.Value == field.Value)"` as fallback |
| Other pages using `DynamicForm` regress | `OnFieldChanged` optional; reference mutation preserves existing behavior |
| Checkbox value format (`"true"`/`"false"`) | Normalize via `e.Value?.ToString()`; checkbox onchange passes bool string |

---

## 7. Cleanup (after green)

- Remove diagnostic `Logger.LogInformation` calls added during debugging in
  `DynamicForm.razor` and `ExpenseEntry.razor` (OnInitialized / OnParametersSet / OnAfterRender
  / verbose HandleSubmit traces), keeping only essential error logging.

---

## 8. Out of Scope

- No change to render mode configuration.
- No migration to WebAssembly.
- No change to the Playwright spec (selectors remain valid).
