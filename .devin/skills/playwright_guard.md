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
