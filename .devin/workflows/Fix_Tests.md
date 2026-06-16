---
description: Fix failing tests using pattern-based and domain-safe approach
---

# Fix Tests Workflow

> **Hard Stop, Domain Protection, Objective Lock:** See `.windsurfrules`

## Mode: FIX_ONLY_TESTS
Fix failing tests only. No features, no architecture redesign, no unrelated refactoring, no domain modifications without approval.

## Trigger
- `dotnet test` fails
- Test project compiles but assertions, setup, fixtures, mocks, or integration checks fail
- Test project has compile errors caused by namespace, source-of-truth, or test dependency issues

## Playwright Routing
If failing tests are **Playwright E2E tests** (`.spec.ts` files in `6_Testing/`):
- Do NOT use this workflow.
- Route to `.windsurf/workflows/playwright_triage.md` for classification first.
- Then use `.windsurf/workflows/playwright_fix.md` for fixes.
- This workflow handles `dotnet test` failures ONLY.

## Objective
- Restore passing targeted tests first
- Preserve production behavior and domain integrity
- Avoid weakening assertions just to make tests pass
- Distinguish test bugs from production bugs before editing

## Phase Isolation
- **Test Phase:** Fix test setup, fixtures, assertions, builders, namespaces, mocks
- **Application Phase:** Fix production service behavior only if tests reveal a real production bug
- **Infrastructure Phase:** Fix repositories/database/test infrastructure only if root cause is there
- **Domain Phase:** Requires explicit approval before any domain modification

Do not mix phases. Start from tests and escalate to production code only with evidence.

## Test Fix Budget
- Max 3 files per batch
- Re-run targeted tests after each batch
- Reassess after each root-cause pattern
- Failure count increases >10% → STOP and report
- 3 consecutive batches fail to reduce failures → STOP and request re-evaluation

## Context Limits
- Max 5 files per batch by default
- Include only failing test file, directly tested production file, and required shared test utilities
- Re-anchor after each batch: restate objective + remaining failing tests

## Phase 1: Failure Classification
Run or inspect targeted test output first.

Classify each failure as:
- **Test bug:** incorrect setup, outdated assertion, namespace conflict, mock over-verification
- **Production bug:** implementation violates expected business behavior
- **Spec drift:** plan or requirement changed but tests were not updated
- **Environment issue:** database, external service, fixture, timing, or configuration dependency

## Phase 2: Root Cause Grouping
Group failures by:
- test class/file
- failing assertion type
- exception type
- shared setup/fixture
- domain entity construction pattern
- service method/interface mismatch
- namespace/source-of-truth conflict

## Phase 3: Pattern-Based Test Fixing
1. Select the highest-impact root cause group.
2. Fix one representative test or shared test helper first.
3. Apply the verified pattern to similar failures.
4. Re-run targeted tests.
5. Report before/after failure count.

## Active Skills (max 3)
Default:
1. `.windsurf/skills/build-error-analysis.md`
2. `.windsurf/skills/pattern-based-fixing.md`
3. `.windsurf/skills/test-system-upgrade.md`

Use `.windsurf/skills/domain-integrity-validation.md` instead of another skill when:
- failing tests touch domain entities, value objects, AccountingEntry, or immutable constructors
- test setup requires protected/private setters or object initializers
- a test appears to require production Domain changes

## Rules
- Group failing tests by root cause before fixing
- Prefer behavior-correct fixes over assertion weakening
- Do not modify Domain just to satisfy outdated tests
- Do not duplicate production domain entities in test projects
- Do not over-mock when behavior assertions are possible
- Do not change production behavior unless the test proves a real production bug
- Re-run targeted tests after each batch
- Run broader test suite after the root pattern is fixed

## Stop Conditions
- A test fix requires Domain modification without approval
- Assertion changes would hide a real production bug
- Production behavior change is needed but expected behavior is unclear
- 3 consecutive test-fix batches fail to reduce failures
- Failure appears environment-dependent and cannot be verified from code

## Output Report
After each batch, report:
- targeted test command/result
- failures fixed
- remaining failing test groups
- classification: test bug / production bug / spec drift / environment issue
- files changed
- next action

## Post-Fix Checklist
- [ ] Targeted tests pass
- [ ] Broader relevant test suite pass or remaining failures are reported
- [ ] Build: 0 errors
- [ ] Domain integrity maintained
- [ ] No assertion weakening without explicit rationale
- [ ] No duplicate production domain entities in tests
- [ ] Architecture compliance verified
