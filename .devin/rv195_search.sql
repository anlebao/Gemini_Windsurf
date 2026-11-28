\echo '=== PG: search journals by id fragment F6C92443 / 6AA038D2 ==='
SELECT "JournalEntryId", "ReferenceType", "ReferenceId", "EntryDate", "Description"
FROM "JournalEntries"
WHERE "JournalEntryId"::text ILIKE '%f6c92443%' OR "JournalEntryId"::text ILIKE '%6aa038d2%';

\echo '=== PG: any journal with lines for tenant? lines of Order-ref journals GLOBAL ==='
SELECT j."ReferenceType", count(j."JournalEntryId") AS journals, count(l."Id") AS lines
FROM "JournalEntries" j LEFT JOIN "JournalEntryLine" l ON l."JournalEntryId" = j."JournalEntryId"
GROUP BY j."ReferenceType" ORDER BY j."ReferenceType";