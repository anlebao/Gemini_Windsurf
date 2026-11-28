-- RV accounting sweep-first (2026-10-29, user approved class "Xóa journal ref 'Order'"):
-- Xóa MỌI journal ref 'Order' mà order CÓ journal bridge (ManualEntry) cùng entries → duplicate.
-- GIỮ journal ref 'Order' của order KHÔNG có bridge (pre-bridge era — journal duy nhất).
-- Backup: 2 bảng + CSV export.

\echo '=== 1. BACKUP journals (duplicate Order-ref) ==='
DROP TABLE IF EXISTS jnl_order_dup_backup_20261029;
CREATE TABLE jnl_order_dup_backup_20261029 AS
SELECT j.* FROM "JournalEntries" j
WHERE j."ReferenceType" = 'Order'
  AND EXISTS (
    SELECT 1 FROM "JournalEntries" j2
    WHERE j2."ReferenceType" = 'ManualEntry'
      AND j2."ReferenceId" IN (SELECT "Id" FROM "AccountingEntries" e WHERE e."Reference" = j."ReferenceId"::text)
  );
SELECT count(*) AS backed_up_journals FROM jnl_order_dup_backup_20261029;

\echo '=== 2. BACKUP lines ==='
DROP TABLE IF EXISTS jnl_order_dup_lines_backup_20261029;
CREATE TABLE jnl_order_dup_lines_backup_20261029 AS
SELECT l.* FROM "JournalEntryLine" l
WHERE l."JournalEntryId" IN (SELECT "Id" FROM jnl_order_dup_backup_20261029);
SELECT count(*) AS backed_up_lines FROM jnl_order_dup_lines_backup_20261029;

\echo '=== 3. CSV export phòng hờ (gateway /tmp) ==='
\copy (SELECT * FROM jnl_order_dup_backup_20261029) TO '/tmp/rv_acct_dup_journals_backup.csv' WITH CSV HEADER
\copy (SELECT * FROM jnl_order_dup_lines_backup_20261029) TO '/tmp/rv_acct_dup_lines_backup.csv' WITH CSV HEADER
\! ls -la /tmp/rv_acct_dup_*_backup.csv

\echo '=== 4. VERIFY trước khi xóa: tổng theo tenant ==='
SELECT t."Name", count(*) FROM jnl_order_dup_backup_20261029 j
JOIN "Tenants" t ON t."Id" = j."TenantId" GROUP BY t."Name" ORDER BY count(*) DESC;

\echo '=== 5. DELETE (ON DELETE CASCADE xóa lines) ==='
DELETE FROM "JournalEntries" j
WHERE j."ReferenceType" = 'Order'
  AND EXISTS (
    SELECT 1 FROM "JournalEntries" j2
    WHERE j2."ReferenceType" = 'ManualEntry'
      AND j2."ReferenceId" IN (SELECT "Id" FROM "AccountingEntries" e WHERE e."Reference" = j."ReferenceId"::text)
  );

\echo '=== 6. VERIFY sau xóa ==='
SELECT count(*) AS remaining_order_ref FROM "JournalEntries" WHERE "ReferenceType"='Order';
SELECT (SELECT count(*) FROM jnl_order_dup_backup_20261029) AS backup_journals,
       (SELECT count(*) FROM jnl_order_dup_lines_backup_20261029) AS backup_lines;