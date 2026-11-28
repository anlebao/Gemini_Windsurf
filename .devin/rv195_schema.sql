\echo '=== columns of JournalEntryLine ==='
SELECT column_name, data_type FROM information_schema.columns WHERE table_name = 'JournalEntryLine' ORDER BY ordinal_position;
\echo '=== total lines count ==='
SELECT count(*) FROM "JournalEntryLine";
\echo '=== sample lines (any journal) ==='
SELECT "JournalEntryId", "Id", "AccountNumber", "DebitAmount", "CreditAmount" FROM "JournalEntryLine" LIMIT 5;
\echo '=== all journal tables in schema ==='
SELECT table_name FROM information_schema.tables WHERE table_name ILIKE '%journal%' ORDER BY table_name;