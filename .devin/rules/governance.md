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
- No business logic allowed in Controllers or Hubs (NOTE: Gateway operates in ORDER CREATOR + ROUTED ASYNC DELIVERY MODE (Option C approved 2026-07-18, supersedes Option B 2026-07-05 — see `gateway_router_multi_vps_master_plan.md`). Gateway PG is source of truth for Orders + Accounting + Tenants + ShopInstances + Users + FeaturedProducts. Products live in ShopERP per-tenant SQLite. Orders async-delivered to ShopERP via NATS (routed by ShopInstanceId) for kitchen/POS display. Multi-VPS supported via ShopInstances routing table. Client (KhachLink) provides ProductName + VatRate snapshot at checkout — Gateway does NOT query Products table. Prior "pure proxy" rule rescinded.)
- Layers (inner → outer): Domain → Infrastructure → Services → API. Dependencies point INWARD (API → Services → Domain)

### **Domain Layer Protection**
- NEVER modify Domain layer to fix UI or Service layer issues
- UI settings (Theme, Display Mode, Preferences) MUST be stored in Presentation Layer only
- If Domain entity is missing a required property → report as Domain Modeling Defect, await Tech Lead approval
- ONLY modify Domain entities when a genuine modeling defect is confirmed

### **Single-Identity Pattern (HARD STOP — Entity Design Rule)**
Every entity inheriting `BaseEntity` MUST use a **single identity**: `BaseEntity.Id` (PK) is the ONLY identity column. Business key value objects (ProductId, CustomerId, OrderItemId, IngredientId, RecipeId, OrderId, etc.) are **Ignored** in EF Core config — they are NOT mapped to DB columns.

**Mandatory rules for ALL entities (existing + new):**
1. **Constructor sync:** Every entity constructor MUST set `Id = BusinessKey.Value` after `base(tenantId)`. This ensures PK == business key from creation.
2. **EF config:** `builder.Ignore(e => e.BusinessKey)` — no separate DB column, no value converter, no index on business key.
3. **Code reads:** Production code reads `entity.Id`, NOT `entity.BusinessKey.Value`. After EF loads from DB, business key VO is NOT populated (it's ignored) — reading it returns a random GUID from the field initializer.
4. **LINQ queries:** Filter by `e.Id == someGuid`, NOT `e.BusinessKey == new BusinessKey(someGuid)`.
5. **FK references:** FK columns (e.g., `OrderItem.ProductId`) reference `BaseEntity.Id` (PK), NOT the business key VO. This is already the case — the fix ensures the value stored in FK matches the PK.

**Why:** Dual-identity (Id != BusinessKey) causes FK violations when creating related entities (e.g., OrderItem → Product). The business key is what DTOs and code pass around, but FK constraints check against PK. If they differ, save fails with "FK constraint violation" — exactly the POS order creation bug fixed on 2026-07-16.

**Audit checklist for new entities:**
- [ ] Entity has a business key VO (e.g., `FooId`) inheriting from `BaseEntity`?
- [ ] Constructor sets `Id = FooId.Value`?
- [ ] EF config has `builder.Ignore(e => e.FooId)`?
- [ ] No LINQ queries filter by `e.FooId ==`?
- [ ] No code reads `entity.FooId.Value` after DB load?
- [ ] Migration drops the `FooId` column if it existed before?

**Reference implementation:** `Order` entity (UUIDv7 refactor, 2026-07-16) — `OrderConfiguration.Ignore(o => o.OrderId)`, `Order.Create(id, ...)` syncs both. All other entities aligned to this pattern in the same refactor batch.

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
| 7 | EF Core `SqlQueryRaw<T>` ad-hoc type: `could not be mapped because it is of type 'object'` OR `The required column 'X' was not present in the results of a 'FromSql' operation` | Two related issues: (a) `object?` is not a supported EF Core primitive type — model validator rejects it. (b) SQLite PRAGMA returns snake_case column names (e.g. `dflt_value`) that don't match PascalCase C# properties (e.g. `DfltValue`) — EF Core does case-insensitive matching but NOT snake_case→PascalCase conversion. | (a) Use correct .NET primitive type (`string?` for SQLite TEXT/NULL). (b) Add `[Column("dflt_value")]` attribute + `using System.ComponentModel.DataAnnotations.Schema;`. **NEVER delete a field to bypass the mapping error** — map it correctly, or confirm via business logic analysis that the field is truly unused before removing. | Gateway Fix |
| 8 | `Object must implement IConvertible` OR `LINQ expression 'DbSet<Tenant>().Where(t => t.Id.Value == ...)' could not be translated` when querying `Tenants` by Guid from route/DTO | `Tenant.Id` is a `TenantId` **value object** (not Guid) configured with `HasConversion(id => id.Value, value => new TenantId(value))`. Three failing patterns: (a) `EF.Property<Guid>(t, "Id") == guid` → IConvertible cast error (Pattern #1 variant). (b) `t.Id.Value == guid` in `Where` → LINQ translation fails (value object member access not translatable). (c) `guidList.Contains(t.Id)` where `guidList` is `List<Guid>` → type mismatch, fails translation. | Construct `TenantId` value object BEFORE comparison: (a)(b) `t.Id == new TenantId(tenantId)`. (c) Convert collection: `tenantIds.Select(id => new TenantId(id)).ToList()` then `tenantIdValues.Contains(t.Id)`. For `Dictionary` lookups by tenant: `dict[new TenantId(guid)]` or build dict keyed by `TenantId`. **Reference implementation:** `TenantManagementService.GetTenantByIdAsync`, `SocialCampaignRepository.GetActiveByTenantIdValueAsync`. NEVER use `EF.Property<Guid>` or `.Value` in `Where` for any value object Id. | Shop Removal RV |
| 9 | `relation "__efmigrationshistory" does not exist` when querying EF migration history on PostgreSQL | EF Core 10 + Npgsql creates the migration history table with **PascalCase** name `"__EFMigrationsHistory"` (quoted, case-sensitive) on PostgreSQL — NOT lowercase `__efmigrationshistory`. PostgreSQL is case-sensitive for quoted identifiers. Querying `__efmigrationshistory` (unquoted lowercase) fails because the table doesn't exist under that name. | Always query with quoted PascalCase: `SELECT "MigrationId", "ProductVersion" FROM "__EFMigrationsHistory"`. Column names are also PascalCase (`"MigrationId"`, not `migration_id`). For RV scripts: use `docker exec vanan-postgres psql -U vanan_admin -d VanAnCoreHub -c 'SELECT "MigrationId", "ProductVersion" FROM "__EFMigrationsHistory" WHERE "MigrationId" LIKE '"'"'%<Keyword>%'"'"';'`. NEVER use lowercase unquoted `__efmigrationshistory` on PostgreSQL. | Loyalty Alliance P1 RV |
| 10 | `The format of value 'application/json; charset=utf-8' is invalid.` (FormatException) when forwarding request body in Gateway controllers | `StringContent(body, Encoding.UTF8, mediaType)` and `new MediaTypeHeaderValue(mediaType)` BOTH reject the `; charset=utf-8` suffix — the mediaType parameter accepts ONLY the media type; charset must be set via the `Encoding` parameter (StringContent) or the `MediaTypeHeaderValue.CharSet` property. Browsers/fetch send `Content-Type: application/json; charset=utf-8` by default, so any Gateway forward controller passing `Request.ContentType` straight through throws at runtime on EVERY POST. | Strip charset before passing: `var mediaType = (Request.ContentType ?? "application/json").Split(';', StringSplitOptions.TrimEntries)[0];` then use `mediaType` in `StringContent(body, Encoding.UTF8, mediaType)` OR `new MediaTypeHeaderValue(mediaType)`. NEVER pass `Request.ContentType` raw to either constructor. **Audit all Gateway forward controllers** — both `StringContent` and `StreamContent + MediaTypeHeaderValue` variants are affected. **Reference implementation:** `RedemptionController`, `LoyaltyController`, `CustomerIdentityController`, `CustomerProfileController`, `MissionsController` (fix #106, 2026-08-06). | Gateway Fix #106 |

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

## VPS ACCESS (Runtime Verification)

**SSH command (memorized — do NOT ask user for SSH path/user/host again):**
```
ssh -i "C:\VibeCoding\CD\SSH\vanan.pem" ubuntu@161.118.212.110 "<command>"
```

**VPS details:**
- Host: `161.118.212.110` (also `khachvip.online`)
- User: `ubuntu`
- Key: `C:\VibeCoding\CD\SSH\vanan.pem`
- Domains: `khachvip.online` (ShopERP), `diemthuong.khachvip.online` (KhachLink), `api.khachvip.online` (Gateway)

**PostgreSQL access (inside VPS):**
```
docker exec vanan-postgres psql -U vanan_admin -d VanAnCoreHub -c "<SQL>"
```
- User: `vanan_admin` (NOT `vanan_dev` — that's local dev only)
- DB: `VanAnCoreHub` (NOT `VanAnLocal` — that's local dev only)
- EF migration history table: `"__EFMigrationsHistory"` (PascalCase, quoted — see Pattern #9)

**Container names:** `vanan-gateway`, `vanan-shoperp`, `vanan-khachlink`, `vanan-nginx`, `vanan-postgres`, `vanan-nats`, `vanan-seq`, `vanan-certbot`

**RV script pattern:** Write `.sh` locally → `scp` to `/tmp/` → `sed -i 's/\r$//'` (fix CRLF) → `bash /tmp/script.sh`. NEVER inline complex SQL in ssh `-c` from PowerShell (escape hell) — always use scp + sed approach.

**Port exposure:** Internal ports 5001/5002/5003 are NOT exposed to VPS localhost. Test via public domains or `docker exec` into containers.

---

## GOAL
Build a clean, stable, production-ready Core Accounting Engine.
Architectural integrity, immutability, and data correctness are NON-NEGOTIABLE.