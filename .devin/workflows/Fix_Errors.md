---
description: Fix build errors using pattern-based approach
---

# Fix Errors Workflow

> **Hard Stop, Domain Protection, Objective Lock:** See `.windsurfrules`

## Mode: FIX_ONLY
Fix compile/runtime errors only. No features, no architecture redesign, no domain modifications.

## Trigger
Build errors > 0 after `dotnet build VanAn.sln`

## Phase Isolation
- **Domain:** Only if absolutely necessary (requires approval)
- **Application:** 3_CoreHub/Services, 2_Gateway/Controllers
- **Infrastructure:** Repositories, EF Core, database
- **UI:** 5_WebApps, *.razor, UI Platform components

Do not mix phases. Stay within the layer of the error being fixed.

## Fix Budget
- Max 3 files per batch
- Rebuild after each batch
- Reassess after each pattern elimination
- Error count increases >10% → STOP and report
- 3 consecutive batches fail → STOP and request re-evaluation

## Context Limits
- Max 5 files per batch (only files related to current error pattern)
- Re-anchor after each batch: restate objective + remaining error count

## Phase 1: Classification

```bash
dotnet clean && dotnet build VanAn.sln --no-incremental
```

- **Small (<2):** Direct fix + short report
- **Medium (2-5):** Scan for shared patterns before fixing
- **High (>5):** Full Investigation (Phase 2)
- **Architectural Violations:** ALWAYS Hard Stop

## Phase 2: Full Investigation (>5 errors)

1. **Assessment:** Total errors, top 5 error codes
2. **Root Cause:** Focus on patterns, map to Error Code table below
3. **Impact:** Domain integrity, AccountingEntry immutability, rollback risk
4. **Plan:** Create fix plan → **WAIT FOR APPROVAL**
5. **Validate:** Report new error count + domain integrity

## Phase 3: Pattern-Based Execution

1. **Identify:** Group errors by pattern (Error Code + File Pattern below)
2. **Apply:** 1 source file → rewrite with correct pattern → template to similar files
3. **Validate:** Build → verify reduction → check domain integrity

## Execution Continuity
**After each batch:** error count, errors fixed, remaining patterns, next action
**Every 3 batches:** restate objective, current pattern, progress %
**Track:** completed / in-progress / pending patterns

## Active Skills (max 3)
Default: `build-error-analysis`, `pattern-based-fixing`, `domain-integrity-validation`

| Error Type | Swap skill |
|---|---|
| UI/Razor | `ui-platform-compliance-review` (replace domain) |
| SQLite/NATS | `sqlite-concurrency-analysis` or `outbox-pattern-implementation` |
| Tests | `test-system-upgrade` |

## Test Failures
For failing tests, use `.windsurf/workflows/Fix_Tests.md` instead of this workflow.

Use this workflow only when test failures are caused by build/compile errors and the immediate objective is zero-error build.

## Anti-Panic
- ❌ Never modify Domain.cs, BaseEntity, or AccountingEntry
- ❌ Never bypass protected setters or use object initializers for immutable entities
- ✅ Complete assessment before plan
- ✅ Wait for approval before code changes
- ✅ Systematic batch replacement only (no case-by-case)

## Pattern Reference
**Full definitions:** `docs/Implement/QuyTrinh/RULE_6_1_FullErrorInvestigation_Protocol.md`

### Priority Order
1. **Critical Blockers:** P15-16, 22, 30-31
2. **High Impact:** P1-3, 20-21
3. **Security:** P17-18
4. **Medium:** P4-5, 23-26
5. **UI/UX:** P9-14
6. **Low:** P6-8, 27-29

### Error Code → Pattern
```
CS0200→P1  CS1729→P8  CS1061→P3,6,9,12  CS0246→P4,7,9,31
CS1503→P5  CS0101→P30  CS1660→P16  RZ9980→P15  NU1903→P17
```

### File → Pattern
```
*Tests.cs→P4  *Service*.cs→P3  *Repository*.cs→P5  *Controller*.cs→P8
*.razor→P9-16,31  Directory.Packages.props→P17,18  *Models/*.cs→P30
```

## Post-Fix Checklist
- [ ] Build: 0 errors
- [ ] Domain integrity maintained
- [ ] No new warnings
- [ ] Architecture compliance verified
