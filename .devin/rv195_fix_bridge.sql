-- RV #195: list bridge journals (KEEP) for both orders
SELECT j."ReferenceType", left(j."ReferenceId"::text, 8) AS refid8, j."Description", count(l."Id") AS lines
FROM "JournalEntries" j
LEFT JOIN "JournalEntryLine" l ON l."JournalEntryId" = j."JournalEntryId"
WHERE j."ReferenceId" IN (
  SELECT "Id" FROM "AccountingEntries" WHERE "Reference" = '01a185f0-830e-70ca-8471-178b48338984'
  UNION
  SELECT "Id" FROM "AccountingEntries" WHERE "Reference" = '01a18615-6d62-76dd-a351-597aaa5a8789'
)
GROUP BY j."JournalEntryId", j."ReferenceType", j."ReferenceId", j."Description"
ORDER BY j."ReferenceType", j."ReferenceId";