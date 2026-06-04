# Playwright Execution Ledger

Append-only log. Max 20 recent entries. AI must NOT delete entries.

| Date | Workflow | Spec(s) | Result | Root Cause | Action Taken |
|------|----------|---------|--------|------------|-------------|
| 2026-06-04 | playwright_fix | expense-entry-flow.spec.ts | FIXED | Backend — `criticalFields` HashSet in `HandleSubmit` missing `"account"` and `"date"` → fields read from stale model instead of DOM → account validation failed with empty value | Added `"account"` and `"date"` to criticalFields. 1 file changed: `5_WebApps/ShopERP/Components/Pages/Accounting/ExpenseEntry.razor:229` |
| 2026-06-03 | playwright_fix | expense-entry-flow.spec.ts | FIXED | UI — `@onsubmit:preventDefault` missing on `<form>` in `ExpenseEntry.razor` → native browser submit aborted Blazor async handler before `showSuccess`/`showError` rendered | Added `@onsubmit:preventDefault` to form tag. 1 file changed: `5_WebApps/ShopERP/Components/Pages/Accounting/ExpenseEntry.razor` |
