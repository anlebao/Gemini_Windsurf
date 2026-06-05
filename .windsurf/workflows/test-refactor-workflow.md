---
description: Test refactor workflow with strategy planning
---

# Test Refactor Workflow

> **Hard Stop, Domain Protection, Objective Lock, Context Control:** See `.windsurfrules`

## Mode: ANALYZE → IMPLEMENT
**ANALYZE (Steps 1-3):** Test scope analysis, impact analysis, strategy definition. No code changes.
**IMPLEMENT (Steps 4-6):** Implementation, validation, fix. No scope expansion.

## Phase Isolation
- **Analysis:** Test files, test plan, existing test coverage
- **Implementation:** Test code only (no production code changes unless justified)
- **Validation:** Test execution, coverage reports

Complete each phase before moving to next. Do not mix phases.

## Context Limits
- Max 10 files per phase
- Close unrelated modules after phase completion
- Re-anchor objective + remaining steps after each phase

## Execution Continuity
**After each phase:** completion status, files modified, validation results, next action
**Before each phase:** restate objective, current phase goal, progress %
**Track:** completed / in-progress / pending phases

## Active Skills (max 3 per task)
Select by refactor type. State before coding. Deactivate when switching phases.

| Refactor Type | Skills (in `.windsurf/skills/`) |
|---|---|
| Test Strategy | `test-strategy-planning`, `test-refactor-cost-benefit` |
| Component Tests | `test-strategy-planning`, `pattern-based-fixing` |
| E2E Tests | `test-strategy-planning`, `test-system-upgrade` |
| Unit Tests | `test-strategy-planning`, `domain-integrity-validation` |

## Steps

### 1. Test Scope Analysis
**Goal:** Understand current test landscape and identify gaps

**Activities:**
- Inventory existing tests (Bunit, Playwright, Unit)
- Identify test coverage gaps
- Map test types to features
- Identify duplicate test coverage

**Deliverables:**
- Test inventory document
- Coverage gap analysis
- Duplicate detection report

**Skills:** `test-strategy-planning`

### 2. Impact Analysis
**Goal:** Assess cost-benefit of test refactor options

**Activities:**
- Evaluate refactor options (Simplify vs Rewrite vs New)
- Assess impact on production code (if any)
- Estimate time and effort
- Identify risks and mitigation strategies

**Deliverables:**
- Cost-benefit analysis
- Risk assessment
- Recommended approach (Simplify/Rewrite/New)

**Skills:** `test-refactor-cost-benefit`

### 3. Test Strategy Definition
**Goal:** Define clear test responsibility matrix

**Activities:**
- Define test type responsibilities (Bunit vs Playwright vs Unit)
- Create test strategy document
- Get user approval on strategy

**Deliverables:**
- Test strategy document
- Test responsibility matrix
- User approval

**Skills:** `test-strategy-planning`

### 4. Implementation Plan
**Goal:** Detailed plan for test refactor

**Activities:**
- Create detailed coding plan
- Define file-by-file changes
- Identify dependencies
- Plan validation approach

**Deliverables:**
- Detailed implementation plan
- File change list
- Validation plan

**Skills:** `pattern-based-fixing` (if using patterns)

### 5. Implementation
**Goal:** Execute test refactor according to plan

**Activities:**
- Implement test changes
- Follow test strategy
- No production code changes unless justified
- Update test documentation

**Deliverables:**
- Refactored test files
- Updated test documentation
- Implementation notes

**Skills:** `pattern-based-fixing` or `test-system-upgrade`

### 6. Validation
**Goal:** Verify tests pass and meet objectives

**Activities:**
- Run test suite
- Verify all tests pass
- Check test coverage
- Validate against strategy

**Deliverables:**
- Test execution results
- Coverage report
- Validation summary

**Skills:** `test-system-upgrade`

## Decision Points

### When to Simplify Tests
- Tests are failing due to infrastructure issues (layout, dependencies)
- Duplicate coverage exists with other test types
- Maintenance cost exceeds value
- Production code refactor is not justified

### When to Rewrite Tests
- Tests no longer match current business logic
- Test architecture needs improvement
- Better test patterns available
- Clear business value in improved tests

### When to Create New Tests
- New features need test coverage
- Critical business logic lacks tests
- Risk areas identified
- User requests specific test coverage

## Success Criteria
- All tests pass
- Test strategy followed
- No unnecessary production code changes
- Test coverage maintained or improved
- Documentation updated
- Tests are maintainable

## Exit Criteria
- Tests pass
- Strategy document updated
- Test plan documented
- No regressions introduced
