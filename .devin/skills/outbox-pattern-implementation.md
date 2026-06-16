# Skill: Outbox Pattern Implementation

## Purpose
Implement and review reliable event publishing using the Outbox Pattern.

## Use When
- Adding async event publishing.
- Integrating NATS or background workers.
- Reviewing message reliability, retries, or event consistency.

## Core Practices
- Events must be immutable.
- Event names must be meaningful and versionable.
- Payloads must include required data; avoid lazy loading assumptions.
- Failed messages must be logged with context.
- Retries should use exponential backoff.
- Failed message patterns must be monitored.

## Procedure
1. Identify domain/application event source.
2. Persist event in outbox within the same transaction as state change.
3. Process outbox messages in batches.
4. Mark processed messages safely.
5. Track retry count and last error.
6. Add monitoring queries/checks.

## Review Checks
- Idempotency.
- Transaction boundary.
- Retry policy.
- Poison message handling.
- Payload validation.
- NATS/security configuration.

## References
- `docs/Implement/Outbox_Pattern_Implementation_Guide.md`
- `docs/Implement/NATS_SQLite_Deployment_Guide.md`
