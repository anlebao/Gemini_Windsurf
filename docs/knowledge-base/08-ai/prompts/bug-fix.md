# Prompt: Bug Fix

> **Agent**: Build Fixer  
> **Mode**: FIX_ONLY  
**Workflow**: Fix_Errors.md

## System Prompt

```
You are the Build Fixer Agent for ShopERP.

CURRENT MODE: FIX_ONLY

Your task is to fix build errors and test failures with minimal, 
pattern-based changes.

CONSTRAINTS:
- Max 3 files per batch
- Never modify Domain.cs
- Pattern-based fixing only
- One fix category at a time
- Build must pass after fix

HARD STOPS (escalate to user):
- Domain layer modification required
- Same error after 3 attempts
- Architectural change needed
- Unclear root cause
```

## User Prompt Template

```
## Error Report

**Error Type**: {{ERROR_TYPE}}
- [ ] Compilation error
- [ ] Unit test failure
- [ ] Runtime error
- [ ] Other

**Error Message**:
```
{{ERROR_MESSAGE}}
```

**Stack Trace** (if available):
```
{{STACK_TRACE}}
```

**Affected Files**:
{{AFFECTED_FILES}}

**Last Change** (if known):
{{LAST_CHANGE}}

## Context

Read these files:
- docs/knowledge-base/00-core/PROJECT_CONTEXT.md
- .windsurf/rules/.windsurfrules
- .windsurf/skills/pattern-based-fixing.md

## Expected Output

1. ROOT CAUSE: Analysis of error
2. PATTERN MATCH: What pattern is broken
3. FIX PROPOSAL: Minimal change to fix
4. VALIDATION: How to verify fix
5. IMPLEMENTATION: Execute fix
```

## Example Usage

### Input: Compilation Error
```
Error Type: Compilation error

Error Message:
CS1061: 'Order' does not contain a definition for 'TotalPrice'

Affected Files:
- 5_WebApps/ShopERP/Components/Pages/Orders.razor

Last Change:
- Modified Order entity yesterday
```

### Expected AI Response

**Step 1: Analyze**
```
ROOT CAUSE ANALYSIS:
- Error: CS1061 - property not found
- Location: Orders.razor line 45
- Context: Displaying order total in table

ACTUAL DOMAIN CHECK:
- Order entity has: TotalAmount (protected set)
- Order entity has: TotalPrice => TotalAmount (alias)
- Problem: Property exists but access modifier hoặc naming

PATTERN MATCH:
- Pattern: Domain property renamed hoặc removed
- Fix: Use correct property name (TotalAmount hoặc TotalPrice alias)
```

**Step 2: Fix Options**
```
OPTION 1: Use existing alias (PREFERRED)
- No code change needed
- Document the alias

OPTION 2: Add missing property
- Add TotalPrice getter if removed
- File: 1_Shared/Domain.cs (REJECTED - Domain change)

OPTION 3: Fix caller
- Change Orders.razor to use TotalAmount
- File: 5_WebApps/ShopERP/Components/Pages/Orders.razor (ACCEPTED)
```

**Step 3: Implementation**
```
Fix: Change Orders.razor line 45
FROM: @order.TotalPrice
TO:   @order.TotalAmount

Validation: Build → Test
```

### Input: Test Failure
```
Error Type: Unit test failure

Error Message:
Expected: 100000
Actual: 110000

Stack Trace:
AccountingServiceTests.CalculateVAT_ShouldReturnCorrectAmount()
```

### Expected AI Response

**Step 1: Analyze**
```
ROOT CAUSE:
- Test expects 100000 (before VAT)
- Actual 110000 (with 10% VAT)
- VAT calculation changed recently

PATTERN MATCH:
- Behavior change: VAT now included
- Test expects old behavior

FIX DECISION:
- If new behavior correct → Update test expectation
- If new behavior wrong → Fix calculation logic
```

## Common Fix Patterns

| Pattern | Symptom | Fix |
|---------|---------|-----|
| Missing using | CS0246 | Add using statement |
| Missing namespace | CS0103 | Add namespace hoặc using |
| Property rename | CS1061 | Update reference or alias |
| Async mismatch | CS4032 | Add await hoặc .Result |
| Generic constraint | CS0314 | Add constraint or change type |
| Access modifier | CS0122 | Change to public/internal |

## Validation Commands

```bash
# After fix
dotnet build VanAn.sln
dotnet test 6_Tests/VanAn.Core.Tests/ --filter "FullyQualifiedName~{{TestClass}}"
./guard-check.ps1
```

## Stop Conditions

**Must escalate if:**
- Fix requires Domain.cs change
- Same error persists after 3 attempts
- >3 files needed
- Architectural change required
- Unclear root cause

## References

- `docs/knowledge-base/08-ai/AGENTS.md` - Agent definition
- `.windsurf/workflows/Fix_Errors.md` - Full workflow
- `.windsurf/skills/pattern-based-fixing.md` - Patterns

---

*Version: 1.0*
