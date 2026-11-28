\echo '=== totals ==='
SELECT (SELECT count(*) FROM "JournalEntries") AS journals, (SELECT count(*) FROM "JournalEntryLine") AS lines;
\echo '=== orphan lines (JournalEntryId not in JournalEntries) ==='
SELECT count(*) FROM "JournalEntryLine" l
LEFT JOIN "JournalEntries" j ON j."JournalEntryId" = l."JournalEntryId"
WHERE j."JournalEntryId" IS NULL;
\echo '=== does sample journal 099c1d5a exist in JournalEntries? ==='
SELECT "JournalEntryId", "ReferenceType", "Description" FROM "JournalEntries" WHERE "JournalEntryId" = '099c1d5a-4925-4ec9-8e48-568ff84e3282';
\echo '=== distinct JournalEntryId in lines ==='
SELECT count(DISTINCT "JournalEntryId") FROM "JournalEntryLine";