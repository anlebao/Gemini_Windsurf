---
description: Minimize Playwright browser execution cost with deterministic rules
---

# Skill: Playwright Cost Optimizer

## Purpose
Minimize browser execution cost using deterministic rules.

## When to Use
- Before any Playwright execution
- When selecting which specs to run
- When deciding execution order

## Cost Tiers (Deterministic)

| Affected Specs | Tier | Action |
|----------------|------|--------|
| 1 spec | Low | Run directly |
| 2-5 specs | Medium | Run scoped by project |
| >5 specs | High | Smoke only first |
| Full suite | Critical | Require user approval |

Secondary: known slow specs → treat as +1 tier.
See governance rules for slow-spec list.

## Execution Order
Always execute in this order, stop as early as possible:
1. **Smoke** — basic health check
2. **Feature** — specific feature under test
3. **Actor** — specific user role flow
4. **Regression** — broader coverage (only if requested)

## State Reuse (Default)
- Default: reuse `storageState` for auth
- Reuse browser context when testing same actor
- Prefer API for test data setup/teardown
- Exception: auth lifecycle tests may manage own auth (marked `// AUTH_LIFECYCLE_TEST`)

## Failure Diagnosis Priority
When failure occurs, check in this order:
1. **Selector** — element renamed or removed?
2. **Timing** — timeout too short? async load?
3. **State** — auth expired? data missing?
4. **Backend** — API error? endpoint changed?
5. **Architecture** — component restructured?

Stop at first confirmed cause. Do NOT investigate further categories.

## Do NOT
- Run full regression without approval
- Execute multi-role tests when single-role suffices
- Rewrite tests to work around failures
- Change test contracts to reduce execution cost

## Recommend New Session When
- 3+ repeated failures on same spec
- Browser state appears corrupted
- Auth state expired mid-run
- Unrelated test interference detected
