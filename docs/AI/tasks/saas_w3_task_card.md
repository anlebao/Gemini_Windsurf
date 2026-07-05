# TASK CARD — SaaS W3: CI Pipeline Restore (E2E + Integration)

> **Status:** NOT STARTED | INVESTIGATE → PLAN → IMPLEMENT
> **Prerequisite:** W0+W1+W2 merged (blockers fixed)
> **Branch:** `feature/saas-w3-ci-pipeline-restore`
> **Estimated sessions:** 1-2
> **Sprint:** 1 (Blockers)

## Objective
Enable E2E tests + Integration tests in CI pipeline. Fix service setup issues. Generate missing auth files for multi-role tests.

## Prerequisites (verify before code)
- [ ] W0 merged (Gateway fixed)
- [ ] W1 merged (secrets fixed)
- [ ] W2 merged (.NET upgraded)
- [ ] Verify `.github/workflows/e2e.yml:115` — `if: false`
- [ ] Verify `.github/workflows/ci.yml:198` — `if: false`
- [ ] Verify `6_Testing/e2e-tests/` — 21 spec files
- [ ] Verify auth files: only `auth/admin.json` exists, missing staff/storekeeper/guard
- [ ] Grep `test.skip()` in E2E specs — 29 instances

## Files to Modify
| File | Changes |
|------|---------|
| `.github/workflows/e2e.yml:115` | Remove `if: false`, enable E2E job |
| `.github/workflows/ci.yml:198` | Remove `if: false`, enable Integration job |
| `6_Testing/e2e-tests/global-setup.ts` | Add auth file generation for Staff, StoreKeeper, Guard roles |
| `6_Testing/e2e-tests/rbac-enforcement.spec.ts` | Remove `test.skip()` for multi-role tests |
| `6_Testing/e2e-tests/` (all specs) | Remove unnecessary `test.skip()`, fix service setup issues |

## Detailed Task List

### W3-T1: INVESTIGATE — Why E2E was disabled
- Read `e2e.yml` comments — "E2E tests need service setup fixes"
- Run E2E tests locally: `npx playwright test` — identify failures
- Categorize failures:
  - Service setup issues (missing env vars, wrong ports)
  - Auth file issues (missing staff/storekeeper/guard)
  - Selector/timing issues (UI changed since E2E written)
  - Tier disabled issues (`isTierEnabled('e2e')` returns false)

### W3-T2: Fix service setup
- Check `e2e.yml` service configuration:
  - Are all services started? (ShopERP, Gateway, KhachLink, Seq, NATS)
  - Are ports correct?
  - Are env vars set?
- Fix any missing service startup
- Verify health checks pass before tests run

### W3-T3: Generate missing auth files
**File:** `6_Testing/e2e-tests/global-setup.ts`
- Add login flow for Staff role → save `auth/staff.json`
- Add login flow for StoreKeeper role → save `auth/storekeeper.json`
- Add login flow for Guard role → save `auth/guard.json`
- Verify seed data has users for each role

### W3-T4: Remove test.skip() for multi-role
**File:** `6_Testing/e2e-tests/rbac-enforcement.spec.ts:12-14`
- Remove `test.skip()` for Staff/StoreKeeper/Guard tests
- Verify tests pass with new auth files
- Fix any RBAC assertions that may have drifted

### W3-T5: Remove tier disabled skips
- Find `isTierEnabled('e2e')` checks — 7 instances
- Either enable e2e tier or remove conditional skips
- Verify tests run without tier check

### W3-T6: Enable CI jobs
**File:** `.github/workflows/e2e.yml:115`
```yaml
# BEFORE:
if: false  # Temporarily disabled - E2E tests need service setup fixes
# AFTER:
# (remove the if: false line, or change to if: true)
```

**File:** `.github/workflows/ci.yml:198`
```yaml
# BEFORE:
if: false  # Disabled to save GitHub Actions minutes
# AFTER:
# (remove the if: false line)
```

### W3-T7: Run CI pipeline
- Push to feature branch
- Verify CI pipeline runs E2E + Integration jobs
- Fix any remaining failures
- All E2E tests PASS (or document acceptable skips)

## Verification
- [ ] `.github/workflows/e2e.yml` — no `if: false`
- [ ] `.github/workflows/ci.yml` — no `if: false`
- [ ] Auth files exist: `auth/admin.json`, `auth/staff.json`, `auth/storekeeper.json`, `auth/guard.json`
- [ ] `test.skip()` count reduced from 29 to <5 (only legitimate skips)
- [ ] CI pipeline: E2E job runs and PASS
- [ ] CI pipeline: Integration job runs and PASS
- [ ] Build 0 errors, guard pass

## Rollback
- Git revert (restore `if: false`)
- If E2E tests too flaky: re-disable with documented reason
- If auth generation breaks: revert global-setup.ts

## Open Questions
- Q1: E2E service setup — cần Docker compose trong CI hay start individual services?
- Q2: Auth files — generate trong global-setup hay commit static files?
- Q3: Flaky E2E tests — retry policy? (Playwright built-in retry: 2)
