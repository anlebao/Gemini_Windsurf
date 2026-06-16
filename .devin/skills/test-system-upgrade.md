# Skill: Test System Upgrade

## Purpose
Repair, upgrade, and maintain the test system while preserving source-of-truth and domain integrity.

## Use When
- Test projects are broken or incomplete.
- Test namespace conflicts appear.
- TestEntityBuilder or domain-aligned tests are required.

## Core Rules
- Do not duplicate domain entities in test projects.
- Test namespaces must not leak into production source.
- Use TestEntityBuilder for domain entity construction where required.
- Prefer behavior assertions over fragile mock verification.
- Update plan status after each completed phase.

## Procedure
1. Identify failing test project and namespace scope.
2. Check source-of-truth ownership for referenced types.
3. Fix shared test patterns in batches.
4. Run relevant test project after each batch.
5. Update master/detail plan status when applicable.
6. Report before/after test health.

## Stop Conditions
- Test fix requires modifying Domain in FIX_ONLY mode.
- Test-only model shadows production domain entity.
- Mock assertions replace business behavior checks.

## References
- `docs/plan_MVP/Test_System_Plan/TEST_MASTER_PLAN.md`
- `docs/plan_MVP/Test_System_Plan/TEST_DETAIL_PLAN.md`
- `docs/plan_MVP/test-system-plan-b48ff3.md`
