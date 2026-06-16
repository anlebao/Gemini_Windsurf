---
description: Fix classified Playwright failures — restore existing behavior only
---

# Playwright Fix Workflow

> **Governance:** `.windsurf/rules/playwright.rules.md`

## Mode: FIX_PLAYWRIGHT
Fix Playwright test failures. Restore existing behavior only.

## Prerequisites
- Failures classified via `playwright_triage.md` or `playwright_validation.md`
- Failure category is: Selector, Timing, or UI (local fix possible)
- Backend/Domain/Infrastructure failures are NOT handled here

## Objective
Restore existing test behavior. Do NOT:
- Implement new features
- Improve UX
- Optimize code
- Rewrite test contracts

## Fix Budget
- Max 3 files per fix iteration
- Max 1 rerun per spec after fix
- Stop if: 3 iterations AND no measurable progress

Measurable progress = at least ONE of:
- Failure count decreased
- Failure category changed (e.g., Selector resolved, new Timing issue)

NOT measurable progress:
- Changed files but same failure persists
- Different error message, same root cause
- "Partial fix" without any test turning green

## For Each Failure

1. **Verify expectation** — Is the test assertion still valid?
2. **Inspect actual result** — What does the app actually show?
3. **Identify root cause** — Why does actual ≠ expected?
4. **Patch locally** — Minimal change to restore behavior
5. **Rerun single spec** — 1 rerun only

### If rerun still fails:
- Re-classify the failure
- Do NOT rerun again
- Report and escalate

## Hard Stop Conditions
- More than 3 files touched in single iteration → STOP
- Architecture affected → STOP and escalate
- Unrelated failures appear → STOP and triage
- Test requires rewriting (not patching) → STOP and get approval
- 3 consecutive fix iterations with no measurable progress → STOP

## Completion Criteria
- Original failing tests pass
- No new test failures introduced
- No assertion weakening
- No new tests created

## Output Report
After each iteration:
- Fixed failure(s)
- Remaining failure(s)
- Files changed
- Rerun result
- Next action or escalation

## Active Skills
1. `playwright_cost_optimizer`
