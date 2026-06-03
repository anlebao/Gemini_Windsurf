---
description: Playwright governance rules — validation contract, not development driver
---

# PLAYWRIGHT GOVERNANCE RULES

## Purpose
Use Playwright as a validation and regression contract.
Do NOT use Playwright as a development driver.

## Tests Are Verification Contracts
Tests are verification contracts, not immutable artifacts.
- Do NOT auto-rewrite tests.
- Update tests ONLY when acceptance criteria, business rules, or UI contract explicitly change.
- Never weaken assertions to make tests pass.
- Never regenerate test files without explicit approval.

---

## PLAYWRIGHT MAY
- Validate completed implementation
- Verify acceptance criteria
- Reproduce reported failures
- Collect diagnostics (screenshots, console, network)

## PLAYWRIGHT MUST NOT
- Drive architecture decisions
- Trigger refactors
- Redesign UI structure
- Modify business logic
- Auto-generate or auto-rewrite tests

---

## MODE RESTRICTIONS

| Mode | Playwright Status |
|------|-------------------|
| ANALYZE | DISABLED |
| IMPLEMENT | DISABLED |
| FIX_ONLY | LIMITED — single spec, explicit validation, max 1 execution per session. If fails → route to `playwright_triage.md`, do NOT fix-and-rerun within FIX_ONLY |
| REVIEW_ONLY | DISABLED |
| VALIDATE_ONLY | ENABLED — via `playwright_validation.md` |
| TRIAGE_ONLY | ENABLED — via `playwright_triage.md` (no browser, report only) |
| FIX_PLAYWRIGHT | ENABLED — via `playwright_fix.md` (scoped browser) |

Enable Playwright only after:
1. Implementation complete
2. `dotnet build VanAn.sln` passes
3. `guard-check.ps1` passes

---

## EXECUTION RULES

### Retry Policy (AI Execution)
- Max 1 rerun per spec after a fix attempt.
- Still fails → classify failure type, do NOT rerun.
- Never rerun until green.
- Never rerun full suite after single spec failure.

### Scoped Execution
Prefer: single spec → single feature → single actor.
Avoid: full regression runs.
Full suite requires explicit user approval.

### Cost Tiers (Deterministic)

Primary dimension: spec count.

| Scope | Tier | Action |
|-------|------|--------|
| 1 spec | Low | Run directly |
| 2-5 specs | Medium | Run scoped |
| >5 specs | High | Smoke only first |
| Full suite | Critical | Require user approval |

Secondary dimension: known slow specs (maintained list).
If selected spec is in slow-spec list → treat as +1 tier.

Slow spec list (update after significant runs):
- `order-flow.spec.ts` (multi-page, API calls)
- `accounting-entry-flow.spec.ts` (data seeding)
- Add more as baseline data becomes available.

---

## STATE MANAGEMENT

### Auth State Reuse (Default)
By default, all Playwright specs SHOULD reuse auth state via `storageState`.

Default setup:
1. `globalSetup` script performs login once, saves `storageState` to file.
2. `playwright.config.ts` references `storageState` in `use` section.
3. Individual specs do NOT contain login logic.

Exception — auth lifecycle under test:
- Fresh anonymous session needed
- Multi-login / role-switching flow
- Session expiry behavior
- Login/logout lifecycle
In these cases, spec may manage its own auth. Mark with comment: `// AUTH_LIFECYCLE_TEST`

### Storage State
- Reuse browser storage state across specs within same actor/role.
- Separate state files per actor if multi-role testing is needed.

### Seeded Data
- Prefer API-seeded test data over UI-created data.
- Clean up test data via API, not UI navigation.

---

## FAILURE HANDLING

When tests fail:
1. Analyze first failure only.
2. Classify failure type (see categories below).
3. Fix root cause — one category per fix iteration.
4. Rerun affected spec only (max 1 rerun).

### Failure Categories
- **Selector** — element not found, changed selector
- **Timing** — timeout, race condition, slow load
- **UI** — layout change, component restructure
- **Backend** — API error, missing endpoint, data mismatch
- **Domain** — business logic violation
- **Infrastructure** — server down, network, database

### Do NOT
- Rerun entire suite after single failure.
- Fix multiple unrelated failure categories in one batch.
- Auto-rewrite selectors without reviewing UI change.

---

## SELECTOR STABILITY

Prefer: `data-testid` attributes.

Avoid:
- Text selectors (locale-dependent)
- CSS hierarchy selectors (fragile)
- Positional selectors (order-dependent)

Never rename `data-testid` values unless:
- UI component was intentionally restructured
- Approved by user

---

## TEST OWNERSHIP

Owner: User (tech lead).
AI role: Proposer only.

AI may:
- Identify tests needing update
- Propose specific changes with rationale
- Draft new test code for review

AI must NOT:
- Modify `.spec.ts` files without approval
- Weaken assertions
- Rename `data-testid` values
- Delete or skip tests

Exception: AI may fix selectors/timing in `playwright_fix` workflow
ONLY for classified failures already approved for fix.

---

## EXECUTION LEDGER

After each Playwright workflow completion, append to:
`6_Testing/reports/playwright-ledger.md`

Format (append-only, max 20 recent entries):

| Date | Workflow | Spec(s) | Result | Root Cause | Action Taken |
|------|----------|---------|--------|------------|-------------|

Before starting any Playwright workflow:
- Read last 5 entries from ledger.
- If same spec failed in last 2 entries with same root cause:
  → STOP. Report: "Repeated failure detected. Recommend new session or escalation."

Maintenance:
- User may trim ledger periodically.
- AI must NOT delete ledger entries.

---

## RESULT PRIORITY

1. Acceptance criteria (from plan/spec)
2. Test verification contract
3. Current implementation state
4. Conversation assumptions

Workspace reality overrides test assumptions.
If test expectation conflicts with verified implementation → report as spec drift, do NOT auto-fix.
