# Skill: System Refactor Safety

## Purpose
Keep large refactors controlled, reversible, and architecture-safe.

## Use When
- Planning or reviewing system-wide refactors.
- Fixing architecture conflicts.
- Changing shared infrastructure or domain architecture.

## Required Controls
- Define scope before implementation.
- Work phase by phase.
- Validate after each phase.
- Keep rollback path available.
- Avoid unrelated cleanup.

## Procedure
1. Identify affected layers and risk level.
2. Confirm approval and current objective.
3. Create phase plan with validation points.
4. Limit changes to current phase.
5. Run build, guard check, and tests after phase.
6. Report changed files, validation results, and rollback risk.

## Stop Conditions
- Domain immutability is threatened.
- Multi-tenancy enforcement weakens.
- Public API changes without approval.
- Refactor expands beyond approved scope.
- Validation fails and root cause is unclear.

## References
- `docs/QuyTrinh/Refactor/18_Conflicts_System_Refactor_Plan.md`
- `.windsurf/rules/.windsurfrules`
- `.windsurf/workflows/newfeaturebuild.md`
