# PR Review Checklist

> **Dùng cho mọi Pull Request trong VanAn Ecosystem**
> Workflow: `.devin/workflows/review.md` (REVIEW_ONLY mode)

## Hard Stops (REJECT PR if any fail)

- [ ] **Domain purity:** Domain layer (`1_Shared/`) contains NO EF Core, NO DbContext, NO DataAnnotations
- [ ] **AccountingEntry immutability:** No mutation of existing `AccountingEntry`. Corrections via `CreateReversalEntry()` only
- [ ] **Multi-tenancy:** `TenantId` present on all new entities, no nullable tenant, no bypass of query filters
- [ ] **UI Platform:** No custom HTML/CSS where `VanAn.UI.Platform` component exists. No hardcoded design tokens
- [ ] **Clean Architecture:** Dependencies point INWARD only (API → Services → Domain). No reverse dependencies
- [ ] **CoreHub type:** `3_CoreHub` remains Class Library (.dll). NO `<OutputType>Exe</OutputType>`
- [ ] **Gateway purity:** `2_Gateway` has NO DbContext, NO EF Core namespaces, NO business logic/services
- [ ] **KhachLink data access:** `5_WebApps/KhachLink` does NOT inject `IVanAnDbContext`. Uses HTTP via Gateway only
- [ ] **No new .csproj:** No new project files (e.g., `VanAn.CoreHub.Api`). Use `5_WebApps/ShopERP` as main Web API Host

## Build & Validation

- [ ] `dotnet build VanAn.sln` passes with 0 errors
- [ ] `guard-check.ps1` passes
- [ ] `dotnet test 6_Tests/VanAn.Architecture.Tests/` passes (7/7 or current baseline)
- [ ] `dotnet test 6_Tests/VanAn.Core.Tests/` passes (no new failures)
- [ ] No new analyzer warnings (VA1001-VA1005)
- [ ] No new `<PackageReference Version="...">` in `.csproj` (use central package management)

## Code Quality

### Domain Layer
- [ ] Entities use protected setters (no public setters on domain properties)
- [ ] Factory methods validate invariants before construction
- [ ] Value objects are `record` types with implicit conversions where appropriate
- [ ] No business logic in controllers, gateway, or hubs

### UI Layer
- [ ] Uses `VanAn.UI.Platform` components (VanAnButton, VanACard, VanAForm, VanATable, etc.)
- [ ] Component parameters use enum types (`ButtonVariant.Outline`, not `"outline"`)
- [ ] Razor syntax correct: `OnClick="@(() => ...)"` (with `@` prefix)
- [ ] Navigation via `@inherits VanAn.UI.Platform.Components.Base.BaseComponent` (not `@inject NavigationManager`)
- [ ] Responsive: Mobile (≤640px), Tablet (641-1024px), Desktop (≥1025px)
- [ ] Design tokens used (no hardcoded colors, spacing, fonts)

### Service Layer
- [ ] No sync-over-async (`.Result`, `.Wait()`, `GetAwaiter().GetResult()` in non-test code) — Pattern 32
- [ ] No `.Result` in Blazor Server lifecycle methods (`OnInitialized`, `OnAfterRender`)
- [ ] Async methods suffixed with `Async`
- [ ] Constructor parameters match interface signatures (Pattern 8)
- [ ] No direct entity creation in tests — use `TestEntityBuilder` (Pattern 1)

### Data Layer
- [ ] EF Core configurations use `IEntityTypeConfiguration<T>` (not inline in `DbContext.OnModelCreating`)
- [ ] Value object conversions use `HasConversion(id => id.Value, value => new TypeName(value))`
- [ ] Global query filters enforce tenant isolation
- [ ] No `Database.EnsureCreated()` in production code

## Testing

- [ ] New features have tests (TDD: tests first for new features)
- [ ] Refactored code has retrofit tests before completion
- [ ] Tests use `TestEntityBuilder` for entity creation (Pattern 1)
- [ ] Tests assert business results, not mock interactions (Pattern 2)
- [ ] Test files have `using Xunit;` (Pattern 4)
- [ ] No `[Fact]` without `using Xunit;`

## Security

- [ ] No secrets/keys in code or config files
- [ ] No `GITHUB_TOKEN` or API keys committed
- [ ] No `Console.WriteLine` with sensitive data
- [ ] Authentication packages up to date (check `Directory.Packages.props`)
- [ ] No known vulnerable dependencies (Pattern 17 — run `dotnet list package --vulnerable`)

## Documentation

- [ ] ADR created/updated if architectural decision changed
- [ ] Domain docs updated if business rules changed (`docs/knowledge-base/02-domains/`)
- [ ] `project_state.md` updated if objective completed or new phase started
- [ ] Changelog updated (when Phase 5 Documentation Automation is active)
- [ ] New error patterns documented in `RULE_6_1_FullErrorInvestigation_Protocol.md`

## Git Hygiene

- [ ] Branch naming follows convention (`feat/`, `fix/`, `align-`)
- [ ] Commit messages follow convention (`fix(scope):`, `feat(scope):`, `docs:`)
- [ ] No merge commits in feature branch (rebase if needed)
- [ ] PR description explains "why" not just "what"
- [ ] No commented-out code
- [ ] No `Console.WriteLine` debug statements in production code

## Pattern-Aware Review

If PR touches these file types, check corresponding patterns:

| File Pattern | Patterns to Check |
|---|---|
| `*Tests.cs` | P1 (entity creation), P2 (mock setup), P4 (test deps) |
| `*Service*.cs` | P32 (sync-over-async), ~~P3~~ (OBSOLETE) |
| `*Repository*.cs` | P5 (type conversions), P32 (sync-over-async) |
| `*Controller*.cs` | P8 (constructor), P32 (sync-over-async) |
| `*.razor` | P9 (UI params), P10 (navigation), P11 (TagHelper), P15 (HTML), P16 (event handler) |
| `*Layout*.razor` | P10, P12, P13 (CSS) |
| `*Pages/*.razor` | P9, P11, P15, P16 |
| `Directory.Packages.props` | P17 (security), P18 (duplicate packages) |
| `Directory.Build.props` | P18 (duplicate packages) |
| `*Models/*.cs` | P30 (duplicate class definitions) |

**Note:** P3 (Service Method Mismatch) is OBSOLETE — do NOT apply. P6 (Property Access) is MISLEADING — inspect type before renaming.

---

*Document Status: Active*
*Last Updated: 2026-06-18*
*Source: governance.md, .windsurfrules, ADR-001/002/003/004/005, RULE_6_1_FullErrorInvestigation_Protocol.md, .devin/workflows/review.md*
