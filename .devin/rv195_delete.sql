-- RV #195 data fix: export backup to CSV files + DELETE Order-ref journals (lines cascade)
\copy (SELECT * FROM "JournalEntries" WHERE "ReferenceType"='Order' AND ("ReferenceId"::text LIKE '01a185f0%' OR "ReferenceId"::text LIKE '01a18615%')) TO '/tmp/rv195_journals_backup.csv' WITH CSV HEADER
\copy (SELECT l.* FROM "JournalEntryLine" l WHERE l."JournalEntryId" IN (SELECT "Id" FROM "JournalEntries" WHERE "ReferenceType"='Order' AND ("ReferenceId"::text LIKE '01a185f0%' OR "ReferenceId"::text LIKE '01a18615%'))) TO '/tmp/rv195_lines_backup.csv' WITH CSV HEADER
\echo '=== files written (verify via \! ls) ==='
\! ls -la /tmp/rv195_*_backup.csv

\echo '=== DELETE Order-ref journals (ON DELETE CASCADE removes lines) ==='
DELETE FROM "JournalEntries"
WHERE "ReferenceType" = 'Order'
  AND ("ReferenceId"::text LIKE '01a185f0%' OR "ReferenceId"::text LIKE '01a18615%');
SELECT count(*) AS remaining_order_ref_journals FROM "JournalEntries"
WHERE "ReferenceType" = 'Order'
  AND ("ReferenceId"::text LIKE '01a185f0%' OR "ReferenceId"::text LIKE '01a18615%');