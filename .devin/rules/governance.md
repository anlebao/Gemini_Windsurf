---
description: "VanAn Ecosystem core governance: Domain integrity, workflow modes, UI Platform, hard stops"
trigger: always_on
---

# VANAN ECOSYSTEM GOVERNANCE (v7.0)

## CORE PRINCIPLES (NON-NEGOTIABLE)

## EXECUTIVE DIRECTIVES FOR RAPID EXECUTION (ANTI-BLOAT)

1. ACTION-FIRST METHODOLOGY:
   - For configuration changes, path replacements, file moves, or explicit script/CLI executions: DO NOT write structural analyses. Execute the terminal command or modification tool immediately.
   - Limit to a maximum of 1-2 sentences of explanation BEFORE execution. Provide detailed logging or reporting only AFTER the action successfully concludes.

2. PARALYSIS BY ANALYSIS ELIMINATION:
   - If a task can be resolved via a direct `replace_all`, a quick custom script, or a single shell pipeline, bypass the 4-mode isolation logic entirely.
   - Trust the user's explicit directive over the passive workflow constraints for environment, tooling, or quick-fix updates.

3. CONTEXT THRESHOLD GUARD:
   - You must not open or scan more than 3 files simultaneously for investigation of non-business-logic tasks.

## WORKFLOW PRECEDENCE
- Global rules define non-negotiable constraints.
- Workflow files define execution mode and operational steps.
- When using a workflow, follow the active workflow mode strictly:
  - `Fix_Errors.md` → FIX_ONLY
  - `newfeaturebuild.md` → ANALYZE → IMPLEMENT
  - `review.md` → REVIEW_ONLY
- If global rules and workflow rules appear to conflict:
  1. Hard Stop Rules win.
  2. Domain integrity rules win.
  3. Active workflow mode limits allowed actions.
  4. User approval is required before implementation or architecture changes.

## WORKFLOW MODE ISOLATION
### ANALYZE
- Allowed: inspect files, assess impact, create plans.
- Forbidden: modify files, run destructive commands, implement code.

### IMPLEMENT
- Allowed: implement approved plan, validate build/tests.
- Forbidden: expand scope, redesign architecture without approval.

### FIX_ONLY
- Allowed: fix compile/runtime errors directly related to current error batch.
- Forbidden: add features, redesign architecture, introduce new abstractions, refactor unrelated modules.

### REVIEW_ONLY
- Allowed: report findings with evidence and suggested fixes.
- Forbidden: modify files, refactor code, apply fixes.

## CURRENT OBJECTIVE PROTECTION
- Before each phase, fix batch, or review section, restate the current objective.
- Do not expand scope beyond the user request or active workflow.
- Preserve sprint objective when debugging.
- If a fix suggests new feature work, STOP and ask for approval.

## CONTEXT CONTROL
- Open only files directly relevant to the active task.
- Avoid scanning unrelated modules unless required for impact analysis.
- Prefer fresh sessions for major feature changes or architecture work.
- Re-anchor context after each phase, fix batch, or review section.

### **Domain & Architecture Rules**
- AccountingEntry must be 100% immutable (append-only pattern, changes via Reversal Entry only)
- Domain layer must remain pure: NO EF Core, NO DbContext, NO DataAnnotations
- Single Source of Truth: ALL domain entities MUST exist only in 1_Shared/Domain.cs
- Multi-tenancy must be enforced at every layer
- No business logic allowed in Controllers, Gateway, or Hubs
- Layers (inner → outer): Domain → Infrastructure → Services → API. Dependencies point INWARD (API → Services → Domain)

### **Domain Layer Protection**
- NEVER modify Domain layer to fix UI or Service layer issues
- UI settings (Theme, Display Mode, Preferences) MUST be stored in Presentation Layer only
- If Domain entity is missing a required property → report as Domain Modeling Defect, await Tech Lead approval
- ONLY modify Domain entities when a genuine modeling defect is confirmed

### **Domain Modification By Mode**
- FIX_ONLY: NEVER modify Domain.cs, BaseEntity, or AccountingEntry.
- REVIEW_ONLY: NEVER modify files.
- ANALYZE: Inspect Domain only; report modeling defects.
- IMPLEMENT: Modify Domain only if:
  1. The change is part of the approved feature plan.
  2. The Domain Phase is active.
  3. User approval was granted.
- AccountingEntry remains immutable in all modes.

### **Quality & Stability Rules**
- Stability Before Perfection: Always prioritize a stable, clean build over perfect architecture
- No Panic Mode: Never panic or revert when error count is low (<2 errors)
- Pattern-Based Fixing: Identify error pattern before fixing; for <2 errors, fix directly if pattern is obvious
- Simple & Idiomatic Solutions: Prefer standard C# solutions over complex patterns

### **Validation Requirements**
- guard-check.ps1 + dotnet build VanAn.sln MUST PASS before any submission

## WORKFLOW REFERENCES
- **New Features:** `.devin/workflows/newfeaturebuild.md` (7-step ANALYZE → IMPLEMENT)
- **Fix Errors:** `.devin/workflows/Fix_Errors.md` (pattern-based FIX_ONLY)
- **Code Review:** `.devin/workflows/review.md` (REVIEW_ONLY)
- **Technical Debt:** `.devin/workflows/technical_debt_packaging.md` (post-fix debt marking)

## PLAYWRIGHT ISOLATION
- Playwright is DISABLED during IMPLEMENT mode.
- FIX_ONLY: Playwright allowed for single spec explicit validation only (max 1 per session).
- Enable Playwright ONLY after: build passes AND implementation complete.
- Playwright governance: `.devin/rules/playwright.rules.md`
- Playwright triage: `.devin/workflows/playwright_triage.md`
- Playwright validation: `.devin/workflows/playwright_validation.md`
- Playwright fix: `.devin/workflows/playwright_fix.md`
- Playwright fix (architectural): `.devin/workflows/playwright_fix_architectural.md`

## ERROR HANDLING & FIXING

### **Critical Distinction**
- **Compile Errors:** Classify by count and fix per `.devin/workflows/Fix_Errors.md`
- **Architectural Violations:** ALWAYS trigger Hard Stop regardless of error count

### **Hard Stop Rules (Override All)**
- Domain layer modifications to fix Service/UI issues
- Breaking Clean Architecture dependency directions
- Multi-tenancy violations
- AccountingEntry immutability violations
- UI Platform bypass (using custom HTML/CSS instead of UI Platform components)

> **Error classification, fixing protocol, fix budget:** See `.devin/workflows/Fix_Errors.md`

### **Known Error Pattern Registry (Apply Directly — No Workaround)**
When encountering an error that matches a known pattern below, apply the documented fix IMMEDIATELY.
Do NOT waste rounds on trial-and-error workarounds. If the fix does not resolve the issue, proceed to the 3-Round Rule below.

| # | Pattern | Root Cause | Direct Fix | Source |
|---|---|---|---|---|
| 1 | `Object must implement IConvertible` in EF Core query with `TenantId` | `EF.Property<Guid>(e, "TenantId")` — TenantId stored as TEXT (string) via TenantIdConverter, not Guid. `EF.Property<Guid>` tries to cast string→Guid→IConvertible error. | Use `e.TenantId == tenantId` (direct property comparison). EF Core applies TenantIdConverter automatically. NEVER use `EF.Property<Guid>` for TenantId. | Wave 7 |
| 2 | `decimal.Parse(tenantId.Value.ToString("N"))` throws FormatException | GUID hex string contains non-numeric chars (a-f) that decimal.Parse cannot handle. | Use `tenantId.Value.GetHashCode()` as numeric proxy. ExtractTenantId has FormatException fallback — round-trip precision not required. | Wave 7 |
| 3 | `Value cannot be null. (Parameter 'logger')` in TemplateCalculationEngine | `BaseHKDBookTemplate` passed `null!` as logger to `new TemplateCalculationEngine(...)`. | Use `NullLogger<TemplateCalculationEngine>.Instance` (add `using Microsoft.Extensions.Logging.Abstractions`). | Wave 7 |
| 4 | Formula `SUM_ACCOUNT` returns 0 but data exists | `CalculateFormulaAsync` called `Evaluate(formula, variables)` (legacy overload) → `ExtractTenantId` parses `_TenantId` decimal proxy back as GUID → fails → fallback `Guid.NewGuid()` → wrong tenant → 0 results. | Use `Evaluate(formula, FormulaContext)` overload with correct TenantId from DataProviderContext. NEVER use legacy variables overload when TenantId accuracy matters. | Wave 7 |
| 5 | `Translation of member 'Period' on entity type 'JournalEntry' failed` | `AccountingPeriod` (record) is not mapped in EF Core configuration. `e.Period.Year`/`e.Period.Month` cannot translate to SQL. | Filter by `EntryDate` range: `e.EntryDate >= periodStart && e.EntryDate < periodEnd` where `periodStart = new DateTime(year, month, 1)`. | Wave 7 |
| 6 | Circular dependency: `IFormulaEngine` → `IDataProvider` → `IPreAggregationService` → `IFormulaEngine` | Service A depends on Service B which depends on Service A. | Use `Lazy<T>` to break the cycle. Register `Lazy<IFormulaEngine>` in DI container. | Wave 7 |

**Maintenance:** User decides when to add new patterns. Obsolete patterns (code removed, API changed) must be pruned to keep the registry lean. Do NOT let this table grow unbounded — each entry must remain actionable and current.

### **3-Round Fix Limit (Test Failures & Errors)**
When fixing test failures or errors, follow this protocol strictly:

- **Round 1:** Apply the most likely fix based on error message + code inspection. Re-run test.
- **Round 2:** If Round 1 failed, write a DIAGNOSTIC TEST (temporary `Assert.Fail` with diagnostic info: data counts, query results, EF model inspection) to gather evidence. Use evidence to fix. Re-run test.
- **Round 3:** If Round 2 failed, apply fix based on diagnostic evidence. Re-run test.
- **STOP:** If Round 3 failed → STOP. Do NOT attempt Round 4. Report to user:
  - What was tried in each round
  - What evidence was gathered
  - Current error state
  - Recommended next steps
  - Ask user for decision (skip + debt, revert, continue debugging, or escalate)

**Hard Rule:** NEVER exceed 3 rounds of fix attempts without user approval. Running more than 3 rounds blindly wastes time and context. The diagnostic test approach (Round 2) is MANDATORY — it converts guessing into evidence-based fixing.

### **Domain Inspection Before Mapping**
When compiler raises CS1061 (property not found on domain entity):
1. **STOP** — Do not guess or substitute another property
2. **INSPECT** — Read actual Domain entity definition in 1_Shared/Domain/
3. **VERIFY** — Confirm which property semantically carries the intended reference
4. **REPORT** — If missing, report as Domain Modeling Defect and await approval

Example: `CartItem.Id` (PK) ≠ `CartItem.ProductId` (FK to Product). Mapping wrong GUID is a fatal logical error.

## SYSTEM REFACTORING
- Do not change public API unless explicitly approved
- Write or update tests before refactoring (Retrofit TDD)
- Mark clearly: // REFACTOR: [brief reason]

## TDD APPROACH
- **NEW features:** Tests first
- **EXISTING code:** Retrofit tests before completion

## REPORTING REQUIREMENTS
- Keep reports short and clear
- After each phase: changed files + guard-check.ps1 result + dotnet build result
- Always wait for User approval before implementation, refactoring, architecture changes, or high-risk fixes
- Include error count before/after, files modified, root cause summary

## REVIEW SCOPE CONTROL
- Review findings must be tied to the changed code or active review target.
- Pre-existing bugs: report only if they directly affect changed code or are security/data integrity risks.
- No speculative or low-confidence issues.
- **Review modes and output formats:** See `.devin/workflows/review.md`

## PROHIBITED BEHAVIORS
- Do not create unnecessary interfaces or abstraction layers
- Do not over-engineer simple problems
- Do not declare "Mission Accomplished" while errors > 5
- Do not bypass Hard Stop Rules for any reason

## UI PLATFORM RULES
**Full reference:** docs/UI_Platform_Implementation_Guide.md
**For new modules:** Follow docs/Module_Template_Example.md

### **Core Rules**
- ALWAYS use UI Platform components (VanAnButton, VanAnCard, VanAnAlert, VanAnInput, VanAForm, VanATable, VanAChart)
- NEVER create custom HTML/CSS when UI Platform component exists
- If UI Platform component has errors → Fix it in UI.Platform project, NEVER bypass
- ALWAYS use design tokens instead of hardcoded values
- UI Platform violations are treated as Hard Stop Rules

### **Component Hierarchy**
```
Layer 1: Base (VanAnButton, VanAnCard, VanAnAlert, VanAnInput, VanAModal, VanASpinner)
Layer 2: Composite (VanAForm, VanATable, VanAChart, VanALayout, VanANavigation)
Layer 3: Module-Specific (EmployeeForm, CustomerCard, JournalEntry, etc.)
```

### **Theme & Responsive**
- Inject IThemeProvider + ITenantService for theming
- Mobile-first design with breakpoints: Mobile (≤640px), Tablet (641-1024px), Desktop (≥1025px)
- Use CSS Grid and Flexbox for layout

### **Quality Standards**
- Semantic HTML + ARIA labels + keyboard navigation
- Lazy load components, virtual scrolling for large datasets
- Prioritize component reusability and separation of concerns

## SKILL REFERENCES
- **Technical Debt Management:** `.devin/skills/technical_debt_management.md` (debt classification & remediation planning)
- **Playwright Cost Optimizer:** `.devin/skills/playwright_cost_optimizer.md` (deterministic cost tiers)
- **Playwright Guard:** `.devin/skills/playwright_guard.md` (browser isolation during IMPLEMENT mode)

## KHACHLINK WAVE DEVELOPMENT CHECKLIST

**BẮT BUỘC** khi thêm `@inject XxxService` vào bất kỳ component KhachLink nào:

1. **Đăng ký DI trong `5_WebApps/KhachLink/Program.cs`:**
   ```csharp
   _ = builder.Services.AddScoped<XxxService>();
   // hoặc interface:
   _ = builder.Services.AddScoped<IXxxService, XxxHttpService>();
   ```

2. **Thêm assertion vào `6_Tests/VanAn.Integration.Tests/KhachLinkStartupTests.cs`:**
   ```csharp
   Assert.NotNull(sp.GetRequiredService<XxxService>());
   ```

3. **Dùng Http implementation, không phải CoreHub trực tiếp:**
   - KhachLink KHÔNG được inject CoreHub services có repository dependencies
   - Nếu có `XxxHttpService` trong `Services/Http/` → dùng nó
   - Nếu chưa có → tạo mới `XxxHttpService : IXxxService` gọi Gateway

**Lý do:** Thiếu bước 1 gây 500 trên VPS mà CI không phát hiện được (đã xảy ra thực tế).
`KhachLinkStartupTests` là tầng bảo vệ — nếu quên đăng ký, test BLOCKING sẽ fail tại local CI trước khi push.

**Files tham chiếu:**
- Factory: `6_Tests/VanAn.Integration.Tests/Infrastructure/KhachLinkWebApplicationFactory.cs`
- Tests: `6_Tests/VanAn.Integration.Tests/KhachLinkStartupTests.cs`
- CI step: `scripts/ci-full.ps1` Step 2b (BLOCKING)

---

## GOAL
Build a clean, stable, production-ready Core Accounting Engine.
Architectural integrity, immutability, and data correctness are NON-NEGOTIABLE.