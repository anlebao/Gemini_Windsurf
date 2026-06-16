---
description: Fix failed E2E Playwright tests with systematic approach
---

# Playwright E2E Fix Workflow

> **Governance:** `.windsurf/rules/playwright.rules.md`

## Mode: FIX_ONLY
Fix classified E2E test failures with systematic implementation and verification.

## Trigger
- Playwright triage completed with UI/Backend/Selector issues
- User requests to proceed with fixing failed tests
- Post-implementation validation shows failures requiring fixes

## Prerequisites
- Triaged failure report available (from `playwright_triage.md`)
- Root cause identified and classified
- Fix scope estimated

## Step 1 — Apply Code Fixes

Based on triage classification:

**UI Issues:**
- Modify component rendering logic
- Fix parameter types (enum vs string, etc.)
- Update component lifecycle if needed

**Selector Issues:**
- Update test selectors to match new DOM structure
- Fix element IDs/classes if changed

**Backend Issues:**
- Fix API endpoints
- Update response formats
- Handle error cases

**STOP** - Verify build succeeds before proceeding.

## Step 2 — Rebuild Application

```bash
cd 5_WebApps/ShopERP
dotnet build
```

If build fails:
- Fix compilation errors
- Retry build
- Max 3 build attempts

## Step 3 — Restart Application Server

Kill existing process on port 5003:
```bash
taskkill /F /IM VanAn.ShopERP.exe
# OR find PID and kill
netstat -ano | findstr :5003
taskkill /F /PID <PID>
```

Start application:
```bash
cd 5_WebApps/ShopERP
dotnet run
```

Wait for "Now listening on: http://localhost:5003" message.

## Step 4 — Run Affected Tests

Run only the failing spec to verify fix:
```bash
cd 6_Testing
npx playwright test e2e-tests/<failing-spec>.spec.ts --project=chromium
```

If tests pass:
- Run full test suite for that spec
- Mark as fixed

If tests fail:
- Check application logs for runtime errors
- Inspect browser console for JavaScript errors
- Return to Step 1 with new findings

## Step 5 — Verification Criteria

Test passes when:
- All assertions succeed
- No timeout errors
- Expected elements are visible and interactive
- Alerts/messages appear as expected

## Step 6 — Rollback Plan

If fix doesn't work after 3 iterations:
- Revert code changes
- Document findings
- Escalate to user for alternative approach

## Fix Budget
- Max 3 iterations per fix
- If no measurable progress after 3 attempts → stop and escalate
- Progress = test passes more assertions or different error

## Output Report Template

After fix attempt:
| Step | Status | Notes |
|------|--------|-------|
| Code Fix | ✅/❌ | What was changed |
| Build | ✅/❌ | Build errors if any |
| Server Restart | ✅/❌ | Port conflicts if any |
| Test Run | ✅/❌ | Test results |
| Verification | ✅/❌ | Pass/fail criteria |

## Context Limits
- Single spec per session
- Max 3 fix iterations
- Must have triage report before starting
