# Skill: Dynamic HKD Book Architecture

## Purpose
Implement or review Dynamic HKD Book formula, calculation, caching, and template architecture.

## Use When
- Working on Dynamic HKD Books.
- Implementing formula engine, scoped data provider, pre-aggregation, or explainable calculations.
- Reviewing accounting book calculation stability.

## Core Rules
- DSL syntax must remain stable: `SUM_ACCOUNT("5*", "Credit")`.
- Avoid breaking stored templates or formula data.
- Keep calculation trace explainable.
- Preserve multi-tenancy and request-scoped data access.
- Validate circular dependencies and missing variables.

## Procedure
1. Identify active Dynamic HKD phase.
2. Preserve final DSL syntax.
3. Implement or review one engine component at a time.
4. Add/verify tests for formula behavior and dependencies.
5. Validate performance and caching expectations.
6. Report calculation correctness and risks.

## Stop Conditions
- DSL syntax changes without approval.
- Calculation is not explainable or auditable.
- Tenant scope is missing.
- Caching can return cross-tenant or stale accounting results.

## References
- `docs/plan_MVP/Dynamic_HKD_Books_Master_Plan_v3.0.md`
- `.windsurf/rules/.windsurfrules`
