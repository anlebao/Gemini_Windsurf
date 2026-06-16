# Skill: SQLite Concurrency Analysis

## Purpose
Analyze SQLite concurrency, transaction, and offline/local database risks.

## Use When
- Reviewing SQLite database design.
- Debugging locking, write contention, or offline sync issues.
- Running `review_sqlite` mode.

## Review Areas
- Transaction boundaries.
- Connection lifetime and pooling.
- Write contention and lock duration.
- Retry behavior for busy/locked database states.
- Local database schema consistency.
- Conflict resolution strategy.
- Outbox/event consistency with SQLite transactions.

## Procedure
1. Identify read/write paths.
2. Verify writes are short and transactional.
3. Check connection management.
4. Check conflict handling and retry strategy.
5. Verify outbox and data changes share transaction where required.
6. Report risks with concrete file evidence.

## Stop Conditions
- Suggested fix requires changing Domain semantics.
- Concurrency issue is speculative without code evidence.
- Fix crosses unrelated infrastructure layers.

## References
- `docs/Implement/SQLite_Concurrency_Architecture.md`
- `docs/Implement/SQLite_Concurrency_Features_Update.md`
- `docs/Architecture/Monolithic_Architecture/DB/02-ShopERP-SQLite-Database.md`
- `docs/Architecture/Monolithic_Architecture/DB/03-KhachLink-SQLite-Database.md`
