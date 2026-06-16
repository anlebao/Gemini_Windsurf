# Skill: Accounting UI Implementation

## Purpose
Implement or review Accounting UI screens with UI Platform compliance and correct namespace strategy.

## Use When
- Working on Accounting UI pages.
- Following Sprint 1 Accounting UI plan.
- Implementing accounting dashboards, reports, or entry screens.

## Required Checks
- UI Platform components are used.
- Accounting page namespaces are correct.
- Component gap analysis is complete before implementation.
- Build and guard check pass before and after changes.
- DI registration is verified without duplicates.

## Procedure
1. Read active Accounting UI plan when task is accounting-related.
2. Confirm current screen and phase.
3. Validate UI Platform component availability.
4. Implement only the approved page/component.
5. Run build/guard validation.
6. Report changed files and remaining accounting UI tasks.

## Stop Conditions
- Task is not accounting-related.
- UI fix requires Domain change.
- Component does not exist and workaround would bypass UI Platform.
- Namespace strategy is unclear.

## References
- `docs/plan_MVP/sprint1-accounting-ui-detail-606838.md`
- `docs/IMPLEMENTATION/Accounting_UI_Implementation_Summary.md`
- `docs/UI_Platform_Implementation_Guide.md`
