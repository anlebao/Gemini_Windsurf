\echo '=== current schema + search_path ==='
SELECT current_schema();
SHOW search_path;
\echo '=== schemas containing journal tables ==='
SELECT table_schema, table_name FROM information_schema.tables WHERE table_name IN ('JournalEntries','JournalEntryLine') ORDER BY table_schema, table_name;
\echo '=== FK definition ==='
SELECT pg_get_constraintdef(oid) FROM pg_constraint WHERE conname = 'FK_JournalEntryLine_JournalEntries_JournalEntryId';
\echo '=== line ids that DO match (should be 721 if FK real) ==='
SELECT count(*) FROM "JournalEntryLine" l WHERE EXISTS (SELECT 1 FROM "JournalEntries" j WHERE j."JournalEntryId" = l."JournalEntryId");