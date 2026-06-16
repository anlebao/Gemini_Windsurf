---
description: Validate completed implementation with Playwright — minimal execution cost
---

# Playwright Validation Workflow

> **Governance:** `.windsurf/rules/playwright.rules.md`

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

If failures exist → follow classification from `playwright_triage.md`:
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
- >5 failures → triage first via `playwright_triage.md`
- Backend/Domain failures → escalate to implementation workflow
- Infrastructure failures → escalate to ops
