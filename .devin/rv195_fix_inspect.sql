-- RV #195 data fix: inspect journals for 01a185f0 + 01a18615
\echo '=== JOURNALS ref Order (to DELETE) ==='
SELECT "JournalEntryId", "ReferenceType", "ReferenceId", "EntryDate", "Description"
FROM "JournalEntries"
WHERE "ReferenceType" = 'Order'
  AND ("ReferenceId"::text LIKE '01a185f0%' OR "ReferenceId"::text LIKE '01a18615%')
ORDER BY "ReferenceId";

\echo '=== LINES of ref Order journals (expect 0) ==='
SELECT count(*) FROM "JournalEntryLine"
WHERE "JournalEntryId" IN (
  SELECT "JournalEntryId" FROM "JournalEntries"
  WHERE "ReferenceType" = 'Order'
    AND ("ReferenceId"::text LIKE '01a185f0%' OR "ReferenceId"::text LIKE '01a18615%')
);

\echo '=== BRIDGE journals for both orders (KEEP — single source) ==='
SELECT j."ReferenceType", left(j."ReferenceId"::text, 8) AS refid8, j."Description", count(l."Id") AS lines
FROM "JournalEntries" j
LEFT JOIN "JournalEntryLine" l ON l."JournalEntryId" = j."JournalEntryId"
WHERE j."ReferenceId" IN (
  SELECT "Id" FROM "AccountingEntries" WHERE "Reference" = '01a185f0-830e-70ca-8471-178b48338984'
  UNION
  SELECT "Id" FROM "AccountingEntries" WHERE "Reference" = '01a18615-6d62-76dd-a351-597aaa5a8789'
)
GROUP BY j."JournalEntryId"
ORDER BY j."ReferenceType";

\echo '=== AccountingEntries per order (reference check) ==='
SELECT "Reference", "AccountCode", "Amount", "EntryType" FROM "AccountingEntries"
WHERE "Reference" IN ('01a185f0-830e-70ca-8471-178b48338984', '01a18615-6d62-76dd-a351-597aaa5a8789')
ORDER BY "Reference", "AccountCode";