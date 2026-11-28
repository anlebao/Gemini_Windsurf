\echo '=== remaining journals for both orders (bridge ManualEntry — single source) ==='
SELECT j."Id", j."ReferenceType", left(j."ReferenceId"::text, 8) AS ref8, j."Description", count(l."Id") AS lines
FROM "JournalEntries" j
LEFT JOIN "JournalEntryLine" l ON l."JournalEntryId" = j."Id"
WHERE j."ReferenceId" IN (
  SELECT "Id" FROM "AccountingEntries" WHERE "Reference" = '01a185f0-830e-70ca-8471-178b48338984'
  UNION
  SELECT "Id" FROM "AccountingEntries" WHERE "Reference" = '01a18615-6d62-76dd-a351-597aaa5a8789'
)
GROUP BY j."Id", j."ReferenceType", j."ReferenceId", j."Description"
ORDER BY j."ReferenceId";

\echo '=== revenue 511 credit per order (from remaining journal lines) ==='
SELECT e."Reference" AS order_ref, l."AccountNumber", sum(l."DebitAmount") AS debit, sum(l."CreditAmount") AS credit
FROM "JournalEntryLine" l
JOIN "JournalEntries" j ON j."Id" = l."JournalEntryId"
JOIN "AccountingEntries" e ON e."Id" = j."ReferenceId"
WHERE e."Reference" IN ('01a185f0-830e-70ca-8471-178b48338984', '01a18615-6d62-76dd-a351-597aaa5a8789')
GROUP BY e."Reference", l."AccountNumber"
ORDER BY e."Reference", l."AccountNumber";

\echo '=== backup tables still present ==='
SELECT (SELECT count(*) FROM jnl_backup_20261029) AS backup_journals, (SELECT count(*) FROM jnl_lines_backup_20261029) AS backup_lines;