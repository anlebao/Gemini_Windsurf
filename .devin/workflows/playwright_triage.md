---
description: Triage Playwright failures — collect, classify, route (no fix)
---

# Playwright Triage Workflow

> **Governance:** `.windsurf/rules/playwright.rules.md`

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
| **Selector** | Element not found, locator timeout | `playwright_fix.md` |
| **Timing** | Navigation timeout, waitFor timeout | `playwright_fix.md` |
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
| 1 | file.spec.ts | test name | Selector | element renamed | playwright_fix | 1 file |

**Summary:** X failures classified, Y categories, recommended workflow(s).
