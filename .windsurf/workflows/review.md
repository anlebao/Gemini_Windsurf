---
auto_execution_mode: 0
description: Review code changes for bugs, security issues, and improvements
---

# Code Review Workflow

> **Hard Stop, Domain Protection, Scope rules:** See `.windsurfrules`

## Mode: REVIEW_ONLY
Analyze and report only. No code modifications, no refactoring, no architecture changes.

## Review Modes
- **review_security:** Vulnerabilities, auth, data exposure
- **review_sqlite:** DB operations, SQL injection, transactions, connections
- **review_domain:** Domain logic, entity integrity, business rules, DDD
- **review_ui:** UI components, accessibility, responsive design, UX
- **review_plan_compliance:** Compare implementation vs. approved plan/milestone

Activate only the relevant mode. Do not mix scopes.

## Context Limits
- Max 5 files per review session
- `review_plan_compliance`: max 10 files per pass unless user approves broader scope
- Before starting: restate specific code changes being reviewed

## Execution Continuity
**After each section:** scope completed, findings count (bugs/security/improvements), next section
**Before each section:** restate objective, review mode, progress %
**Track:** completed / in-progress / pending review sections

## Active Skills (default 1, max 2)
State review mode and active skill before reviewing. Deactivate when switching modes.

| Review Mode | Skill (in `.windsurf/skills/`) |
|---|---|
| review_security | `system-refactor-safety` |
| review_sqlite | `sqlite-concurrency-analysis` |
| review_domain | `domain-integrity-validation` |
| review_ui | `ui-platform-compliance-review` |
| review_plan_compliance | Select by plan domain |

Feature-specific: `outbox-pattern-implementation`, `einvoice-integration`, `dynamic-hkd-book-architecture`, `order-workflow-unified`, `accounting-ui-implementation`, `test-system-upgrade`

## Plan Compliance Output (`review_plan_compliance`)

| Plan Task | Expected Evidence | Actual Evidence | Status | Notes |
|---|---|---|---|---|
| Task name | Files/components/tests expected | Found in code or absent | Completed/Partial/Not Completed/Unknown | Gap or risk |

**Status:** Completed (evidence aligns) · Partial (incomplete) · Not Completed (absent) · Unknown (insufficient)
**Summary:** Counts + high-risk gaps + recommended next focus

---

## Review Focus Areas
1. Logic errors and incorrect behavior
2. Unhandled edge cases
3. Null/undefined reference issues
4. Race conditions or concurrency
5. Security vulnerabilities
6. Resource leaks
7. API contract violations
8. Caching bugs (staleness, keys, invalidation)
9. Pattern/convention violations

## Rules
- Call multiple tools in parallel for efficiency
- Pre-existing bugs: report only if they affect changed code or are security/data integrity risks
- No speculative or low-confidence issues
- Git commit may not be checked out; verify local code state