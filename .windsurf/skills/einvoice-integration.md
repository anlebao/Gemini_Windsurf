# Skill: E-Invoice Integration

## Purpose
Implement or review E-Invoice multi-provider integration with reliable provider abstraction and webhook safety.

## Use When
- Working on E-Invoice backend or UI.
- Integrating invoice providers.
- Reviewing provider health, webhook handling, or invoice dashboard.

## Key Areas
- Multi-provider abstraction.
- Provider health endpoints.
- Webhook idempotency.
- Invoice workflow state transitions.
- Dashboard UI with UI Platform.
- Security and data integrity.

## Procedure
1. Confirm active E-Invoice scope.
2. Identify provider integration point.
3. Verify idempotent webhook handling.
4. Validate provider health and error handling.
5. Keep UI implementation separate from provider/domain logic.
6. Report provider risks and validation status.

## Stop Conditions
- Webhook processing is not idempotent.
- Provider-specific logic leaks into generic domain flow.
- Sensitive invoice data is exposed.
- UI work changes provider architecture without approval.

## References
- `docs/design/EInvoice multi provider integration.md`
- `docs/design/EInvoice UI Layout Design.md`
- `docs/plan_MVP/vanan-accounting-implementation-606838.md`
