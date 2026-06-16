---
description: Fix Playwright failures requiring architectural workarounds with technical debt tracking
---

# Playwright Fix Architectural Workflow

**Use when:** Test failures stem from architectural issues (tenant isolation, globalization, component binding, auth claims) requiring workarounds.

**Contrast:** Use `playwright_fix.md` for simple fixes (selectors, timing). Use this workflow when root cause requires workaround + debt packaging.

## Phase 1: Debug & Diagnose

// turbo
- [ ] Run triage via `playwright_triage.md` — confirm Category D (Data/Architectural) or E (Blazor Interactivity)
- [ ] Read app logs (pattern: `**/bin/Debug/**/Logs/*.txt`)
- [ ] Check `runtimeconfig.json` for `InvariantGlobalization`
- [ ] Inspect failed test screenshot/trace
- [ ] Identify: Is this fixable immediately or requires workaround?

## Phase 2: Implement Fix with Workaround

// turbo
- [ ] Fix immediate issues (wrong selectors, missing testids)
- [ ] If architectural fix needed but risky/time-consuming:
  - Apply workaround (fallback tenant, JS interop, culture replace)
  - Mark with TECH DEBT comment (see `technical_debt_management.md`)
- [ ] Update test assertions to match expected behavior
- [ ] Do NOT weaken assertions — fix the root cause or document workaround

## Phase 3: Package Technical Debt

Run `technical_debt_packaging.md`:
- [ ] Mark all workarounds with `// TODO: [TECH DEBT] [VẠN AN PLATFORM HARDENING]`
- [ ] Clean diagnostic logs added during debug
- [ ] Create/update `TECHNICAL_DEBT_LEDGER.md` with:
  - Tier 1: Critical (data integrity, security)
  - Tier 2: Quality (UX, code smell)
  - Remediation plan for each item

## Phase 4: Tiered Remediation Planning

- [ ] Document Tier 1 fixes needed for Hardening Phase 2
- [ ] Document Tier 2 fixes (after Tier 1 complete)
- [ ] Update test assertions to detect if workaround breaks

## Phase 5: Verify

// turbo
- [ ] Rebuild affected project
- [ ] Run targeted spec: `npx playwright test <spec> --project=<project> --reporter=line`
- [ ] Verify E2E passes with current workarounds
- [ ] Build passes with 0 new warnings

## Stop Conditions

- 3 fix iterations with no progress → STOP and escalate
- Workaround required but no clear remediation plan → STOP, request approval
- Tier 1 debt > 5 items → STOP, schedule hardening sprint

## Output Artifacts

1. Code changes with TECH DEBT markers
2. Updated `TECHNICAL_DEBT_LEDGER.md`
3. Test spec with strengthened assertions
4. Rebuild/retest verification report

## Integration

- **Entry point:** Called from `playwright_triage.md` when Category D/E detected
- **During fix:** References `technical_debt_management.md` skill
- **Exit to:** `playwright_validation.md` for final verification
