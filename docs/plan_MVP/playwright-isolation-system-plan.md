# PLAYWRIGHT ISOLATION SYSTEM — DETAILED CODING PLAN

**Created:** 2026-05-26
**Status:** APPROVED (2026-05-26 14:16 UTC+7)
**Modifications applied:** 6 user corrections + execution ledger + test ownership
**Objective:** Isolate Playwright from implementation workflow to prevent AI loop (implement → run → fix → rerun → lose objective)

---

## SCOPE SUMMARY

| # | Type | File | Action |
|---|------|------|--------|
| 1 | Rule | `.windsurf/rules/.windsurfrules` | UPDATE — add 6-line Playwright anchor |
| 2 | Rule | `.windsurf/rules/playwright.rules.md` | CREATE — full governance + state mgmt + retry policy |
| 3 | Workflow | `.windsurf/workflows/playwright_triage.md` | CREATE — collect → classify → route (no fix) |
| 4 | Workflow | `.windsurf/workflows/playwright_validation.md` | CREATE — validate completed implementation |
| 5 | Workflow | `.windsurf/workflows/playwright_fix.md` | CREATE — fix classified failures only |
| 6 | Skill | `.windsurf/skills/playwright_cost_optimizer.md` | CREATE — deterministic cost tiers |
| 7 | Skill | `.windsurf/skills/playwright_guard.md` | CREATE — disable browser during implement |
| 8 | Workflow | `.windsurf/workflows/Fix_Tests.md` | UPDATE — add Playwright routing |
| 9 | Workflow | `.windsurf/workflows/newfeaturebuild.md` | UPDATE — add Playwright guard reference |

| 10 | Ledger | `6_Testing/reports/playwright-ledger.md` | CREATE — execution tracking (append-only) |

**Total:** 2 files updated, 6 files created. Zero production code changes.

### Modifications Applied (post-review)
1. FIX_ONLY allows limited Playwright (single spec, max 1 per session)
2. TRIAGE allows scoped reproduction when reports insufficient
3. Auth State Reuse default with exception for auth lifecycle tests
4. Cost tiers use recorded baseline instead of runtime estimation
5. Fix budget: 3 iterations AND no measurable progress (not hard 3)
6. TEST OWNERSHIP added (User = owner, AI = proposer)
7. Browser session count removed from cost optimizer
8. PLAYWRIGHT EXECUTION LEDGER added (append-only, max 20 entries)

---

## CHANGE #1 — Update `.windsurfrules`

**File:** `.windsurf/rules/.windsurfrules`
**Location:** Insert after line 86 (after `## WORKFLOW REFERENCES` section, before `## ERROR HANDLING`)
**Rationale:** Minimal anchor — keeps `.windsurfrules` lean, delegates detail to `playwright.rules.md`

```markdown
## PLAYWRIGHT ISOLATION
- Playwright is DISABLED during IMPLEMENT mode.
- FIX_ONLY: Playwright allowed for single spec explicit validation only (max 1 per session).
- Enable Playwright ONLY after: build passes AND implementation complete.
- Playwright governance: `.windsurf/rules/playwright.rules.md`
- Playwright triage: `.windsurf/workflows/playwright_triage.md`
- Playwright validation: `.windsurf/workflows/playwright_validation.md`
- Playwright fix: `.windsurf/workflows/playwright_fix.md`
```

**Impact:** 7 lines added to `.windsurfrules`. No existing content modified.

---

## CHANGE #2 — Create `playwright.rules.md`

**File:** `.windsurf/rules/playwright.rules.md`
**Rationale:** Full governance — always referenced by Playwright workflows, keeps detail out of `.windsurfrules`

```markdown
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
```

---

## CHANGE #3 — Create `playwright_triage.md`

**File:** `.windsurf/workflows/playwright_triage.md`
**Rationale:** Collect and classify failures WITHOUT fixing. Routes to correct workflow. Saves tokens by preventing fix attempts on misclassified failures.

```markdown
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
```

---

## CHANGE #4 — Create `playwright_validation.md`

**File:** `.windsurf/workflows/playwright_validation.md`
**Rationale:** Post-implementation validation with minimal execution cost. Step-by-step with STOP points.

```markdown
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
```

---

## CHANGE #5 — Create `playwright_fix.md`

**File:** `.windsurf/workflows/playwright_fix.md`
**Rationale:** Fix classified Playwright failures with hard scope limits. Restore existing behavior only.

```markdown
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
- 3 consecutive fix iterations fail to reduce failures → STOP

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
```

---

## CHANGE #6 — Create `playwright_cost_optimizer.md`

**File:** `.windsurf/skills/playwright_cost_optimizer.md`
**Rationale:** Deterministic cost control — no ambiguous "estimate cost" instructions.

```markdown
---
description: Minimize Playwright browser execution cost with deterministic rules
---

# Skill: Playwright Cost Optimizer

## Purpose
Minimize browser execution cost using deterministic rules.

## When to Use
- Before any Playwright execution
- When selecting which specs to run
- When deciding execution order

## Cost Tiers (Deterministic)

| Affected Specs | Tier | Action |
|----------------|------|--------|
| 1 spec | Low | Run directly |
| 2-5 specs | Medium | Run scoped by project |
| >5 specs | High | Smoke only first |
| Full suite | Critical | Require user approval |

Secondary: known slow specs → treat as +1 tier.
See governance rules for slow-spec list.

## Execution Order
Always execute in this order, stop as early as possible:
1. **Smoke** — basic health check
2. **Feature** — specific feature under test
3. **Actor** — specific user role flow
4. **Regression** — broader coverage (only if requested)

## State Reuse (Default)
- Default: reuse `storageState` for auth
- Reuse browser context when testing same actor
- Prefer API for test data setup/teardown
- Exception: auth lifecycle tests may manage own auth (marked `// AUTH_LIFECYCLE_TEST`)

## Failure Diagnosis Priority
When failure occurs, check in this order:
1. **Selector** — element renamed or removed?
2. **Timing** — timeout too short? async load?
3. **State** — auth expired? data missing?
4. **Backend** — API error? endpoint changed?
5. **Architecture** — component restructured?

Stop at first confirmed cause. Do NOT investigate further categories.

## Do NOT
- Run full regression without approval
- Execute multi-role tests when single-role suffices
- Rewrite tests to work around failures
- Change test contracts to reduce execution cost

## Recommend New Session When
- 3+ repeated failures on same spec
- Browser state appears corrupted
- Auth state expired mid-run
- Unrelated test interference detected
```

---

## CHANGE #7 — Create `playwright_guard.md`

**File:** `.windsurf/skills/playwright_guard.md`
**Rationale:** Hard boundary — prevents browser launch during implementation.

```markdown
---
description: Prevent Playwright execution during implementation mode
---

# Skill: Playwright Implement Guard

## Purpose
Enforce Playwright isolation during implementation.

## When Active
This skill is active during:
- `newfeaturebuild.md` Steps 1-6
- `Fix_Errors.md` all phases
- `Fix_Tests.md` all phases (for dotnet test only, not Playwright)
- Any IMPLEMENT mode

Note: FIX_ONLY mode allows limited Playwright (single spec, max 1 per session).
See governance rules for FIX_ONLY Playwright policy.

## While Implementation Is Active

### ALLOWED
- Write code
- Compile (`dotnet build`)
- Run unit tests (`dotnet test`)
- Run `guard-check.ps1`
- Local validation (build output, compiler errors)

### FORBIDDEN
- `npx playwright test` or any Playwright CLI command
- Browser launch (headless or headed)
- End-to-end test execution
- Playwright test generation (`codegen`)
- Screenshot comparison runs

## Enable Playwright Only When ALL conditions met
1. Implementation phase complete (Step 7 of newfeaturebuild or post-fix)
2. `dotnet build VanAn.sln` returns 0 errors
3. `guard-check.ps1` passes
4. User explicitly requests validation or activates a Playwright workflow

## If Tempted to Run Playwright During Implementation
- STOP
- State: "Playwright is disabled during implementation per playwright_guard skill"
- Suggest: "Complete implementation first, then activate playwright_validation workflow"

## Exception
If user explicitly overrides with a direct command like "run this specific Playwright test now",
acknowledge the override and run ONLY the specified test. Do NOT expand scope.
```

---

## CHANGE #8 — Update `Fix_Tests.md`

**File:** `.windsurf/workflows/Fix_Tests.md`
**Location:** After line 15 (after "Test project has compile errors..." trigger), add Playwright routing block.
**Rationale:** Route Playwright failures to dedicated workflow instead of mixing with dotnet test fixes.

**Add after line 16:**

```markdown

## Playwright Routing
If failing tests are **Playwright E2E tests** (`.spec.ts` files in `6_Testing/`):
- Do NOT use this workflow.
- Route to `.windsurf/workflows/playwright_triage.md` for classification first.
- Then use `.windsurf/workflows/playwright_fix.md` for fixes.
- This workflow handles `dotnet test` failures ONLY.
```

**Impact:** 6 lines added. No existing content modified.

---

## CHANGE #9 — Update `newfeaturebuild.md`

**File:** `.windsurf/workflows/newfeaturebuild.md`
**Location:** After line 21 (after "Do not mix phases."), add Playwright guard reference.
**Rationale:** Explicit guard during implementation phases.

**Add after line 21:**

```markdown

## Playwright Guard
- Playwright is DISABLED during Steps 1-6.
- Active skill: `playwright_guard` (auto-activated for this workflow).
- After Step 7 approval, activate `playwright_validation` workflow if E2E validation needed.
- See `.windsurf/rules/playwright.rules.md` for full governance.
```

**Impact:** 5 lines added. No existing content modified.

---

## IMPLEMENTATION ORDER

| Priority | Change # | File | Risk |
|----------|----------|------|------|
| 1 | #7 | `playwright_guard.md` (skill) | None — new file |
| 2 | #1 | `.windsurfrules` update | Low — append only |
| 3 | #2 | `playwright.rules.md` (rule) | None — new file |
| 4 | #9 | `newfeaturebuild.md` update | Low — append only |
| 5 | #8 | `Fix_Tests.md` update | Low — append only |
| 6 | #3 | `playwright_triage.md` (workflow) | None — new file |
| 7 | #5 | `playwright_fix.md` (workflow) | None — new file |
| 8 | #4 | `playwright_validation.md` (workflow) | None — new file |
| 9 | #6 | `playwright_cost_optimizer.md` (skill) | None — new file |
| 10 | #10 | `playwright-ledger.md` (ledger) | None — new file |

---

## CHANGE #10 — Create `playwright-ledger.md`

**File:** `6_Testing/reports/playwright-ledger.md`
**Rationale:** Track Playwright executions to prevent repeated failure loops across sessions.

```markdown
# Playwright Execution Ledger

Append-only log. Max 20 recent entries. AI must NOT delete entries.

| Date | Workflow | Spec(s) | Result | Root Cause | Action Taken |
|------|----------|---------|--------|------------|-------------|
| (empty — first entry will be added on first Playwright run) | | | | | |
```

---

## FUTURE IMPROVEMENT (OUT OF SCOPE)

These are noted but NOT part of this plan:

1. **Auth state refactor in actual spec files** — Convert `beforeEach` login to `globalSetup` + `storageState` in `6_Testing/`. This is a code change, not a governance change. Should be a separate task after this system is in place.

2. **`data-testid` migration** — Add `data-testid` attributes to Blazor components. Requires UI phase work.

3. **Playwright project restructure** — Current `playwright.config.ts` has projects for `smoke-tests` and `e2e-tests` plus browser-specific projects (chromium, firefox, webkit, mobile). May need cleanup to align with cost tiers.

---

## APPROVAL CHECKLIST

- [x] Agree with anchor approach in `.windsurfrules` (updated: FIX_ONLY limited, not disabled)
- [x] Agree with `playwright.rules.md` governance content (updated with all 6 modifications)
- [x] Agree with `playwright_triage.md` workflow (updated: allow scoped reproduction)
- [x] Agree with `playwright_validation.md` workflow (post-implementation)
- [x] Agree with `playwright_fix.md` workflow (updated: conditional stop, not hard 3)
- [x] Agree with `playwright_cost_optimizer.md` skill (updated: no browser session count, recorded baseline)
- [x] Agree with `playwright_guard.md` skill (updated: FIX_ONLY limited exception)
- [x] Agree with `Fix_Tests.md` routing addition
- [x] Agree with `newfeaturebuild.md` guard addition
- [x] Agree with implementation order
- [x] "Tests are verification contracts, not immutable artifacts" wording
- [x] Retry policy: max 1 rerun per spec, then diagnose
- [x] State management rules (storageState reuse default, exception for auth lifecycle)
- [x] TEST OWNERSHIP added (User = owner, AI = proposer)
- [x] EXECUTION LEDGER added (append-only, max 20)
- [x] Future improvements noted but out of scope
