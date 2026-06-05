# Test System Upgrade Plan

Create two persistent plan files in `docs/plan_MVP/Test_System_Plan/` covering 8 phases to bring the test suite from a broken/incomplete state to a fully green, domain-aligned baseline.

## Files to Create

| File | Path | Purpose |
|---|---|---|
| `TEST_MASTER_PLAN.md` | `docs/plan_MVP/Test_System_Plan/` | 1-page executive summary + phase status table |
| `TEST_DETAIL_PLAN.md` | `docs/plan_MVP/Test_System_Plan/` | Full implementation guide per phase |

## Content Structure

### TEST_MASTER_PLAN.md (1 page)
- Objective + scope
- Phase status table (8 phases) with checkbox `[ ]` / `[x]`
- Current test health dashboard (before/after)
- Success criteria
- How-to-update instructions

### TEST_DETAIL_PLAN.md (multi-page)
- Namespace strategy declaration
- Per-phase: problem → files to touch → exact changes → validation command
- Code snippets for key new test cases
- Update protocol at the bottom

## Update Protocol (embedded in both files)
After each phase completes:
1. Change `[ ]` → `[x]` in Master Plan phase table
2. Add `✅ Completed: YYYY-MM-DD` line in Detail Plan phase section
3. Commit with message: `test: Phase N complete - [description]`
