# Skill: NATS SQLite Deployment Validation

## Purpose
Validate NATS + SQLite deployment health, event publishing, and order/outbox flow.

## Use When
- Running post-deployment smoke tests.
- Validating local/edge SQLite setup.
- Checking NATS event publishing and outbox processing.

## Inputs
- Application URLs.
- SQLite database path.
- NATS connection details.
- Health endpoints.

## Procedure
1. Check ShopERP health endpoint.
2. Check Gateway health endpoint.
3. Verify SQLite database connectivity.
4. Create or inspect test order flow.
5. Confirm order persisted in SQLite.
6. Confirm outbox message created/processed.
7. Subscribe to relevant NATS subjects and verify events.
8. Report health, database, outbox, and NATS status.

## Stop Conditions
- Health endpoint fails.
- SQLite schema/tables missing.
- Outbox messages accumulate without processing.
- NATS publish/subscribe fails.

## References
- `docs/Implement/NATS_SQLite_Deployment_Guide.md`
- `docs/Implement/Outbox_Pattern_Implementation_Guide.md`
