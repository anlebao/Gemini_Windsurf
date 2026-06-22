---
description: Master Playwright workflow — triage, fix, validation in one file
---

# Playwright Master Workflow

> **Governance:** `.devin/rules/playwright.rules.md`

---

# Triage

## Mode: TRIAGE_ONLY
Collect failure data, classify root cause, route to correct workflow.
Do NOT fix, do NOT rerun, do NOT modify any files.

## Trigger
- Playwright test failures reported
- User requests failure analysis
- Post-implementation validation shows failures

## Step 1 — Collect Failure Data

Gather from test output:
- Failing spec file(s) and test name(s)
- Error message and stack trace
- Screenshots (if available in `reports/`)
- Console logs
- Network errors

Prefer existing reports and output.

If reports are insufficient for classification:
- Allow ONE scoped reproduction (run the failing spec once).
- Capture output only — do NOT fix.
- Mark in ledger as TRIAGE reproduction.

STOP — present collected data.

## Step 2 — Classify Each Failure

Assign ONE category per failure:

| Category | Signal | Route To |
|----------|--------|----------|
| **Selector** | Element not found, locator timeout | Fix section |
| **Timing** | Navigation timeout, waitFor timeout | Fix section |
| **UI** | Layout changed, component missing | Implementation workflow (UI phase) |
| **Backend** | API 4xx/5xx, missing endpoint | Implementation workflow (App phase) |
| **Domain** | Business rule violation in response | Report to user — potential domain issue |
| **Infrastructure** | Connection refused, server down | Ops/deployment issue — escalate |

STOP — present classification table.

## Step 3 — Route Recommendation

Output:
- Classified failure table
- Recommended workflow per failure
- Suggested execution order (highest impact first)
- Estimated fix scope (files likely affected)

DO NOT proceed to fix. User must activate the appropriate workflow.

## Context Limits
- Max 5 failure analyses per triage session
- If >5 failures → report top 5 by impact, note remaining count

## Output Report Template

| # | Spec | Test | Category | Root Cause | Route | Est. Scope |
|---|------|------|----------|------------|-------|------------|
| 1 | file.spec.ts | test name | Selector | element renamed | Fix | 1 file |

**Summary:** X failures classified, Y categories, recommended workflow(s).

---

# Fix

## Mode: FIX_PLAYWRIGHT
Fix Playwright test failures. Restore existing behavior only.

## Prerequisites
- Failures classified via Triage section or Validation section
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

---

# Validation

## Mode: VALIDATE_ONLY
Validate implementation. Do NOT fix, refactor, or redesign.

## Prerequisites
- Implementation complete
- `dotnet build VanAn.sln` passes (0 errors)
- `guard-check.ps1` passes
- Active skill: `playwright_cost_optimizer`

## Step 1 — Identify Changed Scope

Inspect recent implementation changes:
- Changed modules / files
- Impacted user flows
- Impacted Playwright specs (map changed files → spec coverage)

Output: list of affected specs.

STOP — confirm scope with user.

## Step 2 — Select Minimum Required Tests

Apply cost tiers from governance rules:

| Affected Specs | Action |
|----------------|--------|
| 1 | Run directly |
| 2-5 | Run scoped |
| >5 | Smoke project only first |
| Full suite | Require user approval |

Priority order: smoke → feature → actor → regression.
Do NOT run broader scope than needed.

STOP — confirm test selection.

## Step 3 — Execute Validation

Run selected tests. Collect:
- Pass/fail results
- Screenshots (on failure)
- Console logs (on failure)
- Network errors (on failure)

No fixes at this step.

STOP — present results.

## Step 4 — Classify Failures (if any)

If failures exist → follow classification from Triage section:
- Selector / Timing / UI / Backend / Domain / Infrastructure
- Classify ONE category per failure

STOP — present classification. Recommend next workflow.

## Step 5 — Report

Output:
- Passed tests count
- Failed tests count + classification
- Remaining risks
- Recommended next action (fix workflow or accept)

DO NOT continue to implementation or fix mode.

## Active Skills
1. `playwright_cost_optimizer` (always)
2. `playwright_guard` (verify implementation is truly complete)

## Stop Conditions
- >5 failures → triage first via Triage section
- Backend/Domain failures → escalate to implementation workflow
- Infrastructure failures → escalate to ops