\echo '=== journals mentioning order 01a18615 (description OR ref) ==='
SELECT j."JournalEntryId", j."ReferenceType", j."ReferenceId", j."Description", count(l."Id") AS lines
FROM "JournalEntries" j
LEFT JOIN "JournalEntryLine" l ON l."JournalEntryId" = j."JournalEntryId"
WHERE j."Description" LIKE '%01a18615%' OR j."ReferenceId"::text LIKE '01a18615%'
GROUP BY j."JournalEntryId", j."ReferenceType", j."ReferenceId", j."Description", j."EntryDate"
ORDER BY j."EntryDate";

\echo '=== journals mentioning order 01a185f0 ==='
SELECT j."JournalEntryId", j."ReferenceType", j."ReferenceId", j."Description", count(l."Id") AS lines
FROM "JournalEntries" j
LEFT JOIN "JournalEntryLine" l ON l."JournalEntryId" = j."JournalEntryId"
WHERE j."Description" LIKE '%01a185f0%' OR j."ReferenceId"::text LIKE '01a185f0%'
GROUP BY j."JournalEntryId", j."ReferenceType", j."ReferenceId", j."Description", j."EntryDate"
ORDER BY j."EntryDate";