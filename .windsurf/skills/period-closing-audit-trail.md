# Skill: Period Closing Audit Trail

## Purpose
Implement or review Period Closing and Audit Trail features with accounting integrity.

## Use When
- Working on Sprint 2 Period Closing.
- Implementing closing services, audit logs, or closing wizard UI.
- Reviewing accounting close workflow.

## Key Areas
- TDD plan execution.
- Domain and infrastructure separation.
- Period closing service logic.
- Audit trail completeness.
- Frontend wizard validation.
- Integration and final validation.

## Procedure
1. Confirm task belongs to Period Closing/Audit Trail.
2. Follow planned day/phase order.
3. Keep Domain, Infrastructure, Services, and UI changes isolated.
4. Validate closing rules and audit events.
5. Run tests/build after each phase.
6. Report phase completion and remaining tasks.

## Stop Conditions
- AccountingEntry immutability is threatened.
- Closing operation can mutate historical entries.
- Audit trail is optional or incomplete.
- UI work leaks into domain logic.

## References
- `docs/plan_MVP/sprint2-period-closing-audit-trail.md`
- `.windsurf/rules/.windsurfrules`
