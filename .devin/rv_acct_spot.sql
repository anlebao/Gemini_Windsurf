\echo '=== SPOT-CHECK: 1 order Vạn An Cafe (08-2026) + 1 Hoàng Nguyên + 1 Vạn An Trading — 2 journal types cùng order ==='
\echo '--- Vạn An Cafe order có cả Order-ref + ManualEntry-ref ---'
SELECT j."ReferenceType", j."EntryDate"::date, j."Description",
       (SELECT string_agg(l."AccountNumber"||':'||l."DebitAmount"||'/'||l."CreditAmount", ', ') FROM "JournalEntryLine" l WHERE l."JournalEntryId"=j."Id") AS lines
FROM "JournalEntries" j
WHERE j."TenantId" = '00000000-0000-0000-0000-000000000001'::uuid
  AND j."ReferenceType" IN ('Order','ManualEntry')
  AND (j."ReferenceId"::text IN (SELECT "Reference" FROM "AccountingEntries" WHERE "Reference" LIKE '01%') OR j."ReferenceType"='Order')
ORDER BY j."EntryDate" DESC
LIMIT 12;

\echo '--- cùng order đó: AccountingEntries ---'
SELECT e."Reference", e."AccountCode", e."Amount", e."EntryType", e."TransactionDate"::date
FROM "AccountingEntries" e
WHERE e."Reference" IN (
  SELECT DISTINCT j."ReferenceId"::text FROM "JournalEntries" j
  WHERE j."TenantId"='00000000-0000-0000-0000-000000000001'::uuid AND j."ReferenceType"='Order'
  ORDER BY 1 LIMIT 1
) ORDER BY e."AccountCode";