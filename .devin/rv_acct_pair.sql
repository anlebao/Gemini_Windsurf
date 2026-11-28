\echo '=== Order 01a1812c-78bc-7718-97ef-ed6a2bd2fbb7 (Vạn An Cafe 2026-10-28): MỌI journal + lines ==='
SELECT j."Id", j."ReferenceType", j."ReferenceId", j."EntryDate"::date, j."Description",
       (SELECT string_agg(l."AccountNumber"||':'||l."DebitAmount"||'/'||l."CreditAmount", ', ') FROM "JournalEntryLine" l WHERE l."JournalEntryId"=j."Id") AS lines
FROM "JournalEntries" j
WHERE j."TenantId" = '00000000-0000-0000-0000-000000000001'::uuid
  AND (j."ReferenceId" = '01a1812c-78bc-7718-97ef-ed6a2bd2fbb7'::uuid
       OR j."ReferenceId" IN (SELECT "Id" FROM "AccountingEntries" WHERE "Reference"='01a1812c-78bc-7718-97ef-ed6a2bd2fbb7'))
ORDER BY j."ReferenceType", j."Id";

\echo '=== entries của order đó ==='
SELECT "Id", "AccountCode", "Amount", "EntryType", "Reference", "TransactionDate"::date
FROM "AccountingEntries"
WHERE "Reference" = '01a1812c-78bc-7718-97ef-ed6a2bd2fbb7'
ORDER BY "AccountCode";