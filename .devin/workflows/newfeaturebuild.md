---
description: 7 steps build new feature
---

# New Feature Build Workflow

> **Hard Stop, Domain Protection, Objective Lock, Context Control:** See `.windsurfrules`

> **Note:** For test refactor tasks, use `test-refactor-workflow.md` instead.

## Mode: ANALYZE → IMPLEMENT
**ANALYZE (Steps 1-4):** Impact analysis, TDD planning, namespace strategy. No code changes.
**IMPLEMENT (Steps 5-7):** Code, validate, fix. No scope expansion or architecture redesign.

## Phase Isolation
- **Domain:** 1_Shared/Domain.cs
- **Application:** 3_CoreHub/Services, 2_Gateway/Controllers
- **Infrastructure:** Repositories, EF Core, database
- **UI:** 5_WebApps Razor pages/components

Complete each phase before moving to next. Do not mix phases.

## Playwright Guard
- Playwright is DISABLED during Steps 1-6.
- Active skill: `playwright_guard` (auto-activated for this workflow).
- After Step 7 approval, activate `playwright_validation` workflow if E2E validation needed.
- See `.windsurf/rules/playwright.rules.md` for full governance.

## Context Limits
- Max 10 files per phase
- Close unrelated modules after phase completion
- Re-anchor objective + remaining steps after each phase

## Execution Continuity
**After each phase:** completion status, files modified, validation results, next action
**Before each phase:** restate objective, current phase goal, progress %
**Track:** completed / in-progress / pending phases

## Active Skills (max 3 per task)
Select by feature type. State before coding. Deactivate when switching phases.

| Feature Type | Skills (in `.windsurf/skills/`) |
|---|---|
| Accounting UI | `accounting-ui-implementation`, `ui-platform-migration`, `domain-integrity-validation` |
| UI Platform | `ui-platform-migration`, `ui-platform-compliance-review` |
| Outbox/NATS | `outbox-pattern-implementation`, `nats-sqlite-deployment-validation`, `sqlite-concurrency-analysis` |
| E-Invoice | `einvoice-integration`, `ui-platform-compliance-review`, `domain-integrity-validation` |
| HKD Books | `dynamic-hkd-book-architecture`, `domain-integrity-validation` |
| Order Workflow | `order-workflow-unified`, `outbox-pattern-implementation` |
| Period Closing | `period-closing-audit-trail`, `domain-integrity-validation` |
| Refactor | `system-refactor-safety`, `domain-integrity-validation` |
| Tests | `test-system-upgrade`, `pattern-based-fixing` |

## Steps
1. Use Case & Business Design
2. Reverse Impact Analysis + TDD Plan (Windsurf)
3. Detailed Coding Plan + Namespace Strategy (Windsurf)
4. Review & Approval (User)
5. Pre-Implementation Validation: domain entities, namespace, guard-check.ps1
6. Implementation by Phase: Code → Validate (guard-check + build) → Fix errors (RULE_6_1)
7. Review & Approval after each Phase
