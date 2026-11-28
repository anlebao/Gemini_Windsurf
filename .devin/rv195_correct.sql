\echo '=== journals (by Id) + lines for order 01a18615 ==='
SELECT j."Id", j."JournalEntryId", j."ReferenceType", j."ReferenceId", j."Description", count(l."Id") AS lines
FROM "JournalEntries" j
LEFT JOIN "JournalEntryLine" l ON l."JournalEntryId" = j."Id"
WHERE j."ReferenceType" = 'Order' AND j."ReferenceId"::text LIKE '01a18615%'
GROUP BY j."Id", j."JournalEntryId", j."ReferenceType", j."ReferenceId", j."Description";

\echo '=== journals (by Id) + lines for order 01a185f0 ==='
SELECT j."Id", j."JournalEntryId", j."ReferenceType", j."ReferenceId", j."Description", count(l."Id") AS lines
FROM "JournalEntries" j
LEFT JOIN "JournalEntryLine" l ON l."JournalEntryId" = j."Id"
WHERE j."ReferenceType" = 'Order' AND j."ReferenceId"::text LIKE '01a185f0%'
GROUP BY j."Id", j."JournalEntryId", j."ReferenceType", j."ReferenceId", j."Description";

\echo '=== bridge journals (by Id) + lines for both orders ==='
SELECT j."Id", j."JournalEntryType", j."ReferenceType", left(j."ReferenceId"::text,8) AS ref8, j."Description", count(l."Id") AS lines
FROM "JournalEntries" j
LEFT JOIN "JournalEntryLine" l ON l."JournalEntryId" = j."Id"
WHERE j."ReferenceId" IN (
  SELECT "Id" FROM "AccountingEntries" WHERE "Reference" = '01a185f0-830e-70ca-8471-178b48338984'
  UNION
  SELECT "Id" FROM "AccountingEntries" WHERE "Reference" = '01a18615-6d62-76dd-a351-597aaa5a8789'
)
GROUP BY j."Id", j."JournalEntryType", j."ReferenceType", j."ReferenceId", j."Description";