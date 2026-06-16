---
description: Plan test strategy and define test responsibility matrix
---

# Test Strategy Planning Skill

## Purpose
Analyze test landscape, define test type responsibilities (Bunit vs Playwright vs Unit), and create test strategy documents.

## When to Use
- Starting test refactor tasks
- Evaluating test coverage gaps
- Defining test strategy for new features
- Resolving test duplication issues

## Key Activities

### 1. Test Inventory
- List all existing tests by type (Bunit, Playwright, Unit)
- Map tests to features and business logic
- Identify test coverage gaps
- Detect duplicate test coverage

### 2. Test Type Analysis
**Bunit Component Tests:**
- Best for: Component rendering, service registration, smoke tests
- Avoid: Layout testing, navigation flows, full business logic
- Cost: Fast, deterministic, easy to maintain

**Playwright E2E Tests:**
- Best for: Business logic flows, validation, navigation, full user journeys
- Avoid: Component-level rendering, service registration
- Cost: Slower, can be flaky, but tests real user behavior

**Unit Tests:**
- Best for: Pure business logic, validation rules, calculations
- Avoid: UI, integration, infrastructure
- Cost: Fastest, most reliable, limited scope

### 3. Responsibility Matrix
Define clear boundaries:
- What each test type should test
- What each test type should NOT test
- How test types complement each other
- When to use which test type

### 4. Strategy Document
Create test strategy document with:
- Test type definitions
- Responsibility matrix
- Decision criteria (when to use which type)
- Examples and patterns

## Decision Framework

### Choose Bunit When:
- Testing component rendering
- Verifying service registration
- Smoke tests for component availability
- No need for full user flow

### Choose Playwright When:
- Testing business logic flows
- Validation across multiple components
- Navigation flows
- Full user journey testing
- Real browser interaction needed

### Choose Unit Tests When:
- Testing pure business logic
- Validation rules
- Calculations
- Domain rules
- No UI or external dependencies

### Avoid Test Duplication:
- If Playwright covers business logic, Bunit should not duplicate
- If Unit tests cover validation, E2E should not duplicate
- Each test type should have unique, complementary scope

## Deliverables
- Test inventory document
- Test strategy document
- Test responsibility matrix
- Decision framework document

## Related Workflows
- `test-refactor-workflow.md` - Primary workflow for test refactor tasks
- `newfeaturebuild.md` - For new feature test planning

## Related Skills
- `test-refactor-cost-benefit` - For cost-benefit analysis
- `pattern-based-fixing` - For implementing test patterns
- `test-system-upgrade` - For test system upgrades
