\echo '=== migrations applied ==='
SELECT "MigrationId" FROM "__EFMigrationsHistory" ORDER BY "MigrationId" DESC LIMIT 15;
\echo '=== any FK constraint on JournalEntryLine.JournalEntryId? ==='
SELECT conname, contype FROM pg_constraint WHERE conrelid = '"JournalEntryLine"'::regclass;
\echo '=== lines by journal reference-type via journals? (orphans have no journal) ==='
SELECT l."AccountNumber", count(*) FROM "JournalEntryLine" l GROUP BY l."AccountNumber" ORDER BY count(*) DESC LIMIT 12;