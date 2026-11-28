\echo '=== types of PK columns ==='
SELECT table_name, column_name, data_type FROM information_schema.columns
WHERE table_name IN ('JournalEntries','JournalEntryLine') AND column_name = 'JournalEntryId';
\echo '=== sample 3 lines: line.JournalEntryId + does it exist as journal id / ref / accounting entry id? ==='
SELECT l."JournalEntryId" AS line_jid,
       (SELECT count(*) FROM "JournalEntries" j WHERE j."JournalEntryId" = l."JournalEntryId") AS in_journal_pk,
       (SELECT count(*) FROM "JournalEntries" j WHERE j."ReferenceId" = l."JournalEntryId") AS in_journal_ref,
       (SELECT count(*) FROM "AccountingEntries" a WHERE a."Id" = l."JournalEntryId") AS in_accounting_entry
FROM "JournalEntryLine" l LIMIT 5;
\echo '=== journal PK vs line FK: full outer comparison on first 3 journals ==='
SELECT j."JournalEntryId" AS journal_pk, j."Description"
FROM "JournalEntries" j ORDER BY j."EntryDate" DESC LIMIT 3;