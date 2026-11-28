-- RV #195 data fix (user approved 2026-10-29): BACKUP journals ref 'Order' + lines cho 2 order
\echo '=== backup journals ==='
DROP TABLE IF EXISTS jnl_backup_20261029;
CREATE TABLE jnl_backup_20261029 AS
SELECT * FROM "JournalEntries"
WHERE "ReferenceType" = 'Order'
  AND ("ReferenceId"::text LIKE '01a185f0%' OR "ReferenceId"::text LIKE '01a18615%');
SELECT count(*) AS backed_up_journals FROM jnl_backup_20261029;

\echo '=== backup lines ==='
DROP TABLE IF EXISTS jnl_lines_backup_20261029;
CREATE TABLE jnl_lines_backup_20261029 AS
SELECT l.* FROM "JournalEntryLine" l
WHERE l."JournalEntryId" IN (
  SELECT "Id" FROM "JournalEntries"
  WHERE "ReferenceType" = 'Order'
    AND ("ReferenceId"::text LIKE '01a185f0%' OR "ReferenceId"::text LIKE '01a18615%')
);
SELECT count(*) AS backed_up_lines FROM jnl_lines_backup_20261029;

\echo '=== verify backup content (journals) ==='
SELECT "Id", "JournalEntryId", "ReferenceType", "ReferenceId", "Description" FROM jnl_backup_20261029 ORDER BY "ReferenceId";