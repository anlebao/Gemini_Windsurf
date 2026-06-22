---
description: Fix UI Platform compile errors for P9-P14 patterns
---

# Fix UI Platform Errors Workflow

> **Hard Stop, Domain Protection, Objective Lock:** See `.windsurfrules`

## Mode: FIX_UI_ONLY
Fix UI/Razor compile errors only. No domain changes, no service logic changes.

## Trigger
- Errors in `.razor` files (P9-P14, P31)
- RZ9980, RZ10012, RZ1030 error codes
- UI Platform component parameter mismatches

## Connected Patterns
This workflow provides **concrete fix procedures** for orphan patterns:
- **P9:** UI Platform Component Parameter Mismatch
- **P10:** Navigation Service Injection Architecture
- **P11:** Razor TagHelper Syntax Errors
- **P12:** Navigation Variable Mismatch
- **P13:** CSS Media Query in Code Block
- **P14:** Static Method Call Context
- **P31:** Razor Generated File Namespace Resolution

## Phase Isolation
- **UI Layer only:** `*.razor`, `*.cshtml`, `5_WebApps/`, `UI.Platform/`
- **No Domain changes:** Never modify `Domain.cs`, entity constructors
- **No Service changes:** Interface method names stay fixed

## Fix Budget
- Max 3 `.razor` files per batch
- Rebuild after each batch: `dotnet build VanAn.sln`
- Error count increases >10% → STOP and report

## Active Skills (max 2)
1. `ui-platform-compliance-review` (identify violations)
2. `pattern-based-fixing` (apply batch fixes)

## Pattern-Based Fixes

### P9: UI Platform Component Parameter Mismatch
**Error:** `RZ10012: Found markup element with unexpected name`  
**or:** Component property type mismatch

**Fix Procedure:**
1. Check component definition in `UI.Platform/Components/`
2. Map incorrect parameter to correct enum/type:
   ```razor
   <!-- WRONG -->
   <VanAnButton Variant="outline" Size="small" />
   <VanAnAlert Variant="info" />

   <!-- CORRECT -->
   <VanAnButton Variant="ButtonVariant.Outline" Size="ButtonSize.Small" />
   <VanAnAlert Variant="AlertVariant.Info" />
   ```
3. Use `replace_all` for same pattern in file

---

### P10: Navigation Service Injection Architecture
**Error:** `CS0103: The name 'NavigationManager' does not exist in the current context`

**Fix Procedure:**
1. Check if component already inherits from `BaseComponent`:
   ```razor
   @inherits VanAn.UI.Platform.Components.Base.BaseComponent
   ```
2. If yes → Use `Navigation` (inherited property):
   ```csharp
   Navigation.NavigateTo("/route");
   ```
3. If no → Add proper inheritance, remove duplicate injection:
   ```razor
   <!-- WRONG -->
   @inject NavigationManager NavigationManager

   <!-- CORRECT -->
   @inherits VanAn.UI.Platform.Components.Base.BaseComponent
   ```

---

### P11: Razor TagHelper Syntax Errors
**Error:** `RZ1030: TagHelper attributes must be well-formed`  
**or:** Lambda not binding to event

**Fix Procedure:**
1. Ensure proper Razor expression syntax:
   ```razor
   <!-- WRONG -->
   <VanAnButton OnClick="() => Navigation.NavigateTo('/cart')" />
   <input @oninput="(e => field.Value = e.Value)" />

   <!-- CORRECT -->
   <VanAnButton OnClick="@(() => Navigation.NavigateTo("/cart"))" />
   <input @oninput="@(ChangeEventArgs e => field.Value = e.Value?.ToString())" />
   ```
2. Use explicit delegate types for lambda parameters

---

### P12: Navigation Variable Mismatch
**Error:** `CS0103: Navigation does not exist` (but `NavigationManager` injected)

**Fix Procedure:**
1. If using `@inherits BaseComponent` → use `Navigation`:
   ```csharp
   Navigation.NavigateTo("/cart");
   ```
2. If using explicit injection → use injected variable name:
   ```razor
   @inject NavigationManager NavManager
   <!-- Then use: NavManager.NavigateTo("/cart") -->
   ```
3. Standardize: Prefer `Navigation` via BaseComponent inheritance

---

### P13: CSS Media Query in Code Block
**Error:** CSS media queries inside `@code { }` block

**Fix Procedure:**
1. Move CSS to proper `<style>` block:
   ```razor
   <!-- WRONG -->
   @code {
       @media (max-width: 640px) { ... }
   }

   <!-- CORRECT -->
   <style>
   @media (max-width: 640px) {
       .my-class { ... }
   }
   </style>

   @code {
       // C# code only
   }
   ```

---

### P14: Static Method Call Context
**Error:** `CS0103: The name 'ThemeTypeExtensions' does not exist`  
**or:** Extension method not found

**Fix Procedure:**
1. Add proper `using` directive:
   ```razor
   @using VanAn.UI.Platform.Extensions
   ```
2. Or use full qualified name:
   ```csharp
   var cssClass = VanAn.UI.Platform.Extensions.ThemeTypeExtensions.ToCssClass(theme);
   ```
3. Check if extension method is actually `static` (not instance)

---

### P31: Razor Generated File Namespace Resolution
**Error:** `CS0246 in .g.cs file` - Type not found in generated Razor file

**Fix Procedure:**
1. **Verify _Imports.razor:**
   ```razor
   @using VanAn.UI.Platform.Services
   @using VanAn.UI.Platform.Components
   ```
2. **Verify ProjectReference** in `.csproj`:
   ```xml
   <ProjectReference Include="..\UI.Platform\UI.Platform.csproj" />
   ```
3. **Verify relative path** (Rule 6.2.X): Count `..` correctly
4. **Clean build:**
   ```bash
   dotnet clean && dotnet build --no-incremental
   ```
5. **Last resort:** Add explicit `@using` at top of `.razor` file

---

## Execution Continuity
**After each batch:** Error count, patterns fixed, remaining patterns
**Every 3 batches:** Restate objective, progress %, next pattern batch
**Track:** completed / in-progress / pending patterns

## Stop Conditions
- Fix requires Domain modification
- Fix requires Service interface change
- Same pattern fails after 3 fix attempts
- Error count increases >10%

## Post-Fix Checklist
- [ ] Build: 0 Razor compile errors
- [ ] UI Platform components render correctly
- [ ] Navigation works (no 404s)
- [ ] Responsive behavior intact
- [ ] No new warnings

## References
- `.devin/rules/.windsurfrules` - UI Platform section
- `.devin/skills/ui-platform-compliance-review.md` - For review mode
- `.devin/skills/ui-platform-migration.md` - For component mapping
- `docs/Implement/QuyTrinh/RULE_6_1_FullErrorInvestigation_Protocol.md` - Pattern definitions
