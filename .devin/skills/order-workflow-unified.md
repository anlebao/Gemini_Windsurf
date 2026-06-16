# Skill: Order Workflow Unified

## Purpose
Implement or review unified order workflow across frontend, service, API, and sync boundaries.

## Use When
- Working on Order module features.
- Reviewing order flow consistency.
- Implementing order processing, cart checkout, or order sync.

## Review Areas
- Order state transitions.
- API/service responsibility separation.
- Customer/device identity consistency.
- Inventory or product references.
- Outbox/event creation for order changes.
- UI actions preserving backend workflow.

## Procedure
1. Confirm the current order workflow objective.
2. Identify affected layer: UI, API, service, domain, or sync.
3. Verify state transition rules.
4. Preserve source-of-truth types and IDs.
5. Validate order persistence and event/outbox behavior.
6. Report workflow gaps and next action.

## Stop Conditions
- UI creates business rules that belong in services.
- Product/order IDs are semantically mixed.
- Order change is not persisted or evented consistently.
- Fix requires unrelated module refactor.

## References
- `docs/MVP_Product/Solution/Order workflow unified.md`
- `docs/MVP_Product/Solution/codingPlanOrderUnified_FIXED.md`
- `docs/MVP_Product/Solution/codingPlanOrderUnifiled.md`
- `docs/Implement/Outbox_Pattern_Implementation_Guide.md`
